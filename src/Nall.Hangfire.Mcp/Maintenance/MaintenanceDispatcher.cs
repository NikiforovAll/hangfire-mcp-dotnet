using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;

namespace Nall.Hangfire.Mcp.Maintenance;

public sealed class MaintenanceDispatcher
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly MaintenanceQueryService _query;
    private readonly MaintenanceCommandService _commands;

    public MaintenanceDispatcher(MaintenanceQueryService query, MaintenanceCommandService commands)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(commands);
        _query = query;
        _commands = commands;
    }

    public CallToolResult Invoke(CallToolRequestParams? @params)
    {
        var name = @params?.Name;
        if (name is null)
        {
            return Error("Missing tool name.");
        }

        try
        {
            var args = @params?.Arguments?.AsReadOnly();
            return name switch
            {
                MaintenanceTools.GetStatistics => HandleGetStatistics(args),
                MaintenanceTools.ListJobs => HandleListJobs(args),
                MaintenanceTools.GetJob => HandleGetJob(args),
                MaintenanceTools.DeleteJob => HandleDeleteJob(args),
                MaintenanceTools.RequeueJob => HandleRequeueJob(args),
                MaintenanceTools.DeleteJobs => HandleBulk(args, delete: true),
                MaintenanceTools.RequeueJobs => HandleBulk(args, delete: false),
                _ => Error($"Unknown maintenance tool '{name}'."),
            };
        }
        catch (Exception ex)
            when (ex is ArgumentException or InvalidOperationException or JsonException)
        {
            return Error(ex.Message);
        }
    }

    private CallToolResult HandleGetStatistics(IReadOnlyDictionary<string, JsonElement>? args)
    {
        var windowHours = ReadIntInRange(args, "windowHours", 24, 1, 168);
        var recentLimit = ReadIntInRange(args, "recentLimit", 20, 1, 100);
        var groupScan = ReadIntInRange(args, "groupScan", 100, 1, 500);
        var rich = _query.GetStatisticsRich(windowHours, recentLimit, groupScan);
        return Json(ProjectStatisticsRich(rich));
    }

    private static int ReadIntInRange(
        IReadOnlyDictionary<string, JsonElement>? args,
        string key,
        int @default,
        int min,
        int max
    )
    {
        var value = ReadInt(args, key) ?? @default;
        if (value < min || value > max)
        {
            throw new ArgumentException($"'{key}' must be between {min} and {max}.");
        }
        return value;
    }

    private CallToolResult HandleListJobs(IReadOnlyDictionary<string, JsonElement>? args)
    {
        var state = ReadEnum<JobStateKind>(args, "state");
        var filter = ReadFilter(args, "filter", state);
        var from = ReadInt(args, "from") ?? 0;
        var count = ReadInt(args, "count") ?? 50;
        if (count is < 1 or > 100)
        {
            throw new ArgumentException("'count' must be between 1 and 100.");
        }
        var scan = _query.ListJobs(state, filter, from, count);
        return Json(
            new
            {
                scan.Scanned,
                scan.StateTotal,
                scan.Truncated,
                scan.NextFrom,
                Jobs = scan.Matches.Select(ProjectMatch).ToArray(),
            }
        );
    }

    private CallToolResult HandleGetJob(IReadOnlyDictionary<string, JsonElement>? args)
    {
        var id = ReadString(args, "id") ?? throw new ArgumentException("'id' is required.");
        var dto = _query.GetJob(id);
        if (dto is null)
        {
            return Error($"Job '{id}' not found.");
        }
        return Json(
            new
            {
                Id = id,
                Type = dto.Job?.Type?.FullName,
                Method = dto.Job?.Method?.Name,
                Args = dto.Job?.Args?.Select(SafeRenderArg).ToArray(),
                Properties = dto.Properties,
                ExpireAt = dto.ExpireAt,
                CreatedAt = dto.CreatedAt,
                History = dto
                    .History.Select(h => new
                    {
                        h.StateName,
                        h.Reason,
                        h.CreatedAt,
                        h.Data,
                    })
                    .ToArray(),
            }
        );
    }

    private CallToolResult HandleDeleteJob(IReadOnlyDictionary<string, JsonElement>? args)
    {
        var id = ReadString(args, "id") ?? throw new ArgumentException("'id' is required.");
        return Json(_commands.DeleteOne(id));
    }

    private CallToolResult HandleRequeueJob(IReadOnlyDictionary<string, JsonElement>? args)
    {
        var id = ReadString(args, "id") ?? throw new ArgumentException("'id' is required.");
        return Json(_commands.RequeueOne(id));
    }

    private CallToolResult HandleBulk(IReadOnlyDictionary<string, JsonElement>? args, bool delete)
    {
        var ids = ReadStringArray(args, "ids");
        var filter = ReadFilter(args, "filter", state: null);
        var dryRun = ReadBool(args, "dryRun") ?? false;
        var result = delete
            ? _commands.DeleteMany(ids, filter, dryRun)
            : _commands.RequeueMany(ids, filter, dryRun);
        return Json(result);
    }

    private static JobFilter? ReadFilter(
        IReadOnlyDictionary<string, JsonElement>? args,
        string key,
        JobStateKind? state
    )
    {
        if (
            args is null
            || !args.TryGetValue(key, out var el)
            || el.ValueKind is not JsonValueKind.Object
        )
        {
            return null;
        }
        var filterState = ReadEnum<JobStateKind>(el, "state") ?? state;
        return new JobFilter
        {
            State = filterState,
            Queue = ReadStringProp(el, "queue"),
            JobType = ReadStringProp(el, "jobType"),
            Method = ReadStringProp(el, "method"),
            MessageContains = ReadStringProp(el, "messageContains"),
            ExceptionContains = ReadStringProp(el, "exceptionContains"),
            Since = ReadDateTimeProp(el, "since"),
            Until = ReadDateTimeProp(el, "until"),
        };
    }

    private static string? ReadString(IReadOnlyDictionary<string, JsonElement>? args, string key) =>
        args is not null && args.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static string? ReadStringProp(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static DateTime? ReadDateTimeProp(JsonElement obj, string key)
    {
        if (
            !obj.TryGetProperty(key, out var v)
            || v.ValueKind != JsonValueKind.String
            || v.GetString() is not { } s
        )
        {
            return null;
        }
        return DateTime.TryParse(
            s,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var dt
        )
            ? dt
            : null;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, JsonElement>? args, string key) =>
        args is not null && args.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32()
            : null;

    private static bool? ReadBool(IReadOnlyDictionary<string, JsonElement>? args, string key)
    {
        if (args is null || !args.TryGetValue(key, out var v))
        {
            return null;
        }
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static IReadOnlyCollection<string>? ReadStringArray(
        IReadOnlyDictionary<string, JsonElement>? args,
        string key
    )
    {
        if (args is null || !args.TryGetValue(key, out var v) || v.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var list = new List<string>(v.GetArrayLength());
        foreach (var item in v.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString()!);
            }
        }
        return list;
    }

    private static T? ReadEnum<T>(IReadOnlyDictionary<string, JsonElement>? args, string key)
        where T : struct, Enum =>
        ReadString(args, key) is { } s && Enum.TryParse<T>(s, ignoreCase: true, out var v)
            ? v
            : null;

    private static T? ReadEnum<T>(JsonElement obj, string key)
        where T : struct, Enum =>
        ReadStringProp(obj, key) is { } s && Enum.TryParse<T>(s, ignoreCase: true, out var v)
            ? v
            : null;

    private static object ProjectStatisticsRich(StatisticsResult r) =>
        new
        {
            now = r.Now,
            counts = new
            {
                r.Counts.Servers,
                r.Counts.Queues,
                r.Counts.Enqueued,
                r.Counts.Failed,
                r.Counts.Processing,
                r.Counts.Scheduled,
                r.Counts.Succeeded,
                r.Counts.Deleted,
                r.Counts.Recurring,
                r.Counts.Retries,
            },
            trend24h = new { r.Trend.Succeeded, r.Trend.Failed },
            r.WindowHours,
            r.FailedInWindow,
            failureGroups = r.FailureGroups.Count == 0 ? null : (object)r.FailureGroups,
            recentFailedIds = r.RecentFailedIds.Count == 0 ? null : (object)r.RecentFailedIds,
            servers = r
                .Servers.Select(s => new
                {
                    s.Name,
                    s.Heartbeat,
                    s.WorkersCount,
                    s.Queues,
                    s.StartedAt,
                })
                .ToArray(),
        };

    private static object ProjectMatch(JobMatch m) =>
        new
        {
            m.Id,
            State = m.State.ToString(),
            m.At,
            Type = m.Job?.Type?.FullName,
            Method = m.Job?.Method?.Name,
            m.Queue,
            m.Reason,
            m.ExceptionType,
            m.ExceptionMessage,
        };

    private static object? SafeRenderArg(object? arg)
    {
        if (arg is null)
        {
            return null;
        }
        if (arg is CancellationToken)
        {
            return "<CancellationToken>";
        }
        try
        {
            return JsonSerializer.SerializeToElement(arg, s_json);
        }
        catch (NotSupportedException)
        {
            return arg.GetType().FullName;
        }
    }

    private static CallToolResult Json(object payload) =>
        new()
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(payload, s_json) }],
        };

    private static CallToolResult Error(string message) =>
        new() { Content = [new TextContentBlock { Text = message }], IsError = true };
}
