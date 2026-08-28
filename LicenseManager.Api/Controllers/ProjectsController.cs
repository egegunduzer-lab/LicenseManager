using LicenseManager.Api.Data;
using LicenseManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LicenseManager.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly ProjectService _projectService;
    private readonly AdminApiKeyValidator _adminApiKeyValidator;
    private readonly ApplicationDbContext? _dbContext;


    // =================================================
    // ESKİ CONSTRUCTOR
    //
    // Mevcut testleri bozmamak için tutuluyor.
    // POST / rotation işlemleri DB context'i doğrudan
    // kullanmadığı için çalışmaya devam eder.
    // =================================================
    public ProjectsController(
        ProjectService projectService,
        AdminApiKeyValidator adminApiKeyValidator)
    {
        _projectService =
            projectService;

        _adminApiKeyValidator =
            adminApiKeyValidator;

        _dbContext =
            null;
    }


    // =================================================
    // API'DE KULLANILACAK CONSTRUCTOR
    // =================================================
    [ActivatorUtilitiesConstructor]
    public ProjectsController(
        ProjectService projectService,
        AdminApiKeyValidator adminApiKeyValidator,
        ApplicationDbContext dbContext)
    {
        _projectService =
            projectService;

        _adminApiKeyValidator =
            adminApiKeyValidator;

        _dbContext =
            dbContext;
    }


    // =================================================
    // TÜM PROJELERİ LİSTELE
    //
    // GET /api/projects
    // ADMIN ONLY
    // =================================================
    [HttpGet]
    public async Task<IActionResult> GetProjects(
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
        // 2. Projeleri getir
        // -------------------------------------------------
        var projects =
            await _dbContext.Projects
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ThenBy(p => p.Version)
                .Select(p => new
                {
                    id =
                        p.Id,

                    name =
                        p.Name,

                    version =
                        p.Version,

                    key_version =
                        p.KeyVersion,

                    created_at =
                        p.CreatedAt,

                    license_count =
                        p.Licenses.Count(),

                    key_count =
                        p.Keys.Count()
                })
                .ToListAsync();


        return Ok(
            projects);
    }


    // =================================================
    // TEK PROJE DETAYI
    //
    // GET /api/projects/{id}
    // ADMIN ONLY
    // =================================================
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProject(
        Guid id,

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
        // 2. Projeyi getir
        // -------------------------------------------------
        var project =
            await _dbContext.Projects
                .AsNoTracking()
                .Include(p => p.Keys)
                .Include(p => p.Licenses)
                .SingleOrDefaultAsync(
                    p => p.Id == id);


        if (project is null)
        {
            return NotFound(
                new
                {
                    message =
                        "Proje bulunamadı."
                });
        }


        // -------------------------------------------------
        // 3. Private key KESİNLİKLE dönülmez
        // -------------------------------------------------
        return Ok(
            new
            {
                id =
                    project.Id,

                name =
                    project.Name,

                version =
                    project.Version,

                key_version =
                    project.KeyVersion,

                created_at =
                    project.CreatedAt,

                license_count =
                    project.Licenses.Count,

                key_count =
                    project.Keys.Count,

                active_key =
                    project.Keys
                        .Where(k => k.IsActive)
                        .Select(k => new
                        {
                            key_version =
                                k.KeyVersion,

                            public_key =
                                Convert.ToBase64String(
                                    k.PublicKey),

                            created_at =
                                k.CreatedAt
                        })
                        .SingleOrDefault()
            });
    }


    // =================================================
    // PROJE KEY'LERİNİ LİSTELE
    //
    // GET /api/projects/{projectId}/keys
    // ADMIN ONLY
    // =================================================
    [HttpGet("{projectId:guid}/keys")]
    public async Task<IActionResult> GetProjectKeys(
        Guid projectId,

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
        // 2. Proje var mı?
        // -------------------------------------------------
        bool projectExists =
            await _dbContext.Projects
                .AsNoTracking()
                .AnyAsync(
                    p => p.Id == projectId);


        if (!projectExists)
        {
            return NotFound(
                new
                {
                    message =
                        "Proje bulunamadı."
                });
        }


        // -------------------------------------------------
        // 3. Key'leri getir
        //
        // PRIVATE KEY RESPONSE'A ÇIKMAZ.
        // -------------------------------------------------
        var keys =
            await _dbContext.ProjectKeys
                .AsNoTracking()
                .Where(
                    k => k.ProjectId == projectId)
                .OrderBy(
                    k => k.KeyVersion)
                .ToListAsync();


        var response =
            keys.Select(
                k => new
                {
                    id =
                        k.Id,

                    key_version =
                        k.KeyVersion,

                    public_key =
                        Convert.ToBase64String(
                            k.PublicKey),

                    is_active =
                        k.IsActive,

                    created_at =
                        k.CreatedAt
                })
                .ToList();


        return Ok(
            response);
    }


    // =================================================
    // PROJE OLUŞTUR
    //
    // POST /api/projects
    // ADMIN ONLY
    // =================================================
    [HttpPost]
    public async Task<IActionResult> CreateProject(
        CreateProjectRequest request,

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
        // 2. Request doğrulama
        // -------------------------------------------------
        if (string.IsNullOrWhiteSpace(
                request.Name) ||
            string.IsNullOrWhiteSpace(
                request.Version))
        {
            return BadRequest(
                new
                {
                    message =
                        "Name ve Version zorunludur."
                });
        }


        // -------------------------------------------------
        // 3. Projeyi oluştur
        // -------------------------------------------------
        var project =
            await _projectService
                .CreateProjectAsync(
                    request.Name,
                    request.Version);


        if (project is null)
        {
            return Conflict(
                new
                {
                    message =
                        "Bu isim ve sürüme sahip proje zaten mevcut."
                });
        }


        // -------------------------------------------------
        // 4. Private key kesinlikle response'a verilmez
        // -------------------------------------------------
        return Ok(
            new
            {
                project.Id,

                project.Name,

                project.Version,

                KeyVersion =
                    project.KeyVersion,

                PublicKey =
                    Convert.ToBase64String(
                        project.PublicKey),

                project.CreatedAt
            });
    }


    // =================================================
    // KEY ROTATION HAZIRLA
    //
    // POST /api/projects/{projectId}/keys/prepare
    // ADMIN ONLY
    // =================================================
    [HttpPost("{projectId:guid}/keys/prepare")]
    public async Task<IActionResult> PrepareKeyRotation(
        Guid projectId,

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
        // 2. Yeni key hazırla
        // -------------------------------------------------
        var result =
            await _projectService
                .PrepareKeyRotationAsync(
                    projectId);


        if (result is null)
        {
            return NotFound(
                new
                {
                    message =
                        "Proje bulunamadı."
                });
        }


        return Ok(
            result);
    }


    // =================================================
    // KEY ROTATION AKTİF ET
    //
    // POST
    // /api/projects/{projectId}/keys/{keyVersion}/activate
    //
    // ADMIN ONLY
    // =================================================
    [HttpPost(
        "{projectId:guid}/keys/{keyVersion:int}/activate")]
    public async Task<IActionResult> ActivateKeyRotation(
        Guid projectId,
        int keyVersion,

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
        // 2. Temel KeyVersion kontrolü
        // -------------------------------------------------
        if (keyVersion <= 0)
        {
            return BadRequest(
                new
                {
                    message =
                        "Geçersiz key version."
                });
        }


        // -------------------------------------------------
        // 3. Key'i aktif et
        // -------------------------------------------------
        bool activated =
            await _projectService
                .ActivateKeyRotationAsync(
                    projectId,
                    keyVersion);


        if (!activated)
        {
            return NotFound(
                new
                {
                    message =
                        "Proje veya key version bulunamadı."
                });
        }


        return Ok(
            new
            {
                project_id =
                    projectId,

                key_version =
                    keyVersion,

                activated =
                    true
            });
    }
}


// =====================================================
// CREATE PROJECT REQUEST
// =====================================================
public class CreateProjectRequest
{
    public string Name { get; set; } =
        string.Empty;

    public string Version { get; set; } =
        string.Empty;
}
