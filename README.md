# Nall.Hangfire.Mcp

Remote MCP server for [Hangfire](https://www.hangfire.io/) — exposes background jobs as MCP tools, in-process with the Hangfire server.

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

## Parameter binding

For each tool call:
- C# default → used when the argument is omitted.
- Nullable type (`T?` value or annotated reference) and no default → bound to `null` when omitted.
- Otherwise required; missing argument returns an MCP error.

## Sample

`samples/Web` exercises overloads, complex objects, enums, collections, defaults, nullable optionals, and manifest-only one-shot jobs. `GET /jobs` lists the discovered catalog.

## License

MIT
