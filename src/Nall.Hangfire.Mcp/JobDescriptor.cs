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
        return new JobDescriptor(synthetic, declaringType, method);
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
