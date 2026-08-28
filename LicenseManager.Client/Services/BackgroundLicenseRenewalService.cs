using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseManager.Client.Services;

public class BackgroundLicenseRenewalService
{
    private readonly ILicenseManagerService
        _licenseManagerService;

    private readonly Guid _companyId;
    private readonly string _licenseKey;
    private readonly string _requiredModule;

    private readonly TimeSpan _checkInterval;

    private readonly Action<string>? _log;


    public BackgroundLicenseRenewalService(
        ILicenseManagerService licenseManagerService,
        Guid companyId,
        string licenseKey,
        string requiredModule,
        TimeSpan? checkInterval = null,
        Action<string>? log = null)
    {
        _licenseManagerService =
            licenseManagerService;

        _companyId =
            companyId;

        _licenseKey =
            licenseKey;

        _requiredModule =
            requiredModule;

        _checkInterval =
            checkInterval ??
            TimeSpan.FromHours(12);

        _log =
            log;
    }


    // -------------------------------------------------
    // Tek bir background kontrolü
    // -------------------------------------------------
    public async Task RunOnceAsync()
    {
        try
        {
            _log?.Invoke(
                "Arka plan lisans kontrolü başlatıldı.");

            var result =
                await _licenseManagerService
                    .CheckLicenseAsync(
                        _companyId,
                        _licenseKey,
                        _requiredModule);

            if (result.IsValid)
            {
                _log?.Invoke(
                    $"Arka plan kontrolü başarılı: " +
                    $"{result.Message}");
            }
            else
            {
                _log?.Invoke(
                    "Arka plan lisans kontrolü başarısız.");
            }
        }
        catch
        {
            // Kullanıcıya teknik exception ayrıntısı
            // göstermiyoruz.
            _log?.Invoke(
                "Arka plan lisans kontrolü tamamlanamadı.");
        }
    }


    // -------------------------------------------------
    // Sürekli background çalışma
    // -------------------------------------------------
    public async Task RunAsync(
        CancellationToken cancellationToken)
    {
        using var timer =
            new PeriodicTimer(
                _checkInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(
                cancellationToken))
            {
                await RunOnceAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal kapanış.
        }
    }
}