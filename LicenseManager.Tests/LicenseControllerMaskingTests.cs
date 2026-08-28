using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using LicenseManager.Api.Controllers;
using LicenseManager.Api.Data;
using LicenseManager.Api.Services;
using LicenseManager.Core.DTOs;
using LicenseManager.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LicenseManager.Tests;

public class LicenseControllerMaskingTests
{
    // -------------------------------------------------
    // Her test için ayrı geçici veritabanı
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
    // Gerçek Controller + Service oluştur
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


        // -------------------------------------------------
        // Test için sahte configuration oluştur
        // -------------------------------------------------
        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                    {
                        "AdminSecurity:ApiKey",
                        "TEST-ADMIN-KEY"
                    }
                    })
                .Build();


        // -------------------------------------------------
        // Test için AdminApiKeyValidator oluştur
        // -------------------------------------------------
        var adminApiKeyValidator =
            new AdminApiKeyValidator(
                configuration);


        // -------------------------------------------------
        // Controller oluştur
        // -------------------------------------------------
        return new LicensesController(
            licenseService,
            adminApiKeyValidator,
            dbContext);
    }


    // -------------------------------------------------
    // TEST 1
    // Olmayan lisans genel hata vermeli
    // -------------------------------------------------
    [Fact]
    public async Task MissingLicense_ShouldReturnGenericNotFound()
    {
        await using var dbContext =
            CreateDbContext();

        var controller =
            CreateController(dbContext);

        var request =
            new LicenseGenerateRequest
            {
                CompanyId = Guid.NewGuid(),
                LicenseKey = "NOT-EXISTING"
            };

        var result =
            await controller.GenerateLicense(
                request);

        var notFound =
            Assert.IsType<NotFoundObjectResult>(
                result);

        Assert.Equal(
            404,
            notFound.StatusCode);

        Assert.Equal(
            "Lisans doğrulanamadı.",
            notFound.Value);
    }


    // -------------------------------------------------
    // TEST 2
    // Blocked lisans da AYNI genel hatayı vermeli
    // -------------------------------------------------
    [Fact]
    public async Task BlockedLicense_ShouldReturnGenericNotFound()
    {
        await using var dbContext =
            CreateDbContext();

        Guid companyId =
            Guid.NewGuid();

        Guid projectId =
            Guid.NewGuid();


        var company =
            new Company
            {
                Id = companyId,
                Name = "Test Company",
                CreatedAt = DateTime.UtcNow
            };


        var project =
            new Project
            {
                Id = projectId,
                Name = "Test ERP",
                Version = "1.0",

                // Bu test signing aşamasına gelmeyecek.
                PublicKey = Array.Empty<byte>(),
                PrivateKey = Array.Empty<byte>(),

                CreatedAt = DateTime.UtcNow
            };


        var license =
            new License
            {
                Id = Guid.NewGuid(),

                CompanyId = companyId,
                ProjectId = projectId,

                LicenseKey =
                    "BLOCKED-LICENSE",

                Modules =
                    new List<string>
                    {
                        "Reports"
                    },

                IssuedAt =
                    DateTime.UtcNow,

                ExpiryDate =
                    DateTime.UtcNow.AddDays(30),

                IsBlocked = true,

                CreatedAt =
                    DateTime.UtcNow
            };


        dbContext.Companies.Add(company);
        dbContext.Projects.Add(project);
        dbContext.Licenses.Add(license);

        await dbContext.SaveChangesAsync();


        var controller =
            CreateController(dbContext);


        var request =
            new LicenseGenerateRequest
            {
                CompanyId = companyId,
                LicenseKey =
                    "BLOCKED-LICENSE"
            };


        var result =
            await controller.GenerateLicense(
                request);


        var notFound =
            Assert.IsType<NotFoundObjectResult>(
                result);


        Assert.Equal(
            404,
            notFound.StatusCode);

        Assert.Equal(
            "Lisans doğrulanamadı.",
            notFound.Value);
    }


    // -------------------------------------------------
    // TEST 3
    // Missing ve Blocked mesajları birebir aynı olmalı
    // -------------------------------------------------
    [Fact]
    public async Task MissingAndBlockedLicense_ShouldLookTheSame()
    {
        // -------------------------
        // Missing lisans
        // -------------------------

        await using var missingDb =
            CreateDbContext();

        var missingController =
            CreateController(missingDb);

        var missingRequest =
            new LicenseGenerateRequest
            {
                CompanyId = Guid.NewGuid(),
                LicenseKey = "MISSING"
            };

        var missingResult =
            await missingController.GenerateLicense(
                missingRequest);

        var missingNotFound =
            Assert.IsType<NotFoundObjectResult>(
                missingResult);


        // -------------------------
        // Blocked lisans
        // -------------------------

        await using var blockedDb =
            CreateDbContext();

        Guid companyId =
            Guid.NewGuid();

        Guid projectId =
            Guid.NewGuid();


        blockedDb.Companies.Add(
            new Company
            {
                Id = companyId,
                Name = "Blocked Company",
                CreatedAt = DateTime.UtcNow
            });


        blockedDb.Projects.Add(
            new Project
            {
                Id = projectId,
                Name = "Blocked ERP",
                Version = "1.0",

                PublicKey =
                    Array.Empty<byte>(),

                PrivateKey =
                    Array.Empty<byte>(),

                CreatedAt =
                    DateTime.UtcNow
            });


        blockedDb.Licenses.Add(
            new License
            {
                Id = Guid.NewGuid(),

                CompanyId = companyId,
                ProjectId = projectId,

                LicenseKey =
                    "BLOCKED",

                Modules =
                    new List<string>
                    {
                        "Reports"
                    },

                IssuedAt =
                    DateTime.UtcNow,

                ExpiryDate =
                    DateTime.UtcNow.AddDays(30),

                IsBlocked = true,

                CreatedAt =
                    DateTime.UtcNow
            });


        await blockedDb.SaveChangesAsync();


        var blockedController =
            CreateController(blockedDb);

        var blockedRequest =
            new LicenseGenerateRequest
            {
                CompanyId = companyId,
                LicenseKey = "BLOCKED"
            };


        var blockedResult =
            await blockedController.GenerateLicense(
                blockedRequest);

        var blockedNotFound =
            Assert.IsType<NotFoundObjectResult>(
                blockedResult);


        // -------------------------
        // İki cevap aynı mı?
        // -------------------------

        Assert.Equal(
            missingNotFound.StatusCode,
            blockedNotFound.StatusCode);

        Assert.Equal(
            missingNotFound.Value,
            blockedNotFound.Value);
    }

    // =================================================
    // TEST 4
    // Renew sırasında olmayan lisans
    // genel hata vermeli
    // =================================================
    [Fact]
    public async Task MissingLicenseRenew_ShouldReturnGenericNotFound()
    {
        await using var dbContext =
            CreateDbContext();

        var controller =
            CreateController(dbContext);

        var request =
            new LicenseGenerateRequest
            {
                CompanyId =
                    Guid.NewGuid(),

                LicenseKey =
                    "MISSING-RENEW-LICENSE"
            };


        var result =
            await controller.RenewLicense(
                request);


        var notFound =
            Assert.IsType<NotFoundObjectResult>(
                result);


        Assert.Equal(
            404,
            notFound.StatusCode);

        Assert.Equal(
            "Lisans doğrulanamadı.",
            notFound.Value);
    }


    // =================================================
    // TEST 5
    // Renew sırasında blocked lisans da
    // AYNI genel hatayı vermeli
    // =================================================
    [Fact]
    public async Task BlockedLicenseRenew_ShouldReturnSameGenericNotFound()
    {
        await using var dbContext =
            CreateDbContext();


        Guid companyId =
            Guid.NewGuid();

        Guid projectId =
            Guid.NewGuid();


        var company =
            new Company
            {
                Id =
                    companyId,

                Name =
                    "Blocked Renew Company",

                CreatedAt =
                    DateTime.UtcNow
            };


        var project =
            new Project
            {
                Id =
                    projectId,

                Name =
                    "Blocked Renew ERP",

                Version =
                    "1.0",

                KeyVersion =
                    1,

                PublicKey =
                    Array.Empty<byte>(),

                PrivateKey =
                    Array.Empty<byte>(),

                CreatedAt =
                    DateTime.UtcNow
            };


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
                    "BLOCKED-RENEW-LICENSE",

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
                    true,

                CreatedAt =
                    DateTime.UtcNow
            };


        dbContext.Companies.Add(
            company);

        dbContext.Projects.Add(
            project);

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
                    companyId,

                LicenseKey =
                    "BLOCKED-RENEW-LICENSE"
            };


        var result =
            await controller.RenewLicense(
                request);


        var notFound =
            Assert.IsType<NotFoundObjectResult>(
                result);


        Assert.Equal(
            404,
            notFound.StatusCode);

        Assert.Equal(
            "Lisans doğrulanamadı.",
            notFound.Value);
    }


    // -------------------------------------------------
    // GET /api/licenses/{licenseKey} gereksiz ilişki
    // kimliklerini dışarı vermemeli.
    // -------------------------------------------------
    [Fact]
    public async Task GetLicense_ShouldHideCompanyAndProjectId()
    {
        await using var dbContext =
            CreateDbContext();

        var company =
            new Company
            {
                Id = Guid.NewGuid(),
                Name = "Detail Company",
                CreatedAt = DateTime.UtcNow
            };

        var project =
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Detail ERP",
                Version = "1.0",
                KeyVersion = 2,
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
                LicenseKey = "DETAIL-LICENSE",
                Modules = new List<string> { "Reports" },
                IssuedAt = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(30),
                ValidUntil = DateTime.UtcNow.AddYears(1),
                CreatedAt = DateTime.UtcNow
            };

        dbContext.AddRange(company, project, license);
        await dbContext.SaveChangesAsync();

        var controller =
            CreateController(dbContext);

        var result =
            await controller.GetLicense(
                license.LicenseKey,
                "TEST-ADMIN-KEY");

        var okResult =
            Assert.IsType<OkObjectResult>(result);

        using var document =
            JsonDocument.Parse(
                JsonSerializer.Serialize(okResult.Value));

        JsonElement root =
            document.RootElement;

        Assert.False(
            root.TryGetProperty("company", out _));

        JsonElement projectResponse =
            root.GetProperty("project");

        Assert.False(
            projectResponse.TryGetProperty("id", out _));

        Assert.Equal(
            "Detail ERP",
            projectResponse.GetProperty("name").GetString());
    }
}
