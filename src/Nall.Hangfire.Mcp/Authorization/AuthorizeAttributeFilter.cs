using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Nall.Hangfire.Mcp.Authorization;

public sealed class AuthorizeAttributeFilter : IJobInvocationFilter
{
    public async ValueTask<string> InvokeAsync(
        JobInvocationContext context,
        JobInvocationDelegate next,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var attributes = JobAuthorizationAttributeResolver.Resolve(context.Descriptor.Method);
        if (attributes.Count == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var policyProvider = context.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await AuthorizationPolicy
            .CombineAsync(policyProvider, attributes)
            .ConfigureAwait(false);
        if (policy is null)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        if (context.User is null)
        {
            throw new JobAuthorizationException(
                $"Forbidden: tool '{context.Descriptor.ToolName}' requires authentication."
            );
        }

        var authorizationService = context.Services.GetRequiredService<IAuthorizationService>();
        var result = await authorizationService
            .AuthorizeAsync(context.User, resource: null, policy)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var reasons = result.Failure?.FailureReasons?.Select(r => r.Message).ToArray() ?? [];
            var detail = reasons.Length > 0 ? $" ({string.Join("; ", reasons)})" : string.Empty;
            throw new JobAuthorizationException(
                $"Forbidden: tool '{context.Descriptor.ToolName}' requires authorization{detail}."
            );
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
