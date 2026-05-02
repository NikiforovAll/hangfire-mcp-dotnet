using System.Security.Claims;
using System.Text.Json;

namespace Nall.Hangfire.Mcp.Authorization;

public delegate ValueTask<string> JobInvocationDelegate(CancellationToken cancellationToken);

public interface IJobInvocationFilter
{
    ValueTask<string> InvokeAsync(
        JobInvocationContext context,
        JobInvocationDelegate next,
        CancellationToken cancellationToken
    );
}

public sealed record JobInvocationContext(
    JobDescriptor Descriptor,
    IReadOnlyDictionary<string, JsonElement>? Arguments,
    ClaimsPrincipal? User,
    IServiceProvider Services
);
