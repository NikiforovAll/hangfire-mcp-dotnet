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
            You are a Hangfire operations assistant. Produce a one-screen health report.

            1. Call `hangfire_get_statistics` (single call gives counters, 24h trend, servers, recent failure groups).
            2. Synthesize:
               - **Backlog**: Enqueued + Scheduled.
               - **Failures**: Failed + failedInWindow; flag dominant failureGroups.
               - **Throughput**: Processing vs servers.WorkersCount; flag Processing > 0 with no live servers.
               - **Verdict**: one line — "Healthy" | "Degraded" | "Unhealthy" + dominant reason.

            Read-only: do not call `hangfire_delete_*` or `hangfire_requeue_*`.
            """;
        return TextUserPrompt("Hangfire operational health summary.", text);
    }

    private static GetPromptResult RenderTriageFailures()
    {
        const string text = """
            You are a Hangfire failure-triage assistant. Investigate failed jobs and recommend remediation per failure group.

            Steps:
            1. Call `hangfire_get_statistics` — `failureGroups` already aggregates by (type, exception) with a sample id.
            2. For each group, call `hangfire_get_job` on the sample id for full state history. Narrow further with `hangfire_list_jobs` (state="Failed", filter.jobType / filter.exceptionContains / filter.since) only if needed.
            3. Classify:
               - **Transient** (timeout, network, 5xx, deadlock) → **requeue**.
               - **Poison** (deserialization, ArgumentException on stable input, 4xx with stable args) → **delete**.
               - **Unknown** → manual investigation, do not act.
            4. Present a markdown table: jobType | exception | count | recommendation | reasoning.
            5. SAFETY: Before any destructive call, run `hangfire_delete_jobs`/`hangfire_requeue_jobs` with `dryRun=true` and the same filter, show matched ids, and wait for explicit user confirmation before calling again with `dryRun=false`.
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
            foreach (
                var tool in catalog.ListToolsResult.Tools.OrderBy(
                    t => t.Name,
                    StringComparer.Ordinal
                )
            )
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
