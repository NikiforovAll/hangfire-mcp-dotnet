using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Nall.Hangfire.Mcp.Tests.Fixtures;
using Shouldly;

namespace Nall.Hangfire.Mcp.Tests.Tools;

public class McpToolsIntegrationTests : IAsyncLifetime
{
    private IHost _host = null!;
    private McpClient _client = null!;
    private JobStorage _storage = null!;

    public async Task InitializeAsync()
    {
        var storage = new InMemoryStorage();
        _storage = storage;
        JobStorage.Current = storage;

        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton<JobStorage>(storage);
                    services.AddSingleton<IBackgroundJobClient>(_ => new BackgroundJobClient(
                        storage
                    ));
                    services.AddSingleton<IRecurringJobManager>(_ => new RecurringJobManager(
                        storage
                    ));
                    services.AddHangfireMcp();
                    services.AddRouting();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapHangfireMcp("/mcp"));
                });
            });

        _host = await builder.StartAsync();

        var manager = _host.Services.GetRequiredService<IRecurringJobManager>();
        manager.AddOrUpdate<ReportJob>("nightly", j => j.GenerateAsync(2026, "pdf"), Cron.Daily());

        var testServer = _host.GetTestServer();
        var http = testServer.CreateClient();
        http.BaseAddress = new Uri(testServer.BaseAddress, "/mcp");

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = http.BaseAddress },
            http,
            loggerFactory: null!,
            ownsHttpClient: false
        );
        _client = await McpClient.CreateAsync(transport);
    }

    public async Task DisposeAsync()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task ListTools_returns_maintenance_and_run_job_tools()
    {
        var tools = await _client.ListToolsAsync();

        var names = tools.Select(t => t.Name).ToArray();
        names.ShouldContain("hangfire_get_statistics");
        names.ShouldContain("hangfire_list_queues");
        names.ShouldContain("hangfire_list_jobs");
        names.ShouldContain("hangfire_get_job");
        names.ShouldContain("hangfire_delete_job");
        names.ShouldContain("hangfire_requeue_job");
        names.ShouldContain("hangfire_delete_jobs");
        names.ShouldContain("hangfire_requeue_jobs");
        names.ShouldContain("Run_nightly");
    }

    [Fact]
    public async Task CallTool_run_job_enqueues_hangfire_job()
    {
        var result = await _client.CallToolAsync(
            "Run_nightly",
            new Dictionary<string, object?> { ["year"] = 2030, ["format"] = "csv" }
        );

        (result.IsError ?? false).ShouldBeFalse();
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        text.ShouldStartWith("Enqueued Hangfire job ");

        var monitor = _storage.GetMonitoringApi();
        monitor.EnqueuedCount("default").ShouldBe(1);
    }

    [Fact]
    public async Task CallTool_unknown_returns_error()
    {
        var result = await _client.CallToolAsync("Run_does_not_exist");

        (result.IsError ?? false).ShouldBeTrue();
        result.Content.OfType<TextContentBlock>().Single().Text.ShouldContain("Unknown tool");
    }

    [Fact]
    public async Task CallTool_get_statistics_returns_counts()
    {
        var result = await _client.CallToolAsync("hangfire_get_statistics");

        (result.IsError ?? false).ShouldBeFalse();
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        text.ShouldContain("Enqueued");
        text.ShouldContain("Failed");
        text.ShouldContain("Servers");
    }

    [Fact]
    public async Task CallTool_list_jobs_returns_enqueued_after_dispatch()
    {
        await _client.CallToolAsync(
            "Run_nightly",
            new Dictionary<string, object?> { ["year"] = 2031 }
        );

        var result = await _client.CallToolAsync(
            "hangfire_list_jobs",
            new Dictionary<string, object?> { ["state"] = "Enqueued", ["count"] = 50 }
        );

        (result.IsError ?? false).ShouldBeFalse();
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        text.ShouldContain("ReportJob");
    }

    [Fact]
    public async Task CallTool_delete_jobs_dryRun_does_not_remove()
    {
        var jobClient = _host.Services.GetRequiredService<IBackgroundJobClient>();
        var jobId = jobClient.Enqueue<ReportJob>(j => j.GenerateAsync(2032, "pdf"));

        var result = await _client.CallToolAsync(
            "hangfire_delete_jobs",
            new Dictionary<string, object?>
            {
                ["filter"] = new Dictionary<string, object?> { ["state"] = "Enqueued" },
                ["dryRun"] = true,
            }
        );

        (result.IsError ?? false).ShouldBeFalse();
        _storage.GetMonitoringApi().EnqueuedCount("default").ShouldBe(1);
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        text.ShouldContain(jobId);
    }
}
