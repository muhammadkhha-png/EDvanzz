using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>
/// Global operation filter that makes response documentation fully automatic for every
/// endpoint, current and future, in two steps:
///
/// 1. <b>Ensures the standard status codes exist</b> on every operation. The
///    <c>[ProducesResponseType]</c> attributes are documentation only — at runtime every
///    endpoint can return 400 (Result-pattern failures default to BadRequest; model-binding
///    validation) and 500 (exception middleware); every effectively-authorized endpoint
///    (anything not <c>[AllowAnonymous]</c> — the global FallbackPolicy authenticates the
///    rest) can return 401/403 (JWT + SecurityStamp middleware, permission policies); and
///    routes with a <c>{param}</c> segment can return 404. Previously an action with no
///    attributes documented only a lone 200, so the spec/Postman import lied by omission.
///    Codes already declared by attributes are never touched — hand-curated responses
///    (e.g. the payment 422) keep full precedence.
///
/// 2. <b>Gives every JSON response a realistic example</b> in the project's standard
///    <c>{ success, message, data }</c> envelope produced by
///    <see cref="Controllers.ApiBaseController"/>.ToResponse — success for 2xx, failure
///    for 4xx/5xx. 3xx responses (e.g. the gated-file 302 redirect) and non-JSON media
///    types (PDF/Excel exports) are left alone — they carry no JSON envelope at runtime.
///    Declared error responses with no body type get an <c>application/json</c> content
///    added so they receive an example too.
///
/// The <c>data</c> payload is populated from the response body's own schema example
/// (schema <c>$ref</c>s are resolved) when the action declares a typed response;
/// endpoints declared as <c>ProducesResponseType(typeof(object), …)</c> carry no type,
/// so their <c>data</c> is shown as <c>null</c> (type the response to enrich it).
/// Never overwrites an example already present.
/// </summary>
public sealed class ResponseEnvelopeExampleFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Responses ??= new OpenApiResponses();

        EnsureStandardResponses(operation, context);

        foreach (var (status, response) in operation.Responses)
        {
            if (response?.Content is null || response.Content.Count == 0) continue;

            // 3xx responses (302 gated-file redirect) have no JSON body — no envelope.
            if (status.StartsWith('3')) continue;
            // 204/205 responses have no body by definition — no envelope either.
            if (status is "204" or "205") continue;
            bool success = status.StartsWith('2');

            foreach (var (mediaType, media) in response.Content)
            {
                if (!IsJsonLike(mediaType)) continue;    // PDF/Excel/binary — no JSON example
                if (media.Example is not null) continue; // respect explicit examples

                media.Example = success
                    ? new JsonObject
                    {
                        ["success"] = true,
                        ["message"] = "Operation completed successfully.",
                        ["data"] = ExtractInner(media, context.SchemaRepository),
                    }
                    : new JsonObject
                    {
                        ["success"] = false,
                        ["message"] = MessageFor(status),
                    };
            }
        }
    }

    /// <summary>
    /// Adds the status codes every endpoint can actually produce at runtime but that the
    /// action's attributes (if any) did not declare. Existing entries are never replaced.
    /// Also gives declared 4xx/5xx responses with no content an application/json body so
    /// the example loop below can fill them.
    /// </summary>
    private static void EnsureStandardResponses(OpenApiOperation operation, OperationFilterContext context)
    {
        // [AllowAnonymous] (method or controller) is the only escape from the global
        // FallbackPolicy — everything else is authenticated and can 401/403.
        bool anonymous =
            context.MethodInfo?.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null
            || context.MethodInfo?.DeclaringType?.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;

        bool hasRouteParam = context.ApiDescription?.RelativePath?.Contains('{') == true;

        AddIfMissing(operation, "400", "Bad Request");
        if (!anonymous)
        {
            AddIfMissing(operation, "401", "Unauthorized");
            AddIfMissing(operation, "403", "Forbidden");
        }
        if (hasRouteParam)
            AddIfMissing(operation, "404", "Not Found");
        AddIfMissing(operation, "500", "Internal Server Error");

        // Declared error codes with no body type (e.g. [ProducesResponseType(401)] without
        // typeof) emit no content — give them a JSON body so they get an envelope example.
        foreach (var (status, response) in operation.Responses!)
        {
            if (response is not OpenApiResponse concrete) continue;
            if (!status.StartsWith('4') && !status.StartsWith('5')) continue;
            if (concrete.Content is { Count: > 0 }) continue;
            concrete.Content = JsonContent();
        }
    }

    private static void AddIfMissing(OpenApiOperation operation, string status, string description)
    {
        if (operation.Responses!.ContainsKey(status)) return;
        operation.Responses[status] = new OpenApiResponse
        {
            Description = description,
            Content = JsonContent(),
        };
    }

    private static Dictionary<string, OpenApiMediaType> JsonContent() => new()
    {
        ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
    };

    /// <summary>JSON-envelope media types; excludes PDF/Excel/binary downloads.</summary>
    private static bool IsJsonLike(string mediaType)
        => mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
           || mediaType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Pulls the response DTO's own example (from the schema) if available. Typed
    /// responses are emitted as $ref schemas whose <c>Target</c> is not yet resolvable
    /// during generation, so refs are looked up in the <see cref="SchemaRepository"/>.
    /// Actions declared as <c>Result&lt;T&gt;</c> carry the envelope INSIDE the schema
    /// example (isSuccess/message/data) — the inner <c>data</c> is lifted so the final
    /// example is not double-wrapped.
    /// </summary>
    private static JsonNode? ExtractInner(OpenApiMediaType media, SchemaRepository repository)
    {
        IOpenApiSchema? schema = media.Schema;
        if (schema is OpenApiSchemaReference reference
            && reference.Reference?.Id is string id
            && repository.Schemas.TryGetValue(id, out IOpenApiSchema? resolved))
        {
            schema = resolved;
        }

        if (schema is not OpenApiSchema concrete || concrete.Example is null) return null;

        JsonNode example = concrete.Example.DeepClone();
        if (example is JsonObject envelope
            && envelope.ContainsKey("isSuccess") && envelope.ContainsKey("data"))
        {
            return envelope["data"]?.DeepClone();
        }
        return example;
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
