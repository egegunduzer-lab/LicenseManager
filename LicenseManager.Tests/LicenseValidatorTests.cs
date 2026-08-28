using LicenseManager.Client.Services;
using LicenseManager.Core.DTOs;

namespace LicenseManager.Tests;

public class LicenseValidatorTests
{
    // -------------------------------------------------
    // Testlerde gerçek Ed25519 kullanmak yerine
    // istediğimiz sonucu döndüren sahte servis
    // -------------------------------------------------
    private class FakeVerificationService
        : ILicenseVerificationService
    {
        private readonly bool _result;

        public FakeVerificationService(bool result)
        {
            _result = result;
        }

        public bool Verify(
            LicensePayload body,
            string signatureBase64)
        {
            return _result;
        }
    }


    // -------------------------------------------------
    // Testlerde kullanacağımız geçerli lisans
    //
    // CompanyId dışarıdan veriliyor.
    // Böylece test, Client'ın beklediği şirket kimliğini
    // açıkça tanımlayabiliyor.
    // -------------------------------------------------
    private static SignedLicenseResponse CreateValidLicense(
        Guid companyId)
    {
        return new SignedLicenseResponse
        {
            Body =
                new LicensePayload
                {
                    LicenseId =
                        Guid.NewGuid(),

                    CompanyId =
                        companyId,

                    ProjectId =
                        Guid.NewGuid(),

                    ProjectName =
                        "Giltas ERP",

                    ProjectVersion =
                        "1.0",

                    KeyVersion =
                        1,

                    Modules =
                        new List<string>
                        {
                            "Reports",
                            "Inventory"
                        },

                    IssuedAt =
                        DateTime.UtcNow.AddDays(-1),

                    ExpiryDate =
                        DateTime.UtcNow.AddDays(30)
                },

            // FakeVerificationService kullandığımız için
            // burada gerçek signature'a ihtiyacımız yok.
            Signature =
                "TEST-SIGNATURE"
        };
    }


    // =================================================
    // TEST 1
    // Her şey doğruysa lisans geçerli olmalı
    // =================================================
    [Fact]
    public void ValidLicense_ShouldBeValid()
    {
        var verificationService =
            new FakeVerificationService(true);

        var validator =
            new LicenseValidatorService(
                verificationService);


        Guid expectedCompanyId =
            Guid.NewGuid();

        var license =
            CreateValidLicense(
                expectedCompanyId);


        var result =
            validator.Validate(
                license,
                "Reports",
                expectedCompanyId);


        Assert.True(
            result.IsValid);

        Assert.Equal(
            "Lisans geçerli.",
            result.Message);
    }


    // =================================================
    // TEST 2
    // İmza geçersizse lisans reddedilmeli
    // =================================================
    [Fact]
    public void InvalidSignature_ShouldReturnGenericError()
    {
        var verificationService =
            new FakeVerificationService(false);

        var validator =
            new LicenseValidatorService(
                verificationService);


        Guid expectedCompanyId =
            Guid.NewGuid();

        var license =
            CreateValidLicense(
                expectedCompanyId);


        var result =
            validator.Validate(
                license,
                "Reports",
                expectedCompanyId);


        Assert.False(
            result.IsValid);

        Assert.Equal(
            "Lisans doğrulanamadı.",
            result.Message);
    }


    // =================================================
    // TEST 3
    // Süresi dolmuş lisans reddedilmeli
    // =================================================
    [Fact]
    public void ExpiredLicense_ShouldReturnGenericError()
    {
        var verificationService =
            new FakeVerificationService(true);

        var validator =
            new LicenseValidatorService(
                verificationService);


        Guid expectedCompanyId =
            Guid.NewGuid();

        var license =
            CreateValidLicense(
                expectedCompanyId);


        // Lisansı geçmiş tarihe çekiyoruz
        license.Body.ExpiryDate =
            DateTime.UtcNow.AddDays(-1);


        var result =
            validator.Validate(
                license,
                "Reports",
                expectedCompanyId);


        Assert.False(
            result.IsValid);

        Assert.Equal(
            "Lisans doğrulanamadı.",
            result.Message);
    }


    // =================================================
    // TEST 4
    // İstenen modül yoksa lisans reddedilmeli
    // =================================================
    [Fact]
    public void MissingModule_ShouldReturnGenericError()
    {
        var verificationService =
            new FakeVerificationService(true);

        var validator =
            new LicenseValidatorService(
                verificationService);


        Guid expectedCompanyId =
            Guid.NewGuid();

        var license =
            CreateValidLicense(
                expectedCompanyId);


        var result =
            validator.Validate(
                license,
                "Admin",
                expectedCompanyId);


        Assert.False(
            result.IsValid);

        Assert.Equal(
            "Lisans doğrulanamadı.",
            result.Message);
    }


    // =================================================
    // TEST 5
    // Farklı hata nedenleri kullanıcıya
    // aynı mesajı vermeli
    // =================================================
    [Fact]
    public void DifferentFailures_ShouldReturnSameMessage()
    {
        // -------------------------------------------------
        // İmza hatası
        // -------------------------------------------------

        var invalidSignatureValidator =
            new LicenseValidatorService(
                new FakeVerificationService(false));


        Guid firstCompanyId =
            Guid.NewGuid();

        var firstLicense =
            CreateValidLicense(
                firstCompanyId);


        var signatureResult =
            invalidSignatureValidator.Validate(
                firstLicense,
                "Reports",
                firstCompanyId);


        // -------------------------------------------------
        // Expiry hatası
        // -------------------------------------------------

        var validSignatureValidator =
            new LicenseValidatorService(
                new FakeVerificationService(true));


        Guid secondCompanyId =
            Guid.NewGuid();

        var secondLicense =
            CreateValidLicense(
                secondCompanyId);


        secondLicense.Body.ExpiryDate =
            DateTime.UtcNow.AddDays(-1);


        var expiryResult =
            validSignatureValidator.Validate(
                secondLicense,
                "Reports",
                secondCompanyId);


        // -------------------------------------------------
        // Module hatası
        // -------------------------------------------------

        Guid thirdCompanyId =
            Guid.NewGuid();

        var thirdLicense =
            CreateValidLicense(
                thirdCompanyId);


        var moduleResult =
            validSignatureValidator.Validate(
                thirdLicense,
                "Admin",
                thirdCompanyId);


        // -------------------------------------------------
        // Hepsinin mesajı aynı olmalı
        // -------------------------------------------------

        Assert.Equal(
            signatureResult.Message,
            expiryResult.Message);

        Assert.Equal(
            expiryResult.Message,
            moduleResult.Message);

        Assert.Equal(
            "Lisans doğrulanamadı.",
            signatureResult.Message);
    }


    // -------------------------------------------------
    // Verify metodunun kaç kez çalıştığını sayan servis
    // -------------------------------------------------
    private class CountingVerificationService
        : ILicenseVerificationService
    {
        public int CallCount { get; private set; }

        public bool Verify(
            LicensePayload body,
            string signatureBase64)
        {
            CallCount++;

            return false;
        }
    }


    // =================================================
    // TEST 6
    // Eksik lisans masking işlemi de
    // verification yolunu çalıştırmalı
    // =================================================
    [Fact]
    public void MissingLicenseMaskingWork_ShouldRunVerificationPath()
    {
        var verificationService =
            new CountingVerificationService();

        var validator =
            new LicenseValidatorService(
                verificationService);


        validator.PerformMissingLicenseMaskingWork();


        Assert.Equal(
            1,
            verificationService.CallCount);
    }


    // =================================================
    // TEST 7
    // Lisans başka şirkete aitse reddedilmeli
    // =================================================
    [Fact]
    public void DifferentCompanyId_ShouldReturnInvalid()
    {
        var verificationService =
            new FakeVerificationService(true);

        var validator =
            new LicenseValidatorService(
                verificationService);


        Guid licenseCompanyId =
            Guid.NewGuid();

        Guid differentCompanyId =
            Guid.NewGuid();


        var license =
            CreateValidLicense(
                licenseCompanyId);


        var result =
            validator.Validate(
                license,
                "Reports",
                differentCompanyId);


        Assert.False(
            result.IsValid);

        Assert.Equal(
            "Lisans doğrulanamadı.",
            result.Message);
    }
}