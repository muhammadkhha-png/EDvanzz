using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>
/// Global operation filter that gives EVERY response a realistic example in the
/// project's standard <c>{ success, message, data }</c> envelope produced by
/// <see cref="Controllers.ApiBaseController"/>.ToResponse — success for 2xx, failure
/// for 4xx/5xx. Combined with <see cref="AutoExampleSchemaFilter"/> (which fills
/// request-body examples), this covers every endpoint's request AND response
/// scenarios automatically, with no per-endpoint work.
///
/// The <c>data</c> payload is populated from the response body's own schema example
/// when the action declares a typed response; endpoints declared as
/// <c>ProducesResponseType(typeof(object), …)</c> carry no type, so their
/// <c>data</c> is shown as <c>null</c> (type the response to enrich it).
/// Never overwrites an example already present.
/// </summary>
public sealed class ResponseEnvelopeExampleFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Responses is null) return;

        foreach (var (status, response) in operation.Responses)
        {
            if (response?.Content is null || response.Content.Count == 0) continue;
            bool success = status.StartsWith('2');

            foreach (var media in response.Content.Values)
            {
                if (media.Example is not null) continue; // respect explicit examples

                media.Example = success
                    ? new JsonObject
                    {
                        ["success"] = true,
                        ["message"] = "Operation completed successfully.",
                        ["data"] = ExtractInner(media),
                    }
                    : new JsonObject
                    {
                        ["success"] = false,
                        ["message"] = MessageFor(status),
                    };
            }
        }
    }

    /// <summary>Pulls the response DTO's own example (from the schema) if available.</summary>
    private static JsonNode? ExtractInner(OpenApiMediaType media)
    {
        if (media.Schema is OpenApiSchema concrete && concrete.Example is not null)
            return concrete.Example.DeepClone();
        return null;
    }

    private static string MessageFor(string status) => status switch
    {
        "400" => "Invalid request.",
        "401" => "Unauthorized — a valid access token is required.",
        "403" => "Forbidden — you do not have permission for this action.",
        "404" => "The requested resource was not found.",
        "409" => "Conflict — the request could not be completed in the current state.",
        "422" => "Validation failed. See message for details.",
        "429" => "Too many requests — please slow down and try again shortly.",
        "500" => "An unexpected server error occurred. Please try again later.",
        "503" => "The service is temporarily unavailable. Please try again shortly.",
        _ => "The request could not be completed.",
    };
}
