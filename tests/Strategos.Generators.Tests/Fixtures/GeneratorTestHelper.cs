// -----------------------------------------------------------------------
// <copyright file="GeneratorTestHelper.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis.Text;

namespace Strategos.Generators.Tests.Fixtures;

/// <summary>
/// Provides test infrastructure for running source generators in tests.
/// </summary>
public static class GeneratorTestHelper
{
    /// <summary>
    /// Runs the workflow generator against the provided source code.
    /// </summary>
    /// <param name="source">The source code to compile and run the generator against.</param>
    /// <returns>The generator driver run result containing generated output and diagnostics.</returns>
    public static GeneratorDriverRunResult RunGenerator(string source)
    {
        return RunGenerator<WorkflowIncrementalGenerator>(source);
    }

    /// <summary>
    /// Runs the workflow generator only after proving that the authored fixture itself compiles.
    /// </summary>
    /// <param name="source">The source code to compile and run the generator against.</param>
    /// <returns>The generator driver run result containing generated output and diagnostics.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the authored source or generated output has compiler errors, or when the
    /// generator reports an unexpected error.
    /// </exception>
    /// <param name="allowedGeneratorErrorIds">
    /// Generator error identifiers that the calling negative test will assert explicitly.
    /// </param>
    public static GeneratorDriverRunResult RunGeneratorWithValidInput(
        string source,
        params string[] allowedGeneratorErrorIds) =>
        RunGeneratorWithValidInput(
            source,
            [new WorkflowIncrementalGenerator(), new StateReducerIncrementalGenerator()],
            [],
            allowedGeneratorErrorIds,
            allowInvalidOutputAfterExpectedError: false);

    /// <summary>
    /// Runs the workflow generator with additional files after proving the complete fixture compiles.
    /// </summary>
    /// <param name="source">The source code to compile and run the generator against.</param>
    /// <param name="additionalTexts">Additional files supplied to the generator.</param>
    /// <param name="allowedGeneratorErrorIds">
    /// Generator error identifiers that the calling negative test will assert explicitly.
    /// </param>
    /// <returns>The generator driver run result containing generated output and diagnostics.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the authored source or generated output has compiler errors, or when the
    /// generator reports an unexpected error.
    /// </exception>
    public static GeneratorDriverRunResult RunGeneratorWithValidInput(
        string source,
        IEnumerable<AdditionalText> additionalTexts,
        params string[] allowedGeneratorErrorIds) =>
        RunGeneratorWithValidInput(
            source,
            [new WorkflowIncrementalGenerator(), new StateReducerIncrementalGenerator()],
            additionalTexts,
            allowedGeneratorErrorIds,
            allowInvalidOutputAfterExpectedError: false);

    /// <summary>
    /// Runs a topology-rejection fixture whose expected error describes DSL that cannot be lowered.
    /// </summary>
    /// <param name="source">The source code to compile and run the generator against.</param>
    /// <param name="allowedGeneratorErrorIds">
    /// Generator error identifiers that the calling negative test will assert explicitly.
    /// </param>
    /// <returns>The generator driver run result containing generated output and diagnostics.</returns>
    /// <remarks>
    /// This is intentionally narrower than <see cref="RunGeneratorWithValidInput(string, string[])"/>.
    /// It skips generated-output compilation only after an allowed generator error is actually
    /// reported. Use it solely for fail-closed topology fixtures whose rejected DSL has no valid
    /// lowering; ordinary binding and import proofs must use the authoritative overload above.
    /// </remarks>
    public static GeneratorDriverRunResult RunRejectedTopologyWithValidInput(
        string source,
        params string[] allowedGeneratorErrorIds) =>
        RunGeneratorWithValidInput(
            source,
            [new WorkflowIncrementalGenerator(), new StateReducerIncrementalGenerator()],
            [],
            allowedGeneratorErrorIds,
            allowInvalidOutputAfterExpectedError: true);

    /// <summary>
    /// Runs a specified generator after proving the authored source and generated output compile.
    /// </summary>
    /// <typeparam name="TGenerator">The incremental generator type.</typeparam>
    /// <param name="source">The source code to compile and run the generator against.</param>
    /// <param name="allowedGeneratorErrorIds">
    /// Generator error identifiers that the calling negative test will assert explicitly.
    /// </param>
    /// <returns>The generator driver run result containing generated output and diagnostics.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the authored source or generated output has compiler errors, or when the
    /// generator reports an unexpected error.
    /// </exception>
    public static GeneratorDriverRunResult RunGeneratorWithValidInput<TGenerator>(
        string source,
        params string[] allowedGeneratorErrorIds)
        where TGenerator : IIncrementalGenerator, new() =>
        RunGeneratorWithValidInput(
            source,
            [new TGenerator()],
            [],
            allowedGeneratorErrorIds,
            allowInvalidOutputAfterExpectedError: false);

    /// <summary>
    /// Runs the state reducer generator against the provided source code.
    /// </summary>
    /// <param name="source">The source code to compile and run the generator against.</param>
    /// <returns>The generator driver run result containing generated output and diagnostics.</returns>
    public static GeneratorDriverRunResult RunStateReducerGenerator(string source)
    {
        return RunGenerator<StateReducerIncrementalGenerator>(source);
    }

    /// <summary>
    /// Runs the specified generator against the provided source code.
    /// </summary>
    /// <typeparam name="TGenerator">The type of incremental generator to run.</typeparam>
    /// <param name="source">The source code to compile and run the generator against.</param>
    /// <returns>The generator driver run result containing generated output and diagnostics.</returns>
    public static GeneratorDriverRunResult RunGenerator<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out _,
            out _);

        return driver.GetRunResult();
    }

    /// <summary>
    /// Gets the generated source code from the run result by hint name suffix.
    /// </summary>
    /// <param name="result">The generator driver run result.</param>
    /// <param name="hintNameSuffix">The suffix of the hint name to find (e.g., "Phase.g.cs").</param>
    /// <returns>The generated source code, or empty string if not found.</returns>
    public static string GetGeneratedSource(GeneratorDriverRunResult result, string hintNameSuffix)
    {
        ArgumentNullException.ThrowIfNull(result, nameof(result));
        ArgumentNullException.ThrowIfNull(hintNameSuffix, nameof(hintNameSuffix));

        return result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.EndsWith(hintNameSuffix, StringComparison.Ordinal))
            ?.GetText()
            .ToString() ?? string.Empty;
    }

    /// <summary>
    /// Gets the compilation diagnostics (errors/warnings) after running the generator.
    /// </summary>
    /// <param name="source">The source code to compile.</param>
    /// <returns>The collection of compilation diagnostics.</returns>
    public static IEnumerable<Diagnostic> GetCompilationDiagnostics(string source)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new WorkflowIncrementalGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _);

        return outputCompilation.GetDiagnostics();
    }

    /// <summary>Gets compiler diagnostics for authored source before generated trees are added.</summary>
    /// <param name="source">The source code to compile.</param>
    /// <returns>The authored compilation diagnostics.</returns>
    public static IEnumerable<Diagnostic> GetInputCompilationDiagnostics(string source)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText("global using System.Collections.Generic;"),
            ],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation.GetDiagnostics();
    }

    private static GeneratorDriverRunResult RunGeneratorWithValidInput(
        string source,
        IEnumerable<IIncrementalGenerator> generators,
        IEnumerable<AdditionalText> additionalTexts,
        IReadOnlyCollection<string> allowedGeneratorErrorIds,
        bool allowInvalidOutputAfterExpectedError)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));
        ArgumentNullException.ThrowIfNull(generators, nameof(generators));
        ArgumentNullException.ThrowIfNull(additionalTexts, nameof(additionalTexts));
        ArgumentNullException.ThrowIfNull(allowedGeneratorErrorIds, nameof(allowedGeneratorErrorIds));

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText("global using System.Collections.Generic;"),
            ],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        EnsureNoCompilerErrors(
            compilation.GetDiagnostics(),
            "The generator fixture does not compile before generation");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: generators.Select(static generator => generator.AsSourceGenerator()),
            additionalTexts: additionalTexts.ToArray(),
            parseOptions: null,
            optionsProvider: null);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var driverDiagnostics);

        var allowedIds = new HashSet<string>(allowedGeneratorErrorIds, StringComparer.Ordinal);
        var unexpectedDriverErrors = driverDiagnostics
            .Where(diagnostic =>
                (diagnostic.Severity == DiagnosticSeverity.Error
                    && !allowedIds.Contains(diagnostic.Id))
                || diagnostic.Id is "CS8784" or "CS8785")
            .ToArray();
        if (unexpectedDriverErrors.Length > 0)
        {
            throw new InvalidOperationException(
                "The generator fixture produced unexpected driver errors: "
                + DescribeDiagnostics(unexpectedDriverErrors));
        }

        var hasExpectedGeneratorError = driverDiagnostics.Any(diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error
            && allowedIds.Contains(diagnostic.Id));
        if (!hasExpectedGeneratorError || !allowInvalidOutputAfterExpectedError)
        {
            EnsureNoCompilerErrors(
                outputCompilation.GetDiagnostics(),
                "The generated output does not compile");
        }

        return driver.GetRunResult();
    }

    /// <summary>
    /// Runs <see cref="WorkflowIncrementalGenerator"/> over <paramref name="source"/> under the
    /// given compilation options and returns the generator diagnostics as the consumer build
    /// sees them: the driver applies the compilation's diagnostic options (<c>/nowarn</c>,
    /// <c>#pragma warning</c>, rulesets) before recording them, so a suppressed diagnostic is
    /// absent here. The fixture must compile on its own; a driver crash (CS8784/CS8785) is a
    /// harness failure, never a result.
    /// </summary>
    /// <param name="source">The C# source to compile.</param>
    /// <param name="options">The compilation options, including any specific diagnostic options.</param>
    /// <returns>The generator diagnostics that survive the compilation options.</returns>
    public static ImmutableArray<Diagnostic> RunWorkflowGeneratorThroughDriverFilter(
        string source,
        CSharpCompilationOptions options)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: GetMetadataReferences(),
            options: options);

        EnsureNoCompilerErrors(
            compilation.GetDiagnostics(),
            "The fixture source does not compile before generation");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new WorkflowIncrementalGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out _,
            out var diagnostics);

        var driverCrashes = diagnostics
            .Where(static diagnostic => diagnostic.Id is "CS8784" or "CS8785")
            .ToArray();
        if (driverCrashes.Length > 0)
        {
            throw new InvalidOperationException(
                "The generator crashed inside the driver: " + DescribeDiagnostics(driverCrashes));
        }

        return diagnostics;
    }

    private static void EnsureNoCompilerErrors(
        IEnumerable<Diagnostic> diagnostics,
        string message)
    {
        var errors = diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(message + ": " + DescribeDiagnostics(errors));
        }
    }

    private static string DescribeDiagnostics(IEnumerable<Diagnostic> diagnostics) =>
        string.Join(" | ", diagnostics.Select(static diagnostic =>
            diagnostic.Id + ": " + diagnostic.GetMessage()));

    private static List<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>();
        var paths = new HashSet<string>(StringComparer.Ordinal);

        // Reference every dependency copied beside the test host. This is the consumer
        // compile graph for the generated Wolverine/Marten saga, including assemblies
        // that the current test has not happened to load yet.
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            AddReference(path);
        }

        // Reference the complete target-framework surface, including assemblies such as
        // System.Text.Json and DiagnosticSource that generated code uses but the test host
        // may not have loaded.
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string platformAssemblies)
        {
            foreach (var path in platformAssemblies.Split(Path.PathSeparator))
            {
                AddReference(path);
            }
        }

        // Add loaded assemblies (filtering out dynamic ones)
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            {
                try
                {
                    AddReference(assembly.Location);
                }
                catch
                {
                    // Ignore assemblies that can't be loaded as references
                }
            }
        }

        // Add the Workflow library reference
        var workflowAssembly = typeof(Strategos.Abstractions.IWorkflowState).Assembly;
        if (!string.IsNullOrEmpty(workflowAssembly.Location))
        {
            AddReference(workflowAssembly.Location);
        }

        // #167 workflow-binding proof reads ontology declarations from the same
        // compilation as the workflow. Keep the test compilation representative
        // by making the public ontology authoring surface resolvable.
        var ontologyAssembly = typeof(Strategos.Ontology.DomainOntology).Assembly;
        if (!string.IsNullOrEmpty(ontologyAssembly.Location))
        {
            AddReference(ontologyAssembly.Location);
        }

        return references;

        void AddReference(string path)
        {
            if (!File.Exists(path) || !paths.Add(path))
            {
                return;
            }

            try
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
            catch (BadImageFormatException)
            {
                // Native or otherwise non-managed binaries are not metadata references.
            }
        }
    }
}
