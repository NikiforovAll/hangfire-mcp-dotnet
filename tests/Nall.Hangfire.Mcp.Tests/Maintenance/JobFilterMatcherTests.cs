using Nall.Hangfire.Mcp.Maintenance;
using Nall.Hangfire.Mcp.Tests.Fixtures;
using Shouldly;
using Job = Hangfire.Common.Job;

namespace Nall.Hangfire.Mcp.Tests.Maintenance;

public class JobFilterMatcherTests
{
    private static JobMatch Make(
        Job? job,
        string? queue = null,
        string? reason = null,
        string? exType = null,
        string? exMsg = null,
        JobStateKind state = JobStateKind.Enqueued
    ) => new("id-1", job, queue, reason, exType, exMsg, state);

    private static Job ReportJobInstance() =>
        new(
            typeof(ReportJob),
            typeof(ReportJob).GetMethod(nameof(ReportJob.GenerateAsync))!,
            2026,
            "pdf"
        );

    [Fact]
    public void Matches_returns_true_when_filter_is_empty_aside_from_state()
    {
        var match = Make(ReportJobInstance());
        var filter = new JobFilter { State = JobStateKind.Failed };
        JobFilterMatcher.Matches(match, filter).ShouldBeTrue();
    }

    [Fact]
    public void Queue_filter_is_case_insensitive_and_exact()
    {
        var match = Make(ReportJobInstance(), queue: "critical");
        JobFilterMatcher
            .Matches(match, new JobFilter { State = JobStateKind.Enqueued, Queue = "CRITICAL" })
            .ShouldBeTrue();
        JobFilterMatcher
            .Matches(match, new JobFilter { State = JobStateKind.Enqueued, Queue = "default" })
            .ShouldBeFalse();
    }

    [Fact]
    public void JobType_and_Method_are_case_insensitive_substring()
    {
        var match = Make(ReportJobInstance());
        JobFilterMatcher
            .Matches(match, new JobFilter { State = JobStateKind.Failed, JobType = "reportjob" })
            .ShouldBeTrue();
        JobFilterMatcher
            .Matches(match, new JobFilter { State = JobStateKind.Failed, Method = "GENERATE" })
            .ShouldBeTrue();
        JobFilterMatcher
            .Matches(match, new JobFilter { State = JobStateKind.Failed, JobType = "EmailJob" })
            .ShouldBeFalse();
    }

    [Fact]
    public void Null_job_fails_jobtype_or_method_filters()
    {
        var match = Make(job: null);
        JobFilterMatcher
            .Matches(match, new JobFilter { State = JobStateKind.Failed, JobType = "anything" })
            .ShouldBeFalse();
        JobFilterMatcher
            .Matches(match, new JobFilter { State = JobStateKind.Failed, Method = "anything" })
            .ShouldBeFalse();
    }

    [Fact]
    public void MessageContains_matches_reason_or_exception_message()
    {
        var match = Make(ReportJobInstance(), reason: "Connection timeout", exMsg: "SQL deadlock");
        JobFilterMatcher
            .Matches(
                match,
                new JobFilter { State = JobStateKind.Failed, MessageContains = "TIMEOUT" }
            )
            .ShouldBeTrue();
        JobFilterMatcher
            .Matches(
                match,
                new JobFilter { State = JobStateKind.Failed, MessageContains = "deadlock" }
            )
            .ShouldBeTrue();
        JobFilterMatcher
            .Matches(match, new JobFilter { State = JobStateKind.Failed, MessageContains = "404" })
            .ShouldBeFalse();
    }

    [Fact]
    public void ExceptionContains_matches_type_or_message()
    {
        var match = Make(
            ReportJobInstance(),
            exType: "System.Data.SqlClient.SqlException",
            exMsg: "deadlock victim"
        );
        JobFilterMatcher
            .Matches(
                match,
                new JobFilter { State = JobStateKind.Failed, ExceptionContains = "SqlException" }
            )
            .ShouldBeTrue();
        JobFilterMatcher
            .Matches(
                match,
                new JobFilter { State = JobStateKind.Failed, ExceptionContains = "victim" }
            )
            .ShouldBeTrue();
        JobFilterMatcher
            .Matches(
                match,
                new JobFilter { State = JobStateKind.Failed, ExceptionContains = "Timeout" }
            )
            .ShouldBeFalse();
    }
}
