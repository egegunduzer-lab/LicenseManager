using LicenseManager.Api.Data;
using LicenseManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LicenseManager.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly AdminApiKeyValidator _adminApiKeyValidator;


    public DashboardController(
        ApplicationDbContext dbContext,
        AdminApiKeyValidator adminApiKeyValidator)
    {
        _dbContext =
            dbContext;

        _adminApiKeyValidator =
            adminApiKeyValidator;
    }


    // =================================================
    // DASHBOARD ÖZETİ
    //
    // GET /api/dashboard
    // ADMIN ONLY
    // =================================================
    [HttpGet]
    public async Task<IActionResult> GetDashboard(
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


        DateTime now =
            DateTime.UtcNow;


        DateTime signedExpiryWarningDate =
            now.AddDays(7);


        DateTime realExpiryWarningDate =
            now.AddDays(30);


        // -------------------------------------------------
        // 2. Genel sayılar
        // -------------------------------------------------
        int totalCompanies =
            await _dbContext.Companies
                .AsNoTracking()
                .CountAsync();


        int totalProjects =
            await _dbContext.Projects
                .AsNoTracking()
                .CountAsync();


        int totalLicenses =
            await _dbContext.Licenses
                .AsNoTracking()
                .CountAsync();


        // -------------------------------------------------
        // 3. Blocked lisanslar
        // -------------------------------------------------
        int blockedLicenses =
            await _dbContext.Licenses
                .AsNoTracking()
                .CountAsync(
                    l => l.IsBlocked);


        // -------------------------------------------------
        // 4. Gerçek lisans hakkı devam eden lisanslar
        //
        // Buradaki "active", müşterinin gerçek lisans
        // entitlement'ının devam ettiği anlamına gelir.
        // -------------------------------------------------
        int activeLicenses =
            await _dbContext.Licenses
                .AsNoTracking()
                .CountAsync(
                    l =>
                        !l.IsBlocked &&
                        l.ValidUntil > now);


        // -------------------------------------------------
        // 5. Gerçek lisans süresi bitmiş olanlar
        // -------------------------------------------------
        int expiredLicenses =
            await _dbContext.Licenses
                .AsNoTracking()
                .CountAsync(
                    l =>
                        l.ValidUntil <= now);


        // -------------------------------------------------
        // 6. Signed license dosyası
        // 7 gün içinde bitecek olanlar
        // -------------------------------------------------
        int signedLicensesExpiringSoon =
            await _dbContext.Licenses
                .AsNoTracking()
                .CountAsync(
                    l =>
                        !l.IsBlocked &&
                        l.ValidUntil > now &&
                        l.ExpiryDate > now &&
                        l.ExpiryDate <=
                            signedExpiryWarningDate);


        // -------------------------------------------------
        // 7. Gerçek lisans hakkı
        // 30 gün içinde bitecek olanlar
        // -------------------------------------------------
        int realLicensesExpiringSoon =
            await _dbContext.Licenses
                .AsNoTracking()
                .CountAsync(
                    l =>
                        !l.IsBlocked &&
                        l.ValidUntil > now &&
                        l.ValidUntil <=
                            realExpiryWarningDate);


        // -------------------------------------------------
        // 8. Son oluşturulan 5 lisans
        // -------------------------------------------------
        var recentLicenses =
            await _dbContext.Licenses
                .AsNoTracking()
                .OrderByDescending(
                    l => l.CreatedAt)
                .Take(5)
                .Select(
                    l => new
                    {
                        id =
                            l.Id,

                        company_name =
                            l.Company.Name,

                        project_name =
                            l.Project.Name,

                        project_version =
                            l.Project.Version,

                        license_key =
                            l.LicenseKey,

                        expiry_date =
                            l.ExpiryDate,

                        valid_until =
                            l.ValidUntil,

                        is_blocked =
                            l.IsBlocked,

                        created_at =
                            l.CreatedAt
                    })
                .ToListAsync();


        // -------------------------------------------------
        // 9. Gerçek lisans süresi en yakın bitecek
        // ilk 5 lisans
        // -------------------------------------------------
        var expiringLicenseData =
    await _dbContext.Licenses
        .AsNoTracking()
        .Where(
            l =>
                !l.IsBlocked &&
                l.ValidUntil > now)
        .OrderBy(
            l => l.ValidUntil)
        .Take(5)
        .Select(
            l => new
            {
                id =
                    l.Id,

                company_name =
                    l.Company.Name,

                project_name =
                    l.Project.Name,

                project_version =
                    l.Project.Version,

                valid_until =
                    l.ValidUntil
            })
        .ToListAsync();


        var expiringLicenses =
            expiringLicenseData
                .Select(
                    l => new
                    {
                        l.id,

                        l.company_name,

                        l.project_name,

                        l.project_version,

                        l.valid_until,

                        days_remaining =
                            Math.Max(
                                0,
                                (int)Math.Ceiling(
                                    (l.valid_until - now)
                                        .TotalDays))
                    })
                .ToList();


        // -------------------------------------------------
        // 10. Response
        // -------------------------------------------------
        return Ok(
            new
            {
                generated_at =
                    now,

                summary =
                    new
                    {
                        total_companies =
                            totalCompanies,

                        total_projects =
                            totalProjects,

                        total_licenses =
                            totalLicenses,

                        active_licenses =
                            activeLicenses,

                        blocked_licenses =
                            blockedLicenses,

                        expired_licenses =
                            expiredLicenses,

                        signed_expiring_within_7_days =
                            signedLicensesExpiringSoon,

                        real_expiring_within_30_days =
                            realLicensesExpiringSoon
                    },

                recent_licenses =
                    recentLicenses,

                expiring_licenses =
                    expiringLicenses
            });
    }
}