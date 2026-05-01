using System.Collections.Concurrent;
using System.Reflection;

namespace Nall.Hangfire.Mcp.Manifest;

public static class JobManifestRegistry
{
    private static readonly ConcurrentDictionary<Assembly, IReadOnlyList<JobDescriptor>> Sources =
        new();
    private static IReadOnlyList<JobDescriptor>? s_flattened;

    public static void Add(
        Assembly source,
        ReadOnlySpan<(Type Type, string Method, Type[] ParameterTypes)> entries
    )
    {
        ArgumentNullException.ThrowIfNull(source);

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

        Sources[source] = resolved;
        s_flattened = null;
    }

    public static IReadOnlyList<JobDescriptor> AllDescriptors =>
        s_flattened ??= Sources.Values.SelectMany(v => v).ToList();

    internal static void Clear()
    {
        Sources.Clear();
        s_flattened = null;
    }
}
