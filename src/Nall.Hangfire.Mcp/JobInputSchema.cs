using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Nall.Hangfire.Mcp;

public static class JobInputSchema
{
    private static readonly JsonSerializerOptions s_schemaOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static JsonElement Build(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        var properties = new JsonObject();
        var required = new JsonArray();
        var nullability = new NullabilityInfoContext();

        foreach (var p in method.GetParameters())
        {
            if (p.Name is null)
            {
                continue;
            }

            var schema = JsonSchemaExporter.GetJsonSchemaAsNode(s_schemaOptions, p.ParameterType);
            properties[p.Name] = schema;

            if (!p.HasDefaultValue && !ParameterNullability.IsOptional(p, nullability))
            {
                required.Add(p.Name);
            }
        }

        var root = new JsonObject { ["type"] = "object", ["properties"] = properties };
        if (required.Count > 0)
        {
            root["required"] = required;
        }

        return JsonSerializer.SerializeToElement(root, s_schemaOptions);
    }
}
