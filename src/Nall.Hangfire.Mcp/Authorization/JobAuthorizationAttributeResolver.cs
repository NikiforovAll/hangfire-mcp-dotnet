using System.Reflection;
using Microsoft.AspNetCore.Authorization;

namespace Nall.Hangfire.Mcp.Authorization;

internal static class JobAuthorizationAttributeResolver
{
    public static IReadOnlyList<IAuthorizeData> Resolve(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        var direct = method.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().ToArray();
        if (direct.Length > 0)
        {
            return direct;
        }

        foreach (var interfaceMethod in EnumerateInterfaceMethods(method))
        {
            var ifaceAttrs = interfaceMethod
                .GetCustomAttributes(inherit: true)
                .OfType<IAuthorizeData>()
                .ToArray();
            if (ifaceAttrs.Length > 0)
            {
                return ifaceAttrs;
            }
        }

        return [];
    }

    private static IEnumerable<MethodInfo> EnumerateInterfaceMethods(MethodInfo method)
    {
        var declaring = method.DeclaringType;
        if (declaring is null || declaring.IsInterface)
        {
            yield break;
        }

        foreach (var iface in declaring.GetInterfaces())
        {
            InterfaceMapping map;
            try
            {
                map = declaring.GetInterfaceMap(iface);
            }
            catch (ArgumentException)
            {
                continue;
            }

            for (var i = 0; i < map.TargetMethods.Length; i++)
            {
                if (map.TargetMethods[i] == method)
                {
                    yield return map.InterfaceMethods[i];
                }
            }
        }
    }
}
