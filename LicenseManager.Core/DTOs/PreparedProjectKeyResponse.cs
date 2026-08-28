using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace LicenseManager.Core.DTOs;

public class PreparedProjectKeyResponse
{
    [JsonPropertyName("project_id")]
    public Guid ProjectId { get; set; }

    [JsonPropertyName("key_version")]
    public int KeyVersion { get; set; }

    [JsonPropertyName("public_key")]
    public string PublicKeyBase64 { get; set; } = string.Empty;
}
