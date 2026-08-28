using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LicenseManager.Client.Models;
using LicenseManager.Client.Services;
using LicenseManager.Core.DTOs;

namespace LicenseManager.Tests;

public class LicenseCompanyBindingTests
{
    // Bu testlerde kriptografik imzanın geçerli olduğunu
    // varsayıyoruz. Amacımız yalnızca CompanyId kontrolünü
    // test etmek.
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
    // TEST 1
    // Lisans doğru şirkete aitse kabul edilmeli
    // -------------------------------------------------
    [Fact]
    public void SameCompanyId_ShouldBeValid()
    {
        Guid companyId =
            Guid.NewGuid();

        var validator =
            new LicenseValidatorService(
                new AlwaysValidVerificationService());

        var license =
            CreateLicense(companyId);


        LicenseValidationResult result =
            validator.Validate(
                license,
                "Reports",
                companyId);


        Assert.True(result.IsValid);
    }


    // -------------------------------------------------
    // TEST 2
    // Başka şirkete ait geçerli lisans reddedilmeli
    // -------------------------------------------------
    [Fact]
    public void DifferentCompanyId_ShouldBeInvalid()
    {
        Guid licenseCompanyId =
            Guid.NewGuid();

        Guid expectedCompanyId =
            Guid.NewGuid();


        var validator =
            new LicenseValidatorService(
                new AlwaysValidVerificationService());

        var license =
            CreateLicense(
                licenseCompanyId);


        LicenseValidationResult result =
            validator.Validate(
                license,
                "Reports",
                expectedCompanyId);


        Assert.False(result.IsValid);

        Assert.Equal(
            "Lisans doğrulanamadı.",
            result.Message);
    }


    // -------------------------------------------------
    // TEST 3
    // CompanyId farkı kullanıcıya özel hata sızdırmamalı
    // -------------------------------------------------
    [Fact]
    public void DifferentCompanyId_ShouldUseGenericErrorMessage()
    {
        Guid licenseCompanyId =
            Guid.NewGuid();

        Guid expectedCompanyId =
            Guid.NewGuid();


        var validator =
            new LicenseValidatorService(
                new AlwaysValidVerificationService());

        var license =
            CreateLicense(
                licenseCompanyId);


        LicenseValidationResult result =
            validator.Validate(
                license,
                "Reports",
                expectedCompanyId);


        Assert.Equal(
            "Lisans doğrulanamadı.",
            result.Message);
    }


    // -------------------------------------------------
    // Test lisansı oluştur
    // -------------------------------------------------
    private static SignedLicenseResponse CreateLicense(
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

            Signature =
                "test-signature"
        };
    }
}
