using Microsoft.Extensions.Logging;

namespace HangfireJobs;

public interface IReportJob
{
    Task GenerateAsync(int year, string format = "pdf", DateTimeOffset? since = null);

    Task<int> PreviewAsync(int year);
}

public class ReportJob(ILogger<ReportJob> logger) : IReportJob
{
    public Task GenerateAsync(int year, string format = "pdf", DateTimeOffset? since = null)
    {
        logger.LogInformation(
            "Generating report year={Year} format={Format} since={Since}",
            year,
            format,
            since
        );
        return Task.CompletedTask;
    }

    public Task<int> PreviewAsync(int year)
    {
        logger.LogInformation("Previewing report year={Year}", year);
        return Task.FromResult(42);
    }
}
