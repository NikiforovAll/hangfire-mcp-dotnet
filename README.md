# Nall.Hangfire.Mcp

Remote MCP server for [Hangfire](https://www.hangfire.io/) — exposes background jobs as first-class MCP tools, in-process with the Hangfire server.

## Status

Early development. Successor to [`hangfire-mcp`](https://github.com/NikiforovAll/hangfire-mcp) (archived); the standalone/ad-hoc `Nall.HangfireMCP` `dotnet tool` remains available there for users on the original env-var design.

## Design at a glance

- **In-process.** MCP runs inside the same ASP.NET host that hosts the Hangfire server. Job types are already loaded; no out-of-process assembly loading.
- **Remote.** Mounted as an HTTP/SSE endpoint at `/mcp` (`MapHangfireMcp`). Any MCP client (VS Code, Claude Desktop, custom agents) can connect across the network.
- **Truly dynamic.** Discovery scans assemblies and exposes one MCP tool per discovered job method, with JSON Schema generated from `MethodInfo`. Catalog refresh emits `notifications/tools/list_changed`.
- **Zero ceremony.** No marker attributes, no shim interfaces — your existing Hangfire jobs are exposed automatically.

## Quick start (target API)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHangfire(cfg => cfg.UsePostgreSqlStorage(...));
builder.Services.AddHangfireServer();
builder.Services.AddHangfireMcp(opts =>
{
    opts.ScanAssemblies(typeof(IMyJob).Assembly);
    // optional filter:
    // opts.Where(t => t.IsInterface && t.Name.EndsWith("Job"));
});

var app = builder.Build();
app.MapHangfireDashboard();
app.MapHangfireMcp("/mcp").RequireAuthorization();
app.Run();
```

VS Code MCP config:

```json
{
  "servers": {
    "hangfire": {
      "url": "https://your-host/mcp"
    }
  }
}
```

## License

MIT
