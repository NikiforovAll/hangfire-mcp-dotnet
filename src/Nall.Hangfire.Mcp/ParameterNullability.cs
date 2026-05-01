using System.Reflection;

namespace Nall.Hangfire.Mcp;

internal static class ParameterNullability
{
    public static bool IsOptional(ParameterInfo p, NullabilityInfoContext ctx)
    {
        if (Nullable.GetUnderlyingType(p.ParameterType) is not null)
        {
            return true;
        }
        if (p.ParameterType.IsValueType)
        {
            return false;
        }
        return ctx.Create(p).WriteState == NullabilityState.Nullable;
    }
}
