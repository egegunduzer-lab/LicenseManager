using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LicenseManager.Api.Data;
using LicenseManager.Api.Services;
using LicenseManager.Client.Services;
using LicenseManager.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LicenseManager.Tests;

public class LicenseCreationEndToEndTests
{
    // -------------------------------------------------
    // Test için public key provider
    // -------------------------------------------------
    private class TestPublicKeyProvider
        : ILicensePublicKeyProvider
    {
        private readonly Dictionary<int, byte[]> _keys;

        public TestPublicKeyProvider(
            string projectName,
            string projectVersion,
            Dictionary<int, byte[]> keys)
        {
            ProjectName =
                projectName;

            ProjectVersion =
                projectVersion;

            _keys =
                keys;
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
                publicKey =
                    key;

                return true;
            }

            publicKey =
                Array.Empty<byte>();

            return false;
        }
    }


    // -------------------------------------------------
    // Her test için ayrı geçici DB
    // -------------------------------------------------
    private static ApplicationDbContext
        CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        return new ApplicationDbContext(
            options);
    }


    // =================================================
    // ANA TEST
    // Company + Project
    // -> Create License
    // -> Generate Signed License
    // -> Client Verify
    // =================================================
    [Fact]
    public async Task CreatedLicense_ShouldBeGeneratedAndVerifiedByClient()
    {
        await using var dbContext =
            CreateDbContext();


        // -------------------------------------------------
        // 1. Gerçek Ed25519 keypair üret
        // -------------------------------------------------
        var keyService =
            new Ed25519KeyService();

        var keyPair =
            keyService.GenerateKeyPair();


        // -------------------------------------------------
        // 2. Private key'i DB'ye koymadan önce koru
        // -------------------------------------------------
        var privateKeyProtector =
            TestPrivateKeyProtector.Create();

        byte[] protectedPrivateKey =
            privateKeyProtector.Protect(
                keyPair.PrivateKey);


        // -------------------------------------------------
        // 3. Test firması oluştur
        // -------------------------------------------------
        var company =
            new Company
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Integration Company",

                CreatedAt =
                    DateTime.UtcNow
            };


        // -------------------------------------------------
        // 4. Test projesi oluştur
        // -------------------------------------------------
        var project =
            new Project
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Integration ERP",

                Version =
                    "1.0",

                KeyVersion =
                    1,

                PublicKey =
                    keyPair.PublicKey,

                PrivateKey =
                    protectedPrivateKey,

                CreatedAt =
                    DateTime.UtcNow
            };


        // -------------------------------------------------
        // 5. Aktif ProjectKey oluştur
        // -------------------------------------------------
        var projectKey =
            new ProjectKey
            {
                Id =
                    Guid.NewGuid(),

                ProjectId =
                    project.Id,

                KeyVersion =
                    1,

                PublicKey =
                    keyPair.PublicKey,

                PrivateKey =
                    protectedPrivateKey,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow
            };


        dbContext.Companies.Add(
            company);

        dbContext.Projects.Add(
            project);

        dbContext.ProjectKeys.Add(
            projectKey);

        await dbContext.SaveChangesAsync();


        // -------------------------------------------------
        // 6. Gerçek LicenseService oluştur
        // -------------------------------------------------
        var signingService =
            new LicenseSigningService(
                privateKeyProtector);

        var licenseService =
            new LicenseService(
                dbContext,
                signingService);


        // -------------------------------------------------
        // 7. Kayıtlı firmaya sıfırdan yeni lisans oluştur
        // -------------------------------------------------
        DateTime validUntil =
    DateTime.UtcNow.AddYears(1);

        var createdLicense =
            await licenseService
                .CreateLicenseAsync(
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


        // LicenseKey gerçekten üretildi mi?
        Assert.False(
            string.IsNullOrWhiteSpace(
                createdLicense!.LicenseKey));

        Assert.StartsWith(
            "LM-",
            createdLicense.LicenseKey);


        // -------------------------------------------------
        // 8. DB'ye gerçekten kaydedildi mi?
        // -------------------------------------------------
        var savedLicense =
            await dbContext.Licenses
                .SingleAsync();


        Assert.Equal(
            company.Id,
            savedLicense.CompanyId);

        Assert.Equal(
            project.Id,
            savedLicense.ProjectId);

        Assert.Equal(
            createdLicense.LicenseKey,
            savedLicense.LicenseKey);

        Assert.False(
            savedLicense.IsBlocked);


        // -------------------------------------------------
        // 9. Lisans süresi yaklaşık 30 gün mü?
        // -------------------------------------------------
        TimeSpan licenseDuration =
            savedLicense.ExpiryDate -
            savedLicense.IssuedAt;


        Assert.True(
            licenseDuration >=
            TimeSpan.FromDays(29.99));

        Assert.True(
            licenseDuration <=
            TimeSpan.FromDays(30.01));


        // -------------------------------------------------
        // 10. Yeni LicenseKey ile imzalı lisans üret
        // -------------------------------------------------
        var signedLicense =
            await licenseService
                .GenerateLicenseAsync(
                    company.Id,
                    createdLicense.LicenseKey);


        Assert.NotNull(
            signedLicense);


        Assert.False(
            string.IsNullOrWhiteSpace(
                signedLicense!.Signature));


        // Server aktif KeyVersion 1'i kullanmalı
        Assert.Equal(
            1,
            signedLicense.Body.KeyVersion);


        // -------------------------------------------------
        // 11. Payload doğru firmaya/projeye ait mi?
        // -------------------------------------------------
        Assert.Equal(
            company.Id,
            signedLicense.Body.CompanyId);

        Assert.Equal(
            project.Id,
            signedLicense.Body.ProjectId);

        Assert.Equal(
            "Integration ERP",
            signedLicense.Body.ProjectName);

        Assert.Equal(
            "1.0",
            signedLicense.Body.ProjectVersion);


        // -------------------------------------------------
        // 12. Client için public key provider
        // -------------------------------------------------
        var publicKeyProvider =
            new TestPublicKeyProvider(
                "Integration ERP",
                "1.0",
                new Dictionary<int, byte[]>
                {
                    {
                        1,
                        keyPair.PublicKey
                    }
                });


        var verificationService =
            new LicenseVerificationService(
                publicKeyProvider);


        // -------------------------------------------------
        // 13. Client gerçek imzayı doğrulasın
        // -------------------------------------------------
        bool signatureValid =
            verificationService.Verify(
                signedLicense.Body,
                signedLicense.Signature);


        Assert.True(
            signatureValid);
    }
}
