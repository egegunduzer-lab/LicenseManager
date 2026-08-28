using System.Security.Cryptography;
using System.Runtime.Versioning;
using LicenseManager.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LicenseManager.Api.Services;

/// <summary>
/// Yalnızca mevcut Development/Demo verisini DPAPI'den LMK1 AES-GCM
/// zarfına taşımak için çalışır. Yeni private key'ler hiçbir zaman DPAPI
/// ile korunmaz.
/// </summary>
public sealed class LegacyDpapiPrivateKeyMigrationService :
    IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AesGcmPrivateKeyProtector _aesProtector;
    private readonly ILogger<LegacyDpapiPrivateKeyMigrationService> _logger;


    public LegacyDpapiPrivateKeyMigrationService(
        IServiceScopeFactory scopeFactory,
        AesGcmPrivateKeyProtector aesProtector,
        ILogger<LegacyDpapiPrivateKeyMigrationService> logger)
    {
        _scopeFactory =
            scopeFactory;

        _aesProtector =
            aesProtector;

        _logger =
            logger;
    }


    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
            _scopeFactory.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var projects =
            await dbContext.Projects
                .ToListAsync(cancellationToken);

        var projectKeys =
            await dbContext.ProjectKeys
                .ToListAsync(cancellationToken);

        bool hasLegacyValues =
            projects.Any(p => IsLegacy(p.PrivateKey)) ||
            projectKeys.Any(k => IsLegacy(k.PrivateKey));

        if (!hasLegacyValues)
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Legacy DPAPI private key kayıtları yalnızca onları oluşturan Windows kullanıcısı altında AES-GCM'e taşınabilir.");
        }

        await using var transaction =
            await dbContext.Database
                .BeginTransactionAsync(cancellationToken);

        int migratedCount = 0;

        foreach (var project in projects)
        {
            if (!IsLegacy(project.PrivateKey))
            {
                continue;
            }

            project.PrivateKey =
                MigrateValue(project.PrivateKey);

            migratedCount++;
        }

        foreach (var projectKey in projectKeys)
        {
            if (!IsLegacy(projectKey.PrivateKey))
            {
                continue;
            }

            projectKey.PrivateKey =
                MigrateValue(projectKey.PrivateKey);

            migratedCount++;
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        _logger.LogInformation(
            "{MigratedCount} legacy DPAPI private key kaydı AES-256-GCM formatına taşındı.",
            migratedCount);
    }


    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }


    [SupportedOSPlatform("windows")]
    private byte[] MigrateValue(
        byte[] legacyValue)
    {
        byte[] plaintext =
            ProtectedData.Unprotect(
                legacyValue,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);

        byte[]? verification =
            null;

        try
        {
            byte[] migratedValue =
                _aesProtector.Protect(
                    plaintext);

            verification =
                _aesProtector.Unprotect(
                    migratedValue);

            if (!CryptographicOperations.FixedTimeEquals(
                    plaintext,
                    verification))
            {
                throw new CryptographicException(
                    "AES-GCM private key geçiş doğrulaması başarısız oldu.");
            }

            return migratedValue;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                plaintext);

            if (verification is not null)
            {
                CryptographicOperations.ZeroMemory(
                    verification);
            }
        }
    }


    private static bool IsLegacy(
        byte[] value)
    {
        return value.Length > 0 &&
               !AesGcmPrivateKeyProtector
                   .IsProtectedPayload(value);
    }
}
