# Options Reference

`HangfireMcpOptions` is configured via the delegate passed to `AddHangfireMcp`:

```csharp
builder.Services.AddHangfireMcp(o =>
{
    o.Sources = JobDiscoverySources.All;
    o.Filter  = rj => !rj.Id.StartsWith("internal.");
});
```

| Property  | Type                              | Default                            | Purpose                                                                                                       |
| --------- | --------------------------------- | ---------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| `Sources` | `JobDiscoverySources` (flags)     | `RecurringStorage`                 | Which discovery source(s) to use. Combine with `\|` or use `All`.                                             |
| `Filter`  | `Func<RecurringJobDto, bool>?`    | `null` (no filter)                 | Predicate applied to recurring entries before scanning. Returning `false` excludes the job from the catalog. |

## `JobDiscoverySources`

```csharp
[Flags]
public enum JobDiscoverySources
{
    RecurringStorage = 1,
    StaticManifest   = 2,
    All              = RecurringStorage | StaticManifest,
}
```

## Parameter binding rules

These rules apply to every tool the catalog produces:

- **C# default** is used when the argument is omitted from the tool call.
- **Nullable type** (`T?` value or annotated reference) without a default → bound to `null` when omitted.
- **Otherwise required** — a missing argument returns an MCP error.

### Schema mapping

| C# signature                   | JSON Schema                                           |
| ------------------------------ | ----------------------------------------------------- |
| `string name`                  | required string                                       |
| `string format = "pdf"`        | optional string (default applied server-side)         |
| `string? tag`                  | optional string (nullable reference)                  |
| `int? priority`                | optional integer (nullable value type)                |
| `ExportFormat format`          | required string enum                                  |
| `IReadOnlyList<string> tables` | required array of strings                             |
| `Message message`              | required object with inferred JSON Schema             |
| `DateTimeOffset? since`        | optional string (ISO 8601)                            |

## Endpoint mapping

`MapHangfireMcp(path)` returns an `IEndpointConventionBuilder` so you can chain ASP.NET Core conventions — most importantly `RequireAuthorization`. See [Authentication](/authentication).
