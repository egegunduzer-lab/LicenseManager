using LicenseManager.Api.Data;
using LicenseManager.Api.Services;
using LicenseManager.Core.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LicenseManager.Api.Controllers;

[ApiController]
[Route("api/licenses")]
public class LicensesController : ControllerBase
{
    private readonly ApplicationDbContext? _dbContext;
    private readonly LicenseService _licenseService;
    private readonly AdminApiKeyValidator _adminApiKeyValidator;


    // =================================================
    // ESKİ CONSTRUCTOR
    //
    // Mevcut testleri bozmamak için tutuluyor.
    // Generate / Renew / Create / Block / Unblock
    // işlemleri çalışmaya devam eder.
    // =================================================
    public LicensesController(
        LicenseService licenseService,
        AdminApiKeyValidator adminApiKeyValidator)
    {
        _licenseService =
            licenseService;

        _adminApiKeyValidator =
            adminApiKeyValidator;

        _dbContext =
            null;
    }


    // =================================================
    // GERÇEK API'DE KULLANILACAK CONSTRUCTOR
    //
    // GET endpointleri için DbContext gerekir.
    // =================================================
    [ActivatorUtilitiesConstructor]
    public LicensesController(
        LicenseService licenseService,
        AdminApiKeyValidator adminApiKeyValidator,
        ApplicationDbContext dbContext)
    {
        _licenseService =
            licenseService;

        _adminApiKeyValidator =
            adminApiKeyValidator;

        _dbContext =
            dbContext;
    }


    // =================================================
    // TÜM LİSANSLARI LİSTELE
    //
    // GET /api/licenses
    // ADMIN ONLY
    // =================================================
    [HttpGet]
    public async Task<IActionResult> GetLicenses(
        [FromHeader(Name = "X-Admin-Api-Key")]
        string? adminApiKey)
    {
        // -------------------------------------------------
        // 1. Admin kontrolü
        // -------------------------------------------------
        if (!_adminApiKeyValidator.IsValid(
                adminApiKey))
        {
            return Unauthorized(
                new
                {
                    message =
                        "Yetkisiz istek."
                });
        }


        if (_dbContext is null)
        {
            throw new InvalidOperationException(
                "ApplicationDbContext bulunamadı.");
        }


        // -------------------------------------------------
        // 2. Lisansları getir
        // -------------------------------------------------
        var licenses =
            await _dbContext.Licenses
                .AsNoTracking()
                .OrderByDescending(
                    l => l.CreatedAt)
                .Select(l => new
                {
                    id =
                        l.Id,

                    company_id =
                        l.CompanyId,

                    company_name =
                        l.Company.Name,

                    project_id =
                        l.ProjectId,

                    project_name =
                        l.Project.Name,

                    project_version =
                        l.Project.Version,

                    license_key =
                        l.LicenseKey,

                    modules =
                        l.Modules,

                    issued_at =
                        l.IssuedAt,

                    // Signed license dosyasının
                    // geçerlilik sonu
                    expiry_date =
                        l.ExpiryDate,

                    // Müşterinin gerçek lisans
                    // hakkının son tarihi
                    valid_until =
                        l.ValidUntil,

                    is_blocked =
                        l.IsBlocked,

                    created_at =
                        l.CreatedAt
                })
                .ToListAsync();


        return Ok(
            licenses);
    }


    // =================================================
    // TEK LİSANS DETAYI
    //
    // GET /api/licenses/{licenseKey}
    // ADMIN ONLY
    // =================================================
    [HttpGet("{licenseKey}")]
    public async Task<IActionResult> GetLicense(
        string licenseKey,

        [FromHeader(Name = "X-Admin-Api-Key")]
        string? adminApiKey)
    {
        // -------------------------------------------------
        // 1. Admin kontrolü
        // -------------------------------------------------
        if (!_adminApiKeyValidator.IsValid(
                adminApiKey))
        {
            return Unauthorized(
                new
                {
                    message =
                        "Yetkisiz istek."
                });
        }


        if (_dbContext is null)
        {
            throw new InvalidOperationException(
                "ApplicationDbContext bulunamadı.");
        }


        // -------------------------------------------------
        // 2. Lisansı getir
        // -------------------------------------------------
        var license =
            await _dbContext.Licenses
                .AsNoTracking()
                .Where(
                    l => l.LicenseKey == licenseKey)
                .Select(l => new
                {
                    id =
                        l.Id,

                    project =
                        new
                        {
                            name =
                                l.Project.Name,

                            version =
                                l.Project.Version,

                            active_key_version =
                                l.Project.KeyVersion
                        },

                    license_key =
                        l.LicenseKey,

                    modules =
                        l.Modules,

                    issued_at =
                        l.IssuedAt,

                    expiry_date =
                        l.ExpiryDate,

                    valid_until =
                        l.ValidUntil,

                    is_blocked =
                        l.IsBlocked,

                    created_at =
                        l.CreatedAt
                })
                .SingleOrDefaultAsync();


        if (license is null)
        {
            return NotFound(
                new
                {
                    message =
                        "Lisans bulunamadı."
                });
        }


        return Ok(
            license);
    }


    // =================================================
    // MEVCUT LİSANSI İMZALA / GETİR
    //
    // POST /api/licenses/generate
    // CLIENT
    // =================================================
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateLicense(
        LicenseGenerateRequest request)
    {
        if (request.CompanyId == Guid.Empty ||
            string.IsNullOrWhiteSpace(
                request.LicenseKey))
        {
            return BadRequest(
                "Geçersiz istek.");
        }


        var result =
            await _licenseService
                .GenerateLicenseAsync(
                    request.CompanyId,
                    request.LicenseKey);


        if (result is null)
        {
            // Missing / blocked / expired ayrımı
            // client'a sızdırılmıyor.
            return NotFound(
                "Lisans doğrulanamadı.");
        }


        return Ok(
            result);
    }


    // =================================================
    // LİSANSI YENİLE
    //
    // POST /api/licenses/renew
    // CLIENT
    // =================================================
    [HttpPost("renew")]
    public async Task<IActionResult> RenewLicense(
        LicenseGenerateRequest request)
    {
        if (request.CompanyId == Guid.Empty ||
            string.IsNullOrWhiteSpace(
                request.LicenseKey))
        {
            return BadRequest(
                "Geçersiz istek.");
        }


        var result =
            await _licenseService
                .RenewLicenseAsync(
                    request.CompanyId,
                    request.LicenseKey);


        if (result is null)
        {
            // Generate ile aynı genel hata.
            // Blocked / bulunamadı / expired
            // ayrımı sızdırılmıyor.
            return NotFound(
                "Lisans doğrulanamadı.");
        }


        return Ok(
            result);
    }


    // =================================================
    // YENİ LİSANS OLUŞTUR
    //
    // POST /api/licenses/create
    // ADMIN ONLY
    // =================================================
    [HttpPost("create")]
    public async Task<IActionResult> CreateLicense(
        [FromBody]
        CreateLicenseRequest request,

        [FromHeader(Name = "X-Admin-Api-Key")]
        string? adminApiKey)
    {
        // -------------------------------------------------
        // 1. Admin API Key kontrolü
        // -------------------------------------------------
        if (!_adminApiKeyValidator.IsValid(
                adminApiKey))
        {
            return Unauthorized(
                new
                {
                    message =
                        "Yetkisiz istek."
                });
        }


        // -------------------------------------------------
        // 2. Request kontrolü
        // -------------------------------------------------
        if (request.CompanyId == Guid.Empty ||
            request.ProjectId == Guid.Empty ||
            request.Modules is null ||
            request.Modules.Count == 0 ||
            request.ValidUntil <= DateTime.UtcNow)
        {
            return BadRequest(
                new
                {
                    message =
                        "Lisans oluşturulamadı."
                });
        }


        // -------------------------------------------------
        // 3. Yeni lisansı oluştur
        // -------------------------------------------------
        var result =
            await _licenseService
                .CreateLicenseAsync(
                    request.CompanyId,
                    request.ProjectId,
                    request.Modules,
                    request.ValidUntil);


        // Firma / proje bulunamadı,
        // ValidUntil geçersiz veya
        // modül listesi geçersiz olabilir.
        if (result is null)
        {
            return BadRequest(
                new
                {
                    message =
                        "Lisans oluşturulamadı."
                });
        }


        // -------------------------------------------------
        // 4. Başarılı
        // -------------------------------------------------
        return StatusCode(
            StatusCodes.Status201Created,
            result);
    }


    // =================================================
    // LİSANSI ENGELLE
    //
    // POST /api/licenses/{licenseId}/block
    // ADMIN ONLY
    // =================================================
    [HttpPost("{licenseId:guid}/block")]
    public async Task<IActionResult> BlockLicense(
        Guid licenseId,

        [FromHeader(Name = "X-Admin-Api-Key")]
        string? adminApiKey)
    {
        // -------------------------------------------------
        // 1. Admin kontrolü
        // -------------------------------------------------
        if (!_adminApiKeyValidator.IsValid(
                adminApiKey))
        {
            return Unauthorized(
                new
                {
                    message =
                        "Yetkisiz istek."
                });
        }


        // -------------------------------------------------
        // 2. Lisansı block et
        // -------------------------------------------------
        bool updated =
            await _licenseService
                .SetBlockedStatusAsync(
                    licenseId,
                    true);


        if (!updated)
        {
            return NotFound(
                new
                {
                    message =
                        "Lisans bulunamadı."
                });
        }


        return Ok(
            new
            {
                license_id =
                    licenseId,

                is_blocked =
                    true
            });
    }


    // =================================================
    // LİSANS ENGELİNİ KALDIR
    //
    // POST /api/licenses/{licenseId}/unblock
    // ADMIN ONLY
    // =================================================
    [HttpPost("{licenseId:guid}/unblock")]
    public async Task<IActionResult> UnblockLicense(
        Guid licenseId,

        [FromHeader(Name = "X-Admin-Api-Key")]
        string? adminApiKey)
    {
        // -------------------------------------------------
        // 1. Admin kontrolü
        // -------------------------------------------------
        if (!_adminApiKeyValidator.IsValid(
                adminApiKey))
        {
            return Unauthorized(
                new
                {
                    message =
                        "Yetkisiz istek."
                });
        }


        // -------------------------------------------------
        // 2. Block durumunu kaldır
        // -------------------------------------------------
        bool updated =
            await _licenseService
                .SetBlockedStatusAsync(
                    licenseId,
                    false);


        if (!updated)
        {
            return NotFound(
                new
                {
                    message =
                        "Lisans bulunamadı."
                });
        }


        return Ok(
            new
            {
                license_id =
                    licenseId,

                is_blocked =
                    false
            });
    }
}
