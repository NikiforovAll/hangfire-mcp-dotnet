namespace Nall.Hangfire.Mcp;

using System.Reflection;

public sealed record JobDescriptor(string RecurringJobId, Type DeclaringType, MethodInfo Method)
{
    public string ToolName { get; init; } = BuildToolName(RecurringJobId);

    public static JobDescriptor FromManifest(Type declaringType, MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentNullException.ThrowIfNull(method);
        var synthetic = $"{Sanitize(declaringType.Name)}_{Sanitize(method.Name)}";
        var index = OverloadIndex(declaringType, method);
        if (index > 0)
        {
            synthetic = $"{synthetic}_{index}";
        }
        return new JobDescriptor(synthetic, declaringType, method);
    }

    private static int OverloadIndex(Type declaringType, MethodInfo method)
    {
        var siblings = declaringType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => string.Equals(m.Name, method.Name, StringComparison.Ordinal))
            .OrderBy(m => m.GetParameters().Length)
            .ThenBy(
                m =>
                    string.Join(
                        ",",
                        m.GetParameters()
                            .Select(p => p.ParameterType.FullName ?? p.ParameterType.Name)
                    ),
                StringComparer.Ordinal
            )
            .ToArray();
        if (siblings.Length <= 1)
        {
            return 0;
        }
        return Array.IndexOf(siblings, method) + 1;
    }

    private static string BuildToolName(string recurringJobId) => $"Run_{Sanitize(recurringJobId)}";

    private static string Sanitize(string s) =>
        string.Create(
            s.Length,
            s,
            static (span, source) =>
            {
                for (var i = 0; i < source.Length; i++)
                {
                    var c = source[i];
                    span[i] = char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_';
                }
            }
        );
}
