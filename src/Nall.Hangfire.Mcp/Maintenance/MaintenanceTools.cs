using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Nall.Hangfire.Mcp.Maintenance;

public static class MaintenanceTools
{
    public const string Prefix = "hangfire_";

    public const string GetStatistics = "hangfire_get_statistics";
    public const string ListQueues = "hangfire_list_queues";
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
                    "Return Hangfire global statistics: counts for Enqueued/Failed/Processing/Scheduled/Succeeded/Deleted/Recurring/Retries plus Servers and Queues.",
                InputSchema = EmptyObject(),
            },
            new Tool
            {
                Name = ListQueues,
                Description =
                    "List Hangfire queues with their lengths and a sample of first enqueued jobs.",
                InputSchema = EmptyObject(),
            },
            new Tool
            {
                Name = ListJobs,
                Description =
                    "Page Hangfire jobs with optional state and filter. Omit 'state' to query across all states. Use this to discover ids before bulk delete/requeue.",
                InputSchema = ListJobsSchema(),
            },
            new Tool
            {
                Name = GetJob,
                Description =
                    "Return full details for a single Hangfire job: type, method, args, properties, and state history.",
                InputSchema = JobIdSchema(),
            },
            new Tool
            {
                Name = DeleteJob,
                Description = "Move a single job to the Deleted state.",
                InputSchema = JobIdSchema(),
            },
            new Tool
            {
                Name = RequeueJob,
                Description =
                    "Requeue a single job back to Enqueued (covers retry of Failed jobs).",
                InputSchema = JobIdSchema(),
            },
            new Tool
            {
                Name = DeleteJobs,
                Description =
                    "Bulk delete jobs by explicit id list OR filter (exactly one). RECOMMENDED: pass dryRun:true first to preview matches before acting. Capped by MaintenanceMaxBulkSize.",
                InputSchema = BulkSchema(),
            },
            new Tool
            {
                Name = RequeueJobs,
                Description =
                    "Bulk requeue jobs by explicit id list OR filter (exactly one). RECOMMENDED: pass dryRun:true first to preview matches before acting. Capped by MaintenanceMaxBulkSize.",
                InputSchema = BulkSchema(),
            },
        };

    private static JsonElement EmptyObject() =>
        ToElement(new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() });

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
