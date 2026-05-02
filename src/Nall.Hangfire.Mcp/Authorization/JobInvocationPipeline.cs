using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Nall.Hangfire.Mcp.Authorization;

internal static class JobInvocationPipeline
{
    public static async ValueTask<string> RunAsync(
        JobDescriptor descriptor,
        IReadOnlyDictionary<string, JsonElement>? arguments,
        IServiceProvider? services,
        HangfireDynamicScheduler scheduler,
        CancellationToken cancellationToken
    )
    {
        JobInvocationDelegate terminal = ct =>
            ValueTask.FromResult(scheduler.Enqueue(descriptor, arguments));

        if (services is null)
        {
            return await terminal(cancellationToken).ConfigureAwait(false);
        }

        var filters = services.GetServices<IJobInvocationFilter>().ToArray();
        if (filters.Length == 0)
        {
            return await terminal(cancellationToken).ConfigureAwait(false);
        }

        var user = services.GetService<IHttpContextAccessor>()?.HttpContext?.User;
        var context = new JobInvocationContext(descriptor, arguments, user, services);

        var pipeline = terminal;
        for (var i = filters.Length - 1; i >= 0; i--)
        {
            var current = filters[i];
            var next = pipeline;
            pipeline = ct => current.InvokeAsync(context, next, ct);
        }

        return await pipeline(cancellationToken).ConfigureAwait(false);
    }
}
