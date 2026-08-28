using System.Text.Json.Serialization;

namespace LicenseManager.Core.DTOs;

public class CreateLicenseRequest
{
    [JsonPropertyName("company_id")]
    public Guid CompanyId { get; set; }

    [JsonPropertyName("project_id")]
    public Guid ProjectId { get; set; }

    [JsonPropertyName("modules")]
    public List<string> Modules { get; set; } =
        new();

    // Müşterinin gerçek lisans hakkının bitiş tarihi
    [JsonPropertyName("valid_until")]
    public DateTime ValidUntil { get; set; }
}