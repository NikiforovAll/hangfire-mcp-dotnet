using Hangfire;
using Hangfire.InMemory;
using Nall.Hangfire.Mcp.Manifest;
using Nall.Hangfire.Mcp.Tests.Fixtures;
using Shouldly;

namespace Nall.Hangfire.Mcp.Tests;

public class JobManifestTests
{
    [Fact]
    public void Generator_populates_manifest_with_registration_call_targets()
    {
        // Module initializer in the generated HangfireJobManifest runs at assembly load,
        // so AllDescriptors is already populated.
        var entries = JobManifestRegistry.AllDescriptors;

        entries.ShouldNotBeEmpty();
        entries.ShouldContain(d =>
            d.DeclaringType == typeof(IEmailJob) && d.Method.Name == "SendAsync"
        );
        entries.ShouldContain(d =>
            d.DeclaringType == typeof(ReportJob) && d.Method.Name == "GenerateAsync"
        );
    }

    [Fact]
    public void Manifest_descriptors_get_synthetic_tool_names()
    {
        var d = JobManifestRegistry.AllDescriptors.First(x =>
            x.DeclaringType == typeof(ReportJob) && x.Method.Name == "GenerateAsync"
        );
        d.ToolName.ShouldBe("Run_ReportJob_GenerateAsync");
    }

    [Fact]
    public void Scan_unions_storage_and_manifest_with_dedupe()
    {
        var storage = new InMemoryStorage();
        var manager = new RecurringJobManager(storage);
        manager.AddOrUpdate<IEmailJob>("send-welcome", j => j.SendAsync("a"), Cron.Daily());

        var result = JobScanner.Scan(storage, JobDiscoverySources.All);

        result.Count(d => d.RecurringJobId == "send-welcome").ShouldBe(1);
        // The manifest contains IEmailJob.SendAsync(string) too; dedupe by (Type, Method) keeps the storage entry.
        result
            .Count(d =>
                d.DeclaringType == typeof(IEmailJob) && d.Method.GetParameters().Length == 1
            )
            .ShouldBe(1);
        // Manifest-only entries (e.g., ReportJob.GenerateAsync registered in other tests) should still appear.
        result.ShouldContain(d =>
            d.DeclaringType == typeof(ReportJob) && d.Method.Name == "GenerateAsync"
        );
    }

    [Fact]
    public void Scan_storage_only_excludes_manifest_entries()
    {
        var storage = new InMemoryStorage();

        var result = JobScanner.Scan(storage, JobDiscoverySources.RecurringStorage);

        result.ShouldBeEmpty();
    }
}
