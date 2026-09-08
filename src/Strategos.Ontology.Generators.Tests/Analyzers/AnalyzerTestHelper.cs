using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Strategos.Ontology.Generators.Analyzers;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

internal static class AnalyzerTestHelper
{
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = CreateCompilation(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new OntologyDefinitionAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    /// <summary>
    /// Runs the ontology analyzer with an explicit set of consumer suppression channels applied,
    /// so a test can prove which diagnostics survive them.
    /// </summary>
    /// <param name="source">The consumer source to analyze.</param>
    /// <param name="noWarn">
    /// Ids to suppress through specific diagnostic options — the channel an MSBuild
    /// <c>&lt;NoWarn&gt;</c> / <c>/nowarn</c> feeds.
    /// </param>
    /// <param name="editorConfigNone">
    /// Ids to suppress through the syntax-tree options provider — the channel an
    /// <c>.editorconfig</c> <c>dotnet_diagnostic.ID.severity = none</c> entry feeds.
    /// </param>
    /// <returns>The analyzer diagnostics that survive both channels.</returns>
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsUnderSuppressionAsync(
        string source,
        IEnumerable<string> noWarn,
        IEnumerable<string> editorConfigNone)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithSpecificDiagnosticOptions(noWarn.ToDictionary(
                id => id,
                _ => ReportDiagnostic.Suppress,
                StringComparer.Ordinal))
            .WithSyntaxTreeOptionsProvider(
                new SeveritySyntaxTreeOptionsProvider(editorConfigNone));

        var compilation = CreateCompilation(source).WithOptions(options);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new OntologyDefinitionAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsWithIdAsync(string source, string diagnosticId)
    {
        var diagnostics = await GetDiagnosticsAsync(source);
        return diagnostics.Where(d => d.Id == diagnosticId).ToImmutableArray();
    }

    /// <summary>
    /// The in-memory equivalent of an <c>.editorconfig</c> that sets
    /// <c>dotnet_diagnostic.ID.severity = none</c> for a fixed set of ids. Roslyn routes
    /// editorconfig severity through this provider, so a diagnostic that ignores it here
    /// ignores an <c>.editorconfig</c> in a real build.
    /// </summary>
    private sealed class SeveritySyntaxTreeOptionsProvider(IEnumerable<string> suppressedIds)
        : SyntaxTreeOptionsProvider
    {
        private readonly HashSet<string> _suppressedIds = new(suppressedIds, StringComparer.OrdinalIgnoreCase);

        public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken) =>
            GeneratedKind.Unknown;

        public override bool TryGetDiagnosticValue(
            SyntaxTree tree,
            string diagnosticId,
            CancellationToken cancellationToken,
            out ReportDiagnostic severity) =>
            TryGetGlobalDiagnosticValue(diagnosticId, cancellationToken, out severity);

        public override bool TryGetGlobalDiagnosticValue(
            string diagnosticId,
            CancellationToken cancellationToken,
            out ReportDiagnostic severity)
        {
            if (_suppressedIds.Contains(diagnosticId))
            {
                severity = ReportDiagnostic.Suppress;
                return true;
            }

            severity = ReportDiagnostic.Default;
            return false;
        }
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Get references from the Strategos.Ontology assembly
        var ontologyAssembly = typeof(Strategos.Ontology.OntologyGraph).Assembly;
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Numerics.BigInteger).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ImmutableArray<>).Assembly.Location),
            MetadataReference.CreateFromFile(ontologyAssembly.Location),
        };

        // Add System.Runtime and other needed assemblies
        var runtimeDir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(
            System.IO.Path.Combine(runtimeDir, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(
            System.IO.Path.Combine(runtimeDir, "System.Collections.dll")));
        references.Add(MetadataReference.CreateFromFile(
            System.IO.Path.Combine(runtimeDir, "System.Linq.Expressions.dll")));

        // Add Microsoft.Extensions.DependencyInjection.Abstractions if available
        var diAbstractionsAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Microsoft.Extensions.DependencyInjection.Abstractions");
        if (diAbstractionsAssembly != null)
        {
            references.Add(MetadataReference.CreateFromFile(diAbstractionsAssembly.Location));
        }

        return CSharpCompilation.Create(
            "TestCompilation",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
