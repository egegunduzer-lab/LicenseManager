using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.Json.Serialization;

namespace LicenseManager.Core.DTOs;

public class SignedLicenseResponse
{
    [JsonPropertyName("body")]
    public LicensePayload Body { get; set; } = new();

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}