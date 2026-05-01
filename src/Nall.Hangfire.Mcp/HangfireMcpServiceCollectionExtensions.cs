using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nall.Hangfire.Mcp;

namespace Microsoft.Extensions.DependencyInjection;

public static class HangfireMcpServiceCollectionExtensions
{
    public static IMcpServerBuilder AddHangfireMcp(
        this IServiceCollection services,
        Action<HangfireMcpOptions>? configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new HangfireMcpOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(sp => sp.GetService<JobStorage>() ?? JobStorage.Current);
        services.TryAddSingleton(sp => new JobCatalog(
            sp.GetRequiredService<JobStorage>(),
            options.Sources,
            options.Filter
        ));
        services.TryAddSingleton<HangfireDynamicScheduler>(sp => new HangfireDynamicScheduler(
            sp.GetRequiredService<IBackgroundJobClient>()
        ));

        return services
            .AddMcpServer()
            .WithHttpTransport(o => o.Stateless = true)
            .WithListToolsHandler(
                static (request, ct) =>
                {
                    var catalog = request.Services!.GetRequiredService<JobCatalog>();
                    return ValueTask.FromResult(HangfireMcpHandlers.BuildListToolsResult(catalog));
                }
            )
            .WithCallToolHandler(
                static (request, ct) =>
                {
                    var catalog = request.Services!.GetRequiredService<JobCatalog>();
                    var scheduler =
                        request.Services!.GetRequiredService<HangfireDynamicScheduler>();
                    return ValueTask.FromResult(
                        HangfireMcpHandlers.InvokeTool(catalog, scheduler, request.Params)
                    );
                }
            );
    }
}
