namespace Edvanz.API.Filters;

using System.Collections.Concurrent;

/// <summary>
/// Collision-proof Swagger schemaId selector. Swashbuckle's default selector uses the
/// bare CLR type name, so two DTOs sharing a class name in different namespaces (e.g.
/// <c>Dtos.Exams.CreateExamDto</c> vs <c>Dtos.VideoContentManagement.CreateExamDto</c>)
/// abort the ENTIRE spec generation with "schemaId already used" — Swagger UI 500s and
/// <c>generate-openapi.sh</c> dies. With this selector the first type keeps the friendly
/// short name and any later same-named type gets its namespace segments prepended
/// (innermost first) until the id is unique, so a future duplicate DTO name can never
/// break spec generation again.
/// </summary>
public static class SwaggerSchemaIds
{
    private static readonly ConcurrentDictionary<string, Type> Used = new();

    /// <summary>Returns a schemaId that is stable for the type and unique across types.</summary>
    public static string For(Type type)
    {
        string baseName = Friendly(type);
        string candidate = baseName;
        string[] segments = (type.Namespace ?? string.Empty).Split('.');
        int next = segments.Length;

        while (true)
        {
            Type owner = Used.GetOrAdd(candidate, type);
            if (owner == type) return candidate;
            if (next == 0) return type.FullName!.Replace('+', '.'); // nested-type last resort
            candidate = segments[--next] + baseName;                // e.g. ExamsCreateExamDto
        }
    }

    /// <summary>Human-readable name; generic args are flattened (PaginatedResponseStudentDto).</summary>
    private static string Friendly(Type type)
    {
        if (type.IsArray) return Friendly(type.GetElementType()!) + "Array";
        if (!type.IsGenericType) return type.Name;

        string name = type.Name;
        int tick = name.IndexOf('`');
        if (tick > 0) name = name[..tick];
        return name + string.Concat(type.GetGenericArguments().Select(Friendly));
    }
}
