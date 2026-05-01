namespace Nall.Hangfire.Mcp;

[Flags]
public enum JobDiscoverySources
{
    RecurringStorage = 1,
    StaticManifest = 2,
    All = RecurringStorage | StaticManifest,
}
