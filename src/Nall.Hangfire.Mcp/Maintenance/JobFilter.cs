namespace Nall.Hangfire.Mcp.Maintenance;

public enum JobStateKind
{
    Enqueued,
    Processing,
    Scheduled,
    Failed,
    Succeeded,
    Deleted,
}

public sealed record JobFilter
{
    public JobStateKind State { get; init; }
    public string? Queue { get; init; }
    public string? JobType { get; init; }
    public string? Method { get; init; }
    public string? MessageContains { get; init; }
    public string? ExceptionContains { get; init; }
}
