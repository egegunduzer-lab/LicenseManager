using LicenseManager.Client.Configuration;
using LicenseManager.Client.Services;
using Microsoft.Extensions.Configuration;


// -------------------------------------------------
// Configuration
// -------------------------------------------------

var configuration =
    new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)

        // Genel / gizli olmayan ayarlar
        .AddJsonFile(
            "appsettings.json",
            optional: false,
            reloadOnChange: false)

        // Development ortamındaki gizli değerler
        // Örn: LicenseClient:LicenseKey
        .AddUserSecrets<Program>(
            optional: true)

        // Production ortamında environment variable
        // değerleri hepsinin üzerine yazabilir.
        .AddEnvironmentVariables()

        .Build();


// -------------------------------------------------
// LicenseClient ayarlarını yükle
// -------------------------------------------------

var settings =
    configuration
        .GetSection("LicenseClient")
        .Get<LicenseClientSettings>();


if (settings is null)
{
    Console.WriteLine(
        "Uygulama ayarları yüklenemedi.");

    return;
}


// -------------------------------------------------
// Ayar kontrolü
// -------------------------------------------------

if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl) ||
    settings.CompanyId == Guid.Empty ||
    string.IsNullOrWhiteSpace(settings.LicenseKey) ||
    string.IsNullOrWhiteSpace(settings.RequiredModule))
{
    Console.WriteLine(
        "Uygulama ayarları geçersiz.");

    return;
}


// -------------------------------------------------
// Alt servisler
// -------------------------------------------------

var licenseFileService =
    new LicenseFileService();

var verificationService =
    new LicenseVerificationService();

var validatorService =
    new LicenseValidatorService(
        verificationService);

var apiService =
    new LicenseApiService(
        settings.ApiBaseUrl);


// -------------------------------------------------
// Ana License Manager
// -------------------------------------------------

var licenseManagerService =
    new LicenseManagerService(
        licenseFileService,
        apiService,
        validatorService,
        settings.RenewalThresholdDays);


// -------------------------------------------------
// İlk lisans kontrolü
// -------------------------------------------------

Console.WriteLine(
    "Lisans kontrolü başlatıldı.");

Console.WriteLine();


var result =
    await licenseManagerService.CheckLicenseAsync(
        settings.CompanyId,
        settings.LicenseKey,
        settings.RequiredModule);


Console.WriteLine(
    result.Message);


// -------------------------------------------------
// Lisans geçersizse uygulamayı başlatma
// -------------------------------------------------

if (!result.IsValid)
{
    Console.WriteLine();

    Console.WriteLine(
        "Uygulama lisans nedeniyle çalıştırılamadı.");

    return;
}


// -------------------------------------------------
// Lisans başarılı
// -------------------------------------------------

Console.WriteLine();

Console.WriteLine(
    $"{settings.RequiredModule} modülü kullanılabilir.");

Console.WriteLine(
    "Lisans kontrolü başarılı.");


// -------------------------------------------------
// Background renewal servisini oluştur
// -------------------------------------------------

var backgroundRenewalService =
    new BackgroundLicenseRenewalService(
        licenseManagerService,
        settings.CompanyId,
        settings.LicenseKey,
        settings.RequiredModule,
        log: message =>
            Console.WriteLine(
                $"[BACKGROUND] {message}"));


// -------------------------------------------------
// Uygulama kapanma token'ı
// -------------------------------------------------

using var cancellationTokenSource =
    new CancellationTokenSource();


// Ctrl + C basıldığında uygulamayı düzgün kapat
Console.CancelKeyPress +=
    (sender, eventArgs) =>
    {
        // Console'un aniden kapanmasını engelle
        eventArgs.Cancel = true;

        Console.WriteLine();

        Console.WriteLine(
            "Uygulama kapatılıyor...");

        cancellationTokenSource.Cancel();
    };


// -------------------------------------------------
// Background lisans kontrolünü başlat
// -------------------------------------------------

Console.WriteLine();

Console.WriteLine(
    "Arka plan lisans yenileme servisi başlatıldı.");

Console.WriteLine(
    "Uygulamayı kapatmak için Ctrl + C tuşlarına basın.");

Console.WriteLine();


try
{
    await backgroundRenewalService.RunAsync(
        cancellationTokenSource.Token);
}
catch (OperationCanceledException)
{
    // Normal kapanış
}


// -------------------------------------------------
// Uygulama kapandı
// -------------------------------------------------

Console.WriteLine(
    "Uygulama kapatıldı.");