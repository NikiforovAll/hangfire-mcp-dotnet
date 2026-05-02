using Hangfire;
using Hangfire.InMemory;
using Hangfire.States;
using Nall.Hangfire.Mcp.Maintenance;
using Nall.Hangfire.Mcp.Tests.Fixtures;
using Shouldly;

namespace Nall.Hangfire.Mcp.Tests.Maintenance;

public class MaintenanceCommandServiceTests
{
    private static (
        BackgroundJobClient client,
        MaintenanceQueryService query,
        MaintenanceCommandService cmd,
        JobStorage storage
    ) Setup(int max = 100)
    {
        var storage = new InMemoryStorage();
        JobStorage.Current = storage;
        var client = new BackgroundJobClient(storage);
        var query = new MaintenanceQueryService(storage);
        var cmd = new MaintenanceCommandService(client, query, max);
        return (client, query, cmd, storage);
    }

    [Fact]
    public void DeleteOne_marks_job_deleted()
    {
        var (client, query, cmd, _) = Setup();
        var id = client.Enqueue<ReportJob>(j => j.GenerateAsync(2026, "pdf"));

        var result = cmd.DeleteOne(id);

        result.Ok.ShouldBeTrue();
        var details = query.GetJob(id);
        details!.History[0].StateName.ShouldBe(DeletedState.StateName);
    }

    [Fact]
    public void DeleteMany_with_ids_returns_per_id_results()
    {
        var (client, _, cmd, _) = Setup();
        var ids = Enumerable
            .Range(0, 3)
            .Select(_ => client.Enqueue<ReportJob>(j => j.GenerateAsync(2026, "pdf")))
            .ToArray();

        var result = cmd.DeleteMany(ids, filter: null, dryRun: false);

        result.DryRun.ShouldBeFalse();
        result.Results.Count.ShouldBe(3);
        result.Results.ShouldAllBe(r => r.Ok);
    }

    [Fact]
    public void DeleteMany_dryRun_returns_targets_without_acting()
    {
        var (client, query, cmd, _) = Setup();
        var id = client.Enqueue<ReportJob>(j => j.GenerateAsync(2026, "pdf"));

        var result = cmd.DeleteMany(new[] { id }, filter: null, dryRun: true);

        result.DryRun.ShouldBeTrue();
        result.Results.Single().Id.ShouldBe(id);
        var details = query.GetJob(id);
        details!.History[0].StateName.ShouldNotBe(DeletedState.StateName);
    }

    [Fact]
    public void Bulk_requires_exactly_one_of_ids_or_filter()
    {
        var (_, _, cmd, _) = Setup();

        Should.Throw<ArgumentException>(() => cmd.DeleteMany(null, null, false));
        Should.Throw<ArgumentException>(() =>
            cmd.DeleteMany(new[] { "a" }, new JobFilter { State = JobStateKind.Failed }, false)
        );
    }

    [Fact]
    public void Bulk_ids_count_above_cap_throws()
    {
        var (_, _, cmd, _) = Setup(max: 2);
        Should.Throw<ArgumentException>(() => cmd.DeleteMany(new[] { "a", "b", "c" }, null, false));
    }

    [Fact]
    public void DeleteMany_by_filter_targets_matching_jobs_only()
    {
        var (client, _, cmd, _) = Setup();
        var keep = client.Enqueue<ReportJob>(j => j.GenerateAsync(2026, "pdf"));
        var hit1 = client.Enqueue<MaintJob>(j => j.RunAsync());
        var hit2 = client.Enqueue<MaintJob>(j => j.RunAsync());

        var filter = new JobFilter { State = JobStateKind.Enqueued, JobType = "MaintJob" };
        var result = cmd.DeleteMany(null, filter, dryRun: false);

        result.Results.Select(r => r.Id).ShouldBe(new[] { hit1, hit2 }, ignoreOrder: true);
        result.Results.ShouldAllBe(r => r.Ok);
    }
}

public class MaintJob
{
    public Task RunAsync() => Task.CompletedTask;
}
