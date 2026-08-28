using System.Text;
using System.Text.Json;
using Xunit;
using LicenseManager.Core.DTOs;
using LicenseManager.Core.Services;
namespace LicenseManager.Tests;

public class JsonCanonicalizerTests
{
    [Fact]
    public void LicensePayload_ShouldUseDocumentedSnakeCasePropertyNames()
    {
        var payload = new LicensePayload
        {
            LicenseId =
                Guid.Parse("11111111-1111-1111-1111-111111111111"),

            CompanyId =
                Guid.Parse("22222222-2222-2222-2222-222222222222"),

            ProjectId =
                Guid.Parse("33333333-3333-3333-3333-333333333333"),

            ProjectName = "Giltas ERP",
            ProjectVersion = "1.0",

            Modules = new List<string>
        {
            "Reports",
            "Inventory"
        },

            IssuedAt =
                new DateTime(
                    2026, 8, 19,
                    10, 0, 0,
                    DateTimeKind.Utc),

            ExpiryDate =
                new DateTime(
                    2026, 9, 18,
                    10, 0, 0,
                    DateTimeKind.Utc)
        };

        string canonical =
            JsonCanonicalizer.CanonicalizeToString(
                payload);

        Assert.Contains(
            "\"license_id\"",
            canonical);

        Assert.Contains(
            "\"company_id\"",
            canonical);

        Assert.Contains(
            "\"project_id\"",
            canonical);

        Assert.Contains(
            "\"project_name\"",
            canonical);

        Assert.Contains(
            "\"project_version\"",
            canonical);

        Assert.Contains(
            "\"issued_at\"",
            canonical);

        Assert.Contains(
            "\"expiry_date\"",
            canonical);

        Assert.DoesNotContain(
            "\"LicenseId\"",
            canonical);

        Assert.DoesNotContain(
            "\"ProjectName\"",
            canonical);
    }

    [Fact]
    public void Canonicalize_ShouldProduceSameOutput_WhenPropertyOrderIsDifferent()
    {
        var json1 = """
        {
            "name": "Giltas",
            "version": "1.0"
        }
        """;

        var json2 = """
        {
            "version": "1.0",
            "name": "Giltas"
        }
        """;

        using var doc1 = JsonDocument.Parse(json1);
        using var doc2 = JsonDocument.Parse(json2);

        byte[] result1 =
            JsonCanonicalizer.Canonicalize(doc1.RootElement);

        byte[] result2 =
            JsonCanonicalizer.Canonicalize(doc2.RootElement);

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Canonicalize_ShouldIgnoreWhitespaceDifferences()
    {
        var json1 = """
        {
            "name": "Giltas",
            "version": "1.0"
        }
        """;

        var json2 = """{"name":"Giltas","version":"1.0"}""";

        using var doc1 = JsonDocument.Parse(json1);
        using var doc2 = JsonDocument.Parse(json2);

        byte[] result1 =
            JsonCanonicalizer.Canonicalize(doc1.RootElement);

        byte[] result2 =
            JsonCanonicalizer.Canonicalize(doc2.RootElement);

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Canonicalize_ShouldProduceDifferentOutput_WhenDataChanges()
    {
        var json1 = """
        {
            "name": "Giltas",
            "version": "1.0"
        }
        """;

        var json2 = """
        {
            "name": "Giltas",
            "version": "2.0"
        }
        """;

        using var doc1 = JsonDocument.Parse(json1);
        using var doc2 = JsonDocument.Parse(json2);

        byte[] result1 =
            JsonCanonicalizer.Canonicalize(doc1.RootElement);

        byte[] result2 =
            JsonCanonicalizer.Canonicalize(doc2.RootElement);

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Canonicalize_ShouldSortNestedObjectProperties()
    {
        var json = """
        {
            "project": {
                "version": "1.0",
                "name": "ERP"
            },
            "company": "ABC"
        }
        """;

        using var doc = JsonDocument.Parse(json);

        byte[] result =
            JsonCanonicalizer.Canonicalize(doc.RootElement);

        string canonical =
            Encoding.UTF8.GetString(result);

        Assert.Equal(
            """{"company":"ABC","project":{"name":"ERP","version":"1.0"}}""",
            canonical);
    }

    [Fact]
    public void DifferentPropertyOrder_ShouldProduceSameHash()
    {
        var first = new
        {
            ProjectName = "Giltas ERP",
            ProjectVersion = "1.0",
            CompanyId = "ABC"
        };

        var second = new
        {
            CompanyId = "ABC",
            ProjectVersion = "1.0",
            ProjectName = "Giltas ERP"
        };

        byte[] firstCanonical =
            JsonCanonicalizer.Canonicalize(first);

        byte[] secondCanonical =
            JsonCanonicalizer.Canonicalize(second);

        byte[] firstHash =
            System.Security.Cryptography.SHA256.HashData(
                firstCanonical);

        byte[] secondHash =
            System.Security.Cryptography.SHA256.HashData(
                secondCanonical);

        Assert.Equal(firstHash, secondHash);
    }
}