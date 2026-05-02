using Hangfire;
using Hangfire.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Nall.Hangfire.Mcp.Authorization;
using Nall.Hangfire.Mcp.Tests.Fixtures;
using Shouldly;

namespace Nall.Hangfire.Mcp.Tests.Authorization;

public class JobInvocationPipelineTests
{
    [Fact]
    public async Task Filters_run_in_registration_order_and_can_short_circuit()
    {
        var trace = new List<string>();
        var sc = new ServiceCollection();
        sc.AddSingleton<IJobInvocationFilter>(new TraceFilter("a", trace));
        sc.AddSingleton<IJobInvocationFilter>(new DenyFilter("denied-by-b"));
        sc.AddSingleton<IJobInvocationFilter>(new TraceFilter("c", trace));
        var services = sc.BuildServiceProvider();

        var (descriptor, scheduler) = NewJob();

        var ex = await Should.ThrowAsync<JobAuthorizationException>(async () =>
            await JobInvocationPipeline.RunAsync(
                descriptor,
                arguments: null,
                services,
                scheduler,
                CancellationToken.None
            )
        );

        ex.Message.ShouldBe("denied-by-b");
        trace.ShouldBe(["a:before"]);
    }

    [Fact]
    public async Task No_filters_registered_passes_through_to_scheduler()
    {
        var sc = new ServiceCollection();
        var services = sc.BuildServiceProvider();
        var (descriptor, scheduler) = NewJob();

        var jobId = await JobInvocationPipeline.RunAsync(
            descriptor,
            arguments: null,
            services,
            scheduler,
            CancellationToken.None
        );

        jobId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Null_services_passes_through_to_scheduler()
    {
        var (descriptor, scheduler) = NewJob();

        var jobId = await JobInvocationPipeline.RunAsync(
            descriptor,
            arguments: null,
            services: null,
            scheduler,
            CancellationToken.None
        );

        jobId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Wrapping_filter_observes_inner_job_id()
    {
        var capture = new CaptureFilter();
        var sc = new ServiceCollection();
        sc.AddSingleton<IJobInvocationFilter>(capture);
        var services = sc.BuildServiceProvider();
        var (descriptor, scheduler) = NewJob();

        var jobId = await JobInvocationPipeline.RunAsync(
            descriptor,
            arguments: null,
            services,
            scheduler,
            CancellationToken.None
        );

        capture.Captured.ShouldBe(jobId);
    }

    private static (JobDescriptor descriptor, HangfireDynamicScheduler scheduler) NewJob()
    {
        var storage = new InMemoryStorage();
        var client = new BackgroundJobClient(storage);
        var method =
            typeof(OpenJob).GetMethod(nameof(OpenJob.RunAsync))
            ?? throw new InvalidOperationException();
        var descriptor = JobDescriptor.FromManifest(typeof(OpenJob), method);
        return (descriptor, new HangfireDynamicScheduler(client));
    }

    private sealed class TraceFilter(string name, List<string> trace) : IJobInvocationFilter
    {
        public async ValueTask<string> InvokeAsync(
            JobInvocationContext context,
            JobInvocationDelegate next,
            CancellationToken cancellationToken
        )
        {
            trace.Add($"{name}:before");
            var result = await next(cancellationToken);
            trace.Add($"{name}:after");
            return result;
        }
    }

    private sealed class DenyFilter(string reason) : IJobInvocationFilter
    {
        public ValueTask<string> InvokeAsync(
            JobInvocationContext context,
            JobInvocationDelegate next,
            CancellationToken cancellationToken
        ) => throw new JobAuthorizationException(reason);
    }

    private sealed class CaptureFilter : IJobInvocationFilter
    {
        public string? Captured { get; private set; }

        public async ValueTask<string> InvokeAsync(
            JobInvocationContext context,
            JobInvocationDelegate next,
            CancellationToken cancellationToken
        )
        {
            var jobId = await next(cancellationToken);
            Captured = jobId;
            return jobId;
        }
    }
}
