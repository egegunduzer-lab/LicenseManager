using System.Security.Cryptography;
using System.Text;
using LicenseManager.Api.Services;

namespace LicenseManager.Tests;

internal static class TestPrivateKeyProtector
{
    public static IPrivateKeyProtector Create()
    {
        byte[] key =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    "LicenseManager.Tests.AesGcm.MasterKey"));

        try
        {
            return new AesGcmPrivateKeyProtector(
                key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                key);
        }
    }
}
