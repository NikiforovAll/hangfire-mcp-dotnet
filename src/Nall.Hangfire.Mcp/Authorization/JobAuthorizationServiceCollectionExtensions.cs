using Microsoft.Extensions.DependencyInjection.Extensions;
using Nall.Hangfire.Mcp.Authorization;

namespace Microsoft.Extensions.DependencyInjection;

public static class JobAuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddJobAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddAuthorizationCore();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IJobInvocationFilter, AuthorizeAttributeFilter>()
        );
        return services;
    }
}
