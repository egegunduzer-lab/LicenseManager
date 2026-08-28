using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.Json.Serialization;

namespace LicenseManager.Core.DTOs;

public class LicenseGenerateRequest
{
    [JsonPropertyName("company_id")]
    public Guid CompanyId { get; set; }

    [JsonPropertyName("license_key")]
    public string LicenseKey { get; set; } = string.Empty;
}