using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

namespace Nall.Hangfire.Mcp.Maintenance;

public sealed class MaintenanceQueryService
{
    private const int PageSize = 1000;
    private const string Unknown = "(unknown)";

    private readonly JobStorage _storage;

    public MaintenanceQueryService(JobStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        _storage = storage;
    }

    private IMonitoringApi Api => _storage.GetMonitoringApi();

    public StatisticsDto GetStatistics() => Api.GetStatistics();

    public StatisticsResult GetStatisticsRich(int windowHours, int recentLimit, int groupScan)
    {
        var api = Api;
        var counts = api.GetStatistics();
        var now = DateTime.UtcNow;
        var since = now.AddHours(-windowHours);

        var hourlySucceeded = SafeHourly(api.HourlySucceededJobs);
        var hourlyFailed = SafeHourly(api.HourlyFailedJobs);
        var trend = new Trend24h(SumLast(hourlySucceeded, since), SumLast(hourlyFailed, since));

        var recentFailedIds = new List<string>();
        var groups = new Dictionary<(string, string), FailureGroupAcc>(capacity: 8, comparer: null);
        var failedInWindow = 0;

        if (counts.Failed > 0)
        {
            var sample = api.FailedJobs(0, Math.Min(groupScan, (int)counts.Failed));
            foreach (var kvp in sample)
            {
                var dto = kvp.Value;
                if (dto.FailedAt is { } at && at < since)
                {
                    continue;
                }
                failedInWindow++;
                if (recentFailedIds.Count < recentLimit)
                {
                    recentFailedIds.Add(kvp.Key);
                }
                var jobType = dto.Job?.Type;
                var typeName = jobType?.FullName ?? jobType?.Name ?? Unknown;
                var exType = dto.ExceptionType ?? Unknown;
                var key = (typeName, exType);
                if (!groups.TryGetValue(key, out var acc))
                {
                    acc = new FailureGroupAcc(
                        typeName,
                        exType,
                        kvp.Key,
                        dto.FailedAt,
                        dto.FailedAt
                    );
                }
                acc = acc with
                {
                    Count = acc.Count + 1,
                    OldestAt =
                        dto.FailedAt is { } a && (acc.OldestAt is null || a < acc.OldestAt)
                            ? a
                            : acc.OldestAt,
                    NewestAt =
                        dto.FailedAt is { } b && (acc.NewestAt is null || b > acc.NewestAt)
                            ? b
                            : acc.NewestAt,
                };
                groups[key] = acc;
            }
        }

        var failureGroups = groups
            .Values.OrderByDescending(g => g.Count)
            .Take(5)
            .Select(g => new FailureGroup(
                g.Type,
                g.Exception,
                g.Count,
                g.SampleId,
                g.OldestAt,
                g.NewestAt
            ))
            .ToList();

        var servers = api.Servers()
            .Select(s => new ServerInfo(s.Name, s.Heartbeat, s.WorkersCount, s.Queues, s.StartedAt))
            .ToList();

        return new StatisticsResult(
            now,
            counts,
            trend,
            failedInWindow,
            failureGroups,
            recentFailedIds,
            servers,
            windowHours
        );
    }

    private static IDictionary<DateTime, long> SafeHourly(Func<IDictionary<DateTime, long>> fn)
    {
        try
        {
            return fn() ?? new Dictionary<DateTime, long>();
        }
        catch
        {
            return new Dictionary<DateTime, long>();
        }
    }

    private static long SumLast(IDictionary<DateTime, long> series, DateTime since)
    {
        long total = 0;
        foreach (var (k, v) in series)
        {
            if (k >= since)
            {
                total += v;
            }
        }
        return total;
    }

    private sealed record FailureGroupAcc(
        string Type,
        string Exception,
        string SampleId,
        DateTime? OldestAt,
        DateTime? NewestAt,
        int Count = 1
    );

    public JobDetailsDto? GetJob(string id) => Api.JobDetails(id);

    public ScanResult ListJobs(JobStateKind? state, JobFilter? filter, int from, int count)
    {
        if (state is { } s)
        {
            return ListJobsForState(s, filter, from, count);
        }

        var matches = new List<JobMatch>(count);
        var scanned = 0;
        long total = 0;
        var taken = 0;
        var cursorRemaining = from;
        var truncated = false;

        foreach (var kind in AllStates)
        {
            var stateCount = StateCount(kind);
            total += stateCount;

            if (taken >= count)
            {
                truncated = truncated || stateCount > 0;
                continue;
            }

            if (cursorRemaining >= stateCount)
            {
                cursorRemaining -= (int)stateCount;
                continue;
            }

            var slice = ListJobsForState(kind, filter, cursorRemaining, count - taken);
            cursorRemaining = 0;
            scanned += slice.Scanned;
            matches.AddRange(slice.Matches);
            taken = matches.Count;
            if (slice.Truncated)
            {
                truncated = true;
            }
        }

        return new ScanResult(matches, scanned, total, truncated, from + scanned);
    }

    private ScanResult ListJobsForState(JobStateKind state, JobFilter? filter, int from, int count)
    {
        var matches = new List<JobMatch>(count);
        var scanned = 0;
        var stateCount = StateCount(state);
        var taken = 0;
        var cursor = from;

        while (taken < count && cursor < stateCount)
        {
            var remaining = (int)Math.Min(PageSize, stateCount - cursor);
            var page = FetchPage(state, cursor, remaining);
            if (page.Count == 0)
            {
                break;
            }

            foreach (var entry in page)
            {
                scanned++;
                if (filter is null || JobFilterMatcher.Matches(entry, filter))
                {
                    matches.Add(entry);
                    taken++;
                    if (taken >= count)
                    {
                        break;
                    }
                }
            }

            cursor += page.Count;
        }

        var truncated = taken >= count && cursor < stateCount;
        return new ScanResult(matches, scanned, stateCount, truncated, cursor);
    }

    public ScanResult ScanByFilter(JobFilter filter, int max)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.State is null)
        {
            throw new ArgumentException("'filter.state' is required for bulk operations.");
        }
        return ListJobsForState(filter.State.Value, filter, 0, max);
    }

    private static readonly JobStateKind[] AllStates =
    {
        JobStateKind.Enqueued,
        JobStateKind.Processing,
        JobStateKind.Scheduled,
        JobStateKind.Failed,
        JobStateKind.Succeeded,
        JobStateKind.Deleted,
    };

    private long StateCount(JobStateKind state) =>
        state switch
        {
            JobStateKind.Enqueued => Api.Queues().Sum(q => Api.EnqueuedCount(q.Name)),
            JobStateKind.Processing => Api.ProcessingCount(),
            JobStateKind.Scheduled => Api.ScheduledCount(),
            JobStateKind.Failed => Api.FailedCount(),
            JobStateKind.Succeeded => Api.SucceededListCount(),
            JobStateKind.Deleted => Api.DeletedListCount(),
            _ => 0,
        };

    private IReadOnlyList<JobMatch> FetchPage(JobStateKind state, int from, int count) =>
        state switch
        {
            JobStateKind.Enqueued => FetchEnqueued(from, count),
            JobStateKind.Processing => Project(
                Api.ProcessingJobs(from, count),
                state,
                d => d.Job,
                _ => null,
                _ => null,
                _ => null,
                d => d.StartedAt
            ),
            JobStateKind.Scheduled => Project(
                Api.ScheduledJobs(from, count),
                state,
                d => d.Job,
                _ => null,
                _ => null,
                _ => null,
                d => d.ScheduledAt
            ),
            JobStateKind.Failed => Project(
                Api.FailedJobs(from, count),
                state,
                d => d.Job,
                d => d.Reason,
                d => d.ExceptionType,
                d => d.ExceptionMessage,
                d => d.FailedAt
            ),
            JobStateKind.Succeeded => Project(
                Api.SucceededJobs(from, count),
                state,
                d => d.Job,
                _ => null,
                _ => null,
                _ => null,
                d => d.SucceededAt
            ),
            JobStateKind.Deleted => Project(
                Api.DeletedJobs(from, count),
                state,
                d => d.Job,
                _ => null,
                _ => null,
                _ => null,
                d => d.DeletedAt
            ),
            _ => Array.Empty<JobMatch>(),
        };

    private IReadOnlyList<JobMatch> FetchEnqueued(int from, int count)
    {
        var results = new List<JobMatch>();
        var skip = from;
        var take = count;
        foreach (var queue in Api.Queues())
        {
            if (take <= 0)
            {
                break;
            }
            var len = (int)Api.EnqueuedCount(queue.Name);
            if (skip >= len)
            {
                skip -= len;
                continue;
            }
            var pageCount = Math.Min(take, len - skip);
            foreach (var kvp in Api.EnqueuedJobs(queue.Name, skip, pageCount))
            {
                results.Add(
                    new JobMatch(
                        kvp.Key,
                        kvp.Value.Job,
                        queue.Name,
                        null,
                        null,
                        null,
                        JobStateKind.Enqueued
                    )
                );
            }
            take -= pageCount;
            skip = 0;
        }
        return results;
    }

    private static IReadOnlyList<JobMatch> Project<T>(
        JobList<T> list,
        JobStateKind state,
        Func<T, Job?> getJob,
        Func<T, string?> getReason,
        Func<T, string?> getExceptionType,
        Func<T, string?> getExceptionMessage,
        Func<T, DateTime?> getAt
    )
    {
        var results = new List<JobMatch>(list.Count);
        foreach (var kvp in list)
        {
            var dto = kvp.Value;
            var job = getJob(dto);
            results.Add(
                new JobMatch(
                    kvp.Key,
                    job,
                    job?.Queue,
                    getReason(dto),
                    getExceptionType(dto),
                    getExceptionMessage(dto),
                    state,
                    getAt(dto)
                )
            );
        }
        return results;
    }
}

public sealed record JobMatch(
    string Id,
    Job? Job,
    string? Queue,
    string? Reason,
    string? ExceptionType,
    string? ExceptionMessage,
    JobStateKind State,
    DateTime? At = null
);

public sealed record ScanResult(
    IReadOnlyList<JobMatch> Matches,
    int Scanned,
    long StateTotal,
    bool Truncated,
    int NextFrom
);

public sealed record StatisticsResult(
    DateTime Now,
    StatisticsDto Counts,
    Trend24h Trend,
    int FailedInWindow,
    IReadOnlyList<FailureGroup> FailureGroups,
    IReadOnlyList<string> RecentFailedIds,
    IReadOnlyList<ServerInfo> Servers,
    int WindowHours
);

public sealed record Trend24h(long Succeeded, long Failed);

public sealed record FailureGroup(
    string Type,
    string Exception,
    int Count,
    string SampleId,
    DateTime? OldestAt,
    DateTime? NewestAt
);

public sealed record ServerInfo(
    string Name,
    DateTime? Heartbeat,
    int WorkersCount,
    IList<string> Queues,
    DateTime StartedAt
);
