using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LicenseManager.Client.Services;

namespace LicenseManager.Client.Configuration;

public class EmbeddedLicensePublicKeyProvider
    : ILicensePublicKeyProvider
{
    public string ProjectName =>
        EmbeddedLicenseKey.ProjectName;


    public string ProjectVersion =>
        EmbeddedLicenseKey.ProjectVersion;


    public bool TryGetPublicKey(
        int keyVersion,
        out byte[] publicKey)
    {
        publicKey =
            Array.Empty<byte>();


        // İstenen key version gömülü key listesinde var mı?
        if (!EmbeddedLicenseKey.PublicKeys
                .TryGetValue(
                    keyVersion,
                    out string? publicKeyBase64))
        {
            return false;
        }


        try
        {
            publicKey =
                Convert.FromBase64String(
                    publicKeyBase64);

            return true;
        }
        catch (FormatException)
        {
            // Gömülü key yanlış formatlıysa
            // lisansı doğrulama.
            publicKey =
                Array.Empty<byte>();

            return false;
        }
    }
}