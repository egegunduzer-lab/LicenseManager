using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LicenseManager.Api.Data;
using LicenseManager.Api.Services;
using LicenseManager.Core.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LicenseManager.Tests;

public class LicenseValidityPeriodTests
{
    // =================================================
    // ValidUntil uzaktaysa signed license
    // maksimum 30 gün olmalı.
    // =================================================
    [Fact]
    public async Task CreateLicense_LongValidUntil_ShouldCreateThirtyDaySignedLicense()
    {
        var options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        await using var dbContext =
            new ApplicationDbContext(options);


        var company =
            new Company
            {
                Id = Guid.NewGuid(),
                Name = "Test Company",
                CreatedAt = DateTime.UtcNow
            };


        var project =
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Test Project",
                Version = "1.0",

                // Bu testte imzalama yapılmadığı için
                // gerçek key kullanmamıza gerek yok.
                PublicKey = Array.Empty<byte>(),
                PrivateKey = Array.Empty<byte>(),

                CreatedAt = DateTime.UtcNow
            };


        dbContext.Companies.Add(company);
        dbContext.Projects.Add(project);

        await dbContext.SaveChangesAsync();


        // CreateLicenseAsync signing service kullanmadığı için
        // bu testte null verebiliriz.
        var service =
            new LicenseService(
                dbContext,
                null!);


        DateTime beforeCreate =
            DateTime.UtcNow;

        DateTime validUntil =
            beforeCreate.AddYears(1);


        var result =
            await service.CreateLicenseAsync(
                company.Id,
                project.Id,
                new List<string>
                {
                    "Reports"
                },
                validUntil);


        DateTime afterCreate =
            DateTime.UtcNow;


        Assert.NotNull(result);

        Assert.Equal(
            validUntil,
            result.ValidUntil);


        // Servis DateTime.UtcNow değerini kendi içinde aldığı
        // için birkaç milisaniyelik fark olabilir.
        Assert.InRange(
            result.ExpiryDate,
            beforeCreate.AddDays(30),
            afterCreate.AddDays(30));
    }


    // =================================================
    // Gerçek lisans hakkı 10 gün sonra bitiyorsa,
    // signed license 30 güne uzatılamamalı.
    // =================================================
    [Fact]
    public async Task CreateLicense_NearValidUntil_ShouldCapExpiryAtValidUntil()
    {
        var options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        await using var dbContext =
            new ApplicationDbContext(options);


        var company =
            new Company
            {
                Id = Guid.NewGuid(),
                Name = "Test Company",
                CreatedAt = DateTime.UtcNow
            };


        var project =
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Test Project",
                Version = "1.0",
                PublicKey = Array.Empty<byte>(),
                PrivateKey = Array.Empty<byte>(),
                CreatedAt = DateTime.UtcNow
            };


        dbContext.Companies.Add(company);
        dbContext.Projects.Add(project);

        await dbContext.SaveChangesAsync();


        var service =
            new LicenseService(
                dbContext,
                null!);


        DateTime validUntil =
            DateTime.UtcNow.AddDays(10);


        var result =
            await service.CreateLicenseAsync(
                company.Id,
                project.Id,
                new List<string>
                {
                    "Reports"
                },
                validUntil);


        Assert.NotNull(result);


        // Kritik kontrol:
        //
        // 30 günlük signed license yerine,
        // gerçek lisans bitiş tarihi kullanılmalı.
        Assert.Equal(
            validUntil,
            result.ExpiryDate);

        Assert.Equal(
            validUntil,
            result.ValidUntil);
    }


    // =================================================
    // Gerçek lisans hakkı zaten bittiyse
    // yeni lisans oluşturulamamalı.
    // =================================================
    [Fact]
    public async Task CreateLicense_ExpiredValidUntil_ShouldRejectLicense()
    {
        var options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        await using var dbContext =
            new ApplicationDbContext(options);


        var company =
            new Company
            {
                Id = Guid.NewGuid(),
                Name = "Test Company",
                CreatedAt = DateTime.UtcNow
            };


        var project =
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Test Project",
                Version = "1.0",
                PublicKey = Array.Empty<byte>(),
                PrivateKey = Array.Empty<byte>(),
                CreatedAt = DateTime.UtcNow
            };


        dbContext.Companies.Add(company);
        dbContext.Projects.Add(project);

        await dbContext.SaveChangesAsync();


        var service =
            new LicenseService(
                dbContext,
                null!);


        DateTime expiredValidUntil =
            DateTime.UtcNow.AddDays(-1);


        var result =
            await service.CreateLicenseAsync(
                company.Id,
                project.Id,
                new List<string>
                {
                    "Reports"
                },
                expiredValidUntil);


        Assert.Null(result);


        // Geçersiz lisans DB'ye de yazılmamalı.
        Assert.Empty(
            await dbContext.Licenses.ToListAsync());
    }
}
