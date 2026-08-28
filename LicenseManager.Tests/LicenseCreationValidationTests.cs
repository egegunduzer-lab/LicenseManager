using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LicenseManager.Api.Data;
using LicenseManager.Api.Services;
using LicenseManager.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LicenseManager.Tests;

public class LicenseCreationValidationTests
{
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

        return new ApplicationDbContext(options);
    }


    // -------------------------------------------------
    // LicenseService oluştur
    // -------------------------------------------------
    private static LicenseService CreateLicenseService(
        ApplicationDbContext dbContext)
    {
        var privateKeyProtector =
            TestPrivateKeyProtector.Create();

        var signingService =
            new LicenseSigningService(
                privateKeyProtector);

        return new LicenseService(
            dbContext,
            signingService);
    }


    // -------------------------------------------------
    // Test firması oluştur
    // -------------------------------------------------
    private static Company CreateCompany()
    {
        return new Company
        {
            Id = Guid.NewGuid(),
            Name = "Test Company",
            CreatedAt = DateTime.UtcNow
        };
    }


    // -------------------------------------------------
    // Test projesi oluştur
    // -------------------------------------------------
    private static Project CreateProject()
    {
        return new Project
        {
            Id = Guid.NewGuid(),

            Name = "Test ERP",
            Version = "1.0",

            KeyVersion = 1,

            PublicKey = Array.Empty<byte>(),
            PrivateKey = Array.Empty<byte>(),

            CreatedAt = DateTime.UtcNow
        };
    }


    // =================================================
    // TEST 1
    // Olmayan Company -> lisans oluşturulmamalı
    // =================================================
    [Fact]
    public async Task MissingCompany_ShouldNotCreateLicense()
    {
        await using var dbContext =
            CreateDbContext();

        var project =
            CreateProject();

        dbContext.Projects.Add(
            project);

        await dbContext.SaveChangesAsync();


        var service =
            CreateLicenseService(
                dbContext);


        var result =
            await service.CreateLicenseAsync(
                Guid.NewGuid(),
                project.Id,
                new List<string>
                {
                    "Reports"
                },
        DateTime.UtcNow.AddYears(1));


        Assert.Null(result);

        Assert.Empty(
            dbContext.Licenses);
    }


    // =================================================
    // TEST 2
    // Olmayan Project -> lisans oluşturulmamalı
    // =================================================
    [Fact]
    public async Task MissingProject_ShouldNotCreateLicense()
    {
        await using var dbContext =
            CreateDbContext();

        var company =
            CreateCompany();

        dbContext.Companies.Add(
            company);

        await dbContext.SaveChangesAsync();


        var service =
            CreateLicenseService(
                dbContext);


        var result =
            await service.CreateLicenseAsync(
                company.Id,
                Guid.NewGuid(),
                new List<string>
                {
                    "Reports"
                },
        DateTime.UtcNow.AddYears(1));


        Assert.Null(result);

        Assert.Empty(
            dbContext.Licenses);
    }


    // =================================================
    // TEST 3
    // Boş modül listesi -> lisans oluşturulmamalı
    // =================================================
    [Fact]
    public async Task EmptyModules_ShouldNotCreateLicense()
    {
        await using var dbContext =
            CreateDbContext();

        var company =
            CreateCompany();

        var project =
            CreateProject();

        dbContext.Companies.Add(
            company);

        dbContext.Projects.Add(
            project);

        await dbContext.SaveChangesAsync();


        var service =
            CreateLicenseService(
                dbContext);


        var result =
            await service.CreateLicenseAsync(
                company.Id,
                project.Id,
                new List<string>
                {
                    "",
                    "   ",
                    "\t"
                },
        DateTime.UtcNow.AddYears(1));


        Assert.Null(result);

        Assert.Empty(
            dbContext.Licenses);
    }


    // =================================================
    // TEST 4
    // Modüller temizlenmeli ve tekrarlar kaldırılmalı
    // =================================================
    [Fact]
    public async Task Modules_ShouldBeTrimmedAndDeduplicated()
    {
        await using var dbContext =
            CreateDbContext();

        var company =
            CreateCompany();

        var project =
            CreateProject();

        dbContext.Companies.Add(
            company);

        dbContext.Projects.Add(
            project);

        await dbContext.SaveChangesAsync();


        var service =
            CreateLicenseService(
                dbContext);


        var result =
            await service.CreateLicenseAsync(
                company.Id,
                project.Id,
                new List<string>
                {
                    " Reports ",
                    "Reports",
                    "Inventory",
                    "Inventory",
                    "   "
                },
        DateTime.UtcNow.AddYears(1));


        Assert.NotNull(result);


        Assert.Equal(
            2,
            result!.Modules.Count);

        Assert.Contains(
            "Reports",
            result.Modules);

        Assert.Contains(
            "Inventory",
            result.Modules);


        var savedLicense =
            await dbContext.Licenses
                .SingleAsync();


        Assert.Equal(
            2,
            savedLicense.Modules.Count);

        Assert.Contains(
            "Reports",
            savedLicense.Modules);

        Assert.Contains(
            "Inventory",
            savedLicense.Modules);
    }
    // =================================================
    // TEST 5
    // Aynı Company + Project için birden fazla
    // lisans oluşturulabilmeli.
    // LicenseKey'ler farklı olmalı.
    // =================================================
    [Fact]
    public async Task SameCompanyAndProject_ShouldAllowMultipleLicenses()
    {
        await using var dbContext =
            CreateDbContext();

        var company =
            CreateCompany();

        var project =
            CreateProject();

        dbContext.Companies.Add(
            company);

        dbContext.Projects.Add(
            project);

        await dbContext.SaveChangesAsync();


        var service =
            CreateLicenseService(
                dbContext);


        // İlk lisans
        var firstLicense =
            await service.CreateLicenseAsync(
                company.Id,
                project.Id,
                new List<string>
                {
                "Reports"
                },
        DateTime.UtcNow.AddYears(1));


        // Aynı firma + aynı proje için
        // ikinci lisans
        var secondLicense =
            await service.CreateLicenseAsync(
                company.Id,
                project.Id,
                new List<string>
                {
                "Inventory"
                },
        DateTime.UtcNow.AddYears(1));


        Assert.NotNull(
            firstLicense);

        Assert.NotNull(
            secondLicense);


        // İki lisansın anahtarları
        // mutlaka farklı olmalı.
        Assert.NotEqual(
            firstLicense!.LicenseKey,
            secondLicense!.LicenseKey);


        // DB'de gerçekten iki kayıt olmalı.
        var licenses =
            await dbContext.Licenses
                .ToListAsync();


        Assert.Equal(
            2,
            licenses.Count);


        // İkisi de aynı firmaya bağlı.
        Assert.All(
            licenses,
            license =>
                Assert.Equal(
                    company.Id,
                    license.CompanyId));


        // İkisi de aynı projeye bağlı.
        Assert.All(
            licenses,
            license =>
                Assert.Equal(
                    project.Id,
                    license.ProjectId));
    }
}
