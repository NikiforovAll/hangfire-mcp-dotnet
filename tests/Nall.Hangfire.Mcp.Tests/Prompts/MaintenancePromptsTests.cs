using Hangfire;
using Hangfire.InMemory;
using ModelContextProtocol.Protocol;
using Nall.Hangfire.Mcp.Prompts;
using Nall.Hangfire.Mcp.Tests.Fixtures;
using Shouldly;

namespace Nall.Hangfire.Mcp.Tests.Prompts;

public class MaintenancePromptsTests
{
    private static JobCatalog Setup(
        Action<IRecurringJobManager> register,
        Action<IBackgroundJobClient>? enqueue = null,
        JobDiscoverySources sources = JobDiscoverySources.RecurringStorage
    )
    {
        var storage = new InMemoryStorage();
        JobStorage.Current = storage;
        register(new RecurringJobManager(storage));
        enqueue?.Invoke(new BackgroundJobClient(storage));
        return new JobCatalog(storage, sources);
    }

    [Fact]
    public void All_lists_three_prompts_with_expected_names()
    {
        MaintenancePrompts
            .All.Select(p => p.Name)
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
    public Task Render_health_check() =>
        Verify(ToText(MaintenancePrompts.Render(MaintenancePrompts.HealthCheck, EmptyCatalog())));

    [Fact]
    public Task Render_triage_failures() =>
        Verify(
            ToText(MaintenancePrompts.Render(MaintenancePrompts.TriageFailures, EmptyCatalog()))
        );

    [Fact]
    public Task Render_discover_with_jobs()
    {
        var catalog = Setup(
            CapabilityCatalogFixture.RegisterRecurringJobs,
            CapabilityCatalogFixture.RegisterManifestJobs,
            JobDiscoverySources.All
        );
        return Verify(ToText(MaintenancePrompts.Render(MaintenancePrompts.Discover, catalog)));
    }

    [Fact]
    public Task Render_discover_empty_catalog() =>
        Verify(ToText(MaintenancePrompts.Render(MaintenancePrompts.Discover, EmptyCatalog())));

    [Fact]
    public void Render_throws_for_unknown_prompt()
    {
        Should
            .Throw<ArgumentException>(() =>
                MaintenancePrompts.Render("hangfire_does_not_exist", EmptyCatalog())
            )
            .Message.ShouldContain("Unknown prompt");
    }

    private static JobCatalog EmptyCatalog() => Setup(_ => { });

    private static string ToText(GetPromptResult result)
    {
        var msg = result.Messages.ShouldHaveSingleItem();
        msg.Role.ShouldBe(Role.User);
        var text = msg.Content.ShouldBeOfType<TextContentBlock>().Text;
        return $"description: {result.Description}\n---\n{text}";
    }
}
