using ModelContextProtocol.Protocol;

namespace Nall.Hangfire.Mcp;

public static class HangfireMcpHandlers
{
    public static ListToolsResult BuildListToolsResult(JobCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.ListToolsResult;
    }

    public static CallToolResult InvokeTool(
        JobCatalog catalog,
        HangfireDynamicScheduler scheduler,
        CallToolRequestParams? @params
    )
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(scheduler);

        var name = @params?.Name;
        if (name is null || !catalog.TryGetByToolName(name, out var descriptor))
        {
            return Error($"Unknown tool '{name}'.");
        }

        try
        {
            var jobId = scheduler.Enqueue(descriptor, @params?.Arguments?.AsReadOnly());
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = $"Enqueued Hangfire job {jobId}." }],
            };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Error(ex.Message);
        }
    }

    private static CallToolResult Error(string message) =>
        new() { Content = [new TextContentBlock { Text = message }], IsError = true };
}
