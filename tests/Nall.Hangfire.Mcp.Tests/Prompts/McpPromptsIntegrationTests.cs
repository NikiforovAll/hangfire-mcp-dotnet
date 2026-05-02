using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Nall.Hangfire.Mcp.Prompts;
using Nall.Hangfire.Mcp.Tests.Fixtures;
using Shouldly;

namespace Nall.Hangfire.Mcp.Tests.Prompts;

public class McpPromptsIntegrationTests : IAsyncLifetime
{
    private IHost _host = null!;
    private McpClient _client = null!;

    public async Task InitializeAsync()
    {
        var storage = new InMemoryStorage();
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
                    services.AddHangfireMcp(o => o.Sources = JobDiscoverySources.All);
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
        CapabilityCatalogFixture.RegisterRecurringJobs(manager);

        var client = _host.Services.GetRequiredService<IBackgroundJobClient>();
        CapabilityCatalogFixture.RegisterManifestJobs(client);

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
    public async Task ListPrompts_returns_maintenance_prompts()
    {
        var prompts = await _client.ListPromptsAsync();

        prompts
            .Select(p => p.Name)
            .ShouldBe(
                new[]
                {
                    MaintenancePrompts.Discover,
                    MaintenancePrompts.HealthCheck,
                    MaintenancePrompts.TriageFailures,
                },
                ignoreOrder: true
            );
    }

    [Fact]
    public async Task GetPrompt_health_check_returns_user_message()
    {
        var result = await _client.GetPromptAsync(MaintenancePrompts.HealthCheck);

        result.Description.ShouldNotBeNullOrEmpty();
        var msg = result.Messages.ShouldHaveSingleItem();
        msg.Role.ShouldBe(Role.User);
        msg.Content.ShouldBeOfType<TextContentBlock>()
            .Text.ShouldContain("hangfire_get_statistics");
    }

    [Fact]
    public async Task GetPrompt_discover_lists_tools()
    {
        var result = await _client.GetPromptAsync(MaintenancePrompts.Discover);

        var text = result
            .Messages.ShouldHaveSingleItem()
            .Content.ShouldBeOfType<TextContentBlock>()
            .Text;
        text.ShouldContain("Run_send-message_text");
        text.ShouldContain("Run_send-message_envelope");
        text.ShouldContain("Run_report_generate");
        text.ShouldContain("Run_data_export");
        text.ShouldContain("Run_maint_rebuild-indexes");
        text.ShouldContain("Run_notify_dispatch");
        text.ShouldContain("Run_time_execute");
        text.ShouldContain("Run_INotificationJob_BroadcastAsync");
        text.ShouldContain("Run_IReportJob_PreviewAsync");
        text.ShouldContain("Run_MaintenanceJob_VacuumAsync");
        text.ShouldContain("hangfire_get_statistics");
        text.ShouldContain("hangfire_list_jobs");
    }

    [Fact]
    public async Task ListTools_still_includes_maintenance_and_discovered_tools()
    {
        var tools = await _client.ListToolsAsync();

        var names = tools.Select(t => t.Name).ToArray();
        names.ShouldContain("hangfire_get_statistics");
        names.ShouldContain("Run_time_execute");
    }
}
