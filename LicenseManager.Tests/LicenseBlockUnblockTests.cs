using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LicenseManager.Api.Controllers;
using LicenseManager.Api.Data;
using LicenseManager.Api.Services;
using LicenseManager.Core.DTOs;
using LicenseManager.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LicenseManager.Tests;

public class LicenseBlockUnblockTests
{
    private const string AdminKey =
        "TEST-ADMIN-KEY";


    // -------------------------------------------------
    // Her test için ayrı InMemory database
    // -------------------------------------------------
    private static ApplicationDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        return new ApplicationDbContext(options);
    }


    // -------------------------------------------------
    // Controller oluştur
    // -------------------------------------------------
    private static LicensesController CreateController(
        ApplicationDbContext dbContext)
    {
        var privateKeyProtector =
            TestPrivateKeyProtector.Create();

        var signingService =
            new LicenseSigningService(
                privateKeyProtector);

        var licenseService =
            new LicenseService(
                dbContext,
                signingService);


        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        {
                            "AdminSecurity:ApiKey",
                            AdminKey
                        }
                    })
                .Build();


        var adminApiKeyValidator =
            new AdminApiKeyValidator(
                configuration);


        return new LicensesController(
            licenseService,
            adminApiKeyValidator);
    }


    // =================================================
    // ANA TEST
    //
    // generate başarılı
    // -> block
    // -> generate başarısız
    // -> unblock
    // -> generate tekrar başarılı
    // =================================================
    [Fact]
    public async Task BlockAndUnblock_ShouldControlLicenseGeneration()
    {
        await using var dbContext =
            CreateDbContext();


        // -------------------------------------------------
        // 1. Gerçek Ed25519 keypair oluştur
        // -------------------------------------------------
        var keyService =
            new Ed25519KeyService();

        var keyPair =
            keyService.GenerateKeyPair();


        // -------------------------------------------------
        // 2. Private key'i koru
        // -------------------------------------------------
        var privateKeyProtector =
            TestPrivateKeyProtector.Create();

        byte[] protectedPrivateKey =
            privateKeyProtector.Protect(
                keyPair.PrivateKey);


        // -------------------------------------------------
        // 3. Company oluştur
        // -------------------------------------------------
        var company =
            new Company
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Block Test Company",

                CreatedAt =
                    DateTime.UtcNow
            };


        // -------------------------------------------------
        // 4. Project oluştur
        // -------------------------------------------------
        var project =
            new Project
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Block Test ERP",

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
                    DateTime.UtcNow,

                Project =
                    project
            };


        // -------------------------------------------------
        // 6. Başlangıçta blocked olmayan lisans
        // -------------------------------------------------
        var license =
    new License
    {
        Id =
            Guid.NewGuid(),

        CompanyId =
            company.Id,

        ProjectId =
            project.Id,

        LicenseKey =
            "LM-BLOCK-TEST",

        Modules =
            new List<string>
            {
                "Reports"
            },

        IssuedAt =
            DateTime.UtcNow,

        ExpiryDate =
            DateTime.UtcNow.AddDays(30),

        // YENİ
        ValidUntil =
            DateTime.UtcNow.AddYears(1),

        IsBlocked =
            false,

        CreatedAt =
            DateTime.UtcNow
    };


        dbContext.Companies.Add(
            company);

        dbContext.Projects.Add(
            project);

        dbContext.ProjectKeys.Add(
            projectKey);

        dbContext.Licenses.Add(
            license);

        await dbContext.SaveChangesAsync();


        var controller =
            CreateController(
                dbContext);


        var request =
            new LicenseGenerateRequest
            {
                CompanyId =
                    company.Id,

                LicenseKey =
                    license.LicenseKey
            };


        // =================================================
        // 7. BLOCK ÖNCESİ
        // generate başarılı olmalı
        // =================================================

        var beforeBlock =
            await controller.GenerateLicense(
                request);


        Assert.IsType<OkObjectResult>(
            beforeBlock);


        // =================================================
        // 8. LİSANSI BLOCK ET
        // =================================================

        var blockResult =
            await controller.BlockLicense(
                license.Id,
                AdminKey);


        var blockOk =
            Assert.IsType<OkObjectResult>(
                blockResult);


        Assert.Equal(
            200,
            blockOk.StatusCode);


        // DB gerçekten güncellendi mi?
        var blockedLicense =
            await dbContext.Licenses
                .SingleAsync(
                    x => x.Id == license.Id);


        Assert.True(
            blockedLicense.IsBlocked);


        // =================================================
        // 9. BLOCK SONRASI
        // generate artık başarısız olmalı
        // =================================================

        var afterBlock =
            await controller.GenerateLicense(
                request);


        var blockedNotFound =
            Assert.IsType<NotFoundObjectResult>(
                afterBlock);


        Assert.Equal(
            404,
            blockedNotFound.StatusCode);


        Assert.Equal(
            "Lisans doğrulanamadı.",
            blockedNotFound.Value);


        // =================================================
        // 10. UNBLOCK
        // =================================================

        var unblockResult =
            await controller.UnblockLicense(
                license.Id,
                AdminKey);


        var unblockOk =
            Assert.IsType<OkObjectResult>(
                unblockResult);


        Assert.Equal(
            200,
            unblockOk.StatusCode);


        // DB tekrar false oldu mu?
        var unblockedLicense =
            await dbContext.Licenses
                .SingleAsync(
                    x => x.Id == license.Id);


        Assert.False(
            unblockedLicense.IsBlocked);


        // =================================================
        // 11. UNBLOCK SONRASI
        // generate tekrar başarılı olmalı
        // =================================================

        var afterUnblock =
            await controller.GenerateLicense(
                request);


        Assert.IsType<OkObjectResult>(
            afterUnblock);
    }

    // =================================================
    // TEST 2
    // Admin key yok -> 401
    // Lisans durumu değişmemeli
    // =================================================
    [Fact]
    public async Task BlockLicense_WithoutAdminKey_ShouldReturn401AndNotChangeLicense()
    {
        await using var dbContext =
            CreateDbContext();


        var company =
            new Company
            {
                Id = Guid.NewGuid(),
                Name = "No Key Company",
                CreatedAt = DateTime.UtcNow
            };


        var project =
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "No Key ERP",
                Version = "1.0",
                KeyVersion = 1,
                PublicKey = Array.Empty<byte>(),
                PrivateKey = Array.Empty<byte>(),
                CreatedAt = DateTime.UtcNow
            };


        var license =
            new License
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                ProjectId = project.Id,

                LicenseKey =
                    "LM-NO-ADMIN-KEY",

                Modules =
                    new List<string>
                    {
                    "Reports"
                    },

                IssuedAt =
                    DateTime.UtcNow,

                ExpiryDate =
                    DateTime.UtcNow.AddDays(30),

                IsBlocked =
                    false,

                CreatedAt =
                    DateTime.UtcNow
            };


        dbContext.Companies.Add(company);
        dbContext.Projects.Add(project);
        dbContext.Licenses.Add(license);

        await dbContext.SaveChangesAsync();


        var controller =
            CreateController(dbContext);


        // Admin key gönderilmiyor
        var result =
            await controller.BlockLicense(
                license.Id,
                null);


        var unauthorized =
            Assert.IsType<UnauthorizedObjectResult>(
                result);


        Assert.Equal(
            401,
            unauthorized.StatusCode);


        // DB değişmemiş olmalı
        var savedLicense =
            await dbContext.Licenses
                .SingleAsync(
                    x => x.Id == license.Id);


        Assert.False(
            savedLicense.IsBlocked);
    }


    // =================================================
    // TEST 3
    // Yanlış Admin key -> 401
    // Lisans durumu değişmemeli
    // =================================================
    [Fact]
    public async Task BlockLicense_WithWrongAdminKey_ShouldReturn401AndNotChangeLicense()
    {
        await using var dbContext =
            CreateDbContext();


        var company =
            new Company
            {
                Id = Guid.NewGuid(),
                Name = "Wrong Key Company",
                CreatedAt = DateTime.UtcNow
            };


        var project =
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Wrong Key ERP",
                Version = "1.0",
                KeyVersion = 1,
                PublicKey = Array.Empty<byte>(),
                PrivateKey = Array.Empty<byte>(),
                CreatedAt = DateTime.UtcNow
            };


        var license =
            new License
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                ProjectId = project.Id,

                LicenseKey =
                    "LM-WRONG-ADMIN-KEY",

                Modules =
                    new List<string>
                    {
                    "Reports"
                    },

                IssuedAt =
                    DateTime.UtcNow,

                ExpiryDate =
                    DateTime.UtcNow.AddDays(30),

                IsBlocked =
                    false,

                CreatedAt =
                    DateTime.UtcNow
            };


        dbContext.Companies.Add(company);
        dbContext.Projects.Add(project);
        dbContext.Licenses.Add(license);

        await dbContext.SaveChangesAsync();


        var controller =
            CreateController(dbContext);


        // Yanlış Admin key
        var result =
            await controller.BlockLicense(
                license.Id,
                "YANLIS-ADMIN-KEY");


        var unauthorized =
            Assert.IsType<UnauthorizedObjectResult>(
                result);


        Assert.Equal(
            401,
            unauthorized.StatusCode);


        // DB değişmemiş olmalı
        var savedLicense =
            await dbContext.Licenses
                .SingleAsync(
                    x => x.Id == license.Id);


        Assert.False(
            savedLicense.IsBlocked);
    }

}
