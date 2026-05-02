using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace HangfireJobs;

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
