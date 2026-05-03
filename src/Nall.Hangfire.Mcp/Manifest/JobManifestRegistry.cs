using System.Collections.Concurrent;
using System.Reflection;

namespace Nall.Hangfire.Mcp.Manifest;

public static class JobManifestRegistry
{
    private static readonly ConcurrentDictionary<Assembly, IReadOnlyList<JobDescriptor>> s_sources =
        new();

    public static void Add(
        Assembly source,
        (Type Type, string Method, Type[] ParameterTypes)[] entries
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entries);

        var resolved = new List<JobDescriptor>(entries.Length);
        foreach (var (type, method, paramTypes) in entries)
        {
            if (type is null || method is null || paramTypes is null)
            {
                continue;
            }

            var info = type.GetMethod(
                method,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
                binder: null,
                types: paramTypes,
                modifiers: null
            );
            if (info is null)
            {
                continue;
            }

            resolved.Add(JobDescriptor.FromManifest(type, info));
        }

        s_sources[source] = resolved;
    }

    public static IReadOnlyList<JobDescriptor> AllDescriptors =>
        s_sources.Values.SelectMany(v => v).ToList();

    internal static void Clear() => s_sources.Clear();
}
