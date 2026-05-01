using System.Globalization;
using Hangfire;
using Hangfire.PostgreSql;

namespace Web;

public static class HangfireExtensions
{
    public static void AddHangfireServer(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var defaultCulture = CultureInfo.InvariantCulture;
        GlobalConfiguration.Configuration.UseDefaultCulture(
            culture: defaultCulture,
            uiCulture: defaultCulture,
            captureDefault: false
        );

        builder.Services.AddHangfireServer();

        builder.Services.AddHangfire(globalConfiguration =>
            globalConfiguration
                .UseFilter(new AutomaticRetryAttribute { Attempts = 0 })
                .UsePostgreSqlStorage(options =>
                    options.UseNpgsqlConnection(
                        builder.Configuration.GetConnectionString("hangfire")
                    )
                )
        );
    }
}
