using System.Reflection;

namespace Nall.Hangfire.Mcp;

internal static class JobParameterFilter
{
    public static bool IsCancellationToken(ParameterInfo p) =>
        p.ParameterType == typeof(CancellationToken);
}
