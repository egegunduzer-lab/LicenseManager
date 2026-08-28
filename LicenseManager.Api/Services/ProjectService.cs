using LicenseManager.Api.Data;
using LicenseManager.Core.DTOs;
using LicenseManager.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace LicenseManager.Api.Services;

public class ProjectService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Ed25519KeyService _keyService;
    private readonly IPrivateKeyProtector _privateKeyProtector;

    public ProjectService(
        ApplicationDbContext dbContext,
        Ed25519KeyService keyService,
        IPrivateKeyProtector privateKeyProtector)
    {
        _dbContext = dbContext;
        _keyService = keyService;
        _privateKeyProtector = privateKeyProtector;
    }

    public async Task<Project?> CreateProjectAsync(
        string name,
        string version)
    {
        bool exists = await _dbContext.Projects.AnyAsync(p =>
    p.Name == name &&
    p.Version == version);
        if (exists)
        {
            return null;
        }


        var keyPair = _keyService.GenerateKeyPair();

        byte[] protectedPrivateKey =
            ProtectAndClearPrivateKey(
                keyPair.PrivateKey);

        var now = DateTime.UtcNow;

        var project = new Project
        {
            Id = Guid.NewGuid(),

            Name = name,
            Version = version,

            KeyVersion = 1,

            PublicKey = keyPair.PublicKey,
            PrivateKey = protectedPrivateKey,

            CreatedAt = now
        };

        var projectKey = new ProjectKey
        {
            Id = Guid.NewGuid(),

            ProjectId = project.Id,

            KeyVersion = 1,

            PublicKey = keyPair.PublicKey,
            PrivateKey = protectedPrivateKey,

            IsActive = true,

            CreatedAt = now
        };

        _dbContext.Projects.Add(project);
        _dbContext.ProjectKeys.Add(projectKey);
        await _dbContext.SaveChangesAsync();

        return project;
    }

    public async Task<PreparedProjectKeyResponse?>
    PrepareKeyRotationAsync(Guid projectId)
    {
        var project = await _dbContext.Projects
            .Include(p => p.Keys)
            .FirstOrDefaultAsync(
                p => p.Id == projectId);

        if (project is null)
        {
            return null;
        }

        int nextKeyVersion =
            project.Keys.Count == 0
                ? 1
                : project.Keys.Max(k => k.KeyVersion) + 1;


        // Yeni Ed25519 keypair üret
        var keyPair =
    _keyService.GenerateKeyPair();


        // Private key'i plaintext olarak DB'ye koyma
        byte[] protectedPrivateKey =
            ProtectAndClearPrivateKey(
                keyPair.PrivateKey);


        var projectKey =
            new ProjectKey
            {
                Id = Guid.NewGuid(),

                ProjectId = project.Id,

                KeyVersion = nextKeyVersion,

                PublicKey = keyPair.PublicKey,

                PrivateKey = protectedPrivateKey,

                // ÇOK ÖNEMLİ:
                // hazırlanan key hemen aktif olmuyor
                IsActive = false,

                CreatedAt = DateTime.UtcNow
            };


        _dbContext.ProjectKeys.Add(
            projectKey);

        await _dbContext.SaveChangesAsync();


        return new PreparedProjectKeyResponse
        {
            ProjectId =
                project.Id,

            KeyVersion =
                nextKeyVersion,

            PublicKeyBase64 =
                Convert.ToBase64String(
                    keyPair.PublicKey)
        };
    }


    private byte[] ProtectAndClearPrivateKey(
        byte[] privateKey)
    {
        try
        {
            return _privateKeyProtector.Protect(
                privateKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                privateKey);
        }
    }
    public async Task<bool> ActivateKeyRotationAsync(
    Guid projectId,
    int keyVersion)
    {
        var project = await _dbContext.Projects
            .Include(p => p.Keys)
            .FirstOrDefaultAsync(
                p => p.Id == projectId);

        // Proje yoksa
        if (project is null)
        {
            return false;
        }


        // Aktif edilmek istenen key'i bul
        var targetKey =
            project.Keys
                .FirstOrDefault(
                    k => k.KeyVersion == keyVersion);

        // Böyle bir key yoksa
        if (targetKey is null)
        {
            return false;
        }


        // -------------------------------------------------
        // Önce bütün key'leri pasif yap
        // -------------------------------------------------
        foreach (var key in project.Keys)
        {
            key.IsActive = false;
        }


        // -------------------------------------------------
        // Seçilen key'i aktif yap
        // -------------------------------------------------
        targetKey.IsActive = true;


        // -------------------------------------------------
        // Geçiş sürecinde Projects tablosundaki eski
        // alanları da aktif key ile senkron tut
        // -------------------------------------------------
        project.KeyVersion =
            targetKey.KeyVersion;

        project.PublicKey =
            targetKey.PublicKey;

        project.PrivateKey =
            targetKey.PrivateKey;


        await _dbContext.SaveChangesAsync();

        return true;
    }
}
