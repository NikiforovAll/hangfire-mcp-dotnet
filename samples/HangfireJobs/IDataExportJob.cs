using Microsoft.Extensions.Logging;

namespace HangfireJobs;

public enum ExportFormat
{
    Csv,
    Json,
    Parquet,
}

public interface IDataExportJob
{
    Task ExportAsync(ExportFormat format, IReadOnlyList<string> tables);
}

public class DataExportJob(ILogger<DataExportJob> logger) : IDataExportJob
{
    public Task ExportAsync(ExportFormat format, IReadOnlyList<string> tables)
    {
        logger.LogInformation(
            "Exporting {Count} tables as {Format}: {Tables}",
            tables.Count,
            format,
            string.Join(",", tables)
        );
        return Task.CompletedTask;
    }
}
