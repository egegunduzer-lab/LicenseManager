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

public class AdminLicenseSecurityTests
{
    private const string CorrectAdminKey =
        "TEST-ADMIN-KEY";


    // -------------------------------------------------
    // Her test için ayrı InMemory DB
    // -------------------------------------------------
    private static ApplicationDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        return new ApplicationDbContext(
            options);
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
                            CorrectAdminKey
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


    // -------------------------------------------------
    // Geçerli Company + Project oluştur
    // -------------------------------------------------
    private static async Task<(
        Company Company,
        Project Project)>
        SeedCompanyAndProjectAsync(
            ApplicationDbContext dbContext)
    {
        var company =
            new Company
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Test Company",

                CreatedAt =
                    DateTime.UtcNow
            };


        var project =
            new Project
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Test ERP",

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


        dbContext.Companies.Add(
            company);

        dbContext.Projects.Add(
            project);

        await dbContext.SaveChangesAsync();


        return (
            company,
            project);
    }


    // =================================================
    // TEST 1
    // Admin key yok -> 401
    // =================================================
    [Fact]
    public async Task CreateLicense_WithoutAdminKey_ShouldReturn401()
    {
        await using var dbContext =
            CreateDbContext();


        var data =
            await SeedCompanyAndProjectAsync(
                dbContext);


        var controller =
            CreateController(
                dbContext);


        var request =
            new CreateLicenseRequest
            {
                CompanyId =
                    data.Company.Id,

                ProjectId =
                    data.Project.Id,

                Modules =
                    new List<string>
                    {
                        "Reports"
                    }
            };


        var result =
            await controller.CreateLicense(
                request,
                null);


        var unauthorized =
            Assert.IsType<
                UnauthorizedObjectResult>(
                    result);


        Assert.Equal(
            401,
            unauthorized.StatusCode);


        Assert.Empty(
            dbContext.Licenses);
    }


    // =================================================
    // TEST 2
    // Yanlış Admin key -> 401
    // =================================================
    [Fact]
    public async Task CreateLicense_WithWrongAdminKey_ShouldReturn401()
    {
        await using var dbContext =
            CreateDbContext();


        var data =
            await SeedCompanyAndProjectAsync(
                dbContext);


        var controller =
            CreateController(
                dbContext);


        var request =
            new CreateLicenseRequest
            {
                CompanyId =
                    data.Company.Id,

                ProjectId =
                    data.Project.Id,

                Modules =
                    new List<string>
                    {
                        "Reports"
                    }
            };


        var result =
            await controller.CreateLicense(
                request,
                "YANLIS-ADMIN-KEY");


        var unauthorized =
            Assert.IsType<
                UnauthorizedObjectResult>(
                    result);


        Assert.Equal(
            401,
            unauthorized.StatusCode);


        Assert.Empty(
            dbContext.Licenses);
    }


    // =================================================
    // TEST 3
    // Doğru Admin key -> lisans oluşturulmalı
    // =================================================
    [Fact]
    public async Task CreateLicense_WithCorrectAdminKey_ShouldCreateLicense()
    {
        await using var dbContext =
            CreateDbContext();


        var data =
            await SeedCompanyAndProjectAsync(
                dbContext);


        var controller =
            CreateController(
                dbContext);


        var request =
    new CreateLicenseRequest
    {
        CompanyId =
            data.Company.Id,

        ProjectId =
            data.Project.Id,

        Modules =
            new List<string>
            {
                "Reports",
                "Inventory"
            },

        ValidUntil =
            DateTime.UtcNow.AddYears(1)
    };


        var result =
            await controller.CreateLicense(
                request,
                CorrectAdminKey);


        var created =
            Assert.IsType<ObjectResult>(
                result);


        Assert.Equal(
            201,
            created.StatusCode);


        var response =
            Assert.IsType<
                CreatedLicenseResponse>(
                    created.Value);


        Assert.Equal(
            data.Company.Id,
            response.CompanyId);

        Assert.Equal(
            data.Project.Id,
            response.ProjectId);

        Assert.False(
            response.IsBlocked);

        Assert.False(
            string.IsNullOrWhiteSpace(
                response.LicenseKey));

        Assert.StartsWith(
            "LM-",
            response.LicenseKey);


        // Gerçekten DB'ye yazılmış mı?
        var savedLicense =
            await dbContext.Licenses
                .SingleAsync();


        Assert.Equal(
            response.LicenseId,
            savedLicense.Id);

        Assert.Equal(
            data.Company.Id,
            savedLicense.CompanyId);

        Assert.Equal(
            data.Project.Id,
            savedLicense.ProjectId);

        Assert.Equal(
            response.LicenseKey,
            savedLicense.LicenseKey);

        Assert.False(
            savedLicense.IsBlocked);
    }
}
