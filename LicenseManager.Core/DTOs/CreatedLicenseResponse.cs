using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.Json.Serialization;

namespace LicenseManager.Core.DTOs;

public class CreatedLicenseResponse
{
    [JsonPropertyName("license_id")]
    public Guid LicenseId { get; set; }

    [JsonPropertyName("company_id")]
    public Guid CompanyId { get; set; }

    [JsonPropertyName("project_id")]
    public Guid ProjectId { get; set; }

    [JsonPropertyName("license_key")]
    public string LicenseKey { get; set; } =
        string.Empty;

    [JsonPropertyName("modules")]
    public List<string> Modules { get; set; } =
        new();

    [JsonPropertyName("issued_at")]
    public DateTime IssuedAt { get; set; }

    [JsonPropertyName("expiry_date")]
    public DateTime ExpiryDate { get; set; }

    [JsonPropertyName("valid_until")]
    public DateTime ValidUntil { get; set; }

    [JsonPropertyName("is_blocked")]
    public bool IsBlocked { get; set; }
}
