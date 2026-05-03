using System.ComponentModel;
using Hangfire;

namespace Nall.Hangfire.Mcp.Tests.Fixtures;

public interface IEmailJob
{
    [Description("Send a transactional email to the given recipient.")]
    Task SendAsync(string to);

    Task SendAsync(string to, string subject);
}

public class ReportJob
{
    [Description("Generate the annual report.")]
    public Task GenerateAsync(int year, string format = "pdf") => Task.CompletedTask;

    public Task<int> CountRowsAsync() => Task.FromResult(0);
}

public class NullableJob
{
    public Task RunAsync(int required, int? optionalValue, string? optionalRef) =>
        Task.CompletedTask;

    public Task RefOnlyAsync(string requiredRef, string? optionalRef) => Task.CompletedTask;
}

public interface IDescribedJob
{
    [Description("Iface-level method description.")]
    Task RunAsync([Description("Iface-level param description.")] string name, int count);
}

public class DescribedJob : IDescribedJob
{
    public Task RunAsync(string name, int count) => Task.CompletedTask;
}

public class DirectlyDescribedJob
{
    [Description("Direct method description.")]
    public Task RunAsync([Description("Direct param description.")] string value) =>
        Task.CompletedTask;
}

public class UndescribedJob
{
    public Task RunAsync(string value) => Task.CompletedTask;
}

public class OpenJob
{
    public Task RunAsync() => Task.CompletedTask;
}

public class CancellationTokenJob
{
    public Task RunAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task TokenOnlyAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public class SameArityOverloadsJob
{
    public Task RunAsync(int value) => Task.CompletedTask;

    public Task RunAsync(string value) => Task.CompletedTask;
}

public class MixedOverloadsJob
{
    public static Task DoAsync() => Task.CompletedTask;

    public Task DoAsync(int a) => Task.CompletedTask;

    public Task DoAsync(string a, string b) => Task.CompletedTask;
}

public static class CapabilityCatalogFixture
{
    public static void RegisterRecurringJobs(IRecurringJobManager recurring)
    {
        recurring.AddOrUpdate<ITimeJob>("time.execute", j => j.ExecuteAsync(), Cron.Minutely());
        recurring.AddOrUpdate<ISendMessageJob>(
            "send-message.text",
            j => j.ExecuteAsync("hello from recurring"),
            Cron.Hourly()
        );
        recurring.AddOrUpdate<ISendMessageJob>(
            "send-message.envelope",
            j => j.ExecuteAsync(new Message { Subject = "subj", Text = "body" }),
            Cron.Daily()
        );
        recurring.AddOrUpdate<IReportJob>(
            "report.generate",
            j => j.GenerateAsync(2026, "pdf", null),
            Cron.Daily()
        );
        recurring.AddOrUpdate<IDataExportJob>(
            "data.export",
            j => j.ExportAsync(ExportFormat.Csv, new[] { "users", "orders" }),
            Cron.Weekly()
        );
        recurring.AddOrUpdate<INotificationJob>(
            "notify.dispatch",
            j => j.NotifyAsync("ops", null, null),
            Cron.Hourly()
        );
        recurring.AddOrUpdate<MaintenanceJob>(
            "maint.rebuild-indexes",
            j => j.RebuildIndexesAsync("public"),
            Cron.Weekly()
        );
    }

    public static void RegisterManifestJobs(IBackgroundJobClient client)
    {
        client.Enqueue<IReportJob>(j => j.PreviewAsync(2026));
        client.Enqueue<MaintenanceJob>(j => j.VacuumAsync("orders", true));
        client.Enqueue<INotificationJob>(j => j.BroadcastAsync("startup", null, null));
    }
}

public interface ITimeJob
{
    Task ExecuteAsync();
}

public interface ISendMessageJob
{
    Task ExecuteAsync(Message message);
    Task ExecuteAsync(string text);
}

public sealed class Message
{
    public string Subject { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

public interface IReportJob
{
    [Description("Generate the annual financial report and persist it to the report store.")]
    Task GenerateAsync(
        [Description("Calendar year of the report (e.g. 2026).")] int year,
        [Description("Output file format. Supported: pdf, html, csv.")] string format = "pdf",
        [Description("Optional cutoff: only include data on or after this timestamp.")]
            DateTimeOffset? since = null
    );

    Task<int> PreviewAsync(int year);
}

public enum ExportFormat
{
    Csv,
    Json,
    Parquet,
}

public interface IDataExportJob
{
    [Description("Export the named tables to the data lake in the requested format.")]
    Task ExportAsync(
        [Description("Serialization format used for the exported files.")] ExportFormat format,
        [Description("Fully-qualified table names to include in the export.")]
            IReadOnlyList<string> tables
    );
}

public interface INotificationJob
{
    [Description(
        "Send a notification to a specific channel with an optional message and priority."
    )]
    Task NotifyAsync(
        [Description("Target channel name (e.g. 'ops-alerts').")] string channel,
        [Description("Message body. If omitted, a default template is used.")] string? message,
        [Description("Priority 1 (highest) to 5 (lowest). Defaults to 3 when omitted.")]
            int? priority
    );

    Task BroadcastAsync(string subject, string? tag, DateTimeOffset? expiresAt);
}

public class MaintenanceJob
{
    [Description("Rebuild all indexes in the given PostgreSQL schema.")]
    public Task RebuildIndexesAsync(
        [Description("Target schema name. Defaults to 'public'.")] string schema = "public"
    ) => Task.CompletedTask;

    public Task VacuumAsync(string table, bool full) => Task.CompletedTask;
}
