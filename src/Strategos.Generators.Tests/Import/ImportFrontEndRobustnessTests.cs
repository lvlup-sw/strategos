// -----------------------------------------------------------------------
// <copyright file="ImportFrontEndRobustnessTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;
using System.Threading;

using Strategos.Generators.Tests.Fixtures;

using Microsoft.CodeAnalysis.Text;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// FIX-3 (#100) — import front-end robustness. Drives <see cref="WorkflowIncrementalGenerator"/>
/// over <c>*.workflow.json</c> <c>AdditionalFiles</c> and pins two hardening obligations the earlier
/// front-end left as silent-failure / fail-OPEN modes:
/// <list type="bullet">
///   <item><description>
///     M2 — an unresolvable compensation or approver moniker must fail CLOSED: surface the stable
///     resolution diagnostic (AGWF025) and lower NO saga, matching the primary-step path — rather
///     than discarding the diagnostic and lowering a saga that references an unregistered step.
///   </description></item>
///   <item><description>
///     M4 — a well-formed but structurally schema-invalid document (blank/missing name, or a
///     non-array <c>steps</c> field that binds to zero steps) must surface a STABLE build diagnostic
///     (DR-12), not be silently swallowed into an empty no-op.
///   </description></item>
/// </list>
/// Each hardening case is paired with a negative control (a RESOLVABLE moniker) proving the checks
/// are additive, not over-broad — the resolvable case still lowers a saga.
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class ImportFrontEndRobustnessTests
{
    // Stable AGWF ids under test. Literal ids are permitted in tests (the single-source grep gate
    // excludes *.Tests projects); production C# routes through the generated AgwfCodes constants.
    private const string UnresolvableMonikerCode = "AGWF025";
    private const string EmptyWorkflowNameCode = "AGWF001";
    private const string NoStepsFoundCode = "AGWF002";
    private const string MalformedWorkflowJsonCode = "AGWF023";

    /// <summary>
    /// Real step types so a primary step (and a resolvable compensation/approver) can bind and lower.
    /// The <c>Ghost*</c> monikers under test intentionally have NO matching type.
    /// </summary>
    private const string StepTypes = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Steps;

        namespace RobustNs;

        [WorkflowState]
        public sealed record RobustState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public sealed class RobustStepA : IWorkflowStep<RobustState>
        {
            public Task<StepResult<RobustState>> ExecuteAsync(RobustState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RobustState>.FromState(s));
        }

        public sealed class RobustStepB : IWorkflowStep<RobustState>
        {
            public Task<StepResult<RobustState>> ExecuteAsync(RobustState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RobustState>.FromState(s));
        }

        public sealed class RobustStepC : IWorkflowStep<RobustState>
        {
            public Task<StepResult<RobustState>> ExecuteAsync(RobustState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RobustState>.FromState(s));
        }
        """;

    // M2: a step whose compensation moniker ("GhostCompensationStep") does not resolve.
    private const string UnresolvableCompensationJson = """
        {
          "schemaVersion": "1.0",
          "name": "unresolvable-compensation",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RobustStepA", "isTerminal": true, "stepType": "RobustStepA",
              "configuration": { "compensation": { "compensationStepType": "GhostCompensationStep" } } }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "s1"
        }
        """;

    // M2 negative control: the same shape with a RESOLVABLE compensation moniker ("RobustStepB").
    private const string ResolvableCompensationJson = """
        {
          "schemaVersion": "1.0",
          "name": "resolvable-compensation",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RobustStepA", "isTerminal": true, "stepType": "RobustStepA",
              "configuration": { "compensation": { "compensationStepType": "RobustStepB" } } }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "s1"
        }
        """;

    // M2: a context-free approval whose approver moniker ("GhostApprover") does not resolve.
    private const string UnresolvableApproverJson = """
        {
          "schemaVersion": "1.0",
          "name": "unresolvable-approver",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RobustStepA", "isTerminal": false, "stepType": "RobustStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "RobustStepB", "isTerminal": true, "stepType": "RobustStepB" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": [
            { "approvalPointId": "ap1", "approverType": "GhostApprover", "precedingStepId": "s1", "hasContext": false }
          ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // M2 negative control: the same shape with a RESOLVABLE approver moniker ("RobustStepC").
    private const string ResolvableApproverJson = """
        {
          "schemaVersion": "1.0",
          "name": "resolvable-approver",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RobustStepA", "isTerminal": false, "stepType": "RobustStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "RobustStepB", "isTerminal": true, "stepType": "RobustStepB" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": [
            { "approvalPointId": "ap1", "approverType": "RobustStepC", "precedingStepId": "s1", "hasContext": false }
          ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // M4: a well-formed, schema-1.0 document whose name is blank (whitespace).
    private const string BlankNameJson = """
        {
          "schemaVersion": "1.0",
          "name": "   ",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RobustStepA", "isTerminal": true, "stepType": "RobustStepA" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "s1"
        }
        """;

    // M4: a well-formed, schema-1.0 document with NO name field at all.
    private const string MissingNameJson = """
        {
          "schemaVersion": "1.0",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RobustStepA", "isTerminal": true, "stepType": "RobustStepA" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "s1"
        }
        """;

    // M4: a well-formed, schema-1.0 document whose `steps` is a STRING, not an array. The reader
    // coerces a non-array `steps` to an empty list, so the document binds to zero steps.
    private const string NonArrayStepsJson = """
        {
          "schemaVersion": "1.0",
          "name": "robust-nonarray-steps",
          "steps": "definitely-not-an-array",
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": []
        }
        """;

    /// <summary>M2: an unresolvable compensation moniker surfaces AGWF025 and lowers NO saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnresolvableCompensationMoniker_FailsClosed_WithDiagnosticAndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("unresolvable-compensation.workflow.json", UnresolvableCompensationJson),
            UnresolvableMonikerCode);
        await AssertFailedClosed(result, UnresolvableMonikerCode, "GhostCompensationStep");
    }

    /// <summary>M2 negative control: a resolvable compensation moniker lowers a saga and reports no AGWF025.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ResolvableCompensationMoniker_LowersSaga_WithNoUnresolvableDiagnostic()
    {
        var result = RunGenerator(StepTypes, ("resolvable-compensation.workflow.json", ResolvableCompensationJson));
        await AssertLoweredSaga(result, UnresolvableMonikerCode);
    }

    /// <summary>M2: an unresolvable approver moniker surfaces AGWF025 and lowers NO saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnresolvableApproverMoniker_FailsClosed_WithDiagnosticAndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("unresolvable-approver.workflow.json", UnresolvableApproverJson),
            UnresolvableMonikerCode);
        await AssertFailedClosed(result, UnresolvableMonikerCode, "GhostApprover");
    }

    /// <summary>M2 negative control: a resolvable approver moniker lowers a saga and reports no AGWF025.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ResolvableApproverMoniker_LowersSaga_WithNoUnresolvableDiagnostic()
    {
        var result = RunGenerator(StepTypes, ("resolvable-approver.workflow.json", ResolvableApproverJson));
        await AssertLoweredSaga(result, UnresolvableMonikerCode);
    }

    /// <summary>M4: a blank workflow name surfaces AGWF001 (not a silent skip) and lowers NO saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BlankWorkflowName_SurfacesStableDiagnostic_AndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("blank-name.workflow.json", BlankNameJson),
            EmptyWorkflowNameCode);
        await AssertReportsCodeAndNoSaga(result, EmptyWorkflowNameCode);
    }

    /// <summary>M4: a missing workflow name surfaces AGWF001 (not a silent skip) and lowers NO saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingWorkflowName_SurfacesStableDiagnostic_AndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("missing-name.workflow.json", MissingNameJson),
            EmptyWorkflowNameCode);
        await AssertReportsCodeAndNoSaga(result, EmptyWorkflowNameCode);
    }

    /// <summary>
    /// M4: a non-array <c>steps</c> field binds to zero steps and surfaces AGWF002 (not a silent
    /// skip); no saga is lowered.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NonArraySteps_SurfacesStableDiagnostic_AndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("robust-nonarray-steps.workflow.json", NonArrayStepsJson),
            NoStepsFoundCode);
        await AssertReportsCodeAndNoSaga(result, NoStepsFoundCode);
    }

    /// <summary>
    /// A present <c>action</c> token is never collapsed into the same state as an
    /// omitted optional action. Null, scalar, array, incomplete-object, and blank
    /// identity shapes all fail closed through the stable import diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PresentMalformedActionToken_FailsClosed_WithStableDiagnostic()
    {
        var cases = new (string Name, string Token, string ExpectedDetail)[]
        {
            ("null", "null", "property 'action'"),
            ("scalar", "\"orders\"", "property 'action'"),
            ("array", "[]", "property 'action'"),
            ("incomplete-object", "{ \"domainName\": \"orders\", \"objectTypeName\": \"Order\" }", "action.actionName"),
            ("blank-identity", "{ \"domainName\": \"   \", \"objectTypeName\": \"Order\", \"actionName\": \"run\" }", "action.domainName"),
        };

        foreach (var testCase in cases)
        {
            var result = RunGenerator(
                StepTypes,
                ($"malformed-action-{testCase.Name}.workflow.json", WorkflowWithAction(testCase.Token)),
                MalformedWorkflowJsonCode);
            var errors = ErrorDiagnostics(result);
            await Assert.That(errors).HasCount().EqualTo(1)
                .Because("malformed action input must fail for exactly the stable import reason.");
            var diagnostic = errors.SingleOrDefault(
                diagnostic => diagnostic.Id == MalformedWorkflowJsonCode);

            await Assert.That(diagnostic).IsNotNull()
                .Because($"a present {testCase.Name} action token must not be treated as omission.");
            await Assert.That(diagnostic!.GetMessage()).Contains(testCase.ExpectedDetail)
                .Because("the stable import diagnostic must identify the malformed action field.");
            await Assert.That(result.GeneratedTrees.Any(
                    tree => tree.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
                .IsFalse()
                .Because("a malformed proof-bearing action reference must not lower a saga.");
        }
    }

    /// <summary>
    /// A present typed inverse is proof-bearing data and follows the same fail-closed
    /// object/identity rules as a forward occurrence action reference.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PresentMalformedInverseActionToken_FailsClosed_WithStableDiagnostic()
    {
        var cases = new (string Name, string Token, string ExpectedDetail)[]
        {
            ("null", "null", "property 'inverseAction'"),
            ("scalar", "\"orders\"", "property 'inverseAction'"),
            ("array", "[]", "property 'inverseAction'"),
            ("incomplete-object", "{ \"domainName\": \"orders\", \"objectTypeName\": \"Order\" }", "inverseAction.actionName"),
            ("blank-identity", "{ \"domainName\": \"orders\", \"objectTypeName\": \"   \", \"actionName\": \"undo\" }", "inverseAction.objectTypeName"),
        };

        foreach (var testCase in cases)
        {
            var result = RunGenerator(
                StepTypes,
                ($"malformed-inverse-{testCase.Name}.workflow.json", WorkflowWithInverseAction(testCase.Token)),
                MalformedWorkflowJsonCode);
            var errors = ErrorDiagnostics(result);
            await Assert.That(errors).HasCount().EqualTo(1)
                .Because("a malformed typed inverse must fail for exactly the stable import reason.");
            var diagnostic = errors.SingleOrDefault(
                item => item.Id == MalformedWorkflowJsonCode);

            await Assert.That(diagnostic).IsNotNull();
            await Assert.That(diagnostic!.GetMessage()).Contains(testCase.ExpectedDetail);
            await Assert.That(result.GeneratedTrees.Any(
                    tree => tree.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
                .IsFalse()
                .Because("a malformed inverse action must not lower a saga.");
        }
    }

    /// <summary>An omitted optional action remains importable and distinct from an explicit null.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OmittedAction_RemainsImportable()
    {
        var result = RunGenerator(StepTypes, ("omitted-action.workflow.json", WorkflowWithoutAction()));

        await AssertNoErrors(result);
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == MalformedWorkflowJsonCode))
            .IsFalse();
        await Assert.That(result.GeneratedTrees.Any(
                tree => tree.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsTrue();
    }

    /// <summary>
    /// Asserts the run failed CLOSED on an unresolvable moniker: it reported the stable
    /// <paramref name="expectedId"/> diagnostic naming the offending moniker, and emitted NO saga.
    /// </summary>
    private static async Task AssertFailedClosed(
        GeneratorDriverRunResult result,
        string expectedId,
        string expectedMoniker)
    {
        var errors = ErrorDiagnostics(result);
        await Assert.That(errors).HasCount().EqualTo(1)
            .Because($"an unresolvable moniker must fail exclusively with {expectedId}.");
        var diagnostic = errors.SingleOrDefault(d => d.Id == expectedId);
        await Assert.That(diagnostic).IsNotNull()
            .Because($"an unresolvable moniker must surface the stable {expectedId} diagnostic (fail closed).");
        await Assert.That(diagnostic!.GetMessage()).Contains(expectedMoniker)
            .Because($"{expectedId} must name the offending moniker '{expectedMoniker}'.");

        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsFalse()
            .Because($"a workflow with an unresolvable moniker must not lower a saga (no model is produced).");
    }

    private static string WorkflowWithAction(string actionToken) => $$"""
        {
          "schemaVersion": "1.0",
          "name": "malformed-action",
          "steps": [
            {
              "kind": "skill",
              "stepId": "s1",
              "stepName": "RobustStepA",
              "isTerminal": true,
              "stepType": "RobustStepA",
              "action": {{actionToken}}
            }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "s1"
        }
        """;

    private static string WorkflowWithInverseAction(string inverseActionToken) => $$"""
        {
          "schemaVersion": "1.0",
          "name": "malformed-inverse",
          "steps": [
            {
              "kind": "skill",
              "stepId": "s1",
              "stepName": "RobustStepA",
              "isTerminal": true,
              "stepType": "RobustStepA",
              "configuration": {
                "compensation": {
                  "compensationStepType": "RobustStepB",
                  "inverseAction": {{inverseActionToken}}
                }
              }
            }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "s1"
        }
        """;

    private static string WorkflowWithoutAction() => """
        {
          "schemaVersion": "1.0",
          "name": "omitted-action",
          "steps": [
            {
              "kind": "skill",
              "stepId": "s1",
              "stepName": "RobustStepA",
              "isTerminal": true,
              "stepType": "RobustStepA"
            }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "s1"
        }
        """;

    /// <summary>
    /// Asserts the run reported the stable <paramref name="expectedId"/> diagnostic (a structurally
    /// schema-invalid document is surfaced, not silently swallowed) and emitted NO saga.
    /// </summary>
    private static async Task AssertReportsCodeAndNoSaga(GeneratorDriverRunResult result, string expectedId)
    {
        var expectedDiagnostics = result.Diagnostics
            .Where(diagnostic => diagnostic.Id == expectedId)
            .ToArray();
        await Assert.That(expectedDiagnostics).HasCount().EqualTo(1)
            .Because($"a structurally schema-invalid import must surface the stable {expectedId} diagnostic, not be silently swallowed.");
        await Assert.That(ErrorDiagnostics(result).Where(diagnostic => diagnostic.Id != expectedId))
            .IsEmpty()
            .Because($"a structurally invalid import must not fail for a reason other than {expectedId}.");

        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a structurally schema-invalid import must not lower a saga.");
    }

    /// <summary>
    /// Negative control: asserts the run lowered a saga (the model IS produced) and did NOT report the
    /// <paramref name="forbiddenId"/> diagnostic — proving the fail-closed check is additive.
    /// </summary>
    private static async Task AssertLoweredSaga(GeneratorDriverRunResult result, string forbiddenId)
    {
        await AssertNoErrors(result);
        await Assert.That(result.Diagnostics.Any(d => d.Id == forbiddenId))
            .IsFalse()
            .Because($"a resolvable moniker must NOT surface {forbiddenId} (the fail-closed check is additive).");

        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a workflow whose monikers all resolve must lower a saga.");
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

    /// <summary>An in-memory <see cref="AdditionalText"/> for driving the generator over synthetic import files.</summary>
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
