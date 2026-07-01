using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>
/// Injects request-body and response examples into Swagger UI. Holds no examples
/// itself — delegates to the registered <see cref="IEndpointExampleProvider"/>
/// implementations (one per module). Lookup is by (HTTP method + normalized route);
/// the first provider that owns the route wins. Unclaimed endpoints are left untouched.
/// </summary>
public sealed class SwaggerExamplesFilter : IOperationFilter
{
    private readonly IReadOnlyList<IEndpointExampleProvider> _providers;

    public SwaggerExamplesFilter(IEnumerable<IEndpointExampleProvider> providers)
        => _providers = providers.ToList();

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        string method = (context.ApiDescription.HttpMethod ?? string.Empty).ToUpperInvariant();
        string route = NormalizeRoute(context.ApiDescription.RelativePath);

        EndpointExampleSet? examples = null;
        foreach (var provider in _providers)
        {
            examples = provider.GetExamples(method, route);
            if (examples is not null) break;
        }
        if (examples is null) return;

        ApplyRequestExamples(operation, examples);
        ApplyResponseExamples(operation, examples);
    }

    private static void ApplyRequestExamples(OpenApiOperation operation, EndpointExampleSet set)
    {
        if (operation.RequestBody?.Content is null) return;

        foreach (var mediaType in operation.RequestBody.Content.Values)
        {
            if (set.RequestBodyExamples is { Count: > 0 })
                mediaType.Examples = BuildNamedExamples(set.RequestBodyExamples);   // dropdown
            else if (set.RequestBody is not null)
                mediaType.Example = set.RequestBody;                                 // single
        }
    }

    private static void ApplyResponseExamples(OpenApiOperation operation, EndpointExampleSet set)
    {
        if (operation.Responses is null) return;

        // Named response examples first (they win per-status).
        if (set.ResponseExamples is { Count: > 0 })
        {
            foreach (var (statusCode, named) in set.ResponseExamples)
            {
                if (!operation.Responses.TryGetValue(statusCode, out var response) || response?.Content is null) continue;
                foreach (var mediaType in response.Content.Values)
                    mediaType.Examples = BuildNamedExamples(named);
            }
        }

        // Single response examples for any status not covered above.
        if (set.Responses is { Count: > 0 })
        {
            foreach (var (statusCode, example) in set.Responses)
            {
                if (set.ResponseExamples?.ContainsKey(statusCode) == true) continue;
                if (!operation.Responses.TryGetValue(statusCode, out var response) || response?.Content is null) continue;
                foreach (var mediaType in response.Content.Values)
                    mediaType.Example = example;
            }
        }
    }

    private static Dictionary<string, IOpenApiExample> BuildNamedExamples(
        IReadOnlyDictionary<string, JsonNode> source)
    {
        var result = new Dictionary<string, IOpenApiExample>(source.Count);
        foreach (var (name, value) in source)
            result[name] = new OpenApiExample { Value = value };
        return result;
    }

    private static string NormalizeRoute(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return string.Empty;

        int queryIndex = relativePath.IndexOf('?');
        string path = queryIndex >= 0 ? relativePath[..queryIndex] : relativePath;

        return path.TrimStart('/').TrimEnd('/').ToLowerInvariant();
    }

    private static void ApplyRequestExample(OpenApiOperation operation, JsonNode? requestExample)
    {
        if (requestExample is null || operation.RequestBody?.Content is null) return;
        foreach (var mediaType in operation.RequestBody.Content.Values)
            mediaType.Example = requestExample;
    }

    private static void ApplyResponseExamples(
        OpenApiOperation operation,
        IReadOnlyDictionary<string, JsonNode>? responseExamples)
    {
        if (responseExamples is null || responseExamples.Count == 0 || operation.Responses is null) return;

        foreach (var (statusCode, example) in responseExamples)
        {
            if (!operation.Responses.TryGetValue(statusCode, out var response)) continue;
            if (response?.Content is null) continue;
            foreach (var mediaType in response.Content.Values)
                mediaType.Example = example;
        }
    }
}