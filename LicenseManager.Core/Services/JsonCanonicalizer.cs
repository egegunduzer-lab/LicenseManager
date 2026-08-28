using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LicenseManager.Core.Services;
public static class JsonCanonicalizer
{
    public static byte[] Canonicalize(object value)
    
    {
        JsonElement element = JsonSerializer.SerializeToElement(value);

        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Indented = false
            }))
        {
            WriteCanonical(element, writer);
        }

        return stream.ToArray();
    }

    private static void WriteCanonical(
        JsonElement element,
        Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                foreach (var property in element
                    .EnumerateObject()
                    .OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();

                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(item, writer);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                element.WriteTo(writer);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;

            default:
                throw new InvalidOperationException(
                    $"Desteklenmeyen JSON türü: {element.ValueKind}");
        }
    }

    public static string CanonicalizeToString(object value)
    {
        byte[] bytes = Canonicalize(value);
        return Encoding.UTF8.GetString(bytes);
    }
}
