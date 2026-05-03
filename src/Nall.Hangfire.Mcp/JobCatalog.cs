using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Hangfire;
using Hangfire.Storage;
using ModelContextProtocol.Protocol;

namespace Nall.Hangfire.Mcp;

public sealed class JobCatalog
{
    private readonly FrozenDictionary<string, JobDescriptor> _byToolName;

    public IReadOnlyList<JobDescriptor> Jobs { get; }

    public ListToolsResult ListToolsResult { get; }

    public JobCatalog(
        JobStorage storage,
        JobDiscoverySources sources = JobDiscoverySources.RecurringStorage,
        Func<RecurringJobDto, bool>? filter = null
    )
    {
        ArgumentNullException.ThrowIfNull(storage);
        Jobs = JobScanner.Scan(storage, sources, filter);
        _byToolName = Jobs.ToFrozenDictionary(d => d.ToolName, StringComparer.Ordinal);
        ListToolsResult = new ListToolsResult
        {
            Tools = Jobs.Select(d => new Tool
                {
                    Name = d.ToolName,
                    Description =
                        JobDescriptionResolver.ResolveMethod(d.Method)
                        ?? $"Enqueue Hangfire job '{d.RecurringJobId}'.",
                    InputSchema = JobInputSchema.Build(d.Method),
                })
                .ToList(),
        };
    }

    public bool TryGetByToolName(
        string name,
        [MaybeNullWhen(false)] out JobDescriptor descriptor
    ) => _byToolName.TryGetValue(name, out descriptor);
}
