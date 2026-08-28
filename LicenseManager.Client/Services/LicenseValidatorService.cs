using LicenseManager.Client.Configuration;
using LicenseManager.Client.Models;
using LicenseManager.Core.DTOs;

namespace LicenseManager.Client.Services;

public class LicenseValidatorService
{
    private const string GenericErrorMessage =
        "Lisans doğrulanamadı.";

    private readonly ILicenseVerificationService
        _verificationService;


    public LicenseValidatorService(
        ILicenseVerificationService verificationService)
    {
        _verificationService =
            verificationService;
    }


    // -------------------------------------------------
    // Eksik lisans dosyasında da doğrulama yoluna
    // mümkün olduğunca benzer bir işlem yaptır.
    // -------------------------------------------------
    public void PerformMissingLicenseMaskingWork()
    {
        // Client içinde gömülü olan ilk key version'ı kullan.
        // Hiç key yoksa 1 kullanılır ve Verify false döner.
        int dummyKeyVersion =
            EmbeddedLicenseKey.PublicKeys.Keys
                .OrderBy(k => k)
                .FirstOrDefault();

        if (dummyKeyVersion <= 0)
        {
            dummyKeyVersion = 1;
        }


        var dummyPayload =
            new LicensePayload
            {
                LicenseId =
                    Guid.Empty,

                CompanyId =
                    Guid.Empty,

                ProjectId =
                    Guid.Empty,

                ProjectName =
                    EmbeddedLicenseKey.ProjectName,

                ProjectVersion =
                    EmbeddedLicenseKey.ProjectVersion,

                KeyVersion =
                    dummyKeyVersion,

                Modules =
                    new List<string>(),

                IssuedAt =
                    DateTime.UnixEpoch,

                ExpiryDate =
                    DateTime.UnixEpoch
            };


        // Ed25519 imzası 64 byte.
        // Format olarak geçerli fakat gerçek olmayan imza.
        string dummySignature =
            Convert.ToBase64String(
                new byte[64]);


        _ = _verificationService.Verify(
            dummyPayload,
            dummySignature);
    }


    // =================================================
    // ASIL KULLANILMASI GEREKEN VALIDATE
    // =================================================
    public LicenseValidationResult Validate(
        SignedLicenseResponse license,
        string requiredModule,
        Guid expectedCompanyId)
    {
        // -------------------------------------------------
        // 1. Dijital imzayı doğrula
        // -------------------------------------------------
        bool signatureValid =
            _verificationService.Verify(
                license.Body,
                license.Signature);

        if (!signatureValid)
        {
            return InvalidResult();
        }


        // -------------------------------------------------
        // 2. Lisans doğru şirkete mi ait?
        // -------------------------------------------------
        if (license.Body.CompanyId !=
            expectedCompanyId)
        {
            return InvalidResult();
        }


        // -------------------------------------------------
        // 3. Lisansın süresi dolmuş mu?
        // -------------------------------------------------
        if (DateTime.UtcNow >
            license.Body.ExpiryDate.ToUniversalTime())
        {
            return InvalidResult();
        }


        // -------------------------------------------------
        // 4. Gerekli modül mevcut mu?
        // -------------------------------------------------
        bool hasModule =
            license.Body.Modules.Contains(
                requiredModule,
                StringComparer.Ordinal);

        if (!hasModule)
        {
            return InvalidResult();
        }


        // -------------------------------------------------
        // 5. Her şey başarılı
        // -------------------------------------------------
        return new LicenseValidationResult
        {
            IsValid = true,
            Message = "Lisans geçerli."
        };
    }


    // -------------------------------------------------
    // Bütün başarısız durumlar için aynı genel sonuç
    // -------------------------------------------------
    private static LicenseValidationResult InvalidResult()
    {
        return new LicenseValidationResult
        {
            IsValid = false,
            Message = GenericErrorMessage
        };
    }
}