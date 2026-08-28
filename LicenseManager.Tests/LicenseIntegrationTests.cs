using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LicenseManager.Api.Services;
using LicenseManager.Client.Services;
using LicenseManager.Core.DTOs;
using NSec.Cryptography;

namespace LicenseManager.Tests;

public class LicenseIntegrationTests
{
    private static readonly SignatureAlgorithm Algorithm =
        SignatureAlgorithm.Ed25519;


    // -------------------------------------------------
    // Test için public key provider
    // -------------------------------------------------
    private class TestPublicKeyProvider
    : ILicensePublicKeyProvider
    {
        private readonly byte[] _publicKey;

        public TestPublicKeyProvider(
            byte[] publicKey,
            string projectName,
            string projectVersion)
        {
            _publicKey = publicKey;

            ProjectName = projectName;
            ProjectVersion = projectVersion;
        }

        public string ProjectName { get; }

        public string ProjectVersion { get; }


        public bool TryGetPublicKey(
            int keyVersion,
            out byte[] publicKey)
        {
            if (keyVersion != 1)
            {
                publicKey = Array.Empty<byte>();
                return false;
            }

            publicKey = _publicKey;
            return true;
        }
    }

    // -------------------------------------------------
    // Gerçek server signing için gerekli yapıları oluştur
    // -------------------------------------------------
    private static (
        LicensePayload Payload,
        string Signature,
        byte[] PublicKey)
        CreateSignedLicense()
    {
        var creationParameters =
            new KeyCreationParameters
            {
                ExportPolicy =
                    KeyExportPolicies
                        .AllowPlaintextArchiving
            };

        using var key =
            Key.Create(
                Algorithm,
                creationParameters);

        byte[] privateKey =
            key.Export(
                KeyBlobFormat.RawPrivateKey);

        byte[] publicKey =
            key.Export(
                KeyBlobFormat.RawPublicKey);


        var protector =
            TestPrivateKeyProtector.Create();

        byte[] protectedPrivateKey =
            protector.Protect(
                privateKey);


        var signingService =
            new LicenseSigningService(
                protector);


        var payload =
            new LicensePayload
            {
                LicenseId =
                    Guid.NewGuid(),

                CompanyId =
                    Guid.NewGuid(),

                ProjectId =
                    Guid.NewGuid(),

                ProjectName =
                    "Integration ERP",

                ProjectVersion =
                    "1.0",

                KeyVersion =
                    1,

                Modules =
                    new List<string>
                    {
                        "Reports",
                        "Inventory"
                    },

                IssuedAt =
                    DateTime.UtcNow,

                ExpiryDate =
                    DateTime.UtcNow
                        .AddDays(30)
            };


        string signature =
            signingService.Sign(
                payload,
                protectedPrivateKey);


        return (
            payload,
            signature,
            publicKey);
    }


    // -------------------------------------------------
    // TEST 1
    // Server imzalar -> Client doğrular
    // -------------------------------------------------
    [Fact]
    public void ServerSignedLicense_ShouldVerifyOnClient()
    {
        var signedLicense =
            CreateSignedLicense();


        var publicKeyProvider =
            new TestPublicKeyProvider(
                signedLicense.PublicKey,
                "Integration ERP",
                "1.0");


        var verificationService =
            new LicenseVerificationService(
                publicKeyProvider);


        bool result =
            verificationService.Verify(
                signedLicense.Payload,
                signedLicense.Signature);


        Assert.True(result);
    }


    // -------------------------------------------------
    // TEST 2
    // Body değiştirilirse Verify false olmalı
    // -------------------------------------------------
    [Fact]
    public void TamperedBody_ShouldFailVerification()
    {
        var signedLicense =
            CreateSignedLicense();


        var publicKeyProvider =
            new TestPublicKeyProvider(
                signedLicense.PublicKey,
                "Integration ERP",
                "1.0");


        var verificationService =
            new LicenseVerificationService(
                publicKeyProvider);


        // Server imzaladıktan SONRA
        // payload üzerinde değişiklik yapıyoruz
        signedLicense.Payload.Modules[0] =
            "Admin";


        bool result =
            verificationService.Verify(
                signedLicense.Payload,
                signedLicense.Signature);


        Assert.False(result);
    }


    // -------------------------------------------------
    // TEST 3
    // Signature'ın tek byte'ı değiştirilirse false
    // -------------------------------------------------
    [Fact]
    public void TamperedSignature_ShouldFailVerification()
    {
        var signedLicense =
            CreateSignedLicense();


        var publicKeyProvider =
            new TestPublicKeyProvider(
                signedLicense.PublicKey,
                "Integration ERP",
                "1.0");


        var verificationService =
            new LicenseVerificationService(
                publicKeyProvider);


        byte[] signatureBytes =
            Convert.FromBase64String(
                signedLicense.Signature);


        // Tek bir byte değiştir
        signatureBytes[0] ^= 0x01;


        string tamperedSignature =
            Convert.ToBase64String(
                signatureBytes);


        bool result =
            verificationService.Verify(
                signedLicense.Payload,
                tamperedSignature);


        Assert.False(result);
    }
}
