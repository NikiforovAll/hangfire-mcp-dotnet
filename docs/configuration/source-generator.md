# Source Generator

`Nall.Hangfire.Mcp.Generator` is an optional Roslyn `IIncrementalGenerator` that scans your code at compile time for Hangfire registration call sites — `AddOrUpdate`, `Enqueue`, `Schedule` — and emits a static `JobManifestRegistry`. The runtime library consumes this manifest when `HangfireMcpOptions.Sources` includes `StaticManifest` or `All`.

Use it to expose:

- One-shot jobs that are only ever `Enqueue`d (never registered as recurring).
- Tooling/admin methods that you want surfaced as MCP tools without adding a recurring schedule.
- Jobs in projects that boot before Hangfire storage is reachable.

## Install

```bash
dotnet add package Nall.Hangfire.Mcp.Generator
```

## Wire as an analyzer

The generator must be referenced as an analyzer, not a library. Use `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`:

When consuming the NuGet package, this is set automatically — the package is published under `analyzers/dotnet/cs` with `IncludeBuildOutput=false`.

## Enable in `AddHangfireMcp`

```csharp
builder.Services.AddHangfireMcp(o =>
{
    o.Sources = JobDiscoverySources.All;
});
```

## What gets scanned

The generator walks user code for these call patterns:

- `recurringJobManager.AddOrUpdate<TJob>(id, j => j.Method(...), schedule)`
- `backgroundJobClient.Enqueue<TJob>(j => j.Method(...))`
- `backgroundJobClient.Schedule<TJob>(j => j.Method(...), delay)`

Each unique `(DeclaringType, MethodInfo)` becomes a manifest entry. Recurring entries are deduped against the runtime `RecurringStorage` source so the same job is not exposed twice.

## Constraints

The generator project itself targets `netstandard2.0`, with `EnforceExtendedAnalyzerRules=true` and `IncludeBuildOutput=false`. Keep the analyzer wiring (`OutputItemType="Analyzer" ReferenceOutputAssembly="false"`) in any consumer that uses `ProjectReference` — without it Roslyn won't run the generator.
