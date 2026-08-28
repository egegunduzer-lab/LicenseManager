using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LicenseManager.Api.Services;
using LicenseManager.Client.Services;
using LicenseManager.Core.DTOs;
using NSec.Cryptography;
using LicenseManager.Api.Data;
using LicenseManager.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LicenseManager.Tests;

public class KeyRotationTests
{
    private static readonly SignatureAlgorithm Algorithm =
        SignatureAlgorithm.Ed25519;


    // -------------------------------------------------
    // Birden fazla public key tutabilen test provider
    // -------------------------------------------------
    private class MultiKeyPublicKeyProvider
        : ILicensePublicKeyProvider
    {
        private readonly Dictionary<int, byte[]> _keys;

        public MultiKeyPublicKeyProvider(
            Dictionary<int, byte[]> keys,
            string projectName,
            string projectVersion)
        {
            _keys = keys;

            ProjectName = projectName;
            ProjectVersion = projectVersion;
        }

        public string ProjectName { get; }

        public string ProjectVersion { get; }


        public bool TryGetPublicKey(
            int keyVersion,
            out byte[] publicKey)
        {
            if (_keys.TryGetValue(
                    keyVersion,
                    out byte[]? key))
            {
                publicKey = key;
                return true;
            }

            publicKey = Array.Empty<byte>();
            return false;
        }
    }


    // -------------------------------------------------
    // Test için Ed25519 key pair üret
    // -------------------------------------------------
    private static (
        byte[] PublicKey,
        byte[] PrivateKey)
        CreateKeyPair()
    {
        var parameters =
            new KeyCreationParameters
            {
                ExportPolicy =
                    KeyExportPolicies
                        .AllowPlaintextArchiving
            };

        using var key =
            Key.Create(
                Algorithm,
                parameters);

        return (
            key.Export(
                KeyBlobFormat.RawPublicKey),

            key.Export(
                KeyBlobFormat.RawPrivateKey)
        );
    }


    // -------------------------------------------------
    // Payload oluştur
    // -------------------------------------------------
    private static LicensePayload CreatePayload(
        int keyVersion)
    {
        return new LicensePayload
        {
            LicenseId = Guid.NewGuid(),

            CompanyId = Guid.NewGuid(),

            ProjectId = Guid.NewGuid(),

            ProjectName = "Rotation ERP",

            ProjectVersion = "1.0",

            KeyVersion = keyVersion,

            Modules = new List<string>
            {
                "Reports",
                "Inventory"
            },

            IssuedAt = DateTime.UtcNow,

            ExpiryDate =
                DateTime.UtcNow.AddDays(30)
        };
    }


    // -------------------------------------------------
    // Gerçek LicenseSigningService ile imzala
    // -------------------------------------------------
    private static string Sign(
        LicensePayload payload,
        byte[] privateKey)
    {
        var protector =
            TestPrivateKeyProtector.Create();

        byte[] protectedPrivateKey =
            protector.Protect(
                privateKey);

        var signingService =
            new LicenseSigningService(
                protector);

        return signingService.Sign(
            payload,
            protectedPrivateKey);
    }


    // =================================================
    // TEST 1
    // KeyVersion 1 -> Public Key 1
    // =================================================
    [Fact]
    public void KeyVersion1_ShouldVerifyWithKey1()
    {
        var key1 =
            CreateKeyPair();

        var payload =
            CreatePayload(1);

        string signature =
            Sign(
                payload,
                key1.PrivateKey);

        var provider =
            new MultiKeyPublicKeyProvider(
                new Dictionary<int, byte[]>
                {
                    { 1, key1.PublicKey }
                },
                "Rotation ERP",
                "1.0");

        var verifier =
            new LicenseVerificationService(
                provider);

        bool result =
            verifier.Verify(
                payload,
                signature);

        Assert.True(result);
    }


    // =================================================
    // TEST 2
    // KeyVersion 2 -> Public Key 2
    // =================================================
    [Fact]
    public void KeyVersion2_ShouldVerifyWithKey2()
    {
        var key1 =
            CreateKeyPair();

        var key2 =
            CreateKeyPair();

        var payload =
            CreatePayload(2);

        string signature =
            Sign(
                payload,
                key2.PrivateKey);

        var provider =
            new MultiKeyPublicKeyProvider(
                new Dictionary<int, byte[]>
                {
                    { 1, key1.PublicKey },
                    { 2, key2.PublicKey }
                },
                "Rotation ERP",
                "1.0");

        var verifier =
            new LicenseVerificationService(
                provider);

        bool result =
            verifier.Verify(
                payload,
                signature);

        Assert.True(result);
    }


    // =================================================
    // TEST 3
    // Eski lisans yeni key eklense bile çalışmalı
    // =================================================
    [Fact]
    public void OldKey_ShouldRemainValidAfterRotation()
    {
        var key1 =
            CreateKeyPair();

        var key2 =
            CreateKeyPair();

        var payload =
            CreatePayload(1);

        string signature =
            Sign(
                payload,
                key1.PrivateKey);

        // Client artık hem eski hem yeni key'i biliyor
        var provider =
            new MultiKeyPublicKeyProvider(
                new Dictionary<int, byte[]>
                {
                    { 1, key1.PublicKey },
                    { 2, key2.PublicKey }
                },
                "Rotation ERP",
                "1.0");

        var verifier =
            new LicenseVerificationService(
                provider);

        bool result =
            verifier.Verify(
                payload,
                signature);

        Assert.True(result);
    }


    // =================================================
    // TEST 4
    // Bilinmeyen key version reddedilmeli
    // =================================================
    [Fact]
    public void UnknownKeyVersion_ShouldFail()
    {
        var key1 =
            CreateKeyPair();

        var payload =
            CreatePayload(99);

        string signature =
            Sign(
                payload,
                key1.PrivateKey);

        var provider =
            new MultiKeyPublicKeyProvider(
                new Dictionary<int, byte[]>
                {
                    { 1, key1.PublicKey }
                },
                "Rotation ERP",
                "1.0");

        var verifier =
            new LicenseVerificationService(
                provider);

        bool result =
            verifier.Verify(
                payload,
                signature);

        Assert.False(result);
    }


    // =================================================
    // TEST 5
    // Yanlış private key ile imzalanmış lisans reddedilmeli
    // =================================================
    [Fact]
    public void WrongKeyForKeyVersion_ShouldFail()
    {
        var key1 =
            CreateKeyPair();

        var key2 =
            CreateKeyPair();

        // Body "key 1 kullan" diyor
        var payload =
            CreatePayload(1);

        // Ama saldırgan/yanlış server Key 2 ile imzalıyor
        string signature =
            Sign(
                payload,
                key2.PrivateKey);

        var provider =
            new MultiKeyPublicKeyProvider(
                new Dictionary<int, byte[]>
                {
                    { 1, key1.PublicKey },
                    { 2, key2.PublicKey }
                },
                "Rotation ERP",
                "1.0");

        var verifier =
            new LicenseVerificationService(
                provider);

        bool result =
            verifier.Verify(
                payload,
                signature);

        Assert.False(result);
    }


    // =================================================
    // TEST 6
    // İmzadan sonra KeyVersion değiştirilirse reddedilmeli
    // =================================================
    [Fact]
    public void TamperedKeyVersion_ShouldFail()
    {
        var key1 =
            CreateKeyPair();

        var key2 =
            CreateKeyPair();

        var payload =
            CreatePayload(1);

        string signature =
            Sign(
                payload,
                key1.PrivateKey);

        // İmzadan sonra body değiştiriliyor
        payload.KeyVersion = 2;

        var provider =
            new MultiKeyPublicKeyProvider(
                new Dictionary<int, byte[]>
                {
                    { 1, key1.PublicKey },
                    { 2, key2.PublicKey }
                },
                "Rotation ERP",
                "1.0");

        var verifier =
            new LicenseVerificationService(
                provider);

        bool result =
            verifier.Verify(
                payload,
                signature);

        Assert.False(result);
    }

    // =================================================
    // TEST 7
    // Key rotation gerçek lisans süresini
    // değiştirmemeli.
    //
    // Key 1 ile üretilen mevcut signed license
    // kendi ExpiryDate tarihine kadar geçerli kalmalı.
    //
    // Rotation sonrası renewal ise Key 2 ile
    // imzalanmalı fakat ValidUntil aşılmamalı.
    // =================================================
    [Fact]
    public async Task KeyRotation_ShouldNotChangeRealLicenseValidity()
    {
        // -------------------------------------------------
        // 1. Test veritabanı
        // -------------------------------------------------
        var options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        await using var dbContext =
            new ApplicationDbContext(options);


        // -------------------------------------------------
        // 2. Gerçek Ed25519 Key 1 ve Key 2 üret
        // -------------------------------------------------
        var key1 =
            CreateKeyPair();

        var key2 =
            CreateKeyPair();


        var protector =
            TestPrivateKeyProtector.Create();


        // API tarafında private key'ler plaintext tutulmuyor.
        // LicenseSigningService DB'den gelen protected key'i
        // açarak imza atıyor.
        byte[] protectedKey1 =
            protector.Protect(
                key1.PrivateKey);

        byte[] protectedKey2 =
            protector.Protect(
                key2.PrivateKey);


        // -------------------------------------------------
        // 3. Company oluştur
        // -------------------------------------------------
        var company =
            new Company
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Rotation Test Company",

                CreatedAt =
                    DateTime.UtcNow
            };


        // -------------------------------------------------
        // 4. Project oluştur
        //
        // Başlangıçta KeyVersion 1 aktif.
        // -------------------------------------------------
        var project =
            new Project
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Rotation ERP",

                Version =
                    "1.0",

                KeyVersion =
                    1,

                PublicKey =
                    key1.PublicKey,

                PrivateKey =
                    protectedKey1,

                CreatedAt =
                    DateTime.UtcNow
            };


        var projectKey1 =
            new ProjectKey
            {
                Id =
                    Guid.NewGuid(),

                ProjectId =
                    project.Id,

                KeyVersion =
                    1,

                PublicKey =
                    key1.PublicKey,

                PrivateKey =
                    protectedKey1,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow,

                Project =
                    project
            };


        var projectKey2 =
            new ProjectKey
            {
                Id =
                    Guid.NewGuid(),

                ProjectId =
                    project.Id,

                KeyVersion =
                    2,

                PublicKey =
                    key2.PublicKey,

                PrivateKey =
                    protectedKey2,

                // Henüz aktif değil.
                IsActive =
                    false,

                CreatedAt =
                    DateTime.UtcNow,

                Project =
                    project
            };


        project.Keys.Add(
            projectKey1);

        project.Keys.Add(
            projectKey2);


        dbContext.Companies.Add(
            company);

        dbContext.Projects.Add(
            project);

        await dbContext.SaveChangesAsync();


        // -------------------------------------------------
        // 5. LicenseService oluştur
        // -------------------------------------------------
        var signingService =
            new LicenseSigningService(
                protector);

        var licenseService =
            new LicenseService(
                dbContext,
                signingService);


        // -------------------------------------------------
        // 6. Gerçek lisans hakkı
        //
        // Bu tarih rotation tarafından ASLA
        // değiştirilmemeli.
        // -------------------------------------------------
        DateTime validUntil =
            DateTime.UtcNow.AddYears(1);


        var createdLicense =
            await licenseService.CreateLicenseAsync(
                company.Id,
                project.Id,
                new List<string>
                {
                "Reports",
                "Inventory"
                },
                validUntil);


        Assert.NotNull(
            createdLicense);


        Guid licenseId =
            createdLicense.LicenseId;

        string licenseKey =
            createdLicense.LicenseKey;


        // -------------------------------------------------
        // 7. Key 1 aktifken signed license üret
        // -------------------------------------------------
        var signedWithKey1 =
            await licenseService.GenerateLicenseAsync(
                company.Id,
                licenseKey);


        Assert.NotNull(
            signedWithKey1);

        Assert.Equal(
            1,
            signedWithKey1.Body.KeyVersion);


        DateTime key1Expiry =
            signedWithKey1.Body.ExpiryDate;


        // -------------------------------------------------
        // 8. Key 1 lisansının gerçek signature'ını
        // Public Key 1 ile doğrula
        // -------------------------------------------------
        var providerBeforeRotation =
            new MultiKeyPublicKeyProvider(
                new Dictionary<int, byte[]>
                {
                { 1, key1.PublicKey },
                { 2, key2.PublicKey }
                },
                "Rotation ERP",
                "1.0");


        var verifier =
            new LicenseVerificationService(
                providerBeforeRotation);


        Assert.True(
            verifier.Verify(
                signedWithKey1.Body,
                signedWithKey1.Signature));


        // -------------------------------------------------
        // 9. KEY ROTATION
        //
        // Key 1 artık yeni lisans imzalamayacak.
        // Key 2 aktif hale geliyor.
        //
        // Burada sadece aktif signing key değişiyor.
        // License.ValidUntil'e dokunmuyoruz.
        // -------------------------------------------------
        projectKey1.IsActive =
            false;

        projectKey2.IsActive =
            true;


        // Projects üzerindeki compatibility alanlarını da
        // yeni aktif key ile senkronize ediyoruz.
        project.KeyVersion =
            2;

        project.PublicKey =
            key2.PublicKey;

        project.PrivateKey =
            protectedKey2;


        await dbContext.SaveChangesAsync();


        // -------------------------------------------------
        // 10. Rotation SONRASI DB'deki gerçek
        // lisans süresini kontrol et.
        // -------------------------------------------------
        var licenseAfterRotation =
            await dbContext.Licenses
                .SingleAsync(
                    l => l.Id == licenseId);


        Assert.Equal(
            validUntil,
            licenseAfterRotation.ValidUntil);


        // Rotation mevcut signed license'ın
        // ExpiryDate'ini değiştirmemeli.
        Assert.Equal(
            key1Expiry,
            signedWithKey1.Body.ExpiryDate);


        // -------------------------------------------------
        // 11. Eski Key 1 lisansı rotation'dan sonra
        // hâlâ doğrulanabilmeli.
        //
        // Çünkü client eski PublicKey1'i henüz
        // kaldırmadı.
        // -------------------------------------------------
        Assert.True(
            verifier.Verify(
                signedWithKey1.Body,
                signedWithKey1.Signature));


        // -------------------------------------------------
        // 12. Renewal yap.
        //
        // Artık aktif key = Key 2 olduğu için
        // yeni lisans KeyVersion 2 ile imzalanmalı.
        // -------------------------------------------------
        var renewedLicense =
            await licenseService.RenewLicenseAsync(
                company.Id,
                licenseKey);


        Assert.NotNull(
            renewedLicense);


        Assert.Equal(
            2,
            renewedLicense.Body.KeyVersion);


        // -------------------------------------------------
        // 13. Yeni lisans Key 2 ile gerçekten
        // doğrulanabiliyor mu?
        // -------------------------------------------------
        Assert.True(
            verifier.Verify(
                renewedLicense.Body,
                renewedLicense.Signature));


        // -------------------------------------------------
        // 14. Rotation + renewal sonrasında
        // ValidUntil hâlâ aynı olmalı.
        // -------------------------------------------------
        var finalLicense =
            await dbContext.Licenses
                .SingleAsync(
                    l => l.Id == licenseId);


        Assert.Equal(
            validUntil,
            finalLicense.ValidUntil);


        // Signed license gerçek lisans hakkını
        // hiçbir zaman aşamaz.
        Assert.True(
            renewedLicense.Body.ExpiryDate <=
            finalLicense.ValidUntil);


        // Ayrıca normal durumda 30 günlük
        // signed-license penceresini de aşmamalı.
        Assert.True(
            renewedLicense.Body.ExpiryDate <=
            renewedLicense.Body.IssuedAt.AddDays(30));
    }

}
