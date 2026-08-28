
using System.Security.Cryptography;
using LicenseManager.Core.DTOs;
using LicenseManager.Core.Services;
using NSec.Cryptography;

namespace LicenseManager.Api.Services;

public class LicenseSigningService
{
    private readonly IPrivateKeyProtector _privateKeyProtector;

    private static readonly SignatureAlgorithm Algorithm =
        SignatureAlgorithm.Ed25519;

    public LicenseSigningService(
        IPrivateKeyProtector privateKeyProtector)
    {
        _privateKeyProtector = privateKeyProtector;
    }

    public string Sign(
        LicensePayload payload,
        byte[] protectedPrivateKey)
    {
        byte[] canonicalBytes =
            JsonCanonicalizer.Canonicalize(payload);

        byte[] hash =
            SHA256.HashData(canonicalBytes);

        byte[] privateKeyBytes =
            _privateKeyProtector.Unprotect(
                protectedPrivateKey);

        try
        {
            using var privateKey = Key.Import(
                Algorithm,
                privateKeyBytes,
                KeyBlobFormat.RawPrivateKey);

            byte[] signature =
                Algorithm.Sign(privateKey, hash);

            return Convert.ToBase64String(signature);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                privateKeyBytes);
        }
    }
}
