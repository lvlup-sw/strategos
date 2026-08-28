// -----------------------------------------------------------------------
// <copyright file="ImportIdentityGateTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis.Text;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// JSON import must apply the same AGWF003 / AGWF036 identity gates as C#
/// <c>[Workflow]</c> before <c>EmitWorkflowSources</c>. Sharing emitters is not
/// sharing the gate: a colliding fork twin would otherwise lower to CS0111.
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class ImportIdentityGateTests
{
    private const string StepTypes = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;

        namespace IdentityNs;

        [WorkflowState]
        public sealed record IdentityState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public sealed class PrepareStep : IWorkflowStep<IdentityState>
        {
            public Task<StepResult<IdentityState>> ExecuteAsync(IdentityState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<IdentityState>.FromState(s));
        }

        public sealed class AnalyzeStep : IWorkflowStep<IdentityState>
        {
            public Task<StepResult<IdentityState>> ExecuteAsync(IdentityState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<IdentityState>.FromState(s));
        }

        public sealed class ScoreStep : IWorkflowStep<IdentityState>
        {
            public Task<StepResult<IdentityState>> ExecuteAsync(IdentityState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<IdentityState>.FromState(s));
        }

        public sealed class RiskStep : IWorkflowStep<IdentityState>
        {
            public Task<StepResult<IdentityState>> ExecuteAsync(IdentityState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<IdentityState>.FromState(s));
        }

        public sealed class SynthesizeStep : IWorkflowStep<IdentityState>
        {
            public Task<StepResult<IdentityState>> ExecuteAsync(IdentityState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<IdentityState>.FromState(s));
        }

        public sealed class CompleteStep : IWorkflowStep<IdentityState>
        {
            public Task<StepResult<IdentityState>> ExecuteAsync(IdentityState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<IdentityState>.FromState(s));
        }
        """;

    private const string CollidingPathEndJson = """
        {
          "schemaVersion": "1.0",
          "name": "import-identity-path-end",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "PrepareStep", "isTerminal": false, "stepType": "PrepareStep" },
            { "kind": "skill", "stepId": "s2", "stepName": "SynthesizeStep", "isTerminal": false, "stepType": "SynthesizeStep" },
            { "kind": "skill", "stepId": "s3", "stepName": "CompleteStep", "isTerminal": true, "stepType": "CompleteStep" }
          ],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [
            {
              "forkPointId": "import-identity-path-end-Fork0",
              "fromStepId": "s1",
              "joinStepId": "s2",
              "paths": [
                { "pathId": "p0", "pathIndex": 0, "steps": [ { "kind": "skill", "stepId": "fp0", "stepName": "AnalyzeStep", "instanceName": "Technical", "isTerminal": false, "stepType": "AnalyzeStep" } ] },
                { "pathId": "p1", "pathIndex": 1, "steps": [ { "kind": "skill", "stepId": "fp1", "stepName": "AnalyzeStep", "instanceName": "Fundamental", "isTerminal": false, "stepType": "AnalyzeStep" } ] }
              ]
            }
          ],
          "failureHandlers": [],
          "approvalPoints": [],
          "entryStepId": "s1",
          "terminalStepId": "s3"
        }
        """;

    private const string CollidingInteriorJson = """
        {
          "schemaVersion": "1.0",
          "name": "import-identity-interior",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "PrepareStep", "isTerminal": false, "stepType": "PrepareStep" },
            { "kind": "skill", "stepId": "s2", "stepName": "SynthesizeStep", "isTerminal": false, "stepType": "SynthesizeStep" },
            { "kind": "skill", "stepId": "s3", "stepName": "CompleteStep", "isTerminal": true, "stepType": "CompleteStep" }
          ],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [
            {
              "forkPointId": "import-identity-interior-Fork0",
              "fromStepId": "s1",
              "joinStepId": "s2",
              "paths": [
                { "pathId": "p0", "pathIndex": 0, "steps": [
                    { "kind": "skill", "stepId": "fp0a", "stepName": "AnalyzeStep", "instanceName": "Technical", "isTerminal": false, "stepType": "AnalyzeStep" },
                    { "kind": "skill", "stepId": "fp0b", "stepName": "ScoreStep", "isTerminal": false, "stepType": "ScoreStep" }
                ] },
                { "pathId": "p1", "pathIndex": 1, "steps": [
                    { "kind": "skill", "stepId": "fp1a", "stepName": "AnalyzeStep", "instanceName": "Fundamental", "isTerminal": false, "stepType": "AnalyzeStep" },
                    { "kind": "skill", "stepId": "fp1b", "stepName": "RiskStep", "isTerminal": false, "stepType": "RiskStep" }
                ] }
              ]
            }
          ],
          "failureHandlers": [],
          "approvalPoints": [],
          "entryStepId": "s1",
          "terminalStepId": "s3"
        }
        """;

    /// <summary>
    /// The JSON twin of the C# #190 path-end fixture reports AGWF036 and emits no saga.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_InstanceNamedForkPathEnds_ReportsAGWF036()
    {
        var result = RunGenerator(StepTypes, ("identity-path-end.workflow.json", CollidingPathEndJson));
        await AssertRejected(result, "AGWF036", "AnalyzeStep");
    }

    /// <summary>
    /// The JSON twin of the C# fork-interior fixture reports AGWF036 and emits no saga.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_InstanceNamedForkInteriors_ReportsAGWF036()
    {
        var result = RunGenerator(StepTypes, ("identity-interior.workflow.json", CollidingInteriorJson));
        await AssertRejected(result, "AGWF036", "AnalyzeStep");
    }

    private static async Task AssertRejected(GeneratorDriverRunResult result, string expectedId, string collidingType)
    {
        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == expectedId);
        await Assert.That(diagnostic).IsNotNull()
            .Because($"the colliding import must surface {expectedId} before emission.");
        await Assert.That(diagnostic!.GetMessage()).Contains(collidingType);
        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsFalse()
            .Because($"a workflow rejected by {expectedId} must not emit a saga.");
    }

    private static GeneratorDriverRunResult RunGenerator(string source, params (string Path, string Content)[] additionalTexts)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "IdentityImportTestAssembly",
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

        var runtimePath = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (var assembly in new[] { "System.Runtime.dll", "System.Private.CoreLib.dll", "netstandard.dll" })
        {
            var path = System.IO.Path.Combine(runtimePath, assembly);
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
