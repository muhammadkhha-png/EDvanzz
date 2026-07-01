using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>
/// One endpoint's Swagger example set: an optional request-body example and a
/// response-code-keyed map of response examples (keys are HTTP status strings, e.g. "200").
/// </summary>
public sealed class EndpointExampleSet
{
    /// <summary>Request-body example, or null for endpoints with no body (e.g. query-only).</summary>
    public JsonNode? RequestBody { get; init; }

    /// <summary>Response examples keyed by HTTP status code string ("200", "400", ...).</summary>
    public IReadOnlyDictionary<string, JsonNode>? Responses { get; init; }
}

/// <summary>
/// Supplies Swagger request/response examples for a cohesive set of endpoints
/// (typically one module/controller). Implementations are registered in DI and
/// composed by <see cref="SwaggerExamplesFilter"/>.
///
/// SOLID rationale:
///   - SRP: each provider owns exactly one module's examples.
///   - OCP: a new module is documented by registering a new provider — the filter
///     and existing providers are never edited.
///   - DIP: the filter depends on this abstraction, not concrete providers.
/// </summary>
public interface IEndpointExampleProvider
{
    /// <summary>
    /// Returns the example set for the given normalized (method, route) pair, or
    /// null when this provider does not own that endpoint.
    /// </summary>
    /// <param name="httpMethod">Upper-cased HTTP method, e.g. "POST".</param>
    /// <param name="normalizedRoute">
    /// Lower-cased route template, no leading/trailing slash, e.g. "api/auth/login".
    /// </param>
    EndpointExampleSet? GetExamples(string httpMethod, string normalizedRoute);
}

/// <summary>
/// Base class for example providers. Centralizes the success/failure envelope
/// builders so every provider emits examples in the exact shape produced by
/// <see cref="Controllers.ApiBaseController"/>.ToResponse — i.e.
/// <c>{ "success": ..., "message": ..., "data": ... }</c>.
/// </summary>
public abstract class EndpointExampleProvider : IEndpointExampleProvider
{
    /// <inheritdoc />
    public abstract EndpointExampleSet? GetExamples(string httpMethod, string normalizedRoute);

    /// <summary>Builds a success envelope matching ApiBaseController.ToResponse.</summary>
    protected static JsonObject SuccessEnvelope(string message, JsonNode? data) => new()
    {
        ["success"] = true,
        ["message"] = message,
        ["data"] = data
    };

    /// <summary>Builds a failure envelope matching ApiBaseController.ToResponse.</summary>
    protected static JsonObject FailureEnvelope(string message) => new()
    {
        ["success"] = false,
        ["message"] = message
    };
}