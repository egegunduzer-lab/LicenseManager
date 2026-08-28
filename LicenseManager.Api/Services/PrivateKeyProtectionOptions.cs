namespace LicenseManager.Api.Services;

public sealed class PrivateKeyProtectionOptions
{
    public const string SectionName =
        "PrivateKeyProtection";

    public string Provider { get; set; } =
        string.Empty;

    // Base64 formatında tam 32 bayt. Kaynak kod veya appsettings içine
    // yazılmaz; User Secrets ya da environment variable'dan gelir.
    public string MasterKey { get; set; } =
        string.Empty;

    // Mevcut development veritabanındaki DPAPI kayıtlarını AES-GCM'e
    // taşıyan geçici uyumluluk adımıdır.
    public bool MigrateLegacyDpapi { get; set; }
}
