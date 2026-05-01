using Hangfire;
using Hangfire.InMemory;
using Nall.Hangfire.Mcp.Tests.Fixtures;
using Shouldly;

namespace Nall.Hangfire.Mcp.Tests;

public class JobCatalogTests
{
    private static (JobStorage storage, IRecurringJobManager manager) NewStorage()
    {
        var storage = new InMemoryStorage();
        var manager = new RecurringJobManager(storage);
        return (storage, manager);
    }

    [Fact]
    public void Jobs_populated_from_storage_at_construction()
    {
        var (storage, manager) = NewStorage();
        manager.AddOrUpdate<IEmailJob>("send-welcome", j => j.SendAsync("a"), Cron.Daily());
        manager.AddOrUpdate<ReportJob>("nightly", j => j.GenerateAsync(2026, "pdf"), Cron.Daily());

        var catalog = new JobCatalog(storage);

        catalog
            .Jobs.Select(d => d.RecurringJobId)
            .ShouldBe(["send-welcome", "nightly"], ignoreOrder: true);
    }

    [Fact]
    public void Filter_passed_through_to_scanner()
    {
        var (storage, manager) = NewStorage();
        manager.AddOrUpdate<IEmailJob>("send-welcome", j => j.SendAsync("a"), Cron.Daily());
        manager.AddOrUpdate<ReportJob>("nightly", j => j.GenerateAsync(2026), Cron.Daily());

        var catalog = new JobCatalog(storage, filter: rj => rj.Id.StartsWith("send-"));

        catalog.Jobs.Select(d => d.RecurringJobId).ShouldBe(["send-welcome"]);
    }

    [Fact]
    public void Empty_storage_yields_empty_catalog()
    {
        var (storage, _) = NewStorage();

        new JobCatalog(storage).Jobs.ShouldBeEmpty();
    }

    [Fact]
    public void Throws_ArgumentNullException_on_null_storage()
    {
        Should.Throw<ArgumentNullException>(() => new JobCatalog(null!));
    }

    [Fact]
    public void Snapshot_does_not_observe_jobs_added_after_construction()
    {
        var (storage, manager) = NewStorage();
        manager.AddOrUpdate<IEmailJob>("send-welcome", j => j.SendAsync("a"), Cron.Daily());

        var catalog = new JobCatalog(storage);
        manager.AddOrUpdate<ReportJob>("nightly", j => j.GenerateAsync(2026), Cron.Daily());

        catalog.Jobs.Select(d => d.RecurringJobId).ShouldBe(["send-welcome"]);
    }
}
