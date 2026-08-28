using System.Security.Cryptography;
using System.Text;

namespace LicenseManager.Api.Services;

public class AdminApiKeyValidator
{
    private readonly IConfiguration _configuration;

    public AdminApiKeyValidator(
        IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsValid(
        string? providedApiKey)
    {
        string? configuredApiKey =
            _configuration["AdminSecurity:ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredApiKey) ||
            string.IsNullOrWhiteSpace(providedApiKey))
        {
            return false;
        }

        byte[] expectedHash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    configuredApiKey));

        byte[] providedHash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    providedApiKey));

        return CryptographicOperations.FixedTimeEquals(
            expectedHash,
            providedHash);
    }
}