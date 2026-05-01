using System.Text.Json;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;

namespace Nall.Hangfire.Mcp;

public sealed class HangfireDynamicScheduler
{
    private static readonly EnqueuedState s_defaultState = new(EnqueuedState.DefaultQueue);

    private readonly IBackgroundJobClient _client;

    public HangfireDynamicScheduler(IBackgroundJobClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public string Enqueue(
        JobDescriptor descriptor,
        IReadOnlyDictionary<string, JsonElement>? arguments = null,
        string? queue = null
    )
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var args = JobArgumentBinder.Bind(descriptor.Method, arguments);
        var job = new Job(descriptor.DeclaringType, descriptor.Method, args);
        var state = queue is null ? s_defaultState : new EnqueuedState(queue);
        return _client.Create(job, state);
    }
}
