using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>
/// Adds the Accept-Language header as a visible dropdown on every endpoint in Swagger UI.
/// Supports "en" (English) and "ar" (Arabic) for localized response messages.
/// Compatible with Microsoft.OpenApi 2.x (shipped with Swashbuckle.AspNetCore 10.x).
/// </summary>
public class AcceptLanguageHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters == null)
            operation.Parameters = new List<IOpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Accept-Language",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Response language: 'en' for English, 'ar' for Arabic",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<JsonNode>
                {
                    JsonValue.Create("en")!,
                    JsonValue.Create("ar")!
                },
                Default = JsonValue.Create("en")!
            }
        });
    }
}