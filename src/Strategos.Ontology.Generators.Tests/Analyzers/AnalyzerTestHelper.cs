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

    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsWithIdAsync(string source, string diagnosticId)
    {
        var diagnostics = await GetDiagnosticsAsync(source);
        return diagnostics.Where(d => d.Id == diagnosticId).ToImmutableArray();
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
