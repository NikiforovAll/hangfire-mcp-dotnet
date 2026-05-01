using Microsoft.Extensions.Logging;

namespace HangfireJobs;

public class MaintenanceJob(ILogger<MaintenanceJob> logger)
{
    public Task RebuildIndexesAsync(string schema = "public")
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
