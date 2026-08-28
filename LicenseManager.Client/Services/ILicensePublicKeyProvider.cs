using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseManager.Client.Services;

public interface ILicensePublicKeyProvider
{
    string ProjectName { get; }

    string ProjectVersion { get; }

    bool TryGetPublicKey(
        int keyVersion,
        out byte[] publicKey);
}