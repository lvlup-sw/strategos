// -----------------------------------------------------------------------
// <copyright file="ImportIdentityGateTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;
using System.Threading;

using Strategos.Generators.Tests.Fixtures;

using Microsoft.CodeAnalysis.Text;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// JSON import must apply the same AGWF003 identity gate as C# <c>[Workflow]</c>
/// before <c>EmitWorkflowSources</c>. Path-qualified fork Handles compose with T1b
/// events; the import twin of #190 must emit a saga, not reject with a historical code.
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class ImportIdentityGateTests
{
    private const string StepTypes = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Steps;

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

        public sealed class CompensateStep : IWorkflowStep<IdentityState>
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

    private const string IndependentTopLevelSameNameJson = """
        {
          "schemaVersion": "1.0",
          "name": "import-identity-top-level-dup",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "PrepareStep", "isTerminal": false, "stepType": "PrepareStep" },
            { "kind": "skill", "stepId": "s-independent", "stepName": "AnalyzeStep", "isTerminal": false, "stepType": "AnalyzeStep" },
            { "kind": "skill", "stepId": "s-echo", "stepName": "AnalyzeStep", "isTerminal": false, "stepType": "AnalyzeStep" },
            { "kind": "skill", "stepId": "s2", "stepName": "SynthesizeStep", "isTerminal": false, "stepType": "SynthesizeStep" },
            { "kind": "skill", "stepId": "s3", "stepName": "CompleteStep", "isTerminal": true, "stepType": "CompleteStep" }
          ],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [
            {
              "forkPointId": "import-identity-top-level-dup-Fork0",
              "fromStepId": "s1",
              "joinStepId": "s2",
              "paths": [
                { "pathId": "p0", "pathIndex": 0, "steps": [ { "kind": "skill", "stepId": "fp0", "stepName": "AnalyzeStep", "isTerminal": false, "stepType": "AnalyzeStep" } ] },
                { "pathId": "p1", "pathIndex": 1, "steps": [ { "kind": "skill", "stepId": "fp1", "stepName": "ScoreStep", "isTerminal": false, "stepType": "ScoreStep" } ] }
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
    /// The JSON twin of the C# #190 path-end fixture emits a saga with path-qualified Handles.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_InstanceNamedForkPathEnds_EmitsQualifiedHandles()
    {
        var result = RunGenerator(StepTypes, ("identity-path-end.workflow.json", CollidingPathEndJson));
        await AssertQualifiedForkSaga(result, "TechnicalCompleted evt,", "FundamentalCompleted evt,");
    }

    /// <summary>
    /// The JSON twin of the C# fork-interior fixture emits a saga that chains each path's successor.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_InstanceNamedForkInteriors_EmitsQualifiedHandles()
    {
        var result = RunGenerator(StepTypes, ("identity-interior.workflow.json", CollidingInteriorJson));
        await AssertQualifiedForkSaga(result, "TechnicalCompleted evt,", "FundamentalCompleted evt,");
        var saga = GetSaga(result);
        await Assert.That(saga).Contains("StartScoreStepCommand");
        await Assert.That(saga).Contains("StartRiskStepCommand");
    }

    /// <summary>
    /// An independent top-level step that shares a fork-path EffectiveName still
    /// reports AGWF003. Consuming the round-trip echo must not drop the extra copy.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_IndependentTopLevelSameNameAsForkPath_ReportsAGWF003()
    {
        var result = RunGenerator(
            StepTypes,
            ("identity-top-level-dup.workflow.json", IndependentTopLevelSameNameJson),
            "AGWF003");
        await AssertRejected(result, ["AGWF003"], "AnalyzeStep");
    }

    /// <summary>
    /// A top-level occurrence is not the serialized echo of a fork-path occurrence merely
    /// because both lower to the same phase and CLR type. Their stable step ids distinguish
    /// the two occurrences, so the shared phase remains an ordinary AGWF003 collision.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_DifferentStepIdCannotMasqueradeAsForkPathEcho()
    {
        var json = ForkEchoJson(
            topLevelStepId: "independent",
            topLevelActionName: "inspect",
            pathStepId: "fork-path",
            pathActionName: "inspect");

        var result = RunGenerator(
            StepTypes,
            ("identity-false-echo-id.workflow.json", json),
            "AGWF003");

        await AssertRejected(result, ["AGWF003"], "AnalyzeStep");
    }

    /// <summary>
    /// The two serialized representations of one fork-path occurrence must agree on its
    /// occurrence-scoped ontology action. A conflicting action is not silently first-wins.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_ConflictingActionCannotMasqueradeAsForkPathEcho()
    {
        var json = ForkEchoJson(
            topLevelStepId: "fork-path",
            topLevelActionName: "inspect-top-level",
            pathStepId: "fork-path",
            pathActionName: "inspect-nested");

        var result = RunGenerator(
            StepTypes,
            ("identity-false-echo-action.workflow.json", json),
            "AGWF003",
            "AGWF042");

        await AssertRejected(result, ["AGWF003", "AGWF042"], "AnalyzeStep");
    }

    /// <summary>
    /// A compensation declaration on only one serialized copy is not an echo. Treating the
    /// nested copy as authoritative would silently erase rollback behavior from the other copy.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_ConflictingCompensationCannotMasqueradeAsForkPathEcho()
    {
        const string compensation = """
            {
              "compensation": {
                "compensationStepType": "CompensateStep",
                "requiredOnFailure": true,
                "timeout": "PT30S"
              }
            }
            """;
        var json = ForkEchoJson(topLevelConfiguration: compensation);

        var result = RunGenerator(
            StepTypes,
            ("identity-false-echo-compensation.workflow.json", json),
            "AGWF003",
            "AGWF042");

        await AssertRejected(result, ["AGWF003", "AGWF042"], "fork-path");
    }

    /// <summary>
    /// Retry policy disagreement between duplicate serialized positions is rejected rather than
    /// selecting whichever position happens to survive model composition.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_ConflictingRetryCannotMasqueradeAsForkPathEcho()
    {
        const string retry = """
            {
              "retry": {
                "maxAttempts": 3,
                "initialDelay": "PT1S",
                "backoffMultiplier": 2,
                "maxDelay": "PT5S",
                "useJitter": true
              }
            }
            """;
        var json = ForkEchoJson(topLevelConfiguration: retry);

        var result = RunGenerator(
            StepTypes,
            ("identity-false-echo-retry.workflow.json", json),
            "AGWF003",
            "AGWF042");

        await AssertRejected(result, ["AGWF003", "AGWF042"], "fork-path");
    }

    /// <summary>
    /// Timeout policy disagreement between duplicate serialized positions is rejected.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_ConflictingTimeoutCannotMasqueradeAsForkPathEcho()
    {
        const string timeout = """{ "timeout": "PT10S" }""";
        var json = ForkEchoJson(topLevelConfiguration: timeout);

        var result = RunGenerator(
            StepTypes,
            ("identity-false-echo-timeout.workflow.json", json),
            "AGWF003",
            "AGWF042");

        await AssertRejected(result, ["AGWF003", "AGWF042"], "fork-path");
    }

    /// <summary>
    /// Confidence-routing disagreement between duplicate serialized positions is rejected.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_ConflictingConfidenceCannotMasqueradeAsForkPathEcho()
    {
        const string confidence = """
            {
              "confidenceThreshold": 0.8,
              "onLowConfidence": {
                "handlerId": "risk-handler",
                "handlerSteps": [
                  {
                    "kind": "skill", "stepId": "risk", "stepName": "RiskStep",
                    "isTerminal": true, "stepType": "RiskStep"
                  }
                ],
                "isTerminal": true
              }
            }
            """;
        var json = ForkEchoJson(topLevelConfiguration: confidence);

        var result = RunGenerator(
            StepTypes,
            ("identity-false-echo-confidence.workflow.json", json),
            "AGWF003",
            "AGWF042");

        await AssertRejected(result, ["AGWF003", "AGWF042"], "fork-path");
    }

    /// <summary>
    /// A stable step id cannot name occurrences with different instance identities, even though
    /// their distinct phase names would otherwise evade the duplicate-effective-name gate.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_ConflictingInstanceNameCannotMasqueradeAsForkPathEcho()
    {
        var json = ForkEchoJson(
            topLevelInstanceName: "TopLevel",
            pathInstanceName: "Nested");

        var result = RunGenerator(
            StepTypes,
            ("identity-false-echo-instance.workflow.json", json),
            "AGWF042");

        await AssertRejected(result, ["AGWF042"], "fork-path");
    }

    /// <summary>
    /// Terminal and runtime metadata are part of the serialized occurrence. Conflicting copies
    /// cannot be silently collapsed even where today's runtime lowering ignores a wire field.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_ConflictingTerminalOrRuntimeCannotMasqueradeAsForkPathEcho()
    {
        var json = ForkEchoJson(
            topLevelIsTerminal: true,
            topLevelRuntime: "remote",
            pathIsTerminal: false,
            pathRuntime: "exarchos");

        var result = RunGenerator(
            StepTypes,
            ("identity-false-echo-metadata.workflow.json", json),
            "AGWF003",
            "AGWF042");

        await AssertRejected(result, ["AGWF003", "AGWF042"], "fork-path");
    }

    /// <summary>
    /// Gate back-references are occurrence semantics, so two copies with the same stable id must
    /// agree on the gate declaration they reference.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_ConflictingGateCannotMasqueradeAsForkPathEcho()
    {
        var json = ForkEchoJson(
            stepKind: "gate",
            topLevelGateId: "gate-a",
            pathGateId: "gate-b",
            gates: """
                [
                  { "class": "rules", "id": "gate-a" },
                  { "class": "rules", "id": "gate-b" }
                ]
                """);

        var result = RunGenerator(
            StepTypes,
            ("identity-false-echo-gate.workflow.json", json),
            "AGWF003",
            "AGWF042");

        await AssertRejected(result, ["AGWF003", "AGWF042"], "fork-path");
    }

    /// <summary>
    /// The strict echo comparison still accepts the exact duplicate emitted by projection,
    /// including nested resilience and confidence configuration.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JsonImport_ExactFullyConfiguredForkPathEcho_IsAccepted()
    {
        const string configuration = """
            {
              "retry": {
                "maxAttempts": 3,
                "initialDelay": "PT1S",
                "backoffMultiplier": 2,
                "maxDelay": "PT5S",
                "useJitter": true
              },
              "timeout": "PT10S",
              "compensation": {
                "compensationStepType": "CompensateStep",
                "requiredOnFailure": true,
                "timeout": "PT30S"
              },
              "confidenceThreshold": 0.8,
              "onLowConfidence": {
                "handlerId": "risk-handler",
                "handlerSteps": [
                  {
                    "kind": "skill", "stepId": "risk", "stepName": "RiskStep",
                    "isTerminal": true, "stepType": "RiskStep"
                  }
                ],
                "isTerminal": true
              }
            }
            """;
        var json = ForkEchoJson(
            topLevelConfiguration: configuration,
            pathConfiguration: configuration,
            topLevelInstanceName: "Shared",
            pathInstanceName: "Shared",
            topLevelRuntime: "exarchos",
            pathRuntime: "exarchos");

        var result = RunGenerator(StepTypes, ("identity-exact-configured-echo.workflow.json", json));

        await AssertNoErrors(result);
        await Assert.That(result.Diagnostics.Any(d => d.Id is "AGWF003" or "AGWF042")).IsFalse();
        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsTrue();
    }

    private static string ForkEchoJson(
        string topLevelStepId = "fork-path",
        string topLevelActionName = "inspect",
        string pathStepId = "fork-path",
        string pathActionName = "inspect",
        string? topLevelConfiguration = null,
        string? pathConfiguration = null,
        string? topLevelInstanceName = null,
        string? pathInstanceName = null,
        bool topLevelIsTerminal = false,
        bool pathIsTerminal = false,
        string? topLevelRuntime = null,
        string? pathRuntime = null,
        string stepKind = "skill",
        string? topLevelGateId = null,
        string? pathGateId = null,
        string gates = "[]")
    {
        var topConfigurationProperty = OptionalObjectProperty("configuration", topLevelConfiguration);
        var pathConfigurationProperty = OptionalObjectProperty("configuration", pathConfiguration);
        var topInstanceProperty = OptionalStringProperty("instanceName", topLevelInstanceName);
        var pathInstanceProperty = OptionalStringProperty("instanceName", pathInstanceName);
        var topRuntimeProperty = OptionalStringProperty("runtime", topLevelRuntime);
        var pathRuntimeProperty = OptionalStringProperty("runtime", pathRuntime);
        var topGateProperty = OptionalStringProperty("gateId", topLevelGateId);
        var pathGateProperty = OptionalStringProperty("gateId", pathGateId);
        var topTerminal = topLevelIsTerminal ? "true" : "false";
        var pathTerminal = pathIsTerminal ? "true" : "false";

        return $$"""
        {
          "schemaVersion": "1.0",
          "name": "import-identity-false-echo",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "PrepareStep", "isTerminal": false, "stepType": "PrepareStep" },
            {
              "kind": "{{stepKind}}", "stepId": "{{topLevelStepId}}", "stepName": "AnalyzeStep",
              "isTerminal": {{topTerminal}}, "stepType": "AnalyzeStep",
              "action": { "domainName": "orders", "objectTypeName": "Order", "actionName": "{{topLevelActionName}}" }
              {{topInstanceProperty}}{{topRuntimeProperty}}{{topGateProperty}}{{topConfigurationProperty}}
            },
            { "kind": "skill", "stepId": "s2", "stepName": "SynthesizeStep", "isTerminal": false, "stepType": "SynthesizeStep" },
            { "kind": "skill", "stepId": "s3", "stepName": "CompleteStep", "isTerminal": true, "stepType": "CompleteStep" }
          ],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [
            {
              "forkPointId": "import-identity-false-echo-Fork0",
              "fromStepId": "s1",
              "joinStepId": "s2",
              "paths": [
                {
                  "pathId": "p0", "pathIndex": 0,
                  "steps": [
                    {
                      "kind": "{{stepKind}}", "stepId": "{{pathStepId}}", "stepName": "AnalyzeStep",
                      "isTerminal": {{pathTerminal}}, "stepType": "AnalyzeStep",
                      "action": { "domainName": "orders", "objectTypeName": "Order", "actionName": "{{pathActionName}}" }
                      {{pathInstanceProperty}}{{pathRuntimeProperty}}{{pathGateProperty}}{{pathConfigurationProperty}}
                    }
                  ]
                },
                {
                  "pathId": "p1", "pathIndex": 1,
                  "steps": [
                    { "kind": "skill", "stepId": "fp1", "stepName": "ScoreStep", "isTerminal": false, "stepType": "ScoreStep" }
                  ]
                }
              ]
            }
          ],
          "failureHandlers": [],
          "approvalPoints": [],
          "gates": {{gates}},
          "entryStepId": "s1",
          "terminalStepId": "s3"
        }
        """;
    }

    private static string OptionalStringProperty(string propertyName, string? value) =>
        value is null ? string.Empty : $", \"{propertyName}\": \"{value}\"";

    private static string OptionalObjectProperty(string propertyName, string? value) =>
        value is null ? string.Empty : $", \"{propertyName}\": {value}";

    private static async Task AssertQualifiedForkSaga(
        GeneratorDriverRunResult result,
        string firstHandleParameter,
        string secondHandleParameter)
    {
        await AssertNoErrors(result);
        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF003")).IsNull();
        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF036")).IsNull();
        var saga = GetSaga(result);
        await Assert.That(saga).IsNotEmpty();
        await Assert.That(CountHandlerParameterLines(saga, firstHandleParameter)).IsEqualTo(1);
        await Assert.That(CountHandlerParameterLines(saga, secondHandleParameter)).IsEqualTo(1);
        await Assert.That(CountHandlerParameterLines(saga, "AnalyzeStepCompleted evt,")).IsEqualTo(0);
    }

    private static string GetSaga(GeneratorDriverRunResult result)
        => result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal))
            ?.GetText()
            .ToString() ?? string.Empty;

    private static int CountHandlerParameterLines(string generatedSource, string parameterDeclaration) =>
        generatedSource
            .Split('\n')
            .Count(line => string.Equals(line.Trim(), parameterDeclaration, StringComparison.Ordinal));

    private static async Task AssertRejected(
        GeneratorDriverRunResult result,
        IReadOnlyCollection<string> expectedIds,
        string collidingType)
    {
        var errors = ErrorDiagnostics(result);
        await Assert.That(errors).HasCount().EqualTo(expectedIds.Count)
            .Because("the colliding import must fail with exactly its asserted identity diagnostics.");
        await Assert.That(errors.Select(static diagnostic => diagnostic.Id))
            .IsEquivalentTo(expectedIds)
            .Because("no allowed identity diagnostic may escape the assertion boundary.");
        await Assert.That(errors.Any(diagnostic => diagnostic.GetMessage().Contains(
                collidingType,
                StringComparison.Ordinal)))
            .IsTrue()
            .Because($"an identity diagnostic must name the colliding occurrence '{collidingType}'.");
        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a workflow rejected by the identity gate must not emit a saga.");
    }

    private static GeneratorDriverRunResult RunGenerator(
        string source,
        (string Path, string Content) additionalText,
        params string[] allowedGeneratorErrorIds)
    {
        AdditionalText[] texts = [new InMemoryAdditionalText(additionalText.Path, additionalText.Content)];

        return GeneratorTestHelper.RunGeneratorWithValidInput(
            source,
            texts,
            allowedGeneratorErrorIds);
    }

    private static Diagnostic[] ErrorDiagnostics(GeneratorDriverRunResult result) =>
        result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

    private static async Task AssertNoErrors(GeneratorDriverRunResult result) =>
        await Assert.That(ErrorDiagnostics(result)).IsEmpty()
            .Because("a legal import must not be accepted alongside an allowed error diagnostic.");

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
