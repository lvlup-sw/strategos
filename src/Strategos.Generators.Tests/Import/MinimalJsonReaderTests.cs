// -----------------------------------------------------------------------
// <copyright file="MinimalJsonReaderTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Import;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// DR-12 (#100) — behavioral coverage for the vendored, dependency-free JSON
/// reader and its binding onto the wire-DTO twins. Exercises the import-subset
/// shape (steps, gates + gateId, diagnostic forks, context-marked approval,
/// step-resilience config) end to end, the low-level JSON grammar (escapes,
/// numbers, booleans, null, nesting), and the malformed-input failure mode the
/// bridge turns into a stable diagnostic.
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class MinimalJsonReaderTests
{
    private const string ImportSubsetWorkflow = """
        {
          "schemaVersion": "1.0",
          "name": "sample-import",
          "steps": [
            {
              "kind": "skill",
              "stepId": "s1",
              "stepName": "Intake",
              "isTerminal": false,
              "stepType": "IntakeStep",
              "configuration": {
                "confidenceThreshold": 0.85,
                "timeout": "PT30S",
                "retry": { "maxAttempts": 3, "backoffMultiplier": 2.0, "useJitter": true }
              }
            },
            {
              "kind": "gate",
              "stepId": "g1",
              "stepName": "QualityGate",
              "isTerminal": false,
              "stepType": "QualityGateStep",
              "gateId": "gate-1"
            }
          ],
          "transitions": [
            { "transitionId": "t1", "fromStepId": "s1", "toStepId": "g1", "isDefault": true }
          ],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": [
            { "approvalPointId": "a1", "approverType": "Manager", "precedingStepId": "s1", "hasContext": true }
          ],
          "gates": [
            { "class": "typecheck", "id": "gate-1" }
          ],
          "diagnosticForks": [
            {
              "anchorStepIds": ["s1"],
              "permittedTriggers": [
                { "trigger": "gate_contradiction", "requiredEvidenceFields": ["provisionalStampEventId"] }
              ],
              "maxForks": 2,
              "compensationSeed": "rollback-seed"
            }
          ],
          "entryStepId": "s1",
          "terminalStepId": "g1"
        }
        """;

    /// <summary>The reader binds the workflow root scalars and the entry/terminal step ids.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Read_FullWorkflow_BindsRootScalars()
    {
        var wf = WireWorkflowReader.Read(ImportSubsetWorkflow);

        await Assert.That(wf.SchemaVersion).IsEqualTo("1.0");
        await Assert.That(wf.Name).IsEqualTo("sample-import");
        await Assert.That(wf.EntryStepId).IsEqualTo("s1");
        await Assert.That(wf.TerminalStepId).IsEqualTo("g1");
        await Assert.That(wf.Steps.Count).IsEqualTo(2);
    }

    /// <summary>The reader discriminates step arms by <c>kind</c> and binds arm-specific fields.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Read_Steps_DiscriminatesArmsAndBindsArmFields()
    {
        var wf = WireWorkflowReader.Read(ImportSubsetWorkflow);

        var skill = wf.Steps[0] as SkillStep;
        await Assert.That(skill).IsNotNull();
        await Assert.That(skill!.StepId).IsEqualTo("s1");
        await Assert.That(skill.StepName).IsEqualTo("Intake");
        await Assert.That(skill.StepType).IsEqualTo("IntakeStep");
        await Assert.That(skill.IsTerminal).IsFalse();

        var gate = wf.Steps[1] as GateStep;
        await Assert.That(gate).IsNotNull();
        await Assert.That(gate!.StepType).IsEqualTo("QualityGateStep");
        await Assert.That(gate.GateId).IsEqualTo("gate-1");
    }

    /// <summary>The reader binds the nested step-resilience configuration tree.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Read_StepConfiguration_BindsResilienceTree()
    {
        var wf = WireWorkflowReader.Read(ImportSubsetWorkflow);

        var config = ((SkillStep)wf.Steps[0]).Configuration;
        await Assert.That(config).IsNotNull();
        await Assert.That(config!.ConfidenceThreshold).IsEqualTo(0.85);
        await Assert.That(config.Timeout).IsEqualTo("PT30S");
        await Assert.That(config.Retry).IsNotNull();
        await Assert.That(config.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(config.Retry.BackoffMultiplier).IsEqualTo(2.0);
        await Assert.That(config.Retry.UseJitter).IsEqualTo(true);
    }

    /// <summary>The reader binds gates, the context-marked approval, and the diagnostic fork edge.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Read_GatesApprovalAndDiagnosticFork_Bind()
    {
        var wf = WireWorkflowReader.Read(ImportSubsetWorkflow);

        await Assert.That(wf.Gates.Count).IsEqualTo(1);
        await Assert.That(wf.Gates[0].Class).IsEqualTo("typecheck");
        await Assert.That(wf.Gates[0].Id).IsEqualTo("gate-1");

        await Assert.That(wf.ApprovalPoints.Count).IsEqualTo(1);
        await Assert.That(wf.ApprovalPoints[0].HasContext).IsTrue();
        await Assert.That(wf.ApprovalPoints[0].ApproverType).IsEqualTo("Manager");

        await Assert.That(wf.DiagnosticForks.Count).IsEqualTo(1);
        var fork = wf.DiagnosticForks[0];
        await Assert.That(fork.AnchorStepIds).Contains("s1");
        await Assert.That(fork.MaxForks).IsEqualTo(2);
        await Assert.That(fork.CompensationSeed).IsEqualTo("rollback-seed");
        await Assert.That(fork.PermittedTriggers.Count).IsEqualTo(1);
        await Assert.That(fork.PermittedTriggers[0].Trigger).IsEqualTo("gate_contradiction");
        await Assert.That(fork.PermittedTriggers[0].RequiredEvidenceFields).Contains("provisionalStampEventId");
    }

    /// <summary>Absent optional fields bind to nulls and empty lists rather than throwing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Read_AbsentOptionalFields_YieldNullsAndEmptyLists()
    {
        const string minimal = """
            {
              "schemaVersion": "1.0",
              "name": "minimal",
              "steps": [],
              "transitions": [],
              "branchPoints": [],
              "loops": [],
              "forkPoints": [],
              "failureHandlers": [],
              "approvalPoints": []
            }
            """;

        var wf = WireWorkflowReader.Read(minimal);

        await Assert.That(wf.Name).IsEqualTo("minimal");
        await Assert.That(wf.EntryStepId).IsNull();
        await Assert.That(wf.Steps).IsEmpty();
        await Assert.That(wf.Gates).IsEmpty();
        await Assert.That(wf.DiagnosticForks).IsEmpty();
    }

    /// <summary>The low-level parser handles string escapes, number forms, booleans, null, and nesting.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Parse_HandlesEscapesNumbersBooleansAndNesting()
    {
        const string doc = """
            { "a": "x\nA\t\"q\"", "b": [1, -2.5, 1e3, true, false, null], "c": { } }
            """;

        var root = MinimalJsonReader.Parse(doc);
        await Assert.That(root.Kind).IsEqualTo(JsonKind.Object);

        await Assert.That(root.TryGetMember("a", out var a)).IsTrue();
        await Assert.That(a.AsStringOrNull()).IsEqualTo("x\nA\t\"q\"");

        await Assert.That(root.TryGetMember("b", out var b)).IsTrue();
        await Assert.That(b.Kind).IsEqualTo(JsonKind.Array);
        await Assert.That(b.Items.Count).IsEqualTo(6);
        await Assert.That(b.Items[0].AsIntOrNull()).IsEqualTo(1);
        await Assert.That(b.Items[1].AsDoubleOrNull()).IsEqualTo(-2.5);
        await Assert.That(b.Items[2].AsDoubleOrNull()).IsEqualTo(1000d);
        await Assert.That(b.Items[3].AsBoolOrNull()).IsEqualTo(true);
        await Assert.That(b.Items[4].AsBoolOrNull()).IsEqualTo(false);
        await Assert.That(b.Items[5].Kind).IsEqualTo(JsonKind.Null);

        await Assert.That(root.TryGetMember("c", out var c)).IsTrue();
        await Assert.That(c.Kind).IsEqualTo(JsonKind.Object);
        await Assert.That(c.Members.Count).IsEqualTo(0);
    }

    /// <summary>Malformed JSON surfaces as a <see cref="JsonParseException"/>, never a crash.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Read_MalformedJson_ThrowsJsonParseException()
    {
        await Assert.That(() => WireWorkflowReader.Read("{ \"name\": }"))
            .Throws<JsonParseException>();
    }

    /// <summary>Trailing content after the root value is rejected.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Parse_TrailingContent_ThrowsJsonParseException()
    {
        await Assert.That(() => MinimalJsonReader.Parse("{} garbage"))
            .Throws<JsonParseException>();
    }

    /// <summary>An unknown step <c>kind</c> discriminator is rejected.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Read_UnknownStepKind_ThrowsJsonParseException()
    {
        const string doc = """
            {
              "schemaVersion": "1.0",
              "name": "bad-kind",
              "steps": [ { "kind": "wormhole", "stepId": "x", "stepName": "X", "isTerminal": false } ],
              "transitions": [],
              "branchPoints": [],
              "loops": [],
              "forkPoints": [],
              "failureHandlers": [],
              "approvalPoints": []
            }
            """;

        await Assert.That(() => WireWorkflowReader.Read(doc))
            .Throws<JsonParseException>();
    }

    /// <summary>A non-object root is rejected.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Read_NonObjectRoot_ThrowsJsonParseException()
    {
        await Assert.That(() => WireWorkflowReader.Read("[ 1, 2, 3 ]"))
            .Throws<JsonParseException>();
    }

    /// <summary>
    /// M1: deeply-nested input surfaces a CATCHABLE <see cref="JsonParseException"/> at the
    /// nesting-depth guard rather than recursing until an UNCATCHABLE
    /// <see cref="StackOverflowException"/> crashes the whole generator/IDE host. The parser recurses
    /// once per nested container, so an adversarial "thousands of <c>[</c>" input would otherwise blow
    /// the stack — the exact failure the file's "never a generator crash" contract forbids.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Parse_DeeplyNestedInput_ThrowsJsonParseExceptionNotStackOverflow()
    {
        // 1,000,000 opening brackets: far past any depth guard, and deep enough that recursing one
        // CLR frame per level (the pre-fix behavior) reliably overflows the ~1 MB thread stack — an
        // UNCATCHABLE crash. The guard trips at its bound (a few hundred frames in), so this throws a
        // CATCHABLE JsonParseException instead, fast, without ever building the deep structure.
        var deeplyNested = new string('[', 1_000_000);

        await Assert.That(() => MinimalJsonReader.Parse(deeplyNested))
            .Throws<JsonParseException>();
    }

    /// <summary>
    /// M1 (negative control): a legitimately-nested document well within the depth bound still parses,
    /// proving the guard rejects only adversarial depth, not real workflow nesting.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Parse_ModeratelyNestedInput_ParsesWithoutError()
    {
        // 40 levels of nested arrays around a scalar — deeper than any real workflow document, still
        // comfortably under the depth bound.
        const int levels = 40;
        var doc = new string('[', levels) + "1" + new string(']', levels);

        var root = MinimalJsonReader.Parse(doc);

        var node = root;
        for (var i = 0; i < levels; i++)
        {
            await Assert.That(node.Kind).IsEqualTo(JsonKind.Array);
            await Assert.That(node.Items.Count).IsEqualTo(1);
            node = node.Items[0];
        }

        await Assert.That(node.AsIntOrNull()).IsEqualTo(1);
    }

    /// <summary>
    /// L2: an integer lexeme outside <see cref="int"/> range fails CLOSED (yields null) instead of
    /// silently wrapping via unchecked truncation (e.g. 4000000000 -> -294967296), which would
    /// corrupt a maxAttempts / maxIterations / maxForks slot into a nonsensical value.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AsIntOrNull_ValueOutsideIntRange_ReturnsNullNotWrapped()
    {
        var overLong = MinimalJsonReader.Parse("{ \"n\": 4000000000 }");
        await Assert.That(overLong.TryGetMember("n", out var nLong)).IsTrue();
        await Assert.That(nLong.AsIntOrNull()).IsNull()
            .Because("an out-of-int-range integer must fail closed, not wrap to a negative value.");

        var overDouble = MinimalJsonReader.Parse("{ \"n\": 9e18 }");
        await Assert.That(overDouble.TryGetMember("n", out var nDouble)).IsTrue();
        await Assert.That(nDouble.AsIntOrNull()).IsNull()
            .Because("an out-of-int-range fractional/exponential lexeme must also fail closed.");

        var negOverLong = MinimalJsonReader.Parse("{ \"n\": -4000000000 }");
        await Assert.That(negOverLong.TryGetMember("n", out var nNeg)).IsTrue();
        await Assert.That(nNeg.AsIntOrNull()).IsNull()
            .Because("a large negative out-of-range value must fail closed too.");
    }

    /// <summary>
    /// L2 (tolerance preserved): an in-range fractional lexeme in an integer slot (e.g. "5.0") still
    /// coerces to its truncated integer — the deliberate tolerance the fix must NOT regress.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AsIntOrNull_InRangeFractionalLexeme_StillCoercesToInt()
    {
        var doc = MinimalJsonReader.Parse("{ \"a\": 5.0, \"b\": 2147483647, \"c\": -2147483648 }");

        await Assert.That(doc.TryGetMember("a", out var a)).IsTrue();
        await Assert.That(a.AsIntOrNull()).IsEqualTo(5);

        await Assert.That(doc.TryGetMember("b", out var b)).IsTrue();
        await Assert.That(b.AsIntOrNull()).IsEqualTo(int.MaxValue)
            .Because("the in-range int boundary must still parse.");

        await Assert.That(doc.TryGetMember("c", out var c)).IsTrue();
        await Assert.That(c.AsIntOrNull()).IsEqualTo(int.MinValue)
            .Because("the in-range int boundary must still parse.");
    }
}
