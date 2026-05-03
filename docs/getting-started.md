# Getting Started

## Install

```bash
dotnet add package Nall.Hangfire.Mcp
```

## Minimum host setup

Three lines on top of an existing Hangfire app:

```csharp
builder.Services.AddHangfireMcp();   // registers MCP server + JobCatalog
var app = builder.Build();
app.MapHangfireMcp("/mcp");          // streamable HTTP endpoint
```

## Full minimal example

```csharp
using Hangfire;
using Nall.Hangfire.Mcp;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHangfire(cfg => cfg.UsePostgreSqlStorage(...));
builder.Services.AddHangfireServer();
builder.Services.AddHangfireMcp();

var app = builder.Build();
app.MapHangfireDashboard();
app.MapHangfireMcp("/mcp");

app.Services.GetRequiredService<IRecurringJobManager>()
    .AddOrUpdate<IReportJob>("report.daily", j => j.GenerateAsync(2026, "pdf", null), Cron.Daily);

app.Run();
```

Every recurring job is now an MCP tool: `Run_report.daily` with a JSON Schema derived from `GenerateAsync`'s parameters.

## Connect a client

VS Code MCP config:

```json
{
  "servers": {
    "hangfire": { "url": "https://your-host/mcp" }
  }
}
```

## Built-in maintenance tools

Every MCP server hosted by `AddHangfireMcp()` also exposes a fixed set of `hangfire_*` tools for inspecting and managing jobs alongside the dynamic `Run_*` tools:

| Tool                      | Purpose                                                                                            |
| ------------------------- | -------------------------------------------------------------------------------------------------- |
| `hangfire_get_statistics` | Global counters: Enqueued/Failed/Processing/Scheduled/Succeeded/Deleted/Recurring/Retries/Servers. |
| `hangfire_list_jobs`      | Page jobs by `state` with optional filter. Use this to discover ids before bulk ops.               |
| `hangfire_get_job`        | Full details + state history for one id.                                                           |
| `hangfire_delete_job`     | Move one job to Deleted.                                                                           |
| `hangfire_requeue_job`    | Requeue one job (covers retry of Failed).                                                          |
| `hangfire_delete_jobs`    | Bulk delete by `ids` **or** `filter` (exactly one).                                                |
| `hangfire_requeue_jobs`   | Bulk requeue by `ids` **or** `filter`.                                                             |

## Where to next?

- [User Guide](/user-guide) — sample walkthrough with the MCP Inspector and Hangfire dashboard.
- [Discovery Sources](/configuration/sources) — recurring storage, compile-time manifest, or both.
- [Authentication](/authentication) — secure the `/mcp` endpoint with OAuth 2.1 / OIDC.
