using LicenseManager.Api.Controllers;
using LicenseManager.Api.Data;
using LicenseManager.Api.Services;
using LicenseManager.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LicenseManager.Tests;

public class CompaniesControllerTests
{
    private const string AdminApiKey =
        "TEST-ADMIN-API-KEY";


    // =================================================
    // Test DB oluştur
    // =================================================
    private static ApplicationDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        return new ApplicationDbContext(options);
    }


    // =================================================
    // Test AdminApiKeyValidator oluştur
    // =================================================
    private static AdminApiKeyValidator
        CreateAdminApiKeyValidator()
    {
        var values =
            new Dictionary<string, string?>
            {
                {
                    "AdminSecurity:ApiKey",
                    AdminApiKey
                }
            };


        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();


        return new AdminApiKeyValidator(
            configuration);
    }


    // =================================================
    // TEST 1
    // Admin key yoksa şirket listesi verilmemeli
    // =================================================
    [Fact]
    public async Task GetCompanies_WithoutAdminKey_ShouldReturnUnauthorized()
    {
        await using var dbContext =
            CreateDbContext();


        var controller =
            new CompaniesController(
                dbContext,
                CreateAdminApiKeyValidator());


        var result =
            await controller.GetCompanies(
                null);


        Assert.IsType<UnauthorizedObjectResult>(
            result);
    }


    // =================================================
    // TEST 2
    // Doğru admin key ile şirketler listelenebilmeli
    // =================================================
    [Fact]
    public async Task GetCompanies_WithCorrectAdminKey_ShouldReturnOk()
    {
        await using var dbContext =
            CreateDbContext();


        dbContext.Companies.AddRange(
            new Company
            {
                Id = Guid.NewGuid(),
                Name = "ABC Holding",
                CreatedAt = DateTime.UtcNow
            },
            new Company
            {
                Id = Guid.NewGuid(),
                Name = "XYZ Ltd",
                CreatedAt = DateTime.UtcNow
            });


        await dbContext.SaveChangesAsync();


        var controller =
            new CompaniesController(
                dbContext,
                CreateAdminApiKeyValidator());


        var result =
            await controller.GetCompanies(
                AdminApiKey);


        Assert.IsType<OkObjectResult>(
            result);
    }


    // =================================================
    // TEST 3
    // Tek şirket detayı getirilebilmeli
    // =================================================
    [Fact]
    public async Task GetCompany_ExistingCompany_ShouldReturnOk()
    {
        await using var dbContext =
            CreateDbContext();


        var company =
            new Company
            {
                Id = Guid.NewGuid(),
                Name = "Detail Test Company",
                CreatedAt = DateTime.UtcNow
            };


        dbContext.Companies.Add(
            company);

        await dbContext.SaveChangesAsync();


        var controller =
            new CompaniesController(
                dbContext,
                CreateAdminApiKeyValidator());


        var result =
            await controller.GetCompany(
                company.Id,
                AdminApiKey);


        Assert.IsType<OkObjectResult>(
            result);
    }


    // =================================================
    // TEST 4
    // Olmayan şirket 404 dönmeli
    // =================================================
    [Fact]
    public async Task GetCompany_MissingCompany_ShouldReturnNotFound()
    {
        await using var dbContext =
            CreateDbContext();


        var controller =
            new CompaniesController(
                dbContext,
                CreateAdminApiKeyValidator());


        var result =
            await controller.GetCompany(
                Guid.NewGuid(),
                AdminApiKey);


        Assert.IsType<NotFoundObjectResult>(
            result);
    }


    // =================================================
    // TEST 5
    // Admin yeni şirket oluşturabilmeli
    // =================================================
    [Fact]
    public async Task CreateCompany_WithCorrectAdminKey_ShouldCreateCompany()
    {
        await using var dbContext =
            CreateDbContext();


        var controller =
            new CompaniesController(
                dbContext,
                CreateAdminApiKeyValidator());


        var request =
            new CreateCompanyRequest
            {
                Name =
                    "New Test Company"
            };


        var result =
            await controller.CreateCompany(
                request,
                AdminApiKey);


        Assert.IsType<CreatedAtActionResult>(
            result);


        var company =
            await dbContext.Companies
                .SingleOrDefaultAsync(
                    x =>
                        x.Name ==
                        "New Test Company");


        Assert.NotNull(
            company);


        Assert.Equal(
            "New Test Company",
            company.Name);
    }


    // =================================================
    // TEST 6
    // Aynı isimde şirket tekrar oluşturulmamalı
    // =================================================
    [Fact]
    public async Task CreateCompany_DuplicateName_ShouldReturnConflict()
    {
        await using var dbContext =
            CreateDbContext();


        dbContext.Companies.Add(
            new Company
            {
                Id = Guid.NewGuid(),
                Name = "Duplicate Company",
                CreatedAt = DateTime.UtcNow
            });


        await dbContext.SaveChangesAsync();


        var controller =
            new CompaniesController(
                dbContext,
                CreateAdminApiKeyValidator());


        var request =
            new CreateCompanyRequest
            {
                Name =
                    "Duplicate Company"
            };


        var result =
            await controller.CreateCompany(
                request,
                AdminApiKey);


        Assert.IsType<ConflictObjectResult>(
            result);


        Assert.Equal(
            1,
            await dbContext.Companies.CountAsync());
    }


    // =================================================
    // TEST 7
    // Boş şirket adı reddedilmeli
    // =================================================
    [Fact]
    public async Task CreateCompany_EmptyName_ShouldReturnBadRequest()
    {
        await using var dbContext =
            CreateDbContext();


        var controller =
            new CompaniesController(
                dbContext,
                CreateAdminApiKeyValidator());


        var request =
            new CreateCompanyRequest
            {
                Name =
                    "   "
            };


        var result =
            await controller.CreateCompany(
                request,
                AdminApiKey);


        Assert.IsType<BadRequestObjectResult>(
            result);


        Assert.Empty(
            await dbContext.Companies.ToListAsync());
    }


    // =================================================
    // TEST 8
    // Admin key yoksa DB değişmemeli
    // =================================================
    [Fact]
    public async Task CreateCompany_WithoutAdminKey_ShouldNotCreateCompany()
    {
        await using var dbContext =
            CreateDbContext();


        var controller =
            new CompaniesController(
                dbContext,
                CreateAdminApiKeyValidator());


        var request =
            new CreateCompanyRequest
            {
                Name =
                    "Unauthorized Company"
            };


        var result =
            await controller.CreateCompany(
                request,
                null);


        Assert.IsType<UnauthorizedObjectResult>(
            result);


        Assert.Empty(
            await dbContext.Companies.ToListAsync());
    }
}