using System.Text.RegularExpressions;
using Hangfire;
using Hangfire.InMemory;
using Nall.Hangfire.Mcp.Tests.Fixtures;
using Shouldly;

namespace Nall.Hangfire.Mcp.Tests;

public class JobScannerTests
{
    private static (JobStorage storage, IRecurringJobManager manager) NewStorage()
    {
        var storage = new InMemoryStorage();
        var manager = new RecurringJobManager(storage);
        return (storage, manager);
    }

    [Fact]
    public void Scan_returns_descriptor_per_recurring_job()
    {
        var (storage, manager) = NewStorage();
        manager.AddOrUpdate<IEmailJob>("send-welcome", j => j.SendAsync("a@b"), Cron.Daily());
        manager.AddOrUpdate<ReportJob>(
            "nightly-report",
            j => j.GenerateAsync(2026, "pdf"),
            Cron.Daily()
        );

        var result = JobScanner.Scan(storage);

        result
            .Select(d => d.RecurringJobId)
            .ShouldBe(["send-welcome", "nightly-report"], ignoreOrder: true);
    }

    [Fact]
    public void Scan_captures_declaring_type_and_method()
    {
        var (storage, manager) = NewStorage();
        manager.AddOrUpdate<IEmailJob>(
            "send-with-subject",
            j => j.SendAsync("a", "s"),
            Cron.Daily()
        );

        var result = JobScanner.Scan(storage);

        var d = result.ShouldHaveSingleItem();
        d.DeclaringType.ShouldBe(typeof(IEmailJob));
        d.Method.Name.ShouldBe("SendAsync");
        d.Method.GetParameters().Length.ShouldBe(2);
    }

    [Fact]
    public void Scan_distinguishes_overloads_by_recurring_id()
    {
        var (storage, manager) = NewStorage();
        manager.AddOrUpdate<IEmailJob>("send-1", j => j.SendAsync("a"), Cron.Daily());
        manager.AddOrUpdate<IEmailJob>("send-2", j => j.SendAsync("a", "s"), Cron.Daily());

        var result = JobScanner.Scan(storage);

        result.Count.ShouldBe(2);
        result.Single(d => d.RecurringJobId == "send-1").Method.GetParameters().Length.ShouldBe(1);
        result.Single(d => d.RecurringJobId == "send-2").Method.GetParameters().Length.ShouldBe(2);
    }

    [Fact]
    public void Scan_returns_empty_when_no_recurring_jobs()
    {
        var (storage, _) = NewStorage();

        JobScanner.Scan(storage).ShouldBeEmpty();
    }

    [Fact]
    public void Scan_throws_ArgumentNullException_on_null_storage()
    {
        Should.Throw<ArgumentNullException>(() => JobScanner.Scan(null!));
    }

    [Fact]
    public void Scan_applies_filter_predicate()
    {
        var (storage, manager) = NewStorage();
        manager.AddOrUpdate<IEmailJob>("send-welcome", j => j.SendAsync("a"), Cron.Daily());
        manager.AddOrUpdate<ReportJob>("nightly-report", j => j.GenerateAsync(2026), Cron.Daily());

        var result = JobScanner.Scan(storage, filter: rj => rj.Id.StartsWith("send-"));

        result.Select(d => d.RecurringJobId).ShouldBe(["send-welcome"]);
    }

    [Fact]
    public void ToolName_uses_recurring_id_with_run_prefix()
    {
        var (storage, manager) = NewStorage();
        manager.AddOrUpdate<IEmailJob>("send-welcome", j => j.SendAsync("a"), Cron.Daily());

        var result = JobScanner.Scan(storage);

        result.ShouldHaveSingleItem().ToolName.ShouldBe("Run_send-welcome");
    }

    [Fact]
    public void ToolName_sanitizes_disallowed_characters()
    {
        var (storage, manager) = NewStorage();
        manager.AddOrUpdate<IEmailJob>("group:send.welcome", j => j.SendAsync("a"), Cron.Daily());

        var result = JobScanner.Scan(storage);

        var d = result.ShouldHaveSingleItem();
        d.ToolName.ShouldBe("Run_group_send_welcome");
        Regex.IsMatch(d.ToolName, "^[A-Za-z0-9_-]+$").ShouldBeTrue();
    }
}
