// -----------------------------------------------------------------------
// <copyright file="TwoAssemblyHarness.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Reflection;

namespace Strategos.Generators.Tests.Fixtures;

/// <summary>
/// Compiles one assembly and references it from another, so a test can observe what the
/// workflow-binding proof sees across a real compiled boundary (#204).
/// </summary>
/// <remarks>
/// <para>
/// Every generator fixture in this suite before #204 lived in one <see cref="Compilation"/>.
/// That is precisely the shape the #167 proof is complete for, and precisely the shape that
/// cannot exhibit the defect #204 exists to close: a binding whose action, target workflow, or
/// leaf-action catalog is declared in a REFERENCED assembly. No fixture could express it,
/// because nothing in the repository compiled one assembly and referenced it from a
/// generator-bearing compilation.
/// </para>
/// <para>
/// The boundary here is a real one. The producer is emitted to a PE image and handed to the
/// consumer as a <see cref="MetadataReference"/>, so the consumer's compilation sees exactly
/// what a package reference gives it: metadata and nothing else. No syntax tree of the
/// producer's crosses over, which is the whole point — <c>OntologyActionCatalog</c> walks
/// <see cref="Compilation.SyntaxTrees"/>, so a declaration behind this boundary is invisible to
/// it unless something carries it across deliberately.
/// </para>
/// <para>
/// The harness fails loudly rather than returning an empty result. A producer that does not
/// compile, or emits nothing, would make every consumer-side assertion vacuously true — the
/// exact failure mode a two-assembly test is most prone to.
/// </para>
/// </remarks>
public static class TwoAssemblyHarness
{
    /// <summary>An assembly compiled to a PE image and usable as a metadata reference.</summary>
    /// <param name="AssemblyName">The producer's assembly name.</param>
    /// <param name="Image">The emitted PE image.</param>
    /// <param name="Reference">The metadata reference a consumer compilation adds.</param>
    /// <param name="GeneratorDiagnostics">Diagnostics the generators reported while building it.</param>
    /// <param name="GeneratedHintNames">Hint names of every tree the generators added.</param>
    /// <param name="GeneratedSources">Each generated tree's hint name and text.</param>
    public sealed record ProducerAssembly(
        string AssemblyName,
        ImmutableArray<byte> Image,
        MetadataReference Reference,
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        ImmutableArray<string> GeneratedHintNames,
        ImmutableArray<(string HintName, string Text)> GeneratedSources)
    {
        /// <summary>Gets the text of one generated source, or null when absent.</summary>
        /// <param name="hintName">The hint name to find.</param>
        /// <returns>The generated text, or <see langword="null"/>.</returns>
        public string? Generated(string hintName) => GeneratedSources
            .Where(source => string.Equals(source.HintName, hintName, StringComparison.Ordinal))
            .Select(source => source.Text)
            .FirstOrDefault();
    }

    /// <summary>The result of compiling a consumer against one or more producers.</summary>
    /// <param name="GeneratorDiagnostics">
    /// Diagnostics the generators reported, already filtered by the compilation's diagnostic
    /// options exactly as a consumer build sees them.
    /// </param>
    /// <param name="OutputCompilation">The compilation including every generated tree.</param>
    /// <param name="RunResult">The driver run result, for inspecting generated output.</param>
    public sealed record ConsumerCompilation(
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        Compilation OutputCompilation,
        GeneratorDriverRunResult RunResult)
    {
        /// <summary>Gets the generator diagnostics carrying the given id.</summary>
        /// <param name="id">The diagnostic id.</param>
        /// <returns>Every reported diagnostic with that id.</returns>
        public ImmutableArray<Diagnostic> WithId(string id) =>
            [.. GeneratorDiagnostics.Where(diagnostic =>
                string.Equals(diagnostic.Id, id, StringComparison.Ordinal))];

        /// <summary>Gets the ids of every generator diagnostic reported at error severity.</summary>
        /// <returns>The distinct error ids, ordered.</returns>
        public ImmutableArray<string> ErrorIds() =>
            [.. GeneratorDiagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.Id)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static id => id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Compiles <paramref name="source"/> into a referenceable assembly image.
    /// </summary>
    /// <param name="assemblyName">
    /// The producer's assembly name. It must be distinct per fixture: two producers sharing a
    /// name cannot both be referenced, and the resulting resolution failure reads as a missing
    /// declaration rather than as a harness mistake.
    /// </param>
    /// <param name="source">The producer's C# source.</param>
    /// <param name="runGenerators">
    /// Whether to run the Strategos generators over the producer. Pass <see langword="false"/> to
    /// model a producer built WITHOUT the emitter — the "missing manifest" case, which must fail
    /// closed on the consumer rather than silently degrade.
    /// </param>
    /// <param name="silencedDiagnosticIds">
    /// Diagnostic ids the producer's own project silences. This is not test convenience: an
    /// assembly that declares a binding whose workflow lives elsewhere reports AGWF039 when
    /// compiled alone, and today the only way to ship it is to silence that id in the project
    /// file — which is what AGWF039's configurable severity exists for. A producer fixture that
    /// could not express that silencing would not be the assembly a real consumer references.
    /// </param>
    /// <returns>The compiled producer.</returns>
    /// <exception cref="InvalidOperationException">
    /// The producer does not compile, or emits no image.
    /// </exception>
    public static ProducerAssembly CompileProducer(
        string assemblyName,
        string source,
        bool runGenerators = true,
        params string[] silencedDiagnosticIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        ArgumentNullException.ThrowIfNull(source);

        var compilation = CreateCompilation(
            assemblyName,
            source,
            GeneratorTestHelper.GetCompileGraphReferences());
        EnsureNoErrors(
            compilation.GetDiagnostics(),
            $"producer '{assemblyName}' does not compile before generation");

        var generatorDiagnostics = ImmutableArray<Diagnostic>.Empty;
        var hintNames = ImmutableArray<string>.Empty;
        var sources = ImmutableArray<(string, string)>.Empty;
        Compilation emitted = compilation;

        if (runGenerators)
        {
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                new WorkflowIncrementalGenerator().AsSourceGenerator(),
                new StateReducerIncrementalGenerator().AsSourceGenerator());
            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out emitted,
                out var diagnostics);

            EnsureNoDriverCrash(diagnostics, assemblyName);
            var silenced = new HashSet<string>(
                silencedDiagnosticIds ?? [], StringComparer.Ordinal);
            EnsureNoErrors(
                diagnostics.Where(diagnostic => !silenced.Contains(diagnostic.Id)),
                $"producer '{assemblyName}' reported generator errors");
            EnsureNoErrors(
                emitted.GetDiagnostics(),
                $"producer '{assemblyName}' generated output does not compile");

            generatorDiagnostics = diagnostics;
            sources =
            [
                .. driver.GetRunResult().Results
                    .SelectMany(static result => result.GeneratedSources)
                    .Select(static generated =>
                        (generated.HintName, generated.SourceText.ToString()))
                    .OrderBy(static generated => generated.HintName, StringComparer.Ordinal),
            ];
            hintNames = [.. sources.Select(static generated => generated.Item1)];
        }

        using var stream = new MemoryStream();
        var emitResult = emitted.Emit(stream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(
                $"producer '{assemblyName}' failed to emit: " + Describe(emitResult.Diagnostics));
        }

        var image = stream.ToArray();
        return new ProducerAssembly(
            assemblyName,
            [.. image],
            MetadataReference.CreateFromImage(image),
            generatorDiagnostics,
            hintNames,
            sources);
    }

    /// <summary>
    /// Compiles <paramref name="source"/> against <paramref name="producers"/> and runs the
    /// Strategos generators over it.
    /// </summary>
    /// <param name="source">The consumer's C# source.</param>
    /// <param name="producers">The producer assemblies the consumer references.</param>
    /// <returns>The consumer's generator diagnostics and output compilation.</returns>
    /// <exception cref="InvalidOperationException">
    /// The consumer's authored source does not compile, or the driver crashed.
    /// </exception>
    /// <remarks>
    /// Generated output is NOT required to compile here. A consumer whose binding the proof
    /// refuses is expected to produce a diagnostic and no saga, and demanding that its output
    /// compile would turn a correct refusal into a harness failure. Tests assert on the
    /// diagnostics and, where a saga is expected, on the generated trees.
    /// </remarks>
    public static ConsumerCompilation CompileConsumer(
        string source,
        params ProducerAssembly[] producers)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(producers);

        var references = GeneratorTestHelper.GetCompileGraphReferences();
        references.AddRange(producers.Select(static producer => producer.Reference));

        var compilation = CreateCompilation("ConsumerAssembly", source, references);
        EnsureNoErrors(
            compilation.GetDiagnostics(),
            "consumer does not compile before generation");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new WorkflowIncrementalGenerator().AsSourceGenerator(),
            new StateReducerIncrementalGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var output,
            out var diagnostics);

        EnsureNoDriverCrash(diagnostics, "ConsumerAssembly");
        return new ConsumerCompilation(diagnostics, output, driver.GetRunResult());
    }

    /// <summary>
    /// Resolves <paramref name="producer"/> as an assembly symbol, so a test can read what the
    /// producer's METADATA carries — the shape a consuming analyzer is limited to.
    /// </summary>
    /// <param name="producer">The compiled producer.</param>
    /// <returns>The producer's assembly symbol.</returns>
    /// <exception cref="InvalidOperationException">The reference does not resolve.</exception>
    public static IAssemblySymbol ResolveSymbol(ProducerAssembly producer)
    {
        ArgumentNullException.ThrowIfNull(producer);

        var probe = CSharpCompilation.Create(
            assemblyName: "MetadataProbe",
            syntaxTrees: [],
            references: [.. GeneratorTestHelper.GetCompileGraphReferences(), producer.Reference],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return probe.GetAssemblyOrModuleSymbol(producer.Reference) as IAssemblySymbol
            ?? throw new InvalidOperationException(
                $"producer '{producer.AssemblyName}' did not resolve to an assembly symbol.");
    }

    private static CSharpCompilation CreateCompilation(
        string assemblyName,
        string source,
        IEnumerable<MetadataReference> references) =>
        CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText("global using System.Collections.Generic;"),
            ],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static void EnsureNoDriverCrash(
        ImmutableArray<Diagnostic> diagnostics,
        string assemblyName)
    {
        var crashes = diagnostics
            .Where(static diagnostic => diagnostic.Id is "CS8784" or "CS8785")
            .ToArray();
        if (crashes.Length > 0)
        {
            throw new InvalidOperationException(
                $"the generator crashed inside the driver for '{assemblyName}': " + Describe(crashes));
        }
    }

    private static void EnsureNoErrors(IEnumerable<Diagnostic> diagnostics, string message)
    {
        var errors = diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(message + ": " + Describe(errors));
        }
    }

    private static string Describe(IEnumerable<Diagnostic> diagnostics) =>
        string.Join(" | ", diagnostics.Select(static diagnostic =>
            diagnostic.Id + ": " + diagnostic.GetMessage()));
}
