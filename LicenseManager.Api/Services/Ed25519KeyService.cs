using LicenseManager.Core.DTOs;
using NSec.Cryptography;

namespace LicenseManager.Api.Services;

public class Ed25519KeyService
{
    private static readonly SignatureAlgorithm Algorithm =
        SignatureAlgorithm.Ed25519;

    public GeneratedKeyPair GenerateKeyPair()
    {
        var creationParameters = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextArchiving
        };

        using var key = Key.Create(
            Algorithm,
            creationParameters);

        byte[] publicKey = key.Export(
            KeyBlobFormat.RawPublicKey);

        byte[] privateKey = key.Export(
            KeyBlobFormat.RawPrivateKey);

        return new GeneratedKeyPair
        {
            PublicKey = publicKey,
            PrivateKey = privateKey
        };
    }
}