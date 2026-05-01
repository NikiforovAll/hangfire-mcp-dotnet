# CLAUDE.md

## Project

`Nall.Hangfire.Mcp` — a remote MCP server that exposes Hangfire background jobs as MCP tools, in-process with the ASP.NET host that runs Hangfire. Streamable HTTP endpoint at `/mcp`. Targets `net10.0`. The companion `Nall.Hangfire.Mcp.Generator` (netstandard2.0 Roslyn source generator) scans `AddOrUpdate` / `Enqueue` / `Schedule` call sites at compile time to build a static job manifest.

## Commands

Solution file: `hangfire-mcp-dotnet.slnx`.

```pwsh
dotnet restore hangfire-mcp-dotnet.slnx
dotnet build   hangfire-mcp-dotnet.slnx -p:WarningLevel=0 /clp:ErrorsOnly
dotnet test    hangfire-mcp-dotnet.slnx --no-build
```

Run a single test:

```pwsh
dotnet test tests/Nall.Hangfire.Mcp.Tests --filter "FullyQualifiedName~JobCatalogTests.MethodName"
```

Run the sample (Aspire AppHost orchestrates Hangfire + Web + dependencies):

```pwsh
aspire run            # uses aspire.config.json -> samples/AppHost
# or directly:
dotnet run --project samples/Web
```

Build rule (from global instructions): always pass `-p:WarningLevel=0 /clp:ErrorsOnly`.

If Aspire is running, the `Web` resource locks DLLs and full-solution builds fail. For a full build: `aspire stop` → `dotnet build ...` → `aspire start`. (For changes to a single .NET resource, prefer `aspire resource <name> rebuild` instead of stopping the AppHost.)

## Architecture

Pipeline from a Hangfire job to an MCP tool:

1. **Discovery** (`JobDiscoverySources` + `JobScanner`): two sources, configurable via `HangfireMcpOptions.Sources`:
   - `RecurringStorage` (default) — reads `RecurringJobDto.Job` from `JobStorage`.
   - `StaticManifest` — reads entries the Roslyn generator emitted into `JobManifestRegistry` (in `Manifest/`).
   - `All` — union, deduped by `(DeclaringType, MethodInfo)`.
   `HangfireMcpOptions.Filter` filters recurring entries before scanning.
2. **Catalog** (`JobCatalog`): resolves each entry to a `JobDescriptor` (method, parameters, generated tool name like `Run_<jobId>`). Built once as a singleton.
3. **Schema** (`JobInputSchema` + `ParameterNullability`): generates JSON Schema per `MethodInfo`. Required vs optional respects C# defaults *and* nullable annotations (NRT for reference types, `T?` for value types).
4. **MCP wiring** (`HangfireMcpServiceCollectionExtensions.AddHangfireMcp` → `HangfireMcpEndpointRouteBuilderExtensions.MapHangfireMcp`): registers `JobCatalog` + `HangfireDynamicScheduler` as singletons, then registers MCP `ListTools` / `CallTool` handlers on `AddMcpServer().WithHttpTransport(stateless: true)`.
5. **Invocation** (`HangfireMcpHandlers` + `JobArgumentBinder`): on `CallTool`, the binder converts the JSON arg dict into a typed object array via `MethodInfo.GetParameters()`, applying defaults and nullability rules. `HangfireDynamicScheduler` then enqueues via `IBackgroundJobClient` with a dynamically-built `Job` instance (no shim interfaces, no attributes).

### Source generator

`Nall.Hangfire.Mcp.Generator/HangfireRegistrationGenerator.cs` is a Roslyn `IIncrementalGenerator`. It walks user code for Hangfire registration call sites and emits a `JobManifestRegistry` partial that `Nall.Hangfire.Mcp` consumes at runtime when `Sources` includes `StaticManifest`. Important constraints: netstandard2.0, `EnforceExtendedAnalyzerRules=true`, and `IncludeBuildOutput=false` (packed under `analyzers/dotnet/cs`). The tests project references the generator with `OutputItemType="Analyzer" ReferenceOutputAssembly="false"` — keep that pattern when wiring new consumers.

### Samples

`samples/HangfireJobs` defines job interfaces (overloads, complex objects, enums, collections, defaults, nullable optionals). `samples/Web` is the host (registers Hangfire, dashboard, MCP, recurring jobs, `GET /jobs` to dump the catalog). `samples/AppHost` is the Aspire orchestrator.
