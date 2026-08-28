
using System.Net.Http.Json;
using System.Text.Json;
using LicenseManager.Client.Models;
using LicenseManager.Core.DTOs;

namespace LicenseManager.Client.Services;

public class LicenseApiService
{
    private readonly HttpClient _httpClient;


    // -------------------------------------------------
    // Normal uygulamada kullanılan constructor
    // -------------------------------------------------
    public LicenseApiService(string baseAddress)
    {
        _httpClient =
            new HttpClient
            {
                BaseAddress =
                    new Uri(baseAddress)
            };
    }


    // -------------------------------------------------
    // Testlerde özel HttpClient verebilmek için
    // -------------------------------------------------
    public LicenseApiService(HttpClient httpClient)
    {
        _httpClient =
            httpClient;
    }


    // -------------------------------------------------
    // Yeni / güncel lisans al
    // -------------------------------------------------
    public async Task<LicenseFetchResult> GetLicenseAsync(
        Guid companyId,
        string licenseKey)
    {
        return await SendLicenseRequestAsync(
            "api/licenses/generate",
            companyId,
            licenseKey);
    }


    // -------------------------------------------------
    // Lisansı yenile
    // -------------------------------------------------
    public async Task<LicenseFetchResult> RenewLicenseAsync(
        Guid companyId,
        string licenseKey)
    {
        return await SendLicenseRequestAsync(
            "api/licenses/renew",
            companyId,
            licenseKey);
    }


    // -------------------------------------------------
    // Generate ve Renew için ortak HTTP işlemi
    // -------------------------------------------------
    private async Task<LicenseFetchResult>
        SendLicenseRequestAsync(
            string endpoint,
            Guid companyId,
            string licenseKey)
    {
        try
        {
            var request =
                new LicenseGenerateRequest
                {
                    CompanyId =
                        companyId,

                    LicenseKey =
                        licenseKey
                };


            using var response =
                await _httpClient.PostAsJsonAsync(
                    endpoint,
                    request);


            // -------------------------------------------------
            // HTTP başarısız cevap
            // -------------------------------------------------
            if (!response.IsSuccessStatusCode)
            {
                int statusCode =
                    (int)response.StatusCode;


                // 4xx:
                // Lisans bulunamadı / blocked /
                // geçersiz istek vb.
                if (statusCode >= 400 &&
                    statusCode < 500)
                {
                    return RejectedResult();
                }


                // 5xx vb.
                return UnavailableResult();
            }


            // -------------------------------------------------
            // JSON cevabını oku
            // -------------------------------------------------
            SignedLicenseResponse? license =
                await response.Content
                    .ReadFromJsonAsync<
                        SignedLicenseResponse>();


            // -------------------------------------------------
            // Response yapısal olarak geçerli mi?
            // -------------------------------------------------
            if (!IsStructurallyValid(
                    license))
            {
                return UnavailableResult();
            }


            return new LicenseFetchResult
            {
                Status =
                    LicenseFetchStatus.Success,

                License =
                    license
            };
        }
        catch (JsonException)
        {
            // 200 OK geldi ama JSON bozuk.
            return UnavailableResult();
        }
        catch (NotSupportedException)
        {
            // Response içeriği beklenen JSON
            // formatında değil.
            return UnavailableResult();
        }
        catch (HttpRequestException)
        {
            // API / network erişim problemi.
            return UnavailableResult();
        }
        catch (TaskCanceledException)
        {
            // Timeout vb.
            return UnavailableResult();
        }
    }


    // -------------------------------------------------
    // Response'ın minimum gerekli alanlarını kontrol et
    // -------------------------------------------------
    private static bool IsStructurallyValid(
        SignedLicenseResponse? license)
    {
        if (license is null)
        {
            return false;
        }


        if (license.Body is null)
        {
            return false;
        }


        if (string.IsNullOrWhiteSpace(
                license.Signature))
        {
            return false;
        }


        if (license.Body.LicenseId ==
            Guid.Empty)
        {
            return false;
        }


        if (license.Body.CompanyId ==
            Guid.Empty)
        {
            return false;
        }


        if (license.Body.ProjectId ==
            Guid.Empty)
        {
            return false;
        }


        if (string.IsNullOrWhiteSpace(
                license.Body.ProjectName))
        {
            return false;
        }


        if (string.IsNullOrWhiteSpace(
                license.Body.ProjectVersion))
        {
            return false;
        }


        if (license.Body.KeyVersion <= 0)
        {
            return false;
        }


        return true;
    }


    // -------------------------------------------------
    // Ortak sonuçlar
    // -------------------------------------------------
    private static LicenseFetchResult
        RejectedResult()
    {
        return new LicenseFetchResult
        {
            Status =
                LicenseFetchStatus.Rejected
        };
    }


    private static LicenseFetchResult
        UnavailableResult()
    {
        return new LicenseFetchResult
        {
            Status =
                LicenseFetchStatus.Unavailable
        };
    }
}