using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using LicenseManager.Api.Services;
using LicenseManager.Client.Configuration;
using LicenseManager.Client.Services;
using LicenseManager.Core.DTOs;
using LicenseManager.Core.Services;
using NSec.Cryptography;

namespace LicenseManager.Tests;

public class LicenseProjectIdentityTests
{
    private const string ExpectedProjectName =
        "Giltas ERP";

    private const string ExpectedProjectVersion =
        "1.0";


    // -------------------------------------------------
    // Test için embedded-key davranışını taklit eder.
    // -------------------------------------------------
    private class TestPublicKeyProvider
        : ILicensePublicKeyProvider
    {
        private readonly byte[] _publicKey;

        public TestPublicKeyProvider(
            byte[] publicKey)
        {
            _publicKey =
                publicKey;
        }


        public string ProjectName =>
            ExpectedProjectName;

        public string ProjectVersion =>
            ExpectedProjectVersion;


        public bool TryGetPublicKey(
            int keyVersion,
            out byte[] publicKey)
        {
            if (keyVersion == 1)
            {
                publicKey =
                    _publicKey;

                return true;
            }

            publicKey =
                Array.Empty<byte>();

            return false;
        }
    }


    // -------------------------------------------------
    // Test payload'u oluşturur.
    // -------------------------------------------------
    private static LicensePayload CreatePayload(
        string projectName,
        string projectVersion)
    {
        return new LicensePayload
        {
            LicenseId =
                Guid.NewGuid(),

            CompanyId =
                Guid.NewGuid(),

            ProjectId =
                Guid.NewGuid(),

            ProjectName =
                projectName,

            ProjectVersion =
                projectVersion,

            KeyVersion =
                1,

            Modules =
                new List<string>
                {
                    "Reports"
                },

            IssuedAt =
                DateTime.UtcNow,

            ExpiryDate =
                DateTime.UtcNow.AddDays(30)
        };
    }


    // -------------------------------------------------
    // Payload'u gerçek Ed25519 ile imzalar.
    // -------------------------------------------------
    private static string SignPayload(
        LicensePayload payload,
        byte[] privateKeyBytes)
    {
        var algorithm =
            SignatureAlgorithm.Ed25519;


        byte[] canonicalBytes =
            JsonCanonicalizer.Canonicalize(
                payload);


        byte[] hash =
            SHA256.HashData(
                canonicalBytes);


        using var privateKey =
            Key.Import(
                algorithm,
                privateKeyBytes,
                KeyBlobFormat.RawPrivateKey);


        byte[] signature =
            algorithm.Sign(
                privateKey,
                hash);


        return Convert.ToBase64String(
            signature);
    }


    // =================================================
    // TEST 1
    // İmza + proje adı + sürüm doğru
    // =================================================
    [Fact]
    public void MatchingProjectIdentity_ShouldBeValid()
    {
        var keyService =
            new Ed25519KeyService();

        var keyPair =
            keyService.GenerateKeyPair();


        var payload =
            CreatePayload(
                ExpectedProjectName,
                ExpectedProjectVersion);


        string signature =
            SignPayload(
                payload,
                keyPair.PrivateKey);


        var provider =
            new TestPublicKeyProvider(
                keyPair.PublicKey);


        var verificationService =
            new LicenseVerificationService(
                provider);


        bool result =
            verificationService.Verify(
                payload,
                signature);


        Assert.True(
            result);
    }


    // =================================================
    // TEST 2
    //
    // Signature gerçekten geçerli,
    // fakat ProjectName başka proje.
    //
    // Client reddetmeli.
    // =================================================
    [Fact]
    public void ValidSignature_WithDifferentProjectName_ShouldBeInvalid()
    {
        var keyService =
            new Ed25519KeyService();

        var keyPair =
            keyService.GenerateKeyPair();


        // ÖNEMLİ:
        // Önce yanlış project name ile payload oluşturup
        // SONRA bunu imzalıyoruz.
        //
        // Yani signature gerçekten geçerli.
        var payload =
            CreatePayload(
                "Different ERP",
                ExpectedProjectVersion);


        string signature =
            SignPayload(
                payload,
                keyPair.PrivateKey);


        var provider =
            new TestPublicKeyProvider(
                keyPair.PublicKey);


        var verificationService =
            new LicenseVerificationService(
                provider);


        bool result =
            verificationService.Verify(
                payload,
                signature);


        Assert.False(
            result);
    }


    // =================================================
    // TEST 3
    //
    // Signature gerçekten geçerli,
    // fakat ProjectVersion farklı.
    //
    // Client reddetmeli.
    // =================================================
    [Fact]
    public void ValidSignature_WithDifferentProjectVersion_ShouldBeInvalid()
    {
        var keyService =
            new Ed25519KeyService();

        var keyPair =
            keyService.GenerateKeyPair();


        var payload =
            CreatePayload(
                ExpectedProjectName,
                "2.0");


        string signature =
            SignPayload(
                payload,
                keyPair.PrivateKey);


        var provider =
            new TestPublicKeyProvider(
                keyPair.PublicKey);


        var verificationService =
            new LicenseVerificationService(
                provider);


        bool result =
            verificationService.Verify(
                payload,
                signature);


        Assert.False(
            result);
    }
}
