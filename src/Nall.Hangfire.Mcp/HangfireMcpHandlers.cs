using ModelContextProtocol.Protocol;
using Nall.Hangfire.Mcp.Authorization;
using Nall.Hangfire.Mcp.Maintenance;
using Nall.Hangfire.Mcp.Prompts;

namespace Nall.Hangfire.Mcp;

public static class HangfireMcpHandlers
{
    public static ListPromptsResult BuildListPromptsResult() =>
        new() { Prompts = MaintenancePrompts.All.ToList() };

    public static GetPromptResult GetPrompt(JobCatalog catalog, GetPromptRequestParams? @params)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var name =
            @params?.Name
            ?? throw new ArgumentException("Prompt name is required.", nameof(@params));
        if (!MaintenancePrompts.IsKnown(name))
        {
            throw new ArgumentException($"Unknown prompt '{name}'.", nameof(@params));
        }
        return MaintenancePrompts.Render(name, catalog);
    }

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

    public static async ValueTask<CallToolResult> InvokeToolAsync(
        JobCatalog catalog,
        HangfireDynamicScheduler scheduler,
        MaintenanceDispatcher maintenance,
        CallToolRequestParams? @params,
        IServiceProvider? services = null,
        CancellationToken cancellationToken = default
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

        var arguments = @params?.Arguments?.AsReadOnly();

        try
        {
            var jobId = await JobInvocationPipeline
                .RunAsync(descriptor, arguments, services, scheduler, cancellationToken)
                .ConfigureAwait(false);
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = $"Enqueued Hangfire job {jobId}." }],
            };
        }
        catch (JobAuthorizationException ex)
        {
            return Error(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Error(ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Error(ex.Message);
        }
    }

    public static CallToolResult InvokeTool(
        JobCatalog catalog,
        HangfireDynamicScheduler scheduler,
        MaintenanceDispatcher maintenance,
        CallToolRequestParams? @params,
        IServiceProvider? services = null
    ) =>
        InvokeToolAsync(catalog, scheduler, maintenance, @params, services)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    private static CallToolResult Error(string message) =>
        new() { Content = [new TextContentBlock { Text = message }], IsError = true };
}
