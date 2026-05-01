using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Builder;

public static class HangfireMcpEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapHangfireMcp(
        this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern = "/mcp"
    )
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapMcp(pattern);
    }
}
