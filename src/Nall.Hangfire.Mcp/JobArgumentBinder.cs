using System.Reflection;
using System.Text.Json;

namespace Nall.Hangfire.Mcp;

public static class JobArgumentBinder
{
    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web);

    public static object?[] Bind(
        MethodInfo method,
        IReadOnlyDictionary<string, JsonElement>? arguments
    )
    {
        ArgumentNullException.ThrowIfNull(method);

        var parameters = method.GetParameters();
        var bound = new object?[parameters.Length];
        var nullability = new NullabilityInfoContext();

        for (var i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (
                p.Name is not null
                && arguments is not null
                && arguments.TryGetValue(p.Name, out var value)
                && value.ValueKind is not JsonValueKind.Undefined
            )
            {
                bound[i] = value.Deserialize(p.ParameterType, s_options);
                continue;
            }

            if (p.HasDefaultValue)
            {
                bound[i] = p.DefaultValue;
                continue;
            }

            if (ParameterNullability.IsOptional(p, nullability))
            {
                bound[i] = null;
                continue;
            }

            throw new ArgumentException(
                $"Missing required argument '{p.Name}' for {method.DeclaringType?.Name}.{method.Name}.",
                nameof(arguments)
            );
        }

        return bound;
    }
}
