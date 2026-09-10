// -----------------------------------------------------------------------
// <copyright file="HandAuthoredImportFixtureTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IO;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis.Text;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// Task 019 (#100), DR-15 + DR-3 + DR-2 — the hand-authored import-fixture family. These JSON
/// fixtures are DISTINCT from the builder-produced #53 corpus (whose charter forbids hand-written
/// JSON): they are loaded from disk (<see cref="ImportFixtureLoader"/>) and driven through the
/// source generator's import front-end to pin the import channel's semantic gates —
/// <list type="bullet">
///   <item><description>a delegate (lambda) step is rejected (AGWF027);</description></item>
///   <item><description>a gate-bearing definition with a resolvable back-reference lowers a saga (DR-3 tolerated);</description></item>
///   <item><description>a reliability-bearing gate declaration is rejected (AGWF033, DR-2);</description></item>
///   <item><description>a dangling <c>gateId</c> is rejected (AGWF032, DR-3);</description></item>
///   <item><description>a <c>schemaVersion</c> skew is rejected (AGWF024).</description></item>
/// </list>
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class HandAuthoredImportFixtureTests
{
    // Stable AGWF ids under test (literal ids are permitted in *.Tests projects).
    private const string DelegateCode = "AGWF027";
    private const string DanglingGateIdCode = "AGWF032";
    private const string ReliabilityGateCode = "AGWF033";
    private const string SchemaSkewCode = "AGWF024";

    /// <summary>Real step types so a NON-rejected import (the gate-bearing fixture) can resolve its monikers and lower.</summary>
    private const string StepTypes = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;

        namespace ImportFixtureNs;

        [WorkflowState]
        public sealed record ImpState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public sealed class ImpStepA : IWorkflowStep<ImpState>
        {
            public Task<StepResult<ImpState>> ExecuteAsync(ImpState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ImpState>.FromState(s));
        }

        public sealed class ImpStepB : IWorkflowStep<ImpState>
        {
            public Task<StepResult<ImpState>> ExecuteAsync(ImpState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ImpState>.FromState(s));
        }

        public sealed class ImpStepC : IWorkflowStep<ImpState>
        {
            public Task<StepResult<ImpState>> ExecuteAsync(ImpState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ImpState>.FromState(s));
        }
        """;

    /// <summary>The hand-authored delegate-step fixture is rejected with AGWF027 naming the step + its JSON path; no saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DelegateStepFixture_IsRejected_WithDiagnosticAndNoSaga()
    {
        var (path, json) = ImportFixtureLoader.Load("delegate-step.workflow.json");
        var result = RunGenerator(StepTypes, (path, json));
        await AssertRejected(result, DelegateCode, "$.steps[1]", "d1");
    }

    /// <summary>The hand-authored reliability-bearing gate fixture is rejected with AGWF033 (DR-2); no saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReliabilityBearingGateFixture_IsRejected_WithDiagnosticAndNoSaga()
    {
        var (path, json) = ImportFixtureLoader.Load("reliability-gate.workflow.json");
        var result = RunGenerator(StepTypes, (path, json));
        await AssertRejected(result, ReliabilityGateCode, "$.gates[0].reliability", "g1");
    }

    /// <summary>The hand-authored dangling-gateId fixture is rejected with AGWF032 (DR-3); no saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DanglingGateIdFixture_IsRejected_WithDiagnosticAndNoSaga()
    {
        var (path, json) = ImportFixtureLoader.Load("dangling-gate.workflow.json");
        var result = RunGenerator(StepTypes, (path, json));
        await AssertRejected(result, DanglingGateIdCode, "$.steps[1].gateId", "gX");
    }

    /// <summary>
    /// The hand-authored schemaVersion-skew fixture (schemaVersion "2.0") is rejected with AGWF024
    /// naming the file + the declared version; no saga is lowered.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SchemaSkewFixture_IsRejected_WithDiagnosticAndNoSaga()
    {
        var (path, json) = ImportFixtureLoader.Load("schema-skew.workflow.json");
        var result = RunGenerator(StepTypes, (path, json));

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == SchemaSkewCode);
        await Assert.That(diagnostic).IsNotNull()
            .Because("a schemaVersion other than 1.0 must surface the stable AGWF024 diagnostic.");
        await Assert.That(diagnostic!.GetMessage()).Contains("2.0")
            .Because("AGWF024 must name the unsupported declared schemaVersion.");
        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a schema-skewed import must not lower a saga.");
    }

    /// <summary>
    /// The hand-authored gate-bearing fixture (a resolvable <c>gateId</c>, no reliability) is NOT
    /// rejected (no AGWF032/AGWF033) and still lowers a saga — proving the DR-3 gate tolerance +
    /// DR-2 check are additive, not over-broad, from a hand-authored source.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GateBearingFixture_IsNotRejected_AndLowersSaga()
    {
        var (path, json) = ImportFixtureLoader.Load("gate-bearing.workflow.json");
        var result = RunGenerator(StepTypes, (path, json));

        await Assert.That(result.Diagnostics.Any(d => d.Id == DanglingGateIdCode || d.Id == ReliabilityGateCode))
            .IsFalse()
            .Because("a gate with a resolvable gateId and no reliability block is tolerated (DR-3), not rejected.");
        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a well-declared gate import must still lower a saga.");
    }

    /// <summary>
    /// Asserts the run reported the expected stable diagnostic (naming the construct + JSON path) and
    /// emitted NO saga for the rejected workflow.
    /// </summary>
    private static async Task AssertRejected(
        GeneratorDriverRunResult result,
        string expectedId,
        string expectedJsonPath,
        string expectedConstruct)
    {
        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == expectedId);
        await Assert.That(diagnostic).IsNotNull()
            .Because($"the rejected carrier/violation must surface the stable {expectedId} diagnostic.");

        var message = diagnostic!.GetMessage();
        await Assert.That(message).Contains(expectedJsonPath)
            .Because($"{expectedId} must name the JSON path of the offending construct.");
        await Assert.That(message).Contains(expectedConstruct)
            .Because($"{expectedId} must name the offending construct.");

        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsFalse()
            .Because($"a workflow rejected by {expectedId} must not emit a saga (no model is lowered).");
    }

    private static GeneratorDriverRunResult RunGenerator(string source, params (string Path, string Content)[] additionalTexts)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "ImportFixtureTestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var texts = additionalTexts
            .Select(t => (AdditionalText)new InMemoryAdditionalText(t.Path, t.Content))
            .ToArray();

        var driver = CSharpGeneratorDriver.Create(
            generators: [new WorkflowIncrementalGenerator().AsSourceGenerator()],
            additionalTexts: texts,
            parseOptions: null,
            optionsProvider: null);

        return driver.RunGenerators(compilation).GetRunResult();
    }

    private static List<MetadataReference> GetReferences()
    {
        var references = new List<MetadataReference>();

        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (var assembly in new[] { "System.Runtime.dll", "System.Private.CoreLib.dll", "netstandard.dll" })
        {
            var path = Path.Combine(runtimePath, assembly);
            if (File.Exists(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
                catch
                {
                    // Ignore assemblies that can't be loaded as references.
                }
            }
        }

        var abstractions = typeof(Strategos.Abstractions.IWorkflowState).Assembly;
        if (!string.IsNullOrEmpty(abstractions.Location))
        {
            references.Add(MetadataReference.CreateFromFile(abstractions.Location));
        }

        return references;
    }

    /// <summary>An in-memory <see cref="AdditionalText"/> for driving the generator over a loaded fixture.</summary>
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText text;

        public InMemoryAdditionalText(string path, string content)
        {
            this.Path = path;
            this.text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => this.text;
    }
}
