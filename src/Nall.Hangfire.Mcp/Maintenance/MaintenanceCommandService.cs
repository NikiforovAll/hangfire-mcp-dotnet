using Hangfire;

namespace Nall.Hangfire.Mcp.Maintenance;

public sealed class MaintenanceCommandService
{
    private readonly IBackgroundJobClient _client;
    private readonly MaintenanceQueryService _query;
    private readonly int _maxBulkSize;

    public MaintenanceCommandService(
        IBackgroundJobClient client,
        MaintenanceQueryService query,
        int maxBulkSize
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(query);
        if (maxBulkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBulkSize));
        }
        _client = client;
        _query = query;
        _maxBulkSize = maxBulkSize;
    }

    public int MaxBulkSize => _maxBulkSize;

    public OpResult DeleteOne(string id) => RunOne(id, _client.Delete);

    public OpResult RequeueOne(string id) => RunOne(id, _client.Requeue);

    public BulkResult DeleteMany(
        IReadOnlyCollection<string>? ids,
        JobFilter? filter,
        bool dryRun
    ) => RunMany(ids, filter, dryRun, _client.Delete);

    public BulkResult RequeueMany(
        IReadOnlyCollection<string>? ids,
        JobFilter? filter,
        bool dryRun
    ) => RunMany(ids, filter, dryRun, _client.Requeue);

    private static OpResult RunOne(string id, Func<string, bool> op)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        try
        {
            var ok = op(id);
            return new OpResult(id, ok, null);
        }
        catch (Exception ex)
        {
            return new OpResult(id, false, ex.Message);
        }
    }

    private BulkResult RunMany(
        IReadOnlyCollection<string>? ids,
        JobFilter? filter,
        bool dryRun,
        Func<string, bool> op
    )
    {
        var bothNull = ids is null && filter is null;
        var bothSet = ids is not null && filter is not null;
        if (bothNull || bothSet)
        {
            throw new ArgumentException("Provide exactly one of 'ids' or 'filter'.");
        }

        IReadOnlyList<string> targets;
        var truncated = false;
        var scanned = 0;
        long stateTotal = 0;

        if (ids is not null)
        {
            if (ids.Count > _maxBulkSize)
            {
                throw new ArgumentException(
                    $"ids count {ids.Count} exceeds MaintenanceMaxBulkSize {_maxBulkSize}."
                );
            }
            targets = ids.ToArray();
        }
        else
        {
            var scan = _query.ScanByFilter(filter!, _maxBulkSize);
            targets = scan.Matches.Select(m => m.Id).ToArray();
            truncated = scan.Truncated;
            scanned = scan.Scanned;
            stateTotal = scan.StateTotal;
        }

        if (dryRun)
        {
            return new BulkResult(
                targets.Select(id => new OpResult(id, true, null)).ToArray(),
                DryRun: true,
                Truncated: truncated,
                Scanned: scanned,
                StateTotal: stateTotal
            );
        }

        var results = new List<OpResult>(targets.Count);
        foreach (var id in targets)
        {
            results.Add(RunOne(id, op));
        }
        return new BulkResult(results, DryRun: false, truncated, scanned, stateTotal);
    }
}

public sealed record OpResult(string Id, bool Ok, string? Error);

public sealed record BulkResult(
    IReadOnlyList<OpResult> Results,
    bool DryRun,
    bool Truncated,
    int Scanned,
    long StateTotal
);
