using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseManager.Core.Models;

public class ProjectKey
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public int KeyVersion { get; set; }

    public byte[] PublicKey { get; set; } =
        Array.Empty<byte>();

    public byte[] PrivateKey { get; set; } =
        Array.Empty<byte>();

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Project Project { get; set; } = null!;
}
