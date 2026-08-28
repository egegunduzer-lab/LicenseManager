using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LicenseManager.Client.Services;

namespace LicenseManager.Tests;

public class LicenseFileServiceTests
{
    [Fact]
    public async Task MissingLicenseFile_ShouldReturnNull()
    {
        string folder =
            Path.Combine(
                Path.GetTempPath(),
                "LicenseManagerTests",
                Guid.NewGuid().ToString());

        string filePath =
            Path.Combine(
                folder,
                "license.json");

        try
        {
            var service =
                new LicenseFileService(
                    filePath);

            var result =
                await service.LoadAsync();

            Assert.Null(result);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(
                    folder,
                    recursive: true);
            }
        }
    }


    [Fact]
    public async Task CorruptedJson_ShouldReturnNull()
    {
        string folder =
            Path.Combine(
                Path.GetTempPath(),
                "LicenseManagerTests",
                Guid.NewGuid().ToString());

        string filePath =
            Path.Combine(
                folder,
                "license.json");

        try
        {
            Directory.CreateDirectory(folder);

            await File.WriteAllTextAsync(
                filePath,
                """
                {
                    bu-gecerli-bir-json-degil
                """);

            var service =
                new LicenseFileService(
                    filePath);

            var result =
                await service.LoadAsync();

            Assert.Null(result);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(
                    folder,
                    recursive: true);
            }
        }
    }
}
