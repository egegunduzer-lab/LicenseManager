using System.Security.Cryptography;
using LicenseManager.Api.Data;
using LicenseManager.Core.DTOs;
using LicenseManager.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LicenseManager.Api.Services;

public class LicenseService
{
    private const int SignedLicenseLifetimeDays = 30;

    private readonly ApplicationDbContext _dbContext;
    private readonly LicenseSigningService _licenseSigningService;


    public LicenseService(
        ApplicationDbContext dbContext,
        LicenseSigningService licenseSigningService)
    {
        _dbContext = dbContext;
        _licenseSigningService = licenseSigningService;
    }


    // =================================================
    // YENİ LİSANS OLUŞTUR
    // =================================================
    public async Task<CreatedLicenseResponse?>
        CreateLicenseAsync(
            Guid companyId,
            Guid projectId,
            List<string> modules,
            DateTime validUntil)
    {
        // -------------------------------------------------
        // 1. Firma gerçekten var mı?
        // -------------------------------------------------
        bool companyExists =
            await _dbContext.Companies
                .AnyAsync(
                    c => c.Id == companyId);

        if (!companyExists)
        {
            return null;
        }


        // -------------------------------------------------
        // 2. Proje gerçekten var mı?
        // -------------------------------------------------
        bool projectExists =
            await _dbContext.Projects
                .AnyAsync(
                    p => p.Id == projectId);

        if (!projectExists)
        {
            return null;
        }


        // -------------------------------------------------
        // 3. Modülleri temizle
        // -------------------------------------------------
        var normalizedModules =
            modules
                .Where(
                    m => !string.IsNullOrWhiteSpace(m))
                .Select(
                    m => m.Trim())
                .Distinct(
                    StringComparer.Ordinal)
                .ToList();

        if (normalizedModules.Count == 0)
        {
            return null;
        }


        // -------------------------------------------------
        // 4. Tarihleri hazırla
        // -------------------------------------------------
        DateTime now =
            DateTime.UtcNow;

        DateTime validUntilUtc =
            NormalizeToUtc(
                validUntil);


        // Gerçek lisans hakkı geçmişte olamaz.
        if (validUntilUtc <= now)
        {
            return null;
        }


        // -------------------------------------------------
        // 5. Signed license expiry hesapla
        //
        // ExpiryDate hiçbir zaman müşterinin gerçek
        // lisans süresini (ValidUntil) aşamaz.
        // -------------------------------------------------
        DateTime signedFileExpiry =
            CalculateSignedExpiry(
                now,
                validUntilUtc);


        // -------------------------------------------------
        // 6. Benzersiz LicenseKey üret
        // -------------------------------------------------
        string licenseKey =
            await GenerateUniqueLicenseKeyAsync();


        // -------------------------------------------------
        // 7. License oluştur
        // -------------------------------------------------
        var license =
            new License
            {
                Id =
                    Guid.NewGuid(),

                CompanyId =
                    companyId,

                ProjectId =
                    projectId,

                LicenseKey =
                    licenseKey,

                Modules =
                    normalizedModules,

                // Bu signed license dosyasının başlangıcı
                IssuedAt =
                    now,

                // Maksimum 30 gün,
                // fakat ValidUntil'i aşamaz.
                ExpiryDate =
                    signedFileExpiry,

                // Müşterinin gerçek lisans hakkı.
                // Renewal veya key rotation bunu değiştiremez.
                ValidUntil =
                    validUntilUtc,

                IsBlocked =
                    false,

                CreatedAt =
                    now
            };


        // -------------------------------------------------
        // 8. DB'ye kaydet
        // -------------------------------------------------
        _dbContext.Licenses.Add(
            license);

        await _dbContext.SaveChangesAsync();


        // -------------------------------------------------
        // 9. Admin'e güvenli response dön
        // -------------------------------------------------
        return new CreatedLicenseResponse
        {
            LicenseId =
                license.Id,

            CompanyId =
                license.CompanyId,

            ProjectId =
                license.ProjectId,

            LicenseKey =
                license.LicenseKey,

            Modules =
                license.Modules.ToList(),

            IssuedAt =
                license.IssuedAt,

            ExpiryDate =
                license.ExpiryDate,

            ValidUntil =
                license.ValidUntil,

            IsBlocked =
                license.IsBlocked
        };
    }


    // =================================================
    // MEVCUT LİSANSI OLUŞTUR / İMZALA
    // =================================================
    public async Task<SignedLicenseResponse?>
        GenerateLicenseAsync(
            Guid companyId,
            string licenseKey)
    {
        // -------------------------------------------------
        // 1. Lisansı, projeyi ve key'leri getir
        // -------------------------------------------------
        var license =
            await _dbContext.Licenses
                .Include(
                    l => l.Project)
                .ThenInclude(
                    p => p.Keys)
                .FirstOrDefaultAsync(
                    l =>
                        l.CompanyId == companyId &&
                        l.LicenseKey == licenseKey);


        DateTime now =
            DateTime.UtcNow;


        // -------------------------------------------------
        // 2. Lisans uygun mu?
        //
        // Missing / blocked / gerçek süresi bitmiş
        // durumlarının hepsi üst katmanda aynı genel
        // hata olarak maskelenecek.
        // -------------------------------------------------
        if (license is null ||
            license.IsBlocked ||
            license.ValidUntil <= now)
        {
            return null;
        }


        // -------------------------------------------------
        // 3. Aktif imzalama anahtarını bul
        // -------------------------------------------------
        var activeKeys =
            license.Project.Keys
                .Where(
                    k => k.IsActive)
                .ToList();


        if (activeKeys.Count != 1)
        {
            throw new InvalidOperationException(
                "Proje için tam olarak bir aktif " +
                "imzalama anahtarı bulunmalıdır.");
        }


        var activeKey =
            activeKeys[0];


        // -------------------------------------------------
        // 4. Yeni signed-license penceresini hesapla
        //
        // Örn:
        // now + 30 gün = 30 Eylül
        // ValidUntil    = 10 Eylül
        //
        // ExpiryDate    = 10 Eylül
        // -------------------------------------------------
        license.IssuedAt =
            now;

        license.ExpiryDate =
            CalculateSignedExpiry(
                now,
                license.ValidUntil);


        await _dbContext.SaveChangesAsync();


        // -------------------------------------------------
        // 5. Payload oluştur
        // -------------------------------------------------
        var payload =
            new LicensePayload
            {
                LicenseId =
                    license.Id,

                CompanyId =
                    license.CompanyId,

                ProjectId =
                    license.ProjectId,

                ProjectName =
                    license.Project.Name,

                ProjectVersion =
                    license.Project.Version,

                // Şu anda hangi key aktifse
                // yeni lisans onunla imzalanır.
                KeyVersion =
                    activeKey.KeyVersion,

                Modules =
                    license.Modules.ToList(),

                IssuedAt =
                    license.IssuedAt,

                ExpiryDate =
                    license.ExpiryDate
            };


        // -------------------------------------------------
        // 6. Aktif private key ile imzala
        // -------------------------------------------------
        string signature =
            _licenseSigningService.Sign(
                payload,
                activeKey.PrivateKey);


        return new SignedLicenseResponse
        {
            Body =
                payload,

            Signature =
                signature
        };
    }


    // =================================================
    // LİSANSI YENİLE
    // =================================================
    public async Task<SignedLicenseResponse?>
        RenewLicenseAsync(
            Guid companyId,
            string licenseKey)
    {
        // -------------------------------------------------
        // 1. Lisansı, projeyi ve key'leri getir
        // -------------------------------------------------
        var license =
            await _dbContext.Licenses
                .Include(
                    l => l.Project)
                .ThenInclude(
                    p => p.Keys)
                .FirstOrDefaultAsync(
                    l =>
                        l.CompanyId == companyId &&
                        l.LicenseKey == licenseKey);


        DateTime now =
            DateTime.UtcNow;


        // -------------------------------------------------
        // 2. Renewal yapılabilir mi?
        // -------------------------------------------------
        if (license is null ||
            license.IsBlocked ||
            license.ValidUntil <= now)
        {
            return null;
        }


        // -------------------------------------------------
        // 3. Aktif imzalama anahtarını bul
        // -------------------------------------------------
        var activeKeys =
            license.Project.Keys
                .Where(
                    k => k.IsActive)
                .ToList();


        if (activeKeys.Count != 1)
        {
            throw new InvalidOperationException(
                "Proje için tam olarak bir aktif " +
                "imzalama anahtarı bulunmalıdır.");
        }


        var activeKey =
            activeKeys[0];


        // -------------------------------------------------
        // 4. Signed-license süresini yenile
        //
        // ÖNEMLİ:
        // ValidUntil burada ASLA değiştirilmez.
        // -------------------------------------------------
        license.IssuedAt =
            now;

        license.ExpiryDate =
            CalculateSignedExpiry(
                now,
                license.ValidUntil);


        await _dbContext.SaveChangesAsync();


        // -------------------------------------------------
        // 5. Yeni payload
        // -------------------------------------------------
        var payload =
            new LicensePayload
            {
                LicenseId =
                    license.Id,

                CompanyId =
                    license.CompanyId,

                ProjectId =
                    license.ProjectId,

                ProjectName =
                    license.Project.Name,

                ProjectVersion =
                    license.Project.Version,

                // Rotation yapıldıysa yeni aktif key
                // otomatik olarak burada kullanılır.
                KeyVersion =
                    activeKey.KeyVersion,

                Modules =
                    license.Modules.ToList(),

                IssuedAt =
                    license.IssuedAt,

                ExpiryDate =
                    license.ExpiryDate
            };


        // -------------------------------------------------
        // 6. Aktif key ile yeni imzayı üret
        // -------------------------------------------------
        string signature =
            _licenseSigningService.Sign(
                payload,
                activeKey.PrivateKey);


        return new SignedLicenseResponse
        {
            Body =
                payload,

            Signature =
                signature
        };
    }


    // =================================================
    // BLOCK / UNBLOCK
    // =================================================
    public async Task<bool>
        SetBlockedStatusAsync(
            Guid licenseId,
            bool isBlocked)
    {
        var license =
            await _dbContext.Licenses
                .SingleOrDefaultAsync(
                    x => x.Id == licenseId);

        if (license is null)
        {
            return false;
        }


        license.IsBlocked =
            isBlocked;


        await _dbContext.SaveChangesAsync();


        return true;
    }


    // =================================================
    // SIGNED LICENSE EXPIRY HESABI
    // =================================================
    private static DateTime CalculateSignedExpiry(
        DateTime issuedAt,
        DateTime validUntil)
    {
        DateTime normalExpiry =
            issuedAt.AddDays(
                SignedLicenseLifetimeDays);


        // Signed file asla gerçek lisans
        // hakkından daha uzun yaşayamaz.
        return normalExpiry < validUntil
            ? normalExpiry
            : validUntil;
    }


    // =================================================
    // UTC NORMALIZATION
    // =================================================
    private static DateTime NormalizeToUtc(
        DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }


        if (value.Kind == DateTimeKind.Local)
        {
            return value.ToUniversalTime();
        }


        // API sözleşmemizde tarihlerin UTC gönderilmesini
        // bekliyoruz. Kind belirtilmemişse UTC kabul ediyoruz.
        return DateTime.SpecifyKind(
            value,
            DateTimeKind.Utc);
    }


    // =================================================
    // UNIQUE LICENSE KEY
    // =================================================
    private async Task<string>
        GenerateUniqueLicenseKeyAsync()
    {
        // Teorik collision ihtimali çok düşük.
        // Yine de DB'den kontrol ediyoruz.
        for (int attempt = 0;
             attempt < 5;
             attempt++)
        {
            byte[] randomBytes =
                RandomNumberGenerator.GetBytes(16);


            string randomPart =
                Convert.ToHexString(
                    randomBytes);


            string licenseKey =
                $"LM-{randomPart}";


            bool exists =
                await _dbContext.Licenses
                    .AnyAsync(
                        l =>
                            l.LicenseKey ==
                            licenseKey);


            if (!exists)
            {
                return licenseKey;
            }
        }


        throw new InvalidOperationException(
            "Benzersiz lisans anahtarı oluşturulamadı.");
    }
}
