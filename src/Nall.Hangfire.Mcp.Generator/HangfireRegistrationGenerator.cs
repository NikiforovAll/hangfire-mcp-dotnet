using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nall.Hangfire.Mcp.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class HangfireRegistrationGenerator : IIncrementalGenerator
{
    private static readonly HashSet<string> s_registrationMethodNames = new(
        System.StringComparer.Ordinal
    )
    {
        "AddOrUpdate",
        "Enqueue",
        "Schedule",
        "ContinueJobWith",
        "ContinueWith",
    };

    private static readonly HashSet<string> s_hangfireContainingTypes = new(
        System.StringComparer.Ordinal
    )
    {
        "Hangfire.IRecurringJobManager",
        "Hangfire.RecurringJob",
        "Hangfire.RecurringJobManagerExtensions",
        "Hangfire.IBackgroundJobClient",
        "Hangfire.BackgroundJob",
        "Hangfire.BackgroundJobClientExtensions",
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entries = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (n, _) => IsCandidate(n),
                transform: static (ctx, _) => Extract(ctx)
            )
            .Where(static e => e is not null)
            .Select(static (e, _) => e!.Value)
            .Collect();

        context.RegisterSourceOutput(
            entries,
            static (spc, rawEntries) =>
            {
                if (rawEntries.IsDefaultOrEmpty)
                {
                    return;
                }

                var distinct = rawEntries
                    .Distinct()
                    .OrderBy(e => e.TypeFqn, System.StringComparer.Ordinal)
                    .ThenBy(e => e.MethodName, System.StringComparer.Ordinal)
                    .ThenBy(e => e.ParameterKey, System.StringComparer.Ordinal)
                    .ToImmutableArray();

                spc.AddSource("HangfireJobManifest.g.cs", Emit(distinct));
            }
        );
    }

    private static bool IsCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax inv)
        {
            return false;
        }
        var name = inv.Expression switch
        {
            MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax mb => mb.Name.Identifier.ValueText,
            GenericNameSyntax g => g.Identifier.ValueText,
            _ => null,
        };
        if (name is null)
        {
            return false;
        }
        return s_registrationMethodNames.Contains(name);
    }

    // Accepts any method whose registered-name shape is a Hangfire-style wrapper:
    // it forwards a job lambda via System.Linq.Expressions.Expression<TDelegate>.
    private static bool HasExpressionDelegateParameter(IMethodSymbol method)
    {
        foreach (var p in method.Parameters)
        {
            if (
                p.Type is INamedTypeSymbol { IsGenericType: true } named
                && named.OriginalDefinition.Name == "Expression"
                && named.OriginalDefinition.ContainingNamespace?.ToDisplayString()
                    == "System.Linq.Expressions"
            )
            {
                return true;
            }
        }
        return false;
    }

    private static Entry? Extract(GeneratorSyntaxContext ctx)
    {
        var inv = (InvocationExpressionSyntax)ctx.Node;
        var symbolInfo = ctx.SemanticModel.GetSymbolInfo(inv);
        var method =
            (symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault()) as IMethodSymbol;
        if (method is null)
        {
            return null;
        }

        var containing = method.ContainingType?.ToDisplayString();
        var isHangfireApi =
            containing is not null && s_hangfireContainingTypes.Contains(containing);
        if (!isHangfireApi && !HasExpressionDelegateParameter(method))
        {
            return null;
        }

        LambdaExpressionSyntax? lambda = null;
        foreach (var arg in inv.ArgumentList.Arguments)
        {
            if (arg.Expression is LambdaExpressionSyntax l)
            {
                lambda = l;
                break;
            }
        }
        if (lambda is null)
        {
            return null;
        }

        ExpressionSyntax? body = lambda.Body as ExpressionSyntax;
        if (
            body is null
            && lambda.Body is BlockSyntax block
            && block.Statements.Count == 1
            && block.Statements[0] is ExpressionStatementSyntax es
        )
        {
            body = es.Expression;
        }
        if (body is not InvocationExpressionSyntax targetInv)
        {
            return null;
        }

        var targetSymbol = ctx.SemanticModel.GetSymbolInfo(targetInv).Symbol as IMethodSymbol;
        if (targetSymbol is null)
        {
            return null;
        }
        if (targetSymbol.IsGenericMethod)
        {
            return null;
        }

        var declaringType = targetSymbol.ContainingType;
        if (
            declaringType is null
            || declaringType.IsGenericType && declaringType.IsUnboundGenericType
        )
        {
            return null;
        }

        var typeFqn = declaringType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var paramFqns = targetSymbol
            .Parameters.Select(p =>
                p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            )
            .ToImmutableArray();

        return new Entry(typeFqn, targetSymbol.Name, paramFqns);
    }

    private static string Emit(ImmutableArray<Entry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace Nall.Hangfire.Mcp.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    internal static class HangfireJobManifest");
        sb.AppendLine("    {");
        sb.AppendLine("        [System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        internal static void Register()");
        sb.AppendLine("        {");
        sb.AppendLine(
            "            // Reflection-based to avoid forcing consumers to reference Nall.Hangfire.Mcp at compile time."
        );
        sb.AppendLine(
            "            var registry = System.Type.GetType(\"Nall.Hangfire.Mcp.Manifest.JobManifestRegistry, Nall.Hangfire.Mcp\", throwOnError: false);"
        );
        sb.AppendLine("            if (registry is null) { return; }");
        sb.AppendLine(
            "            var add = registry.GetMethod(\"Add\", new System.Type[] { typeof(System.Reflection.Assembly), typeof((System.Type, string, System.Type[])[]) });"
        );
        sb.AppendLine(
            "            add?.Invoke(null, new object[] { typeof(HangfireJobManifest).Assembly, Entries });"
        );
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine(
            "        internal static readonly (System.Type Type, string Method, System.Type[] ParameterTypes)[] Entries = new (System.Type, string, System.Type[])[]"
        );
        sb.AppendLine("        {");
        foreach (var e in entries)
        {
            sb.Append("            (typeof(")
                .Append(e.TypeFqn)
                .Append("), \"")
                .Append(e.MethodName)
                .Append("\", new System.Type[] { ");
            sb.Append(string.Join(", ", e.ParameterTypeFqns.Select(p => $"typeof({p})")));
            sb.AppendLine(" }),");
        }
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    internal readonly struct Entry : System.IEquatable<Entry>
    {
        public Entry(string typeFqn, string methodName, ImmutableArray<string> parameterTypeFqns)
        {
            TypeFqn = typeFqn;
            MethodName = methodName;
            ParameterTypeFqns = parameterTypeFqns;
            ParameterKey = string.Join(",", parameterTypeFqns);
        }

        public string TypeFqn { get; }
        public string MethodName { get; }
        public ImmutableArray<string> ParameterTypeFqns { get; }
        public string ParameterKey { get; }

        public bool Equals(Entry other) =>
            TypeFqn == other.TypeFqn
            && MethodName == other.MethodName
            && ParameterKey == other.ParameterKey;

        public override bool Equals(object? obj) => obj is Entry e && Equals(e);

        public override int GetHashCode()
        {
            unchecked
            {
                var h = TypeFqn.GetHashCode();
                h = (h * 397) ^ MethodName.GetHashCode();
                h = (h * 397) ^ ParameterKey.GetHashCode();
                return h;
            }
        }
    }
}
