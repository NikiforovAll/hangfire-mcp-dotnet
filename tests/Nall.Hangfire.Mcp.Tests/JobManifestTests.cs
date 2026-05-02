using System.Reflection;
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
    public void FromManifest_appends_overload_index_to_synthetic_id()
    {
        var t = typeof(IEmailJob);
        var oneArg = t.GetMethod(nameof(IEmailJob.SendAsync), new[] { typeof(string) })!;
        var twoArg = t.GetMethod(
            nameof(IEmailJob.SendAsync),
            new[] { typeof(string), typeof(string) }
        )!;

        var d1 = JobDescriptor.FromManifest(t, oneArg);
        var d2 = JobDescriptor.FromManifest(t, twoArg);

        d1.ToolName.ShouldBe("Run_IEmailJob_SendAsync_1");
        d2.ToolName.ShouldBe("Run_IEmailJob_SendAsync_2");
    }

    [Fact]
    public void FromManifest_disambiguates_same_arity_overloads_deterministically()
    {
        var t = typeof(SameArityOverloadsJob);
        var intArg = t.GetMethod(nameof(SameArityOverloadsJob.RunAsync), new[] { typeof(int) })!;
        var stringArg = t.GetMethod(
            nameof(SameArityOverloadsJob.RunAsync),
            new[] { typeof(string) }
        )!;

        var dInt = JobDescriptor.FromManifest(t, intArg);
        var dString = JobDescriptor.FromManifest(t, stringArg);

        // Param-type FullName ordering: System.Int32 < System.String, so int gets _1, string gets _2.
        dInt.ToolName.ShouldBe("Run_SameArityOverloadsJob_RunAsync_1");
        dString.ToolName.ShouldBe("Run_SameArityOverloadsJob_RunAsync_2");
    }

    [Fact]
    public void FromManifest_includes_static_methods_in_overload_set()
    {
        var t = typeof(MixedOverloadsJob);
        var staticArg = t.GetMethod(
            nameof(MixedOverloadsJob.DoAsync),
            BindingFlags.Public | BindingFlags.Static,
            Type.EmptyTypes
        )!;
        var oneArg = t.GetMethod(
            nameof(MixedOverloadsJob.DoAsync),
            BindingFlags.Public | BindingFlags.Instance,
            new[] { typeof(int) }
        )!;
        var twoArg = t.GetMethod(
            nameof(MixedOverloadsJob.DoAsync),
            BindingFlags.Public | BindingFlags.Instance,
            new[] { typeof(string), typeof(string) }
        )!;

        var d0 = JobDescriptor.FromManifest(t, staticArg);
        var d1 = JobDescriptor.FromManifest(t, oneArg);
        var d2 = JobDescriptor.FromManifest(t, twoArg);

        var names = new[] { d0.ToolName, d1.ToolName, d2.ToolName };
        names.ShouldBeUnique();
        d0.ToolName.ShouldBe("Run_MixedOverloadsJob_DoAsync_1");
        d1.ToolName.ShouldBe("Run_MixedOverloadsJob_DoAsync_2");
        d2.ToolName.ShouldBe("Run_MixedOverloadsJob_DoAsync_3");
    }

    [Fact]
    public void FromManifest_overload_index_is_stable_across_calls()
    {
        var t = typeof(MixedOverloadsJob);
        var twoArg = t.GetMethod(
            nameof(MixedOverloadsJob.DoAsync),
            BindingFlags.Public | BindingFlags.Instance,
            new[] { typeof(string), typeof(string) }
        )!;

        var first = JobDescriptor.FromManifest(t, twoArg).ToolName;
        var second = JobDescriptor.FromManifest(t, twoArg).ToolName;

        first.ShouldBe(second);
    }

    [Fact]
    public void FromManifest_omits_index_for_single_overload()
    {
        var t = typeof(ReportJob);
        var m = t.GetMethod(nameof(ReportJob.GenerateAsync))!;

        var d = JobDescriptor.FromManifest(t, m);

        d.ToolName.ShouldBe("Run_ReportJob_GenerateAsync");
    }

    [Fact]
    public void Scan_storage_only_excludes_manifest_entries()
    {
        var storage = new InMemoryStorage();

        var result = JobScanner.Scan(storage, JobDiscoverySources.RecurringStorage);

        result.ShouldBeEmpty();
    }
}
