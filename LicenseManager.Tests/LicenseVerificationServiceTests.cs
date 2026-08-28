using LicenseManager.Client.Configuration;
using LicenseManager.Client.Services;
using LicenseManager.Core.DTOs;

namespace LicenseManager.Tests;

public class LicenseVerificationServiceTests
{
    [Fact]
    public void InvalidBase64Signature_ShouldReturnFalse()
    {
        var service =
            new LicenseVerificationService();

        var payload =
            new LicensePayload
            {
                LicenseId = Guid.NewGuid(),
                CompanyId = Guid.NewGuid(),
                ProjectId = Guid.NewGuid(),

                ProjectName =
                    EmbeddedLicenseKey.ProjectName,

                ProjectVersion =
                    EmbeddedLicenseKey.ProjectVersion,

                Modules = new List<string>
                {
                    "Reports"
                },

                IssuedAt =
                    DateTime.UtcNow.AddDays(-1),

                ExpiryDate =
                    DateTime.UtcNow.AddDays(30)
            };

        string invalidSignature =
            "!!!BU-GECERLI-BASE64-DEGIL!!!";

        bool result =
            service.Verify(
                payload,
                invalidSignature);

        Assert.False(result);
    }
}