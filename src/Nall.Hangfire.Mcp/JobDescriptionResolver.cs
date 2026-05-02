using System.ComponentModel;
using System.Reflection;

namespace Nall.Hangfire.Mcp;

internal static class JobDescriptionResolver
{
    public static string? ResolveMethod(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        var direct = method.GetCustomAttribute<DescriptionAttribute>(inherit: true);
        if (direct is not null)
        {
            return direct.Description;
        }

        foreach (var interfaceMethod in EnumerateInterfaceMethods(method))
        {
            var attr = interfaceMethod.GetCustomAttribute<DescriptionAttribute>(inherit: true);
            if (attr is not null)
            {
                return attr.Description;
            }
        }

        return null;
    }

    public static string? ResolveParameter(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        var direct = parameter.GetCustomAttribute<DescriptionAttribute>(inherit: true);
        if (direct is not null)
        {
            return direct.Description;
        }

        if (parameter.Member is not MethodInfo method)
        {
            return null;
        }

        var position = parameter.Position;
        foreach (var interfaceMethod in EnumerateInterfaceMethods(method))
        {
            var ifaceParams = interfaceMethod.GetParameters();
            if (position >= ifaceParams.Length)
            {
                continue;
            }

            var attr = ifaceParams[position]
                .GetCustomAttribute<DescriptionAttribute>(inherit: true);
            if (attr is not null)
            {
                return attr.Description;
            }
        }

        return null;
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
