using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseManager.Core.Models;

public class Project
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public int KeyVersion { get; set; } = 1;

    public byte[] PublicKey { get; set; } =
        Array.Empty<byte>();

    public byte[] PrivateKey { get; set; } =
        Array.Empty<byte>();

    public DateTime CreatedAt { get; set; }

    public List<License> Licenses { get; set; } =
        new();

    public List<ProjectKey> Keys { get; set; } =
    new();
}
