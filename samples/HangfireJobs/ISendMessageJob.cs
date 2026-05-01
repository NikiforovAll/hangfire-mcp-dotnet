using Microsoft.Extensions.Logging;

namespace HangfireJobs;

public interface ISendMessageJob
{
    Task ExecuteAsync(Message message);
    Task ExecuteAsync(string text);
}

public class Message
{
    public string Subject { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class SendMessageJob(ILogger<SendMessageJob> logger) : ISendMessageJob
{
    public Task ExecuteAsync(string text)
    {
        logger.LogInformation("Text: {Text}", text);
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(Message message)
    {
        logger.LogInformation("Subject: {Subject}, Text: {Text}", message.Subject, message.Text);
        return Task.CompletedTask;
    }
}
