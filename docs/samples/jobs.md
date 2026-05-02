# Sample Jobs

`samples/HangfireJobs` defines job interfaces that cover the full range of supported parameter shapes — primitives, complex objects, enums, collections, defaults, and nullable optionals.

## Recurring jobs

Registered in `samples/Web/Program.cs` via `IRecurringJobManager.AddOrUpdate`:

| Recurring ID            | Interface          | Method                                                                         | Parameters                    |
| ----------------------- | ------------------ | ------------------------------------------------------------------------------ | ----------------------------- |
| `time.execute`          | `ITimeJob`         | `ExecuteAsync()`                                                               | none                          |
| `send-message.text`     | `ISendMessageJob`  | `ExecuteAsync(string text)`                                                    | one required string           |
| `send-message.envelope` | `ISendMessageJob`  | `ExecuteAsync(Message message)`                                                | one required complex object   |
| `report.generate`       | `IReportJob`       | `GenerateAsync(int year, string format = "pdf", DateTimeOffset? since = null)` | one required + two optional   |
| `data.export`           | `IDataExportJob`   | `ExportAsync(ExportFormat format, IReadOnlyList<string> tables)`               | enum + string array           |
| `notify.dispatch`       | `INotificationJob` | `NotifyAsync(string channel, string? message, int? priority)`                  | required + nullable optionals |
| `maint.rebuild-indexes` | `MaintenanceJob`   | `RebuildIndexesAsync(string schema = "public")`                                | one optional with default     |

## Manifest-only jobs

Discovered from one-shot `Enqueue` calls by the source generator. They become tools when `Sources` includes `StaticManifest` or `All`:

- `Run_INotificationJob_BroadcastAsync`
- `Run_IReportJob_PreviewAsync`
- `Run_MaintenanceJob_VacuumAsync`

## Description metadata

Sample jobs annotate parameters with `[Description]` so MCP clients see human-readable schema docs:

```csharp
public interface IReportJob
{
    [Description("Generate the annual financial report and persist it to the report store.")]
    [Authorize(Policy = "jobs:run")]
    Task GenerateAsync(
        [Description("Calendar year of the report (e.g. 2026).")] int year,
        [Description("Output file format. Supported: pdf, html, csv.")] string format = "pdf",
        [Description("Optional cutoff: only include data on or after this timestamp.")]
            DateTimeOffset? since = null
    );
}
```

## Authorization

`IReportJob.GenerateAsync` carries `[Authorize(Policy = "jobs:run")]`. The sample defines this policy as `RequireRole("admin")` — calling the tool without the role returns `Forbidden`. Assign the role in Keycloak to flip it open. See [Authentication](/authentication) for the full setup.

## Parameter schema rules

| C# signature                   | JSON Schema                                           |
| ------------------------------ | ----------------------------------------------------- |
| `string name`                  | required string                                       |
| `string format = "pdf"`        | optional string (default applied server-side)         |
| `string? tag`                  | optional string (nullable reference)                  |
| `int? priority`                | optional integer (nullable value type)                |
| `ExportFormat format`          | required string enum (`"Csv"`, `"Json"`, `"Parquet"`) |
| `IReadOnlyList<string> tables` | required array of strings                             |
| `Message message`              | required object with inferred JSON Schema             |
| `DateTimeOffset? since`        | optional string (ISO 8601)                            |
