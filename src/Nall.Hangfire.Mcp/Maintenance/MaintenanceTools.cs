using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Nall.Hangfire.Mcp.Maintenance;

public static class MaintenanceTools
{
    public const string Prefix = "hangfire_";

    public const string GetStatistics = "hangfire_get_statistics";
    public const string ListJobs = "hangfire_list_jobs";
    public const string GetJob = "hangfire_get_job";
    public const string DeleteJob = "hangfire_delete_job";
    public const string RequeueJob = "hangfire_requeue_job";
    public const string DeleteJobs = "hangfire_delete_jobs";
    public const string RequeueJobs = "hangfire_requeue_jobs";

    public static IReadOnlyList<Tool> All { get; } = Build();

    public static bool IsMaintenance(string? toolName) =>
        toolName is not null && toolName.StartsWith(Prefix, StringComparison.Ordinal);

    private static IReadOnlyList<Tool> Build() =>
        new[]
        {
            new Tool
            {
                Name = GetStatistics,
                Description =
                    "Return Hangfire health snapshot: counters, last-24h trend (succeeded/failed), live servers (heartbeat, workers, queues), recent failure groups and ids in a configurable window. Use this as the entry point for triage",
                InputSchema = StatisticsSchema(),
            },
            new Tool
            {
                Name = ListJobs,
                Description =
                    "Page Hangfire jobs with optional state and filter. Omit 'state' to query across all states. Use this to discover ids before bulk delete/requeue",
                InputSchema = ListJobsSchema(),
            },
            new Tool
            {
                Name = GetJob,
                Description =
                    "Return full details for a single Hangfire job: type, method, args, properties, and state history",
                InputSchema = JobIdSchema(),
            },
            new Tool
            {
                Name = DeleteJob,
                Description = "Move a single job to the Deleted state",
                InputSchema = JobIdSchema(),
            },
            new Tool
            {
                Name = RequeueJob,
                Description = "Requeue a single job back to Enqueued (covers retry of Failed jobs)",
                InputSchema = JobIdSchema(),
            },
            new Tool
            {
                Name = DeleteJobs,
                Description =
                    "Bulk delete jobs by explicit id list OR filter (exactly one). RECOMMENDED: pass dryRun:true first to preview matches before acting. Capped by MaintenanceMaxBulkSize",
                InputSchema = BulkSchema(),
            },
            new Tool
            {
                Name = RequeueJobs,
                Description =
                    "Bulk requeue jobs by explicit id list OR filter (exactly one). RECOMMENDED: pass dryRun:true first to preview matches before acting. Capped by MaintenanceMaxBulkSize",
                InputSchema = BulkSchema(),
            },
        };

    private static JsonElement StatisticsSchema() =>
        ToElement(
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["windowHours"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["maximum"] = 168,
                        ["default"] = 24,
                        ["description"] =
                            "Lookback window for failedInWindow / failureGroups / recentFailedIds.",
                    },
                    ["recentLimit"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["maximum"] = 100,
                        ["default"] = 20,
                        ["description"] = "Max ids returned in recentFailedIds.",
                    },
                    ["groupScan"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["maximum"] = 500,
                        ["default"] = 100,
                        ["description"] =
                            "How many recent Failed jobs to scan when computing failureGroups.",
                    },
                },
            }
        );

    private static JsonElement JobIdSchema() =>
        ToElement(
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["id"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Hangfire job id.",
                    },
                },
                ["required"] = new JsonArray { "id" },
            }
        );

    private static JsonElement ListJobsSchema() =>
        ToElement(
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["state"] = StateEnum(),
                    ["filter"] = FilterSchema(includeState: false),
                    ["from"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 0,
                        ["default"] = 0,
                    },
                    ["count"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["maximum"] = 100,
                        ["default"] = 50,
                    },
                },
            }
        );

    private static JsonElement BulkSchema() =>
        ToElement(
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["ids"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject { ["type"] = "string" },
                        ["description"] = "Explicit job ids. Mutually exclusive with 'filter'.",
                    },
                    ["filter"] = FilterSchema(includeState: true),
                    ["dryRun"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["default"] = false,
                        ["description"] =
                            "Recommended: set true first to preview matched ids without acting.",
                    },
                },
            }
        );

    private static JsonObject FilterSchema(bool includeState)
    {
        var props = new JsonObject
        {
            ["queue"] = new JsonObject { ["type"] = "string" },
            ["jobType"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Case-insensitive substring of Job.Type.FullName.",
            },
            ["method"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Case-insensitive substring of Job.Method.Name.",
            },
            ["messageContains"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] =
                    "Substring match against state Reason / failure message (most useful for Failed).",
            },
            ["exceptionContains"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] =
                    "Substring match against ExceptionType / ExceptionMessage (Failed only).",
            },
            ["since"] = new JsonObject
            {
                ["type"] = "string",
                ["format"] = "date-time",
                ["description"] =
                    "Inclusive lower bound on the state-transition timestamp (e.g. FailedAt for Failed). ISO-8601 UTC. Jobs with no timestamp (Enqueued) are excluded when set.",
            },
            ["until"] = new JsonObject
            {
                ["type"] = "string",
                ["format"] = "date-time",
                ["description"] = "Inclusive upper bound on the state-transition timestamp.",
            },
        };
        var schema = new JsonObject { ["type"] = "object", ["properties"] = props };
        if (includeState)
        {
            props["state"] = StateEnum();
            schema["required"] = new JsonArray { "state" };
        }
        return schema;
    }

    private static JsonObject StateEnum() =>
        new()
        {
            ["type"] = "string",
            ["enum"] = new JsonArray(
                "Enqueued",
                "Processing",
                "Scheduled",
                "Failed",
                "Succeeded",
                "Deleted"
            ),
        };

    private static JsonElement ToElement(JsonNode node) => JsonSerializer.SerializeToElement(node);
}
