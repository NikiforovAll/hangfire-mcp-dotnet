using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace HangfireJobs;

public interface INotificationJob
{
    // channel is required (non-nullable reference); message and priority are nullable
    // and so optional in the MCP schema even though they have no C# default.
    [Description(
        "Send a notification to a specific channel with an optional message and priority."
    )]
    Task NotifyAsync(
        [Description("Target channel name (e.g. 'ops-alerts').")] string channel,
        [Description("Message body. If omitted, a default template is used.")] string? message,
        [Description("Priority 1 (highest) to 5 (lowest). Defaults to 3 when omitted.")]
            int? priority
    );

    // Manifest-only candidate — never registered as recurring; one-shot enqueued in
    // Program.cs so the source generator records it. Optional `tag` (nullable) and
    // `expiresAt` (Nullable<DateTimeOffset>) demonstrate optional behavior on a
    // manifest-only tool.
    Task BroadcastAsync(string subject, string? tag, DateTimeOffset? expiresAt);
}

public class NotificationJob(ILogger<NotificationJob> logger) : INotificationJob
{
    public Task NotifyAsync(string channel, string? message, int? priority)
    {
        logger.LogInformation(
            "Notify channel={Channel} message={Message} priority={Priority}",
            channel,
            message,
            priority
        );
        return Task.CompletedTask;
    }

    public Task BroadcastAsync(string subject, string? tag, DateTimeOffset? expiresAt)
    {
        logger.LogInformation(
            "Broadcast subject={Subject} tag={Tag} expiresAt={ExpiresAt}",
            subject,
            tag,
            expiresAt
        );
        return Task.CompletedTask;
    }
}
