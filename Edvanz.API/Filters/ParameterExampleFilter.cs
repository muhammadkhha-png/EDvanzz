using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>
/// Gives every query/path/header parameter a PARAMETER-LEVEL example. The
/// <see cref="AutoExampleSchemaFilter"/> already stamps an example on each parameter's
/// SCHEMA, but Postman's OpenAPI importer (openapi-to-postmanv2) only reads the
/// parameter-level <c>example</c> — schema-level ones are ignored, so imported GET
/// requests showed <c>&lt;string&gt;</c>/<c>&lt;boolean&gt;</c> placeholders in their
/// query strings while POST bodies were fully filled. Promotes, in order: the schema's
/// example, its default, or its first enum value. Never overwrites an explicit
/// parameter example.
/// </summary>
public sealed class ParameterExampleFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters is null) return;

        foreach (var parameter in operation.Parameters)
        {
            if (parameter is not OpenApiParameter concrete || concrete.Example is not null)
                continue;
            if (concrete.Schema is not OpenApiSchema schema) continue;

            JsonNode? example = schema.Example ?? schema.Default;
            if (example is null && schema.Enum is { Count: > 0 })
                example = schema.Enum[0];

            if (example is not null) concrete.Example = example.DeepClone();
        }
    }
}
