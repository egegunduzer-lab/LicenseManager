using LicenseManager.Api.Data;
using LicenseManager.Api.Services;
using LicenseManager.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LicenseManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly AdminApiKeyValidator _adminApiKeyValidator;

    public CompaniesController(
        ApplicationDbContext dbContext,
        AdminApiKeyValidator adminApiKeyValidator)
    {
        _dbContext = dbContext;
        _adminApiKeyValidator = adminApiKeyValidator;
    }


    // =================================================
    // GET /api/companies
    // Tüm şirketleri getir
    // =================================================
    [HttpGet]
    public async Task<IActionResult> GetCompanies(
        [FromHeader(Name = "X-Admin-Api-Key")]
        string? adminApiKey)
    {
        if (!_adminApiKeyValidator.IsValid(adminApiKey))
        {
            return Unauthorized(
                new
                {
                    message = "Yetkisiz istek."
                });
        }


        var companies =
            await _dbContext.Companies
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    created_at = c.CreatedAt,

                    license_count =
                        c.Licenses.Count()
                })
                .ToListAsync();


        return Ok(companies);
    }


    // =================================================
    // GET /api/companies/{id}
    // Tek şirketin detaylarını getir
    // =================================================
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCompany(
        Guid id,

        [FromHeader(Name = "X-Admin-Api-Key")]
        string? adminApiKey)
    {
        if (!_adminApiKeyValidator.IsValid(adminApiKey))
        {
            return Unauthorized(
                new
                {
                    message = "Yetkisiz istek."
                });
        }


        var company =
            await _dbContext.Companies
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    created_at = c.CreatedAt,

                    licenses =
                        c.Licenses
                            .OrderByDescending(l => l.CreatedAt)
                            .Select(l => new
                            {
                                id = l.Id,

                                project_id =
                                    l.ProjectId,

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
                            .ToList()
                })
                .SingleOrDefaultAsync();


        if (company is null)
        {
            return NotFound(
                new
                {
                    message = "Şirket bulunamadı."
                });
        }


        return Ok(company);
    }


    // =================================================
    // POST /api/companies
    // Yeni şirket oluştur
    // =================================================
    [HttpPost]
    public async Task<IActionResult> CreateCompany(
        [FromBody] CreateCompanyRequest request,

        [FromHeader(Name = "X-Admin-Api-Key")]
        string? adminApiKey)
    {
        if (!_adminApiKeyValidator.IsValid(adminApiKey))
        {
            return Unauthorized(
                new
                {
                    message = "Yetkisiz istek."
                });
        }


        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(
                new
                {
                    message = "Şirket adı gereklidir."
                });
        }


        string normalizedName =
            request.Name.Trim();


        bool exists =
            await _dbContext.Companies
                .AnyAsync(
                    c => c.Name == normalizedName);


        if (exists)
        {
            return Conflict(
                new
                {
                    message =
                        "Bu isimde bir şirket zaten mevcut."
                });
        }


        var company =
            new Company
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    normalizedName,

                CreatedAt =
                    DateTime.UtcNow
            };


        _dbContext.Companies.Add(company);

        await _dbContext.SaveChangesAsync();


        return CreatedAtAction(
            nameof(GetCompany),
            new
            {
                id = company.Id
            },
            new
            {
                id = company.Id,
                name = company.Name,
                created_at = company.CreatedAt
            });
    }
}


// =================================================
// REQUEST DTO
// =================================================
public class CreateCompanyRequest
{
    public string Name { get; set; } =
        string.Empty;
}