using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

namespace Nall.Hangfire.Mcp.Maintenance;

public sealed class MaintenanceQueryService
{
    private const int PageSize = 1000;

    private readonly JobStorage _storage;

    public MaintenanceQueryService(JobStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        _storage = storage;
    }

    private IMonitoringApi Api => _storage.GetMonitoringApi();

    public StatisticsDto GetStatistics() => Api.GetStatistics();

    public IList<QueueWithTopEnqueuedJobsDto> ListQueues() => Api.Queues();

    public JobDetailsDto? GetJob(string id) => Api.JobDetails(id);

    public ScanResult ListJobs(JobStateKind state, JobFilter? filter, int from, int count)
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
        return ListJobs(filter.State, filter, 0, max);
    }

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
                d => d.Job,
                _ => null,
                _ => null,
                _ => null
            ),
            JobStateKind.Scheduled => Project(
                Api.ScheduledJobs(from, count),
                d => d.Job,
                _ => null,
                _ => null,
                _ => null
            ),
            JobStateKind.Failed => Project(
                Api.FailedJobs(from, count),
                d => d.Job,
                d => d.Reason,
                d => d.ExceptionType,
                d => d.ExceptionMessage
            ),
            JobStateKind.Succeeded => Project(
                Api.SucceededJobs(from, count),
                d => d.Job,
                _ => null,
                _ => null,
                _ => null
            ),
            JobStateKind.Deleted => Project(
                Api.DeletedJobs(from, count),
                d => d.Job,
                _ => null,
                _ => null,
                _ => null
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
                results.Add(new JobMatch(kvp.Key, kvp.Value.Job, queue.Name, null, null, null));
            }
            take -= pageCount;
            skip = 0;
        }
        return results;
    }

    private static IReadOnlyList<JobMatch> Project<T>(
        JobList<T> list,
        Func<T, Job?> getJob,
        Func<T, string?> getReason,
        Func<T, string?> getExceptionType,
        Func<T, string?> getExceptionMessage
    )
    {
        var results = new List<JobMatch>(list.Count);
        foreach (var kvp in list)
        {
            var dto = kvp.Value;
            results.Add(
                new JobMatch(
                    kvp.Key,
                    getJob(dto),
                    getJob(dto)?.Queue,
                    getReason(dto),
                    getExceptionType(dto),
                    getExceptionMessage(dto)
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
    string? ExceptionMessage
);

public sealed record ScanResult(
    IReadOnlyList<JobMatch> Matches,
    int Scanned,
    long StateTotal,
    bool Truncated,
    int NextFrom
);
