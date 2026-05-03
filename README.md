# Nall.Hangfire.Mcp

[![NuGet](https://img.shields.io/nuget/v/Nall.Hangfire.Mcp)](https://www.nuget.org/packages/Nall.Hangfire.Mcp)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Nall.Hangfire.Mcp)](https://www.nuget.org/packages/Nall.Hangfire.Mcp)
[![Build](https://github.com/NikiforovAll/hangfire-mcp-dotnet/actions/workflows/build.yml/badge.svg)](https://github.com/NikiforovAll/hangfire-mcp-dotnet/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)

Remote MCP server for [Hangfire](https://www.hangfire.io/) — exposes background jobs as MCP tools, in-process with the Hangfire server.

📖 **Documentation:** <https://nikiforovall.github.io/hangfire-mcp-dotnet/>

## Design

- **In-process.** Runs inside the ASP.NET host that runs Hangfire. No out-of-process assembly loading.
- **Remote.** Streamable HTTP endpoint at `/mcp`. Any MCP client (VS Code, Claude Desktop, custom agents) can connect.
- **Zero ceremony.** No attributes, no shim interfaces — discovery reads what you already register with Hangfire.
- **Schema from `MethodInfo`.** JSON Schema generated per method. Required vs. optional respects both C# defaults and nullable annotations (`int?`, `string?`).

## Getting started

Install:

```bash
dotnet add package Nall.Hangfire.Mcp
```

Minimum host setup — three lines on top of an existing Hangfire app:

```csharp
builder.Services.AddHangfireMcp();   // registers MCP server + JobCatalog
var app = builder.Build();
app.MapHangfireMcp("/mcp");          // streamable HTTP endpoint
```

Full minimal example:

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

VS Code MCP config:

```json
{
  "servers": {
    "hangfire": { "url": "https://your-host/mcp" }
  }
}
```

## Discovery sources

| Source                       | What it sees                                                                                                                            | When to use                                                                                    |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| `RecurringStorage` (default) | `RecurringJobDto.Job` from Hangfire storage.                                                                                            | Every recurring job is a tool.                                                                 |
| `StaticManifest`             | Compile-time scan of `AddOrUpdate` / `Enqueue` / `Schedule` call sites via the optional `Nall.Hangfire.Mcp.Generator` source generator. | Expose helper methods you only ever one-shot enqueue, or jobs not yet registered as recurring. |
| `All`                        | Union of both, deduped by `(DeclaringType, MethodInfo)`.                                                                                | Most apps.                                                                                     |

Configure via `AddHangfireMcp`:

```csharp
builder.Services.AddHangfireMcp(o =>
{
    o.Sources = JobDiscoverySources.All;          // default: RecurringStorage
    o.Filter  = rj => rj.Id.StartsWith("public."); // optional storage filter
});
```

To populate the manifest, install the generator package in each project that contains Hangfire registration calls:

```bash
dotnet add package Nall.Hangfire.Mcp.Generator
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

Filter shape (used by `list_jobs`, `delete_jobs`, `requeue_jobs`):

```json
{
  "state": "Failed",
  "queue": "default",
  "jobType": "ReportJob",
  "method": "Generate",
  "messageContains": "timeout",
  "exceptionContains": "SqlException"
}
```

`jobType` and `method` are case-insensitive substring matches. `messageContains` / `exceptionContains` are most useful for `Failed`.

## Parameter binding

For each tool call:
- C# default → used when the argument is omitted.
- Nullable type (`T?` value or annotated reference) and no default → bound to `null` when omitted.
- Otherwise required; missing argument returns an MCP error.

## Authentication

`MapHangfireMcp` returns `IEndpointConventionBuilder` — the library is auth-agnostic. Chain any ASP.NET Core auth scheme:

```csharp
app.MapHangfireMcp("/mcp")
   .RequireAuthorization(p => p.RequireAuthenticatedUser()
       .AddAuthenticationSchemes(McpAuthenticationDefaults.AuthenticationScheme));
```

- **OAuth 2.1 / OIDC** — `samples/Web` wires Keycloak + JwtBearer + `AddMcp()` from `ModelContextProtocol.AspNetCore.Authentication` to advertise RFC 9728 protected-resource-metadata. End-to-end flow, standards, and gotchas: [docs/authentication.md](docs/authentication.md).
- **API keys / custom schemes** — nothing MCP-specific required. Implement an `AuthenticationHandler<T>`, register it, and pass its scheme to `RequireAuthorization` above. The `Run_*` and `hangfire_*` tools work the same regardless of how the principal got there.

## Sample

[`samples/Web`](samples/Web/Program.cs) exercises overloads, complex objects, enums, collections, defaults, nullable optionals, and manifest-only one-shot jobs. `GET /jobs` lists the discovered catalog.

## License

MIT
