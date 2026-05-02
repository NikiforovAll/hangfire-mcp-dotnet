using System.Text;
using ModelContextProtocol.Protocol;
using Nall.Hangfire.Mcp.Maintenance;

namespace Nall.Hangfire.Mcp.Prompts;

public static class MaintenancePrompts
{
    public const string HealthCheck = "hangfire_health_check";
    public const string TriageFailures = "hangfire_triage_failures";
    public const string Discover = "hangfire_discover";

    public static IReadOnlyList<Prompt> All { get; } = Build();

    public static bool IsKnown(string? name) => name is HealthCheck or TriageFailures or Discover;

    public static GetPromptResult Render(string name, JobCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return name switch
        {
            HealthCheck => RenderHealthCheck(),
            TriageFailures => RenderTriageFailures(),
            Discover => RenderDiscover(catalog),
            _ => throw new ArgumentException($"Unknown prompt '{name}'.", nameof(name)),
        };
    }

    private static IReadOnlyList<Prompt> Build() =>
        [
            new Prompt
            {
                Name = Discover,
                Title = "Discover Hangfire MCP capabilities",
                Description =
                    "Mindmap of what this MCP server exposes: operational prompts and discovered Hangfire jobs that can be run.",
            },
            new Prompt
            {
                Name = HealthCheck,
                Title = "Hangfire health check",
                Description =
                    "Summarize Hangfire backlog, failure rate and server/queue anomalies into a one-screen operational report.",
            },
            new Prompt
            {
                Name = TriageFailures,
                Title = "Triage failed jobs",
                Description =
                    "Investigate failed jobs grouped by job type and exception, then recommend requeue vs delete (with dryRun-then-confirm safety pattern).",
            },
        ];

    private static GetPromptResult RenderHealthCheck()
    {
        const string text = """
            You are a Hangfire operations assistant. Produce a one-screen health report by following these steps:

            1. Call tool `hangfire_get_statistics` to retrieve global counters.
            2. Call tool `hangfire_list_queues` to inspect per-queue lengths.
            3. Synthesize a structured report with these sections:
               - **Backlog**: Enqueued + Scheduled totals; flag if any single queue length exceeds 1000.
               - **Failures**: Failed count and Retries; flag if Failed > 50 or > 1% of Succeeded.
               - **Throughput**: Processing count vs Servers; flag if Processing > 0 but Servers = 0.
               - **Recurring**: Recurring count.
               - **Verdict**: a single line — "Healthy", "Degraded", or "Unhealthy" — with the dominant reason.

            Do not call any destructive tool (`hangfire_delete_*`, `hangfire_requeue_*`). This prompt is read-only.
            """;
        return TextUserPrompt("Hangfire operational health summary.", text);
    }

    private static GetPromptResult RenderTriageFailures()
    {
        const string text = """
            You are a Hangfire failure-triage assistant. Investigate failed jobs and recommend a remediation per failure group.

            Steps:
            1. Call `hangfire_list_jobs` with `state="Failed"`, `count=100`. Optionally narrow with `filter.jobType` if the user named a specific job.
            2. Group results by `(jobType, exceptionType, first 80 chars of exceptionMessage)`. Sort by group size descending.
            3. For each group, call `hangfire_get_job` on one representative id to fetch full state history.
            4. Classify each group:
               - **Transient** (timeout, network, 5xx, deadlock) → recommend **requeue**.
               - **Poison** (deserialization, ArgumentException on stable input, 4xx with stable args) → recommend **delete**.
               - **Unknown** → recommend manual investigation, do not act.
            5. Present a markdown table: jobType | exception | count | recommendation | reasoning.
            6. SAFETY: Before any destructive action, you MUST first call `hangfire_delete_jobs` or `hangfire_requeue_jobs` with `dryRun=true` and the same filter, show the matched ids to the user, and explicitly ask for confirmation. Only call again with `dryRun=false` after the user confirms.
            """;
        return TextUserPrompt("Triage Hangfire failure groups.", text);
    }

    private static GetPromptResult RenderDiscover(JobCatalog catalog)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Hangfire MCP capabilities (mindmap):");
        sb.AppendLine();
        sb.AppendLine("- Maintenance tools");
        foreach (var tool in MaintenanceTools.All)
        {
            sb.AppendLine($"  - `{tool.Name}` — {tool.Description}");
        }
        sb.AppendLine("- Run jobs (tools)");
        if (catalog.ListToolsResult.Tools.Count == 0)
        {
            sb.AppendLine("  - (no jobs discovered)");
        }
        else
        {
            foreach (var tool in catalog.ListToolsResult.Tools)
            {
                sb.AppendLine($"  - `{tool.Name}` — {tool.Description}");
            }
        }

        return TextUserPrompt(
            "Mindmap of Hangfire MCP maintenance and run-job tools.",
            sb.ToString().TrimEnd()
        );
    }

    private static GetPromptResult TextUserPrompt(string description, string text) =>
        new()
        {
            Description = description,
            Messages =
            [
                new PromptMessage
                {
                    Role = Role.User,
                    Content = new TextContentBlock { Text = text },
                },
            ],
        };
}
