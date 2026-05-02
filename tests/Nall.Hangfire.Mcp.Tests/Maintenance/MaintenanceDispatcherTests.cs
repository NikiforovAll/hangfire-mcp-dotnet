using System.Text.Json;
using Hangfire;
using Hangfire.InMemory;
using ModelContextProtocol.Protocol;
using Nall.Hangfire.Mcp.Maintenance;
using Nall.Hangfire.Mcp.Tests.Fixtures;
using Shouldly;

namespace Nall.Hangfire.Mcp.Tests.Maintenance;

public class MaintenanceDispatcherTests
{
    private static (BackgroundJobClient client, MaintenanceDispatcher dispatcher) Setup()
    {
        var storage = new InMemoryStorage();
        JobStorage.Current = storage;
        var client = new BackgroundJobClient(storage);
        var query = new MaintenanceQueryService(storage);
        var commands = new MaintenanceCommandService(client, query, 100);
        return (client, new MaintenanceDispatcher(query, commands));
    }

    private static IDictionary<string, JsonElement> Args(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private static JsonElement ResultJson(CallToolResult r) =>
        JsonDocument.Parse(((TextContentBlock)r.Content.Single()).Text!).RootElement.Clone();

    [Fact]
    public void GetStatistics_returns_counters()
    {
        var (_, d) = Setup();
        var r = d.Invoke(new CallToolRequestParams { Name = MaintenanceTools.GetStatistics });
        r.IsError.ShouldNotBe(true);
        ResultJson(r).TryGetProperty("enqueued", out _).ShouldBeTrue();
    }

    [Fact]
    public void ListJobs_with_filter_finds_matching_enqueued()
    {
        var (client, d) = Setup();
        var id = client.Enqueue<ReportJob>(j => j.GenerateAsync(2026, "pdf"));

        var r = d.Invoke(
            new CallToolRequestParams
            {
                Name = MaintenanceTools.ListJobs,
                Arguments = Args("""{"state": "Enqueued", "filter": {"jobType": "ReportJob"}}"""),
            }
        );

        r.IsError.ShouldNotBe(true);
        var jobs = ResultJson(r).GetProperty("jobs");
        jobs.GetArrayLength().ShouldBe(1);
        jobs[0].GetProperty("id").GetString().ShouldBe(id);
    }

    [Fact]
    public void DeleteJob_succeeds_and_GetJob_shows_deleted_state()
    {
        var (client, d) = Setup();
        var id = client.Enqueue<ReportJob>(j => j.GenerateAsync(2026, "pdf"));

        var del = d.Invoke(
            new CallToolRequestParams
            {
                Name = MaintenanceTools.DeleteJob,
                Arguments = Args($$"""{"id": "{{id}}"}"""),
            }
        );
        del.IsError.ShouldNotBe(true);

        var get = d.Invoke(
            new CallToolRequestParams
            {
                Name = MaintenanceTools.GetJob,
                Arguments = Args($$"""{"id": "{{id}}"}"""),
            }
        );
        get.IsError.ShouldNotBe(true);
        var json = ResultJson(get);
        json.GetProperty("history")[0].GetProperty("stateName").GetString().ShouldBe("Deleted");
    }

    [Fact]
    public void DeleteJobs_dryRun_returns_targets_without_acting()
    {
        var (client, d) = Setup();
        var id = client.Enqueue<ReportJob>(j => j.GenerateAsync(2026, "pdf"));

        var r = d.Invoke(
            new CallToolRequestParams
            {
                Name = MaintenanceTools.DeleteJobs,
                Arguments = Args($$"""{"ids": ["{{id}}"], "dryRun": true}"""),
            }
        );

        r.IsError.ShouldNotBe(true);
        var json = ResultJson(r);
        json.GetProperty("dryRun").GetBoolean().ShouldBeTrue();
        json.GetProperty("results")[0].GetProperty("id").GetString().ShouldBe(id);
    }

    [Fact]
    public void DeleteJobs_rejects_both_ids_and_filter()
    {
        var (_, d) = Setup();
        var r = d.Invoke(
            new CallToolRequestParams
            {
                Name = MaintenanceTools.DeleteJobs,
                Arguments = Args("""{"ids": ["x"], "filter": {"state": "Failed"}}"""),
            }
        );
        r.IsError.ShouldBe(true);
    }

    [Fact]
    public void Unknown_maintenance_tool_returns_error()
    {
        var (_, d) = Setup();
        var r = d.Invoke(new CallToolRequestParams { Name = "hangfire_does_not_exist" });
        r.IsError.ShouldBe(true);
    }
}
