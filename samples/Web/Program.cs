using Hangfire;
using HangfireJobs;
using Nall.Hangfire.Mcp;
using Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddHangfireServer();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<ITimeJob, TimeJob>();
builder.Services.AddTransient<ISendMessageJob, SendMessageJob>();
builder.Services.AddTransient<IReportJob, ReportJob>();
builder.Services.AddTransient<IDataExportJob, DataExportJob>();
builder.Services.AddTransient<INotificationJob, NotificationJob>();
builder.Services.AddTransient<MaintenanceJob>();
builder.Services.AddProblemDetails();

// Opt into BOTH discovery sources: recurring storage + compile-time manifest from
// the source generator. Default is RecurringStorage only.
builder.Services.AddHangfireMcp(o => o.Sources = JobDiscoverySources.All);

var app = builder.Build();
app.UseHttpsRedirection();
app.MapHangfireDashboard(string.Empty);

var recurring = app.Services.GetRequiredService<IRecurringJobManager>();

// (1) Parameterless interface job.
recurring.AddOrUpdate<ITimeJob>("time.execute", j => j.ExecuteAsync(), Cron.Minutely);

// (2) Overloaded interface methods — primitive vs. complex argument.
recurring.AddOrUpdate<ISendMessageJob>(
    "send-message.text",
    j => j.ExecuteAsync("hello from recurring"),
    Cron.Hourly
);
recurring.AddOrUpdate<ISendMessageJob>(
    "send-message.envelope",
    j => j.ExecuteAsync(new Message { Subject = "subj", Text = "body" }),
    Cron.Daily
);

// (3) Default args + nullable + multiple primitives.
recurring.AddOrUpdate<IReportJob>(
    "report.generate",
    j => j.GenerateAsync(2026, "pdf", null),
    Cron.Daily
);

// (4) Enum + collection params.
recurring.AddOrUpdate<IDataExportJob>(
    "data.export",
    j => j.ExportAsync(ExportFormat.Csv, new[] { "users", "orders" }),
    Cron.Weekly
);

// (5) Nullable params without C# defaults — `message` and `priority` are optional in
// the MCP schema because their nullability annotations mark them so.
recurring.AddOrUpdate<INotificationJob>(
    "notify.dispatch",
    j => j.NotifyAsync("ops", null, null),
    Cron.Hourly
);

// (6) Concrete (non-interface) job class with default arg.
recurring.AddOrUpdate<MaintenanceJob>(
    "maint.rebuild-indexes",
    j => j.RebuildIndexesAsync("public"),
    Cron.Weekly
);

// (7) One-shot enqueue — picked up by the source generator into the static manifest
// even though it is never registered as recurring. Demonstrates JobDiscoverySources.StaticManifest.
var client = app.Services.GetRequiredService<IBackgroundJobClient>();
client.Enqueue<IReportJob>(j => j.PreviewAsync(2026));
client.Enqueue<MaintenanceJob>(j => j.VacuumAsync("orders", true));

// Manifest-only with optional nullable params — only `subject` is required.
client.Enqueue<INotificationJob>(j => j.BroadcastAsync("startup", null, null));

app.MapGet(
    "/jobs",
    (JobCatalog catalog) =>
        Results.Ok(
            catalog.Jobs.Select(d => new
            {
                d.RecurringJobId,
                d.ToolName,
                DeclaringType = d.DeclaringType.FullName,
                Method = d.Method.Name,
                Parameters = d
                    .Method.GetParameters()
                    .Select(p => new
                    {
                        p.Name,
                        Type = p.ParameterType.FullName,
                        HasDefault = p.HasDefaultValue,
                    }),
            })
        )
);

app.MapHangfireMcp("/mcp");

app.Run();
