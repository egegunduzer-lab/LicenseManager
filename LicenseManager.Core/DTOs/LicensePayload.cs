using System.Text.Json.Serialization;

namespace LicenseManager.Core.DTOs;

public class LicensePayload
{
    [JsonPropertyName("license_id")]
    public Guid LicenseId { get; set; }

    [JsonPropertyName("company_id")]
    public Guid CompanyId { get; set; }

    [JsonPropertyName("project_id")]
    public Guid ProjectId { get; set; }

    [JsonPropertyName("project_name")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("project_version")]
    public string ProjectVersion { get; set; } = string.Empty;

    [JsonPropertyName("key_version")]
    public int KeyVersion { get; set; }

    [JsonPropertyName("modules")]
    public List<string> Modules { get; set; } = new();

    [JsonPropertyName("issued_at")]
    public DateTime IssuedAt { get; set; }

    [JsonPropertyName("expiry_date")]
    public DateTime ExpiryDate { get; set; }
}