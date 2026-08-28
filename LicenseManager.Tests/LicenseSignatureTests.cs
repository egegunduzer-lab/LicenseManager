using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Security.Cryptography;
using LicenseManager.Core.DTOs;
using LicenseManager.Core.Services;
using NSec.Cryptography;

namespace LicenseManager.Tests;

public class LicenseSignatureTests
{
    private static readonly SignatureAlgorithm Algorithm =
        SignatureAlgorithm.Ed25519;


    // -------------------------------------------------
    // Yardımcı metot:
    // Test için geçici lisans oluştur ve imzala
    // -------------------------------------------------
    private static (
        LicensePayload Payload,
        byte[] PublicKey,
        byte[] Signature)
        CreateSignedLicense()
    {
        var creationParameters =
            new KeyCreationParameters
            {
                ExportPolicy =
                    KeyExportPolicies.AllowPlaintextArchiving
            };

        using var privateKey =
            Key.Create(
                Algorithm,
                creationParameters);

        byte[] publicKey =
            privateKey.Export(
                KeyBlobFormat.RawPublicKey);


        var payload =
            new LicensePayload
            {
                LicenseId = Guid.NewGuid(),
                CompanyId = Guid.NewGuid(),
                ProjectId = Guid.NewGuid(),

                ProjectName = "Test ERP",
                ProjectVersion = "1.0",

                Modules = new List<string>
                {
                    "Reports",
                    "Inventory"
                },

                IssuedAt =
                    new DateTime(
                        2026, 8, 17,
                        10, 0, 0,
                        DateTimeKind.Utc),

                ExpiryDate =
                    new DateTime(
                        2026, 9, 17,
                        10, 0, 0,
                        DateTimeKind.Utc)
            };


        byte[] canonicalBytes =
            JsonCanonicalizer.Canonicalize(
                payload);

        byte[] hash =
            SHA256.HashData(
                canonicalBytes);

        byte[] signature =
            Algorithm.Sign(
                privateKey,
                hash);


        return (
            payload,
            publicKey,
            signature);
    }


    // -------------------------------------------------
    // Yardımcı metot:
    // Public key ile imzayı doğrula
    // -------------------------------------------------
    private static bool Verify(
        LicensePayload payload,
        byte[] publicKeyBytes,
        byte[] signature)
    {
        byte[] canonicalBytes =
            JsonCanonicalizer.Canonicalize(
                payload);

        byte[] hash =
            SHA256.HashData(
                canonicalBytes);

        var publicKey =
            PublicKey.Import(
                Algorithm,
                publicKeyBytes,
                KeyBlobFormat.RawPublicKey);

        return Algorithm.Verify(
            publicKey,
            hash,
            signature);
    }


    // -------------------------------------------------
    // TEST 1:
    // Hiçbir şey değiştirilmediyse başarılı olmalı
    // -------------------------------------------------
    [Fact]
    public void OriginalLicense_ShouldBeValid()
    {
        var license =
            CreateSignedLicense();

        bool result =
            Verify(
                license.Payload,
                license.PublicKey,
                license.Signature);

        Assert.True(result);
    }


    // -------------------------------------------------
    // TEST 2:
    // Module değiştirilirse imza geçersiz olmalı
    // -------------------------------------------------
    [Fact]
    public void ChangedModule_ShouldInvalidateSignature()
    {
        var license =
            CreateSignedLicense();

        license.Payload.Modules[0] =
            "Admin";

        bool result =
            Verify(
                license.Payload,
                license.PublicKey,
                license.Signature);

        Assert.False(result);
    }


    // -------------------------------------------------
    // TEST 3:
    // ExpiryDate değiştirilirse imza geçersiz olmalı
    // -------------------------------------------------
    [Fact]
    public void ChangedExpiryDate_ShouldInvalidateSignature()
    {
        var license =
            CreateSignedLicense();

        license.Payload.ExpiryDate =
            license.Payload.ExpiryDate
                .AddDays(100);

        bool result =
            Verify(
                license.Payload,
                license.PublicKey,
                license.Signature);

        Assert.False(result);
    }


    // -------------------------------------------------
    // TEST 4:
    // ProjectVersion değiştirilirse imza geçersiz olmalı
    // -------------------------------------------------
    [Fact]
    public void ChangedProjectVersion_ShouldInvalidateSignature()
    {
        var license =
            CreateSignedLicense();

        license.Payload.ProjectVersion =
            "99.0";

        bool result =
            Verify(
                license.Payload,
                license.PublicKey,
                license.Signature);

        Assert.False(result);
    }


    // -------------------------------------------------
    // TEST 5:
    // Signature değiştirilirse doğrulama başarısız olmalı
    // -------------------------------------------------
    [Fact]
    public void ChangedSignature_ShouldBeInvalid()
    {
        var license =
            CreateSignedLicense();

        byte[] modifiedSignature =
            license.Signature.ToArray();

        // Signature'ın ilk byte'ını değiştir
        modifiedSignature[0] ^= 0x01;

        bool result =
            Verify(
                license.Payload,
                license.PublicKey,
                modifiedSignature);

        Assert.False(result);
    }

    [Fact]
    public void WrongPublicKey_ShouldBeInvalid()
    {
        var license =
            CreateSignedLicense();

        var creationParameters =
            new KeyCreationParameters
            {
                ExportPolicy =
                    KeyExportPolicies.AllowPlaintextArchiving
            };

        // Tamamen farklı bir Ed25519 key oluştur
        using var differentKey =
            Key.Create(
                Algorithm,
                creationParameters);

        byte[] wrongPublicKey =
            differentKey.Export(
                KeyBlobFormat.RawPublicKey);

        bool result =
            Verify(
                license.Payload,
                wrongPublicKey,
                license.Signature);

        Assert.False(result);
    }
}
