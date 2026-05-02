using System.ComponentModel;
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
    [Description("Export the named tables to the data lake in the requested format.")]
    Task ExportAsync(
        [Description("Serialization format used for the exported files.")] ExportFormat format,
        [Description("Fully-qualified table names to include in the export.")]
            IReadOnlyList<string> tables
    );
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
