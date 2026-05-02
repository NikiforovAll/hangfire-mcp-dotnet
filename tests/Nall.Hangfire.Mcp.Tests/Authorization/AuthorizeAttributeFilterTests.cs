using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Nall.Hangfire.Mcp.Authorization;
using Shouldly;

namespace Nall.Hangfire.Mcp.Tests.Authorization;

public class AuthorizeAttributeFilterTests
{
    private interface IGuardedJob
    {
        [Authorize(Policy = "iface-policy")]
        Task RunAsync();
    }

    private sealed class GuardedJob : IGuardedJob
    {
        public Task RunAsync() => Task.CompletedTask;
    }

    private sealed class DirectlyGuardedJob
    {
        [Authorize(Roles = "admin")]
        public Task RunAsync() => Task.CompletedTask;
    }

    private sealed class OpenJob
    {
        public Task RunAsync() => Task.CompletedTask;
    }

    [Fact]
    public async Task Allows_when_no_attribute_present()
    {
        var (filter, services) = NewFilter(b => { });
        var ctx = Context<OpenJob>(services, principal: null);

        var jobId = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult("ok"), default);

        jobId.ShouldBe("ok");
    }

    [Fact]
    public async Task Throws_when_user_missing()
    {
        var (filter, services) = NewFilter(b =>
            b.AddPolicy("iface-policy", p => p.RequireClaim("scope", "jobs"))
        );
        var ctx = Context<GuardedJob>(services, principal: null);

        var ex = await Should.ThrowAsync<JobAuthorizationException>(async () =>
            await filter.InvokeAsync(ctx, _ => ValueTask.FromResult("ok"), default)
        );
        ex.Message.ShouldContain("requires authentication");
    }

    [Fact]
    public async Task Allows_when_policy_satisfied_via_interface_attribute()
    {
        var (filter, services) = NewFilter(b =>
            b.AddPolicy("iface-policy", p => p.RequireClaim("scope", "jobs"))
        );
        var user = NewUser(("scope", "jobs"));
        var ctx = Context<GuardedJob>(services, user);

        var jobId = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult("ok"), default);

        jobId.ShouldBe("ok");
    }

    [Fact]
    public async Task Denies_when_policy_not_satisfied()
    {
        var (filter, services) = NewFilter(b =>
            b.AddPolicy("iface-policy", p => p.RequireClaim("scope", "jobs"))
        );
        var user = NewUser(("scope", "other"));
        var ctx = Context<GuardedJob>(services, user);

        await Should.ThrowAsync<JobAuthorizationException>(async () =>
            await filter.InvokeAsync(ctx, _ => ValueTask.FromResult("ok"), default)
        );
    }

    [Fact]
    public async Task Allows_when_role_matches_direct_attribute()
    {
        var (filter, services) = NewFilter(b => { });
        var user = NewUser((ClaimTypes.Role, "admin"));
        var ctx = Context<DirectlyGuardedJob>(services, user);

        var jobId = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult("ok"), default);

        jobId.ShouldBe("ok");
    }

    [Fact]
    public async Task Denies_when_role_missing_for_direct_attribute()
    {
        var (filter, services) = NewFilter(b => { });
        var user = NewUser((ClaimTypes.Role, "viewer"));
        var ctx = Context<DirectlyGuardedJob>(services, user);

        await Should.ThrowAsync<JobAuthorizationException>(async () =>
            await filter.InvokeAsync(ctx, _ => ValueTask.FromResult("ok"), default)
        );
    }

    private static (AuthorizeAttributeFilter filter, IServiceProvider services) NewFilter(
        Action<AuthorizationOptions> configure
    )
    {
        var sc = new ServiceCollection();
        sc.AddAuthorizationCore(configure);
        sc.AddLogging();
        var services = sc.BuildServiceProvider();
        return (new AuthorizeAttributeFilter(), services);
    }

    private static ClaimsPrincipal NewUser(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.Type, c.Value)),
            authenticationType: "test"
        );
        return new ClaimsPrincipal(identity);
    }

    private static JobInvocationContext Context<T>(
        IServiceProvider services,
        ClaimsPrincipal? principal
    )
        where T : class
    {
        var method =
            typeof(T).GetMethod(
                "RunAsync",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            ) ?? throw new InvalidOperationException("RunAsync not found");
        var descriptor = JobDescriptor.FromManifest(typeof(T), method);
        return new JobInvocationContext(descriptor, Arguments: null, principal, services);
    }
}
