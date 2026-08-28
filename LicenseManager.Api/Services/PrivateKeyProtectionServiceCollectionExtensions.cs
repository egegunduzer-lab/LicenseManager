namespace LicenseManager.Api.Services;

public static class PrivateKeyProtectionServiceCollectionExtensions
{
    public static IServiceCollection AddPrivateKeyProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        IConfigurationSection section =
            configuration.GetSection(
                PrivateKeyProtectionOptions.SectionName);

        bool isDevelopmentOrDemo =
            environment.IsDevelopment() ||
            environment.IsEnvironment("Demo");

        if (!isDevelopmentOrDemo)
        {
            bool hasProductionProvider =
                services.Any(
                    descriptor =>
                        descriptor.ServiceType ==
                        typeof(IPrivateKeyProtector));

            if (hasProductionProvider)
            {
                return services;
            }

            throw new InvalidOperationException(
                "Production ortamı için bu çağrıdan önce KMS/Vault tabanlı bir IPrivateKeyProtector kaydı eklenmelidir.");
        }

        string? configuredProvider =
            section[nameof(PrivateKeyProtectionOptions.Provider)];

        string provider =
            string.IsNullOrWhiteSpace(configuredProvider) &&
            isDevelopmentOrDemo
                ? "AesGcm"
                : configuredProvider?.Trim() ?? string.Empty;

        if (!provider.Equals(
                "AesGcm",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                isDevelopmentOrDemo
                    ? $"Private key provider '{provider}' desteklenmiyor. Development/Demo için 'AesGcm' kullanın."
                    : "Private key provider yapılandırması geçersiz.");
        }

        services
            .AddOptions<PrivateKeyProtectionOptions>()
            .Bind(section)
            .Validate(
                options =>
                    AesGcmPrivateKeyProtector
                        .IsValidBase64MasterKey(
                            options.MasterKey),
                "PrivateKeyProtection:MasterKey geçerli Base64 formatında 32 bayt olmalıdır.")
            .ValidateOnStart();

        services.AddSingleton<AesGcmPrivateKeyProtector>();

        services.AddSingleton<IPrivateKeyProtector>(
            serviceProvider =>
                serviceProvider.GetRequiredService<AesGcmPrivateKeyProtector>());

        if (section.GetValue<bool>(
                nameof(PrivateKeyProtectionOptions.MigrateLegacyDpapi)))
        {
            services.AddHostedService<LegacyDpapiPrivateKeyMigrationService>();
        }

        return services;
    }
}
