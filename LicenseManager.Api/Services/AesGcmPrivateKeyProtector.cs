using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace LicenseManager.Api.Services;

public sealed class AesGcmPrivateKeyProtector :
    IPrivateKeyProtector,
    IDisposable
{
    private static readonly byte[] Header =
        "LMK1"u8.ToArray();

    private const int MasterKeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int EnvelopePrefixSize = 4 + NonceSize + TagSize;

    private readonly byte[] _masterKey;
    private bool _disposed;


    public AesGcmPrivateKeyProtector(
        IOptions<PrivateKeyProtectionOptions> options)
        : this(ParseMasterKey(options.Value.MasterKey))
    {
    }


    // Testler ve açık composition root'lar için. Anahtar mutlaka 32 bayt
    // olmalıdır ve koruyucu kendi kopyasını alır.
    public AesGcmPrivateKeyProtector(
        byte[] masterKey)
    {
        ArgumentNullException.ThrowIfNull(masterKey);

        if (masterKey.Length != MasterKeySize)
        {
            throw new ArgumentException(
                "AES-256-GCM master key tam 32 bayt olmalıdır.",
                nameof(masterKey));
        }

        if (!AesGcm.IsSupported)
        {
            throw new PlatformNotSupportedException(
                "AES-GCM bu platformda desteklenmiyor.");
        }

        _masterKey =
            masterKey.ToArray();
    }


    public byte[] Protect(
        byte[] privateKey)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        ArgumentNullException.ThrowIfNull(privateKey);

        if (privateKey.Length == 0)
        {
            throw new ArgumentException(
                "Private key boş olamaz.",
                nameof(privateKey));
        }

        byte[] envelope =
            new byte[EnvelopePrefixSize + privateKey.Length];

        Header.CopyTo(
            envelope,
            0);

        Span<byte> nonce =
            envelope.AsSpan(Header.Length, NonceSize);

        Span<byte> tag =
            envelope.AsSpan(Header.Length + NonceSize, TagSize);

        Span<byte> ciphertext =
            envelope.AsSpan(EnvelopePrefixSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes =
            new AesGcm(_masterKey, TagSize);

        aes.Encrypt(
            nonce,
            privateKey,
            ciphertext,
            tag,
            Header);

        return envelope;
    }


    public byte[] Unprotect(
        byte[] protectedPrivateKey)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        ArgumentNullException.ThrowIfNull(protectedPrivateKey);

        if (!IsProtectedPayload(protectedPrivateKey))
        {
            throw new CryptographicException(
                "Private key AES-GCM LMK1 formatında değil. Legacy DPAPI geçişi gerekebilir.");
        }

        ReadOnlySpan<byte> nonce =
            protectedPrivateKey.AsSpan(Header.Length, NonceSize);

        ReadOnlySpan<byte> tag =
            protectedPrivateKey.AsSpan(Header.Length + NonceSize, TagSize);

        ReadOnlySpan<byte> ciphertext =
            protectedPrivateKey.AsSpan(EnvelopePrefixSize);

        byte[] privateKey =
            new byte[ciphertext.Length];

        try
        {
            using var aes =
                new AesGcm(_masterKey, TagSize);

            aes.Decrypt(
                nonce,
                ciphertext,
                tag,
                privateKey,
                Header);

            return privateKey;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(
                privateKey);

            throw;
        }
    }


    public static bool IsProtectedPayload(
        ReadOnlySpan<byte> value)
    {
        return value.Length > EnvelopePrefixSize &&
               value[..Header.Length].SequenceEqual(Header);
    }


    public static bool IsValidBase64MasterKey(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            byte[] key =
                Convert.FromBase64String(value);

            bool isValid =
                key.Length == MasterKeySize;

            CryptographicOperations.ZeroMemory(
                key);

            return isValid;
        }
        catch (FormatException)
        {
            return false;
        }
    }


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(
            _masterKey);

        _disposed =
            true;
    }


    private static byte[] ParseMasterKey(
        string value)
    {
        if (!IsValidBase64MasterKey(value))
        {
            throw new InvalidOperationException(
                "PrivateKeyProtection:MasterKey geçerli Base64 formatında 32 bayt olmalıdır.");
        }

        return Convert.FromBase64String(value);
    }
}
