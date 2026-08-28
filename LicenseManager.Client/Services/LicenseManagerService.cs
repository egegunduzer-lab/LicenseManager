using LicenseManager.Client.Models;

namespace LicenseManager.Client.Services;

public class LicenseManagerService : ILicenseManagerService
{
    private const string GenericErrorMessage =
    "Lisans doğrulanamadı.";
    private readonly LicenseFileService _licenseFileService;
    private readonly LicenseApiService _apiService;
    private readonly LicenseValidatorService _validatorService;

    private readonly int _renewalThresholdDays;

    public LicenseManagerService(
        LicenseFileService licenseFileService,
        LicenseApiService apiService,
        LicenseValidatorService validatorService,
        int renewalThresholdDays = 7)
    {
        _licenseFileService = licenseFileService;
        _apiService = apiService;
        _validatorService = validatorService;
        _renewalThresholdDays = renewalThresholdDays;
    }

    public async Task<LicenseValidationResult> CheckLicenseAsync(
        Guid companyId,
        string licenseKey,
        string requiredModule)
    {
        // -------------------------------------------------
        // 1. Önce yerel lisansı yükle
        // -------------------------------------------------

        var cachedLicense =
            await _licenseFileService.LoadAsync();

        if (cachedLicense is null)
        {
            _validatorService
                .PerformMissingLicenseMaskingWork();
        }


        // -------------------------------------------------
        // 2. Yerel lisans varsa doğrula
        // -------------------------------------------------

        if (cachedLicense is not null)
        {
            var cachedResult =
                _validatorService.Validate(
                    cachedLicense,
                    requiredModule,
                    companyId);

            // Yerel lisans geçerliyse
            if (cachedResult.IsValid)
            {
                TimeSpan remainingTime =
                    cachedLicense.Body.ExpiryDate.ToUniversalTime()
                    - DateTime.UtcNow;

                double remainingDays =
                    remainingTime.TotalDays;


                // -------------------------------------------------
                // 3. 7 günden fazla kaldıysa API'ye gitme
                // -------------------------------------------------

                if (remainingDays > _renewalThresholdDays)
                {
                    return new LicenseValidationResult
                    {
                        IsValid = true,
                        Message =
                            $"Yerel lisans geçerli. " +
                            $"Kalan süre: " +
                            $"{Math.Ceiling(remainingDays)} gün. " +
                            $"Yenileme gerekmiyor."
                    };
                }


                // -------------------------------------------------
                // 4. Süre yaklaştıysa renewal dene
                // -------------------------------------------------

                var renewResult =
                    await _apiService.RenewLicenseAsync(
                        companyId,
                        licenseKey);


                // Renewal başarılı
                if (renewResult.Status ==
                        LicenseFetchStatus.Success &&
                    renewResult.License is not null)
                {
                    var renewedValidation =
                        _validatorService.Validate(
                            renewResult.License,
                            requiredModule,
                            companyId);

                    if (renewedValidation.IsValid)
                    {
                        await _licenseFileService.SaveAsync(
                            renewResult.License);

                        return new LicenseValidationResult
                        {
                            IsValid = true,
                            Message =
                                "Lisans başarıyla yenilendi ve " +
                                "yerel dosyaya kaydedildi."
                        };
                    }
                }


                // -------------------------------------------------
                // 5. Renewal başarısız ama mevcut lisans geçerli
                // -------------------------------------------------

                if (renewResult.Status ==
                    LicenseFetchStatus.Rejected)
                {
                    return new LicenseValidationResult
                    {
                        IsValid = true,
                        Message =
                            "Sunucu lisansı yenilemedi. " +
                            "Mevcut yerel lisans süresi dolana " +
                            "kadar kullanılmaya devam edilecek."
                    };
                }

                return new LicenseValidationResult
                {
                    IsValid = true,
                    Message =
                        "Sunucuya ulaşılamadı. " +
                        "Mevcut yerel lisans çevrimdışı " +
                        "kullanılmaya devam edilecek."
                };
            }
        }


        // -------------------------------------------------
        // 6. Geçerli yerel lisans yok
        //    Sunucudan yeni/güncel lisans al
        // -------------------------------------------------

        var fetchResult =
            await _apiService.GetLicenseAsync(
                companyId,
                licenseKey);


        // -------------------------------------------------
        // 7. Sunucudan lisans geldiyse doğrula
        // -------------------------------------------------

        if (fetchResult.Status ==
                LicenseFetchStatus.Success &&
            fetchResult.License is not null)
        {
            var onlineValidation =
                _validatorService.Validate(
                    fetchResult.License,
                    requiredModule,
                    companyId);

            if (!onlineValidation.IsValid)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = GenericErrorMessage
                };
            }

            await _licenseFileService.SaveAsync(
                fetchResult.License);

            return new LicenseValidationResult
            {
                IsValid = true,
                Message =
                    "Lisans sunucudan alındı ve " +
                    "yerel dosyaya kaydedildi."
            };
        }


        // -------------------------------------------------
        // 8. Sunucu lisansı reddetti
        // -------------------------------------------------

        if (fetchResult.Status ==
            LicenseFetchStatus.Rejected)
        {
            return new LicenseValidationResult
            {
                IsValid = false,
                Message = GenericErrorMessage
            };
        }


        // -------------------------------------------------
        // 9. Sunucuya ulaşılamadı ve geçerli cache yok
        // -------------------------------------------------

        return new LicenseValidationResult
        {
            IsValid = false,
            Message = GenericErrorMessage
        };
    }
}