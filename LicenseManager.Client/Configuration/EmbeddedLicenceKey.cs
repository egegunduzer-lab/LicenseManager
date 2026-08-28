using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;

namespace LicenseManager.Client.Configuration;

public static class EmbeddedLicenseKey
{
    public const string ProjectName =
        "Giltas ERP";

    public const string ProjectVersion =
        "1.0";

    public static IReadOnlyDictionary<int, string>
        PublicKeys
    { get; } =
            LoadPublicKeys();


    private static IReadOnlyDictionary<int, string>
        LoadPublicKeys()
    {
        var keys =
            new Dictionary<int, string>();

        var metadata =
            typeof(EmbeddedLicenseKey)
                .Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>();

        foreach (var item in metadata)
        {
            if (item.Key is null ||
                item.Value is null)
            {
                continue;
            }

            const string prefix =
                "LicensePublicKey_";

            if (!item.Key.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            {
                continue;
            }

            string versionText =
                item.Key.Substring(
                    prefix.Length);

            if (!int.TryParse(
                    versionText,
                    out int keyVersion))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(
                    item.Value))
            {
                continue;
            }

            keys[keyVersion] =
                item.Value.Trim();
        }

        return keys;
    }
}

