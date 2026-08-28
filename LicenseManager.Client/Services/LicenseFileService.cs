using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using LicenseManager.Core.DTOs;

namespace LicenseManager.Client.Services;

public class LicenseFileService
{
    private readonly string _licenseFilePath;


    // -------------------------------------------------
    // Normal uygulamada kullanılan constructor
    // -------------------------------------------------
    public LicenseFileService()
        : this(
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "LicenseManager",
                "license.json"))
    {
    }


    // -------------------------------------------------
    // Testlerde farklı dosya yolu kullanabilmek için
    // -------------------------------------------------
    public LicenseFileService(string licenseFilePath)
    {
        _licenseFilePath = licenseFilePath;

        string? directory =
            Path.GetDirectoryName(
                _licenseFilePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }


    // -------------------------------------------------
    // Lisansı diske kaydet
    // -------------------------------------------------
    public async Task SaveAsync(
        SignedLicenseResponse license)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json =
            JsonSerializer.Serialize(
                license,
                options);

        await File.WriteAllTextAsync(
            _licenseFilePath,
            json);
    }


    // -------------------------------------------------
    // Lisansı diskten yükle
    // -------------------------------------------------
    public async Task<SignedLicenseResponse?> LoadAsync()
    {
        try
        {
            // Dosya yoksa geçersiz lisans gibi davran
            if (!File.Exists(_licenseFilePath))
            {
                return null;
            }

            string json =
                await File.ReadAllTextAsync(
                    _licenseFilePath);

            var options =
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

            var license =
                JsonSerializer.Deserialize<SignedLicenseResponse>(
                    json,
                    options);


            // JSON okunmuş olsa bile gerekli alanlar
            // yoksa geçersiz kabul et
            if (license is null ||
                license.Body is null ||
                string.IsNullOrWhiteSpace(
                    license.Signature))
            {
                return null;
            }


            return license;
        }
        catch (JsonException)
        {
            // Bozuk / müdahale edilmiş JSON
            return null;
        }
        catch (IOException)
        {
            // Dosya okunamadı
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Dosya erişim sorunu
            return null;
        }
    }


    // -------------------------------------------------
    // Lisans dosyasının yolunu döndür
    // -------------------------------------------------
    public string GetLicenseFilePath()
    {
        return _licenseFilePath;
    }
}