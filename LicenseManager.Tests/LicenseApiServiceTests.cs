using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json;
using LicenseManager.Client.Models;
using LicenseManager.Client.Services;
using LicenseManager.Core.DTOs;

namespace LicenseManager.Tests;

public class LicenseApiServiceTests
{
    // -------------------------------------------------
    // Sahte HTTP sunucusu
    // -------------------------------------------------
    private class FakeHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(
            Func<HttpRequestMessage,
                HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _handler(request));
        }
    }


    // -------------------------------------------------
    // Network hatası üreten sahte handler
    // -------------------------------------------------
    private class ThrowingHttpMessageHandler
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            throw new HttpRequestException(
                "Test network error");
        }
    }


    // -------------------------------------------------
    // LicenseApiService oluştur
    // -------------------------------------------------
    private static LicenseApiService CreateService(
        HttpMessageHandler handler)
    {
        var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri(
                        "https://localhost/")
            };

        return new LicenseApiService(
            httpClient);
    }


    // -------------------------------------------------
    // Geçerli test lisansı
    // -------------------------------------------------
    private static SignedLicenseResponse
        CreateValidLicense()
    {
        return new SignedLicenseResponse
        {
            Body =
                new LicensePayload
                {
                    LicenseId =
                        Guid.NewGuid(),

                    CompanyId =
                        Guid.NewGuid(),

                    ProjectId =
                        Guid.NewGuid(),

                    ProjectName =
                        "Giltas ERP",

                    ProjectVersion =
                        "1.0",

                    KeyVersion =
                        2,

                    Modules =
                        new List<string>
                        {
                            "Reports",
                            "Inventory"
                        },

                    IssuedAt =
                        DateTime.UtcNow,

                    ExpiryDate =
                        DateTime.UtcNow
                            .AddDays(30)
                },

            Signature =
                "TEST-SIGNATURE"
        };
    }


    // =================================================
    // TEST 1
    // 200 + geçerli JSON -> Success
    // =================================================
    [Fact]
    public async Task ValidResponse_ShouldReturnSuccess()
    {
        var license =
            CreateValidLicense();

        string json =
            JsonSerializer.Serialize(
                license);

        var handler =
            new FakeHttpMessageHandler(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new StringContent(
                                json,
                                Encoding.UTF8,
                                "application/json")
                    });

        var service =
            CreateService(handler);


        LicenseFetchResult result =
            await service.GetLicenseAsync(
                license.Body.CompanyId,
                "TEST-LICENSE");


        Assert.Equal(
            LicenseFetchStatus.Success,
            result.Status);

        Assert.NotNull(
            result.License);

        Assert.Equal(
            license.Body.LicenseId,
            result.License!.Body.LicenseId);
    }


    // =================================================
    // TEST 2
    // 200 ama JSON bozuk -> Unavailable
    // =================================================
    [Fact]
    public async Task MalformedJson_ShouldReturnUnavailable()
    {
        const string malformedJson =
            """
            {
                "body":
                BU-GECERLI-BIR-JSON-DEGIL
            }
            """;

        var handler =
            new FakeHttpMessageHandler(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new StringContent(
                                malformedJson,
                                Encoding.UTF8,
                                "application/json")
                    });

        var service =
            CreateService(handler);


        LicenseFetchResult result =
            await service.GetLicenseAsync(
                Guid.NewGuid(),
                "TEST-LICENSE");


        Assert.Equal(
            LicenseFetchStatus.Unavailable,
            result.Status);

        Assert.Null(
            result.License);
    }


    // =================================================
    // TEST 3
    // Signature eksik -> Unavailable
    // =================================================
    [Fact]
    public async Task MissingSignature_ShouldReturnUnavailable()
    {
        var license =
            CreateValidLicense();

        license.Signature =
            string.Empty;

        string json =
            JsonSerializer.Serialize(
                license);

        var handler =
            new FakeHttpMessageHandler(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new StringContent(
                                json,
                                Encoding.UTF8,
                                "application/json")
                    });

        var service =
            CreateService(handler);


        LicenseFetchResult result =
            await service.GetLicenseAsync(
                license.Body.CompanyId,
                "TEST-LICENSE");


        Assert.Equal(
            LicenseFetchStatus.Unavailable,
            result.Status);
    }


    // =================================================
    // TEST 4
    // 404 -> Rejected
    // =================================================
    [Fact]
    public async Task Http404_ShouldReturnRejected()
    {
        var handler =
            new FakeHttpMessageHandler(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.NotFound));

        var service =
            CreateService(handler);


        LicenseFetchResult result =
            await service.GetLicenseAsync(
                Guid.NewGuid(),
                "TEST-LICENSE");


        Assert.Equal(
            LicenseFetchStatus.Rejected,
            result.Status);
    }


    // =================================================
    // TEST 5
    // 500 -> Unavailable
    // =================================================
    [Fact]
    public async Task Http500_ShouldReturnUnavailable()
    {
        var handler =
            new FakeHttpMessageHandler(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode
                            .InternalServerError));

        var service =
            CreateService(handler);


        LicenseFetchResult result =
            await service.GetLicenseAsync(
                Guid.NewGuid(),
                "TEST-LICENSE");


        Assert.Equal(
            LicenseFetchStatus.Unavailable,
            result.Status);
    }


    // =================================================
    // TEST 6
    // Network problemi -> Unavailable
    // =================================================
    [Fact]
    public async Task NetworkError_ShouldReturnUnavailable()
    {
        var handler =
            new ThrowingHttpMessageHandler();

        var service =
            CreateService(handler);


        LicenseFetchResult result =
            await service.GetLicenseAsync(
                Guid.NewGuid(),
                "TEST-LICENSE");


        Assert.Equal(
            LicenseFetchStatus.Unavailable,
            result.Status);
    }

    [Fact]
    public async Task RenewLicenseAsync_ShouldCallRenewEndpoint()
    {
        string? requestedPath =
            null;

        var license =
            CreateValidLicense();

        string json =
            JsonSerializer.Serialize(
                license);

        var handler =
            new FakeHttpMessageHandler(
                request =>
                {
                    requestedPath =
                        request.RequestUri
                            ?.AbsolutePath;

                    return new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new StringContent(
                                json,
                                Encoding.UTF8,
                                "application/json")
                    };
                });

        var service =
            CreateService(handler);


        var result =
            await service.RenewLicenseAsync(
                license.Body.CompanyId,
                "TEST-LICENSE");


        Assert.Equal(
            LicenseFetchStatus.Success,
            result.Status);

        Assert.Equal(
            "/api/licenses/renew",
            requestedPath);
    }
}

