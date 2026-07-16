using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Edvanz.API.Filters;

/// <summary>
/// Global schema filter that auto-generates a realistic <c>example</c> for EVERY DTO
/// schema from its CLR type — so every request/response body in Swagger UI and in the
/// exported OpenAPI/Postman collection is pre-filled, for both existing and future
/// endpoints, with no per-endpoint work.
///
/// Design:
///   - Runs once per schema during generation; new DTOs are covered automatically (OCP).
///   - Never overwrites an example already set (so the curated
///     <see cref="SwaggerExamplesFilter"/> providers, which set operation-level examples,
///     keep precedence — operation examples win over schema examples in Swagger UI/Postman).
///   - Values are inferred from property TYPE, refined by property NAME heuristics
///     (email/phone/id/amount/date…). Recursion is depth- and cycle-guarded.
///
/// This is intentionally best-effort documentation sugar: examples are illustrative,
/// not validated payloads.
/// </summary>
public sealed class AutoExampleSchemaFilter : ISchemaFilter
{
    private const int MaxDepth = 5;

    /// <inheritdoc />
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        // Only the mutable implementation can carry an example; refs/read-only skip.
        if (schema is not OpenApiSchema concrete || context?.Type is null) return;
        if (concrete.Example is not null) return; // respect explicit/curated examples

        JsonNode? example = BuildForType(context.Type, new HashSet<Type>(), 0);
        if (example is not null) concrete.Example = example;
    }

    private static JsonNode? BuildForType(Type type, HashSet<Type> seen, int depth)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        // Enums serialize as strings (global JsonStringEnumConverter) → first member name.
        if (type.IsEnum)
        {
            string[] names = Enum.GetNames(type);
            return names.Length > 0 ? JsonValue.Create(names[0]) : JsonValue.Create("value");
        }

        if (TryScalar(type, propertyName: null, out JsonNode? scalar)) return scalar;

        // Binary uploads have no JSON example.
        if (IsFormFile(type)) return null;

        // Collections → single representative element.
        if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type))
        {
            Type element = GetEnumerableElement(type);
            if (element == typeof(object)) return new JsonArray();
            JsonNode? item = BuildForType(element, seen, depth + 1);
            return item is null ? new JsonArray() : new JsonArray(item);
        }

        // Complex object → build from public properties.
        if (depth >= MaxDepth) return null;
        if (!seen.Add(type)) return null; // cycle guard

        try
        {
            var obj = new JsonObject();
            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;
                if (IsFormFile(prop.PropertyType)) continue;

                string name = JsonName(prop);
                Type pType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                if (pType.IsEnum)
                {
                    string[] names = Enum.GetNames(pType);
                    obj[name] = JsonValue.Create(names.Length > 0 ? names[0] : "value");
                }
                else if (TryScalar(pType, prop.Name, out JsonNode? s))
                {
                    obj[name] = s;
                }
                else
                {
                    JsonNode? nested = BuildForType(pType, seen, depth + 1);
                    if (nested is not null) obj[name] = nested;
                }
            }
            return obj.Count > 0 ? obj : null;
        }
        finally
        {
            seen.Remove(type);
        }
    }

    /// <summary>Produces an example for scalar/primitive types, refined by property name.</summary>
    private static bool TryScalar(Type type, string? propertyName, out JsonNode? value)
    {
        string p = (propertyName ?? string.Empty).ToLowerInvariant();

        if (type == typeof(string)) { value = JsonValue.Create(StringExample(p)); return true; }
        if (type == typeof(bool)) { value = JsonValue.Create(true); return true; }
        if (type == typeof(Guid)) { value = JsonValue.Create("3fa85f64-5717-4562-b3fc-2c963f66afa6"); return true; }
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        { value = JsonValue.Create("2026-01-15T09:00:00Z"); return true; }
        if (type == typeof(DateOnly)) { value = JsonValue.Create("2026-01-15"); return true; }
        if (type == typeof(TimeOnly) || type == typeof(TimeSpan)) { value = JsonValue.Create("09:00:00"); return true; }

        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong))
        { value = JsonValue.Create(IntExample(p)); return true; }

        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
        { value = JsonValue.Create(DecimalExample(p)); return true; }

        value = null;
        return false;
    }

    private static string StringExample(string p)
    {
        if (p.Contains("email")) return "user@example.com";
        if (p.Contains("password")) return "P@ssw0rd1";
        if (p.Contains("phone") || p.Contains("mobile") || p.Contains("whatsapp")) return "01012345678";
        if (p.Contains("username") || p == "user") return "omran1";
        if (p.Contains("filename")) return "document.pdf";
        if (p.Contains("mimetype") || p.Contains("contenttype")) return "image/png";
        if (p.Contains("url") || p.Contains("link")) return "https://app-edvanz-api-prod.azurewebsites.net/uploads/sample.png";
        if (p.Contains("otp")) return "1234";
        if (p.Contains("token")) return "eyJhbGciOiJIUzI1NiJ9.<token>";
        if (p.Contains("code")) return "ABC123";
        if (p.Contains("culture") || p.Contains("language") || p == "lang") return "en";
        if (p.Contains("name")) return "Sample Name";
        if (p.Contains("message") || p.Contains("note") || p.Contains("description") || p.Contains("comment"))
            return "Sample text";
        return "string";
    }

    private static long IntExample(string p)
    {
        if (p.Contains("year")) return 2026;
        if (p.Contains("month")) return 1;
        if (p.Contains("day")) return 15;
        if (p == "pagesize" || p.Contains("pagesize")) return 20;
        if (p.Contains("page")) return 1;
        if (p.Contains("id")) return 1;
        return 1;
    }

    private static double DecimalExample(string p)
    {
        if (p.Contains("percent")) return 50.0;
        if (p.Contains("amount") || p.Contains("price") || p.Contains("balance")
            || p.Contains("fee") || p.Contains("cost") || p.Contains("total"))
            return 100.0;
        return 1.0;
    }

    private static string JsonName(PropertyInfo prop)
    {
        var attr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (attr is not null && !string.IsNullOrWhiteSpace(attr.Name)) return attr.Name;
        string n = prop.Name;
        return string.IsNullOrEmpty(n) ? n : char.ToLowerInvariant(n[0]) + n[1..]; // camelCase default
    }

    private static bool IsFormFile(Type type)
        => typeof(IFormFile).IsAssignableFrom(type)
           || (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
               && typeof(IFormFile).IsAssignableFrom(type.GetGenericArguments()[0]));

    private static Type GetEnumerableElement(Type type)
    {
        if (type.IsArray) return type.GetElementType() ?? typeof(object);
        if (type.IsGenericType) return type.GetGenericArguments()[0];
        return typeof(object);
    }
}
