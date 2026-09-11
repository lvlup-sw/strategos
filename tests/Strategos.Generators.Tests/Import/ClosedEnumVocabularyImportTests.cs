// -----------------------------------------------------------------------
// <copyright file="ClosedEnumVocabularyImportTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Text;
using System.Threading;

using Strategos.Generators.Tests.Fixtures;

using Microsoft.CodeAnalysis.Text;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// #221 — the import front-end reads every closed-enum wire slot as an opaque string
/// (the netstandard2.0 twins carry no CLR enum handles, INV-8), and until this gate any
/// token was accepted and carried into the IR. One diagnostic, AGWF049, covers the whole
/// class: a slot typed by a closed contract enum whose declared value is outside that
/// enum's vocabulary.
/// <para>
/// The four slots under test are the four the wire surface actually exposes:
/// <c>gates[].class</c> (<c>GateClass</c>), <c>diagnosticForks[].permittedTriggers[].trigger</c>
/// (<c>ForkTrigger</c>), <c>steps[].runtime</c> (<c>StepRuntime</c>) and the failure-handler
/// <c>scope</c> (<c>FailureHandlerScope</c>). <see cref="AcceptedSet_IsTheEmittedSchemasOwnVocabulary"/>
/// is the anti-drift pin: the set the shipped check enforces is compared to the emitted
/// JSON Schema, so a member added in TypeSpec cannot leave this validator behind.
/// </para>
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class ClosedEnumVocabularyImportTests
{
    // Literal ids are permitted in tests (the single-source grep gate excludes *.Tests
    // projects); production C# routes through the generated AgwfCodes constants.
    private const string ClosedEnumCode = "AGWF049";

    private const string StepTypes = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Steps;

        namespace ClosedEnumNs;

        [WorkflowState]
        public sealed record VocabState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public sealed class VocabStepA : IWorkflowStep<VocabState>
        {
            public Task<StepResult<VocabState>> ExecuteAsync(VocabState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<VocabState>.FromState(s));
        }

        public sealed class VocabStepB : IWorkflowStep<VocabState>
        {
            public Task<StepResult<VocabState>> ExecuteAsync(VocabState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<VocabState>.FromState(s));
        }
        """;

    /// <summary>
    /// The gate class that motivated #221: <c>AntipatternDetection</c> is a pre-0.4.0 class
    /// name that no longer exists in <c>GateClass</c>. Four wire fixtures carried it for four
    /// minors and the .NET arm accepted every one.
    /// </summary>
    private const string RetiredGateClassJson = """
        {
          "schemaVersion": "1.0",
          "name": "vocab-retired-gate-class",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "VocabStepA", "isTerminal": false, "stepType": "VocabStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "VocabStepB", "isTerminal": true, "stepType": "VocabStepB" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "gates": [ { "class": "AntipatternDetection", "id": "g1" } ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    /// <summary>
    /// The member name used in place of its wire value — <c>Typecheck</c> for <c>typecheck</c>.
    /// The contract is explicit that consumers round-trip these BY VALUE, never by member name,
    /// so the near-miss must fail exactly as loudly as the retired class.
    /// </summary>
    private const string PascalCaseGateClassJson = """
        {
          "schemaVersion": "1.0",
          "name": "vocab-pascal-gate-class",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "VocabStepA", "isTerminal": false, "stepType": "VocabStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "VocabStepB", "isTerminal": true, "stepType": "VocabStepB" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "gates": [ { "class": "Typecheck", "id": "g1" } ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    /// <summary>A permitted fork trigger declared by member name rather than wire value.</summary>
    private const string UnknownForkTriggerJson = """
        {
          "schemaVersion": "1.0",
          "name": "vocab-unknown-trigger",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "VocabStepA", "isTerminal": false, "stepType": "VocabStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "VocabStepB", "isTerminal": true, "stepType": "VocabStepB" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [], "gates": [],
          "diagnosticForks": [
            {
              "fromStepId": "s1",
              "compensationSeed": "vocabSeed",
              "permittedTriggers": [
                { "trigger": "RatificationFailure", "requiredEvidenceFields": [ "stampId" ] }
              ]
            }
          ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    /// <summary>A step runtime outside the three-member federation slot.</summary>
    private const string UnknownStepRuntimeJson = """
        {
          "schemaVersion": "1.0",
          "name": "vocab-unknown-runtime",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "VocabStepA", "isTerminal": false, "stepType": "VocabStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "VocabStepB", "isTerminal": true, "stepType": "VocabStepB", "runtime": "kubernetes" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [], "gates": [],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    /// <summary>A failure-handler scope outside <c>workflow | step | forkPath</c>.</summary>
    private const string UnknownHandlerScopeJson = """
        {
          "schemaVersion": "1.0",
          "name": "vocab-unknown-scope",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "VocabStepA", "isTerminal": false, "stepType": "VocabStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "VocabStepB", "isTerminal": true, "stepType": "VocabStepB" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [
            { "handlerId": "h1", "scope": "global", "steps": [], "isTerminal": true }
          ],
          "approvalPoints": [], "gates": [],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    /// <summary>
    /// Negative control: every one of the four slots declared with a legitimate wire value.
    /// Without this the suite could pass by rejecting everything.
    /// </summary>
    private const string AllSlotsWellDeclaredJson = """
        {
          "schemaVersion": "1.0",
          "name": "vocab-all-slots-ok",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "VocabStepA", "isTerminal": false, "stepType": "VocabStepA", "runtime": "strategos" },
            { "kind": "gate", "stepId": "s2", "stepName": "VocabStepB", "isTerminal": true, "stepType": "VocabStepB", "gateId": "g1", "runtime": "exarchos" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [
            { "handlerId": "h1", "scope": "workflow", "steps": [], "isTerminal": true }
          ],
          "approvalPoints": [],
          "gates": [ { "class": "mutation_adequacy", "id": "g1" } ],
          "diagnosticForks": [
            {
              "fromStepId": "s1",
              "anchorStepIds": [ "s1" ],
              "maxForks": 1,
              "compensationSeed": "vocabSeed",
              "permittedTriggers": [
                { "trigger": "operator_explicit", "requiredEvidenceFields": [ "operatorId" ] }
              ]
            }
          ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    /// <summary>Every rejected slot, with the JSON path and the offending token AGWF049 must name.</summary>
    /// <returns>The rejected-slot cases.</returns>
    public static IEnumerable<(string Label, string Json, string JsonPath, string Token, string EnumName)> RejectedSlots()
    {
        yield return ("retired gate class", RetiredGateClassJson, "$.gates[0].class", "AntipatternDetection", "GateClass");
        yield return ("member name for gate class", PascalCaseGateClassJson, "$.gates[0].class", "Typecheck", "GateClass");
        yield return ("member name for fork trigger", UnknownForkTriggerJson, "$.diagnosticForks[0].permittedTriggers[0].trigger", "RatificationFailure", "ForkTrigger");
        yield return ("unknown step runtime", UnknownStepRuntimeJson, "$.steps[1].runtime", "kubernetes", "StepRuntime");
        yield return ("unknown handler scope", UnknownHandlerScopeJson, "$.failureHandlers[0].scope", "global", "FailureHandlerScope");
    }

    /// <summary>
    /// A closed-enum slot carrying a value outside its vocabulary fails the build with AGWF049,
    /// naming the JSON path, the offending token and the enum — and lowers no saga.
    /// </summary>
    /// <param name="label">The case label (surfaced in the failure message).</param>
    /// <param name="json">The wire fixture.</param>
    /// <param name="jsonPath">The path AGWF049 must name.</param>
    /// <param name="token">The offending token AGWF049 must quote.</param>
    /// <param name="enumName">The closed enum AGWF049 must name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodDataSource(nameof(RejectedSlots))]
    public async Task ClosedEnumSlot_OutsideItsVocabulary_IsRejected(
        string label,
        string json,
        string jsonPath,
        string token,
        string enumName)
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(
            StepTypes,
            [new InMemoryAdditionalText("vocab.workflow.json", json)],
            ClosedEnumCode);

        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        await Assert.That(errors).HasCount().EqualTo(1)
            .Because($"'{label}' must fail exclusively with {ClosedEnumCode}.");
        await Assert.That(errors[0].Id).IsEqualTo(ClosedEnumCode)
            .Because($"'{label}' is a closed-enum vocabulary violation.");

        var message = errors[0].GetMessage();
        await Assert.That(message).Contains(jsonPath)
            .Because($"{ClosedEnumCode} must name the JSON path of the offending slot.");
        await Assert.That(message).Contains(token)
            .Because($"{ClosedEnumCode} must quote the token that was refused.");
        await Assert.That(message).Contains(enumName)
            .Because($"{ClosedEnumCode} must name the closed enum whose vocabulary was violated.");

        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsFalse()
            .Because($"a workflow rejected by {ClosedEnumCode} must not lower a saga.");
    }

    /// <summary>
    /// Negative control — the four slots declared with legitimate wire values are accepted and
    /// still lower a saga, so the gate is proven additive rather than a blanket refusal.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EverySlot_WithItsWireValue_IsAcceptedAndStillLowers()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(
            StepTypes,
            [new InMemoryAdditionalText("vocab-ok.workflow.json", AllSlotsWellDeclaredJson)]);

        await Assert.That(result.Diagnostics.Any(d => d.Id == ClosedEnumCode)).IsFalse()
            .Because("every slot carries a value the emitted vocabulary declares.");
        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a well-declared import must still lower a saga.");
    }

    /// <summary>
    /// The anti-drift pin: the vocabulary the SHIPPED check enforces — recovered from the
    /// diagnostic message itself — is exactly the emitted JSON Schema's <c>enum</c> array,
    /// in schema order.
    /// </summary>
    /// <remarks>
    /// This is what makes the accepted set derived rather than duplicated. The check reads a
    /// generated constants file that the codegen guard keeps in step with the schema, and this
    /// test closes the remaining hop: that the file is what the validator actually consults.
    /// Adding a <c>GateClass</c> member without regenerating fails here, not in a consumer.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AcceptedSet_IsTheEmittedSchemasOwnVocabulary()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(
            StepTypes,
            [new InMemoryAdditionalText("vocab.workflow.json", RetiredGateClassJson)],
            ClosedEnumCode);

        var message = result.Diagnostics.Single(d => d.Id == ClosedEnumCode).GetMessage();

        // The descriptor renders the whole vocabulary as the message's closing clause.
        const string Marker = "vocabulary is exactly: ";
        var start = message.LastIndexOf(Marker, StringComparison.Ordinal);
        await Assert.That(start).IsGreaterThanOrEqualTo(0)
            .Because("the diagnostic must render the vocabulary it enforced.");

        var rendered = message[(start + Marker.Length)..]
            .TrimEnd('.')
            .Split(',')
            .Select(part => part.Trim())
            .ToArray();

        var schemaValues = ContractsSchemaPaths.LoadModel("GateClass")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();

        await Assert.That(rendered).IsEquivalentTo(schemaValues)
            .Because(
                "the accepted set must be DERIVED from the emitted contract, not re-typed in the "
                + "validator — a member added in TypeSpec must widen this check with no edit to it.");
    }

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
