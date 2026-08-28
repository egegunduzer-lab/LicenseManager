using System.Security.Cryptography;
using LicenseManager.Client.Configuration;
using LicenseManager.Core.DTOs;
using LicenseManager.Core.Services;
using NSec.Cryptography;

namespace LicenseManager.Client.Services;

public class LicenseVerificationService
    : ILicenseVerificationService
{
    private static readonly SignatureAlgorithm Algorithm =
        SignatureAlgorithm.Ed25519;

    private readonly ILicensePublicKeyProvider _publicKeyProvider;


    // Normal uygulamada embedded key provider kullanılır
    public LicenseVerificationService()
        : this(new EmbeddedLicensePublicKeyProvider())
    {
    }


    // Testlerde farklı provider verebilmek için
    public LicenseVerificationService(
        ILicensePublicKeyProvider publicKeyProvider)
    {
        _publicKeyProvider = publicKeyProvider;
    }


    public bool Verify(
        LicensePayload body,
        string signatureBase64)
    {
        try
        {
            // -------------------------------------------------
            // 1. Payload'ın istediği key_version mevcut mu?
            //
            // Signature doğrulamak için hangi public key'in
            // kullanılacağını bilmemiz gerektiğinden,
            // key_version kontrolü önce yapılmak zorundadır.
            // -------------------------------------------------
            if (!_publicKeyProvider.TryGetPublicKey(
                    body.KeyVersion,
                    out byte[] publicKeyBytes))
            {
                return false;
            }


            // -------------------------------------------------
            // 2. Payload canonical hale getirilir
            // -------------------------------------------------
            byte[] canonicalBytes =
                JsonCanonicalizer.Canonicalize(body);


            // -------------------------------------------------
            // 3. SHA-256 hash oluşturulur
            // -------------------------------------------------
            byte[] hash =
                SHA256.HashData(
                    canonicalBytes);


            // -------------------------------------------------
            // 4. Signature Base64 -> byte[]
            // -------------------------------------------------
            byte[] signature =
                Convert.FromBase64String(
                    signatureBase64);


            // -------------------------------------------------
            // 5. key_version'a ait public key,
            //    Ed25519 public key olarak yüklenir
            // -------------------------------------------------
            var publicKey =
                PublicKey.Import(
                    Algorithm,
                    publicKeyBytes,
                    KeyBlobFormat.RawPublicKey);


            // -------------------------------------------------
            // 6. Önce kriptografik imza doğrulanır
            // -------------------------------------------------
            bool signatureValid =
                Algorithm.Verify(
                    publicKey,
                    hash,
                    signature);


            if (!signatureValid)
            {
                return false;
            }


            // -------------------------------------------------
            // 7. İmza geçerliyse proje kimliği kontrol edilir
            //
            // Bu alanlar imzalı payload'ın içinde olduğundan,
            // önce signature doğrulanır, ardından Client'ın
            // beklediği proje ile eşleşip eşleşmediğine bakılır.
            // -------------------------------------------------
            if (!string.Equals(
                    body.ProjectName,
                    _publicKeyProvider.ProjectName,
                    StringComparison.Ordinal))
            {
                return false;
            }


            if (!string.Equals(
                    body.ProjectVersion,
                    _publicKeyProvider.ProjectVersion,
                    StringComparison.Ordinal))
            {
                return false;
            }


            // -------------------------------------------------
            // 8. Signature + proje kimliği doğru
            // -------------------------------------------------
            return true;
        }
        catch (FormatException)
        {
            // Signature geçerli Base64 değil
            return false;
        }
        catch (ArgumentException)
        {
            // Public key / signature format problemi
            return false;
        }
        catch (CryptographicException)
        {
            // Kriptografik işlem başarısız
            return false;
        }
    }
}