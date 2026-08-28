namespace LicenseManager.Api.Services;

/// <summary>
/// Private signing key'lerin saklama ortamına özel korumasını soyutlar.
/// Development/Demo ortamında AES-GCM, Production ortamında KMS/Vault
/// tabanlı bir implementasyon kullanılabilir.
/// </summary>
public interface IPrivateKeyProtector
{
    byte[] Protect(byte[] privateKey);

    byte[] Unprotect(byte[] protectedPrivateKey);
}
