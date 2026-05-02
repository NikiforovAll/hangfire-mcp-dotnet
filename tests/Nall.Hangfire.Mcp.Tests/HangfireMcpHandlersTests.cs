using System.Text.Json;
using Hangfire;
using Hangfire.InMemory;
using ModelContextProtocol.Protocol;
using Nall.Hangfire.Mcp.Maintenance;
using Nall.Hangfire.Mcp.Tests.Fixtures;
using Shouldly;

namespace Nall.Hangfire.Mcp.Tests;

public class HangfireMcpHandlersTests
{
    private static (
        JobCatalog catalog,
        HangfireDynamicScheduler scheduler,
        MaintenanceDispatcher maintenance
    ) Setup(Action<IRecurringJobManager> register)
    {
        var storage = new InMemoryStorage();
        JobStorage.Current = storage;
        var manager = new RecurringJobManager(storage);
        register(manager);
        var catalog = new JobCatalog(storage);
        var client = new BackgroundJobClient(storage);
        var query = new MaintenanceQueryService(storage);
        var commands = new MaintenanceCommandService(client, query, 100);
        return (
            catalog,
            new HangfireDynamicScheduler(client),
            new MaintenanceDispatcher(query, commands)
        );
    }

    private static IDictionary<string, JsonElement> ParseArgs(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact]
    public void BuildListToolsResult_emits_one_tool_per_job_with_schema()
    {
        var (catalog, _, _) = Setup(m =>
        {
            m.AddOrUpdate<ReportJob>("nightly", j => j.GenerateAsync(2026, "pdf"), Cron.Daily());
            m.AddOrUpdate<IEmailJob>("send-welcome", j => j.SendAsync("a"), Cron.Daily());
        });

        var result = HangfireMcpHandlers.BuildListToolsResult(catalog);

        var names = result.Tools.Select(t => t.Name).ToArray();
        names.ShouldContain("Run_nightly");
        names.ShouldContain("Run_send-welcome");
        names.ShouldContain("hangfire_get_statistics");
        var nightly = result.Tools.Single(t => t.Name == "Run_nightly");
        var schema = JsonDocument.Parse(nightly.InputSchema.GetRawText()).RootElement;
        schema.GetProperty("type").GetString().ShouldBe("object");
        schema.GetProperty("properties").TryGetProperty("year", out _).ShouldBeTrue();
    }

    [Fact]
    public void InvokeTool_enqueues_and_returns_job_id()
    {
        var (catalog, scheduler, maintenance) = Setup(m =>
            m.AddOrUpdate<ReportJob>("nightly", j => j.GenerateAsync(2026, "pdf"), Cron.Daily())
        );

        var result = HangfireMcpHandlers.InvokeTool(
            catalog,
            scheduler,
            maintenance,
            new CallToolRequestParams
            {
                Name = "Run_nightly",
                Arguments = ParseArgs("""{"year": 2027, "format": "csv"}"""),
            }
        );

        result.IsError.ShouldNotBe(true);
        result
            .Content.ShouldHaveSingleItem()
            .ShouldBeOfType<TextContentBlock>()
            .Text.ShouldStartWith("Enqueued Hangfire job ");
    }

    [Fact]
    public void InvokeTool_returns_error_for_unknown_tool()
    {
        var (catalog, scheduler, maintenance) = Setup(m =>
            m.AddOrUpdate<ReportJob>("nightly", j => j.GenerateAsync(2026, "pdf"), Cron.Daily())
        );

        var result = HangfireMcpHandlers.InvokeTool(
            catalog,
            scheduler,
            maintenance,
            new CallToolRequestParams { Name = "Run_does_not_exist" }
        );

        result.IsError.ShouldBe(true);
        result
            .Content.ShouldHaveSingleItem()
            .ShouldBeOfType<TextContentBlock>()
            .Text.ShouldContain("Unknown tool");
    }

    [Fact]
    public void InvokeTool_returns_error_when_required_argument_missing()
    {
        var (catalog, scheduler, maintenance) = Setup(m =>
            m.AddOrUpdate<ReportJob>("nightly", j => j.GenerateAsync(2026, "pdf"), Cron.Daily())
        );

        var result = HangfireMcpHandlers.InvokeTool(
            catalog,
            scheduler,
            maintenance,
            new CallToolRequestParams
            {
                Name = "Run_nightly",
                Arguments = new Dictionary<string, JsonElement>(),
            }
        );

        result.IsError.ShouldBe(true);
        result
            .Content.ShouldHaveSingleItem()
            .ShouldBeOfType<TextContentBlock>()
            .Text.ShouldContain("year");
    }

    [Fact]
    public void InvokeTool_uses_default_argument_when_omitted()
    {
        var (catalog, scheduler, maintenance) = Setup(m =>
            m.AddOrUpdate<ReportJob>("nightly", j => j.GenerateAsync(2026, "pdf"), Cron.Daily())
        );

        var result = HangfireMcpHandlers.InvokeTool(
            catalog,
            scheduler,
            maintenance,
            new CallToolRequestParams
            {
                Name = "Run_nightly",
                Arguments = ParseArgs("""{"year": 2027}"""),
            }
        );

        result.IsError.ShouldNotBe(true);
    }
}
