using System.Net;
using LicenseManager.Client.Models;
using LicenseManager.Client.Services;
using LicenseManager.Core.DTOs;

namespace LicenseManager.Tests;

public class LicenseRevocationPolicyTests
{
    // -------------------------------------------------
    // Signature doğrulamasını başarılı kabul eden
    // sahte verification servisi
    // -------------------------------------------------
    private class AlwaysValidVerificationService
        : ILicenseVerificationService
    {
        public bool Verify(
            LicensePayload body,
            string signatureBase64)
        {
            return true;
        }
    }


    // -------------------------------------------------
    // HTTP cevaplarını kontrol etmek için fake handler
    // -------------------------------------------------
    private class FakeHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public int CallCount { get; private set; }

        public FakeHttpMessageHandler(
            HttpStatusCode statusCode)
        {
            _statusCode =
                statusCode;
        }


        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            CallCount++;

            return Task.FromResult(
                new HttpResponseMessage(
                    _statusCode));
        }
    }


    // -------------------------------------------------
    // Test lisansı oluştur
    // -------------------------------------------------
    private static SignedLicenseResponse
        CreateLicense(
            Guid companyId,
            DateTime expiryDate)
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
                            "Reports"
                        },

                    IssuedAt =
                        DateTime.UtcNow.AddDays(-29),

                    ExpiryDate =
                        expiryDate
                },

            // Fake verifier kullandığımız için
            // gerçek Ed25519 signature gerekmiyor.
            Signature =
                "TEST-SIGNATURE"
        };
    }


    // =================================================
    // TEST 1
    //
    // Cache hâlâ geçerli.
    // Renewal server tarafından reddediliyor.
    //
    // Mevcut offline lisans expiry tarihine
    // kadar kullanılmaya devam etmeli.
    // =================================================
    [Fact]
    public async Task ValidCachedLicense_WhenRenewalRejected_ShouldRemainValid()
    {
        Guid companyId =
            Guid.NewGuid();

        string tempFilePath =
            Path.Combine(
                Path.GetTempPath(),
                $"license-{Guid.NewGuid()}.json");


        try
        {
            // -----------------------------------------
            // Expiry'ye yalnızca 1 gün kaldı.
            // Bu nedenle renewal denenmeli.
            // -----------------------------------------
            var cachedLicense =
                CreateLicense(
                    companyId,
                    DateTime.UtcNow.AddDays(1));


            var fileService =
                new LicenseFileService(
                    tempFilePath);


            await fileService.SaveAsync(
                cachedLicense);


            var verificationService =
                new AlwaysValidVerificationService();


            var validatorService =
                new LicenseValidatorService(
                    verificationService);


            // -----------------------------------------
            // Server 404 döndürüyor.
            // LicenseApiService bunu Rejected
            // olarak değerlendirmeli.
            // -----------------------------------------
            var handler =
                new FakeHttpMessageHandler(
                    HttpStatusCode.NotFound);


            using var httpClient =
                new HttpClient(handler)
                {
                    BaseAddress =
                        new Uri(
                            "https://localhost/")
                };


            var apiService =
                new LicenseApiService(
                    httpClient);


            var manager =
                new LicenseManagerService(
                    fileService,
                    apiService,
                    validatorService,
                    renewalThresholdDays: 7);


            var result =
                await manager.CheckLicenseAsync(
                    companyId,
                    "LM-REVOCATION-TEST",
                    "Reports");


            // -----------------------------------------
            // Server reddetse bile cache henüz
            // expire olmadığı için Client çalışmalı.
            // -----------------------------------------
            Assert.True(
                result.IsValid);


            // Gerçekten server'a renewal
            // isteği yapılmış olmalı.
            Assert.Equal(
                1,
                handler.CallCount);
        }
        finally
        {
            if (File.Exists(
                    tempFilePath))
            {
                File.Delete(
                    tempFilePath);
            }
        }
    }


    // =================================================
    // TEST 2
    //
    // Cache'in süresi dolmuş.
    // Server da lisansı reddediyor.
    //
    // Artık kullanılabilecek geçerli
    // offline lisans yok.
    // =================================================
    [Fact]
    public async Task ExpiredCachedLicense_WhenServerRejects_ShouldBeInvalid()
    {
        Guid companyId =
            Guid.NewGuid();

        string tempFilePath =
            Path.Combine(
                Path.GetTempPath(),
                $"license-{Guid.NewGuid()}.json");


        try
        {
            // -----------------------------------------
            // Cache artık expired.
            // -----------------------------------------
            var cachedLicense =
                CreateLicense(
                    companyId,
                    DateTime.UtcNow.AddDays(-1));


            var fileService =
                new LicenseFileService(
                    tempFilePath);


            await fileService.SaveAsync(
                cachedLicense);


            var verificationService =
                new AlwaysValidVerificationService();


            var validatorService =
                new LicenseValidatorService(
                    verificationService);


            // -----------------------------------------
            // Server lisansı reddediyor.
            // -----------------------------------------
            var handler =
                new FakeHttpMessageHandler(
                    HttpStatusCode.NotFound);


            using var httpClient =
                new HttpClient(handler)
                {
                    BaseAddress =
                        new Uri(
                            "https://localhost/")
                };


            var apiService =
                new LicenseApiService(
                    httpClient);


            var manager =
                new LicenseManagerService(
                    fileService,
                    apiService,
                    validatorService,
                    renewalThresholdDays: 7);


            var result =
                await manager.CheckLicenseAsync(
                    companyId,
                    "LM-REVOCATION-TEST",
                    "Reports");


            // -----------------------------------------
            // Cache expired + server rejected
            // => artık lisans geçersiz.
            // -----------------------------------------
            Assert.False(
                result.IsValid);


            Assert.Equal(
                "Lisans doğrulanamadı.",
                result.Message);


            Assert.Equal(
                1,
                handler.CallCount);
        }
        finally
        {
            if (File.Exists(
                    tempFilePath))
            {
                File.Delete(
                    tempFilePath);
            }
        }
    }
}