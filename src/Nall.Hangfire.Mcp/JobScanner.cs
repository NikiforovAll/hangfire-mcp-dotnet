using System.Reflection;
using Hangfire;
using Hangfire.Storage;
using Nall.Hangfire.Mcp.Manifest;

namespace Nall.Hangfire.Mcp;

public static class JobScanner
{
    public static IReadOnlyList<JobDescriptor> Scan(
        JobStorage storage,
        JobDiscoverySources sources = JobDiscoverySources.RecurringStorage,
        Func<RecurringJobDto, bool>? filter = null
    )
    {
        ArgumentNullException.ThrowIfNull(storage);

        var results = new List<JobDescriptor>();
        var seen = new HashSet<MethodInfo>();

        if ((sources & JobDiscoverySources.RecurringStorage) != 0)
        {
            using var connection = storage.GetConnection();
            foreach (var rj in connection.GetRecurringJobs())
            {
                if (filter is not null && !filter(rj))
                {
                    continue;
                }
                if (rj.Job is null || rj.Job.Type is null || rj.Job.Method is null)
                {
                    continue;
                }
                if (seen.Add(rj.Job.Method))
                {
                    results.Add(new JobDescriptor(rj.Id, rj.Job.Type, rj.Job.Method));
                }
            }
        }

        if ((sources & JobDiscoverySources.StaticManifest) != 0)
        {
            foreach (var d in JobManifestRegistry.AllDescriptors)
            {
                if (seen.Add(d.Method))
                {
                    results.Add(d);
                }
            }
        }

        return results;
    }
}
