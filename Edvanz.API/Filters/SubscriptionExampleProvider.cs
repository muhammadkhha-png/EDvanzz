using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>
/// Injects request-body and response examples into Swagger UI. The filter itself
/// holds no examples — it delegates to the registered <see cref="IEndpointExampleProvider"/>
/// implementations (one per module). Lookup is by (HTTP method + normalized route);
/// the first provider that owns the route wins. Endpoints no provider claims are
/// left untouched, so the filter is safe to register globally.
///
/// To document a new module, register a new IEndpointExampleProvider in Program.cs —
/// this class never changes.
/// </summary>
public sealed class SubscriptionExampleProvider : EndpointExampleProvider
{
    private readonly IReadOnlyList<IEndpointExampleProvider> _providers;

    public SubscriptionExampleProvider(IEnumerable<IEndpointExampleProvider> providers)
        => _providers = providers.ToList();

    public override EndpointExampleSet? GetExamples(string httpMethod, string normalizedRoute)
    {
        throw new NotImplementedException();
    }
}