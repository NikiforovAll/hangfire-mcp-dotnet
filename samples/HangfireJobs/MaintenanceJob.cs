using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace HangfireJobs;

public class MaintenanceJob(ILogger<MaintenanceJob> logger)
{
    [Description("Rebuild all indexes in the given PostgreSQL schema.")]
    public Task RebuildIndexesAsync(
        [Description("Target schema name. Defaults to 'public'.")] string schema = "public"
    )
    {
        logger.LogInformation("Rebuilding indexes in schema {Schema}", schema);
        return Task.CompletedTask;
    }

    public Task VacuumAsync(string table, bool full)
    {
        logger.LogInformation("Vacuum {Table} full={Full}", table, full);
        return Task.CompletedTask;
    }
}
