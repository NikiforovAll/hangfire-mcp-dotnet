using Hangfire.Storage;

namespace Nall.Hangfire.Mcp;

public sealed class HangfireMcpOptions
{
    public JobDiscoverySources Sources { get; set; } = JobDiscoverySources.RecurringStorage;

    public Func<RecurringJobDto, bool>? Filter { get; set; }

    public int MaintenanceMaxBulkSize { get; set; } = 100;
}
