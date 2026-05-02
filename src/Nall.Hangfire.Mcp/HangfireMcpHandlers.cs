using ModelContextProtocol.Protocol;
using Nall.Hangfire.Mcp.Maintenance;

namespace Nall.Hangfire.Mcp;

public static class HangfireMcpHandlers
{
    public static ListToolsResult BuildListToolsResult(JobCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var tools = new List<Tool>(
            catalog.ListToolsResult.Tools.Count + MaintenanceTools.All.Count
        );
        tools.AddRange(MaintenanceTools.All);
        foreach (var t in catalog.ListToolsResult.Tools)
        {
            if (!MaintenanceTools.IsMaintenance(t.Name))
            {
                tools.Add(t);
            }
        }
        return new ListToolsResult { Tools = tools };
    }

    public static CallToolResult InvokeTool(
        JobCatalog catalog,
        HangfireDynamicScheduler scheduler,
        MaintenanceDispatcher maintenance,
        CallToolRequestParams? @params
    )
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(maintenance);

        var name = @params?.Name;
        if (MaintenanceTools.IsMaintenance(name))
        {
            return maintenance.Invoke(@params);
        }
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
