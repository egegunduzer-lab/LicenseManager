using System.Security.Cryptography;
using LicenseManager.Api.Services;

namespace LicenseManager.Tests;

public class AesGcmPrivateKeyProtectorTests
{
    [Fact]
    public void ProtectAndUnprotect_ShouldRoundTrip()
    {
        byte[] masterKey =
            RandomNumberGenerator.GetBytes(32);

        byte[] privateKey =
            RandomNumberGenerator.GetBytes(64);

        using var protector =
            new AesGcmPrivateKeyProtector(masterKey);

        byte[] protectedValue =
            protector.Protect(privateKey);

        byte[] unprotectedValue =
            protector.Unprotect(protectedValue);

        Assert.True(
            AesGcmPrivateKeyProtector
                .IsProtectedPayload(protectedValue));

        Assert.Equal(
            privateKey,
            unprotectedValue);

        Assert.NotEqual(
            privateKey,
            protectedValue);
    }


    [Fact]
    public void Protect_ShouldUseUniqueNonce()
    {
        byte[] masterKey =
            RandomNumberGenerator.GetBytes(32);

        byte[] privateKey =
            RandomNumberGenerator.GetBytes(64);

        using var protector =
            new AesGcmPrivateKeyProtector(masterKey);

        byte[] first =
            protector.Protect(privateKey);

        byte[] second =
            protector.Protect(privateKey);

        Assert.NotEqual(
            first,
            second);
    }


    [Fact]
    public void Unprotect_ShouldRejectTamperedCiphertext()
    {
        byte[] masterKey =
            RandomNumberGenerator.GetBytes(32);

        using var protector =
            new AesGcmPrivateKeyProtector(masterKey);

        byte[] protectedValue =
            protector.Protect(
                RandomNumberGenerator.GetBytes(64));

        protectedValue[^1] ^= 0x01;

        Assert.Throws<AuthenticationTagMismatchException>(
            () => protector.Unprotect(protectedValue));
    }


    [Fact]
    public void Unprotect_ShouldRejectWrongMasterKey()
    {
        using var firstProtector =
            new AesGcmPrivateKeyProtector(
                RandomNumberGenerator.GetBytes(32));

        using var secondProtector =
            new AesGcmPrivateKeyProtector(
                RandomNumberGenerator.GetBytes(32));

        byte[] protectedValue =
            firstProtector.Protect(
                RandomNumberGenerator.GetBytes(64));

        Assert.Throws<AuthenticationTagMismatchException>(
            () => secondProtector.Unprotect(protectedValue));
    }


    [Fact]
    public void Constructor_ShouldRejectNon256BitKey()
    {
        Assert.Throws<ArgumentException>(
            () => new AesGcmPrivateKeyProtector(
                RandomNumberGenerator.GetBytes(16)));
    }
}
