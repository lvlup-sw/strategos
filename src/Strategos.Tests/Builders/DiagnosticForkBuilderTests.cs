// =============================================================================
// <copyright file="DiagnosticForkBuilderTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Builders;

using ForkTrigger = Strategos.Contracts.Generated.ForkTrigger;

namespace Strategos.Tests.Builders;

/// <summary>
/// API-shape / required-parameter tests for the <c>AllowDiagnosticFork</c> staged
/// builder surface (DR-7, #151). These pin the builder contract: the edge captures its
/// anchors, permitted triggers with their evidence-ref schema, compensation seed, and
/// <c>maxForks</c> bound into the workflow IR, and refuses the illegal states the type
/// staging cannot catch at compile time (missing seed / bound, duplicate trigger,
/// out-of-range bound, empty monikers).
/// </summary>
[Property("Category", "Unit")]
public sealed class DiagnosticForkBuilderTests
{
    // =========================================================================
    // A. Happy-path capture into the workflow IR
    // =========================================================================

    /// <summary>
    /// A fully-specified diagnostic fork is captured on the built
    /// <see cref="WorkflowDefinition{TState}.DiagnosticForks"/> with every declared
    /// component preserved.
    /// </summary>
    [Test]
    public async Task AllowDiagnosticFork_FullySpecified_CapturesEdgeOnDefinition()
    {
        var definition = Workflow<TestWorkflowState>
            .Create("diagnostic-fork-workflow")
            .StartWith<ValidateStep>()
            .AllowDiagnosticFork(fork => fork
                .Anchor("RatifyDeployment")
                .PermitTrigger(ForkTrigger.RatificationFailure, "provisionalStampEventId")
                .WithCompensationSeed("RollbackProvisionalStamp")
                .MaxForks(3))
            .Finally<CompleteStep>();

        await Assert.That(definition.DiagnosticForks).HasCount(1);

        var edge = definition.DiagnosticForks[0];
        await Assert.That(edge.AnchorStepIds).IsEquivalentTo(new[] { "RatifyDeployment" });
        await Assert.That(edge.CompensationSeed).IsEqualTo("RollbackProvisionalStamp");
        await Assert.That(edge.MaxForks).IsEqualTo(3);
        await Assert.That(edge.PermittedTriggers).HasCount(1);
        await Assert.That(edge.PermittedTriggers[0].Trigger).IsEqualTo(ForkTrigger.RatificationFailure);
        await Assert.That(edge.PermittedTriggers[0].RequiredEvidenceFields)
            .IsEquivalentTo(new[] { "provisionalStampEventId" });
    }

    /// <summary>
    /// Multiple anchors are captured, the first via the required parameter and the rest
    /// via the params array.
    /// </summary>
    [Test]
    public async Task AllowDiagnosticFork_MultipleAnchors_CapturesAllInOrder()
    {
        var definition = Workflow<TestWorkflowState>
            .Create("diagnostic-fork-workflow")
            .StartWith<ValidateStep>()
            .AllowDiagnosticFork(fork => fork
                .Anchor("RatifyDeployment", "ApproveRollout", "SealVerdict")
                .PermitTrigger(ForkTrigger.OperatorExplicit, "operatorId")
                .WithCompensationSeed("RollbackSeed")
                .MaxForks(1))
            .Finally<CompleteStep>();

        await Assert.That(definition.DiagnosticForks[0].AnchorStepIds)
            .IsEquivalentTo(new[] { "RatifyDeployment", "ApproveRollout", "SealVerdict" });
    }

    /// <summary>
    /// The construct expresses several permitted triggers, each with its own evidence
    /// schema, via additive <c>PermitTrigger</c> calls on the closure stage.
    /// </summary>
    [Test]
    public async Task AllowDiagnosticFork_MultipleTriggers_CapturesEachWithEvidenceSchema()
    {
        var definition = Workflow<TestWorkflowState>
            .Create("diagnostic-fork-workflow")
            .StartWith<ValidateStep>()
            .AllowDiagnosticFork(fork => fork
                .Anchor("RatifyDeployment")
                .PermitTrigger(ForkTrigger.RatificationFailure, "provisionalStampEventId")
                .PermitTrigger(ForkTrigger.GateContradiction, "leftGateId", "rightGateId")
                .WithCompensationSeed("RollbackSeed")
                .MaxForks(5))
            .Finally<CompleteStep>();

        var triggers = definition.DiagnosticForks[0].PermittedTriggers;
        await Assert.That(triggers).HasCount(2);
        await Assert.That(triggers[0].Trigger).IsEqualTo(ForkTrigger.RatificationFailure);
        await Assert.That(triggers[0].RequiredEvidenceFields)
            .IsEquivalentTo(new[] { "provisionalStampEventId" });
        await Assert.That(triggers[1].Trigger).IsEqualTo(ForkTrigger.GateContradiction);
        await Assert.That(triggers[1].RequiredEvidenceFields)
            .IsEquivalentTo(new[] { "leftGateId", "rightGateId" });
    }

    /// <summary>
    /// Two diagnostic-fork edges declared on one workflow are both captured.
    /// </summary>
    [Test]
    public async Task AllowDiagnosticFork_DeclaredTwice_CapturesBothEdges()
    {
        var definition = Workflow<TestWorkflowState>
            .Create("diagnostic-fork-workflow")
            .StartWith<ValidateStep>()
            .AllowDiagnosticFork(fork => fork
                .Anchor("StepA")
                .PermitTrigger(ForkTrigger.RatificationFailure, "eventId")
                .WithCompensationSeed("SeedA")
                .MaxForks(2))
            .AllowDiagnosticFork(fork => fork
                .Anchor("StepB")
                .PermitTrigger(ForkTrigger.GateContradiction, "gateId")
                .WithCompensationSeed("SeedB")
                .MaxForks(4))
            .Finally<CompleteStep>();

        await Assert.That(definition.DiagnosticForks).HasCount(2);
        await Assert.That(definition.DiagnosticForks[0].CompensationSeed).IsEqualTo("SeedA");
        await Assert.That(definition.DiagnosticForks[1].CompensationSeed).IsEqualTo("SeedB");
    }

    // =========================================================================
    // B. Required-parameter / illegal-state refusal (runtime guards the type
    //    staging cannot express)
    // =========================================================================

    /// <summary>
    /// A fork that reaches the closure without ever setting a compensation seed is
    /// refused when the edge is built — the seed has no meaningful default.
    /// </summary>
    [Test]
    public async Task AllowDiagnosticFork_WithoutCompensationSeed_Throws()
    {
        var builder = Workflow<TestWorkflowState>
            .Create("diagnostic-fork-workflow")
            .StartWith<ValidateStep>();

        await Assert.That(() => builder.AllowDiagnosticFork(fork => fork
                .Anchor("StepA")
                .PermitTrigger(ForkTrigger.RatificationFailure, "eventId")
                .MaxForks(2)))
            .Throws<InvalidOperationException>();
    }

    /// <summary>
    /// A fork that reaches the closure without ever setting the <c>maxForks</c> bound is
    /// refused when the edge is built — a fork with no bound cannot enforce DR-9.
    /// </summary>
    [Test]
    public async Task AllowDiagnosticFork_WithoutMaxForks_Throws()
    {
        var builder = Workflow<TestWorkflowState>
            .Create("diagnostic-fork-workflow")
            .StartWith<ValidateStep>();

        await Assert.That(() => builder.AllowDiagnosticFork(fork => fork
                .Anchor("StepA")
                .PermitTrigger(ForkTrigger.RatificationFailure, "eventId")
                .WithCompensationSeed("SeedA")))
            .Throws<InvalidOperationException>();
    }

    /// <summary>
    /// A <c>maxForks</c> bound below 1 forbids the very fork the edge exists to permit
    /// and is refused at the call site.
    /// </summary>
    [Test]
    public async Task AllowDiagnosticFork_MaxForksBelowOne_Throws()
    {
        var builder = Workflow<TestWorkflowState>
            .Create("diagnostic-fork-workflow")
            .StartWith<ValidateStep>();

        await Assert.That(() => builder.AllowDiagnosticFork(fork => fork
                .Anchor("StepA")
                .PermitTrigger(ForkTrigger.RatificationFailure, "eventId")
                .WithCompensationSeed("SeedA")
                .MaxForks(0)))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Permitting the same trigger twice is ambiguous (which evidence schema wins) and
    /// is refused.
    /// </summary>
    [Test]
    public async Task AllowDiagnosticFork_DuplicateTrigger_Throws()
    {
        var builder = Workflow<TestWorkflowState>
            .Create("diagnostic-fork-workflow")
            .StartWith<ValidateStep>();

        await Assert.That(() => builder.AllowDiagnosticFork(fork => fork
                .Anchor("StepA")
                .PermitTrigger(ForkTrigger.RatificationFailure, "eventId")
                .PermitTrigger(ForkTrigger.RatificationFailure, "otherEventId")
                .WithCompensationSeed("SeedA")
                .MaxForks(2)))
            .Throws<InvalidOperationException>();
    }

    /// <summary>
    /// An empty anchor moniker is refused.
    /// </summary>
    [Test]
    public async Task AllowDiagnosticFork_EmptyAnchor_Throws()
    {
        var builder = Workflow<TestWorkflowState>
            .Create("diagnostic-fork-workflow")
            .StartWith<ValidateStep>();

        await Assert.That(() => builder.AllowDiagnosticFork(fork => fork
                .Anchor("  ")
                .PermitTrigger(ForkTrigger.RatificationFailure, "eventId")
                .WithCompensationSeed("SeedA")
                .MaxForks(2)))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// An empty evidence field moniker is refused — a permitted trigger must name a real
    /// justification field.
    /// </summary>
    [Test]
    public async Task AllowDiagnosticFork_EmptyEvidenceField_Throws()
    {
        var builder = Workflow<TestWorkflowState>
            .Create("diagnostic-fork-workflow")
            .StartWith<ValidateStep>();

        await Assert.That(() => builder.AllowDiagnosticFork(fork => fork
                .Anchor("StepA")
                .PermitTrigger(ForkTrigger.RatificationFailure, "  ")
                .WithCompensationSeed("SeedA")
                .MaxForks(2)))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Declaring a diagnostic fork before <c>StartWith</c> is refused — an edge needs a
    /// workflow to anchor into.
    /// </summary>
    [Test]
    public async Task AllowDiagnosticFork_BeforeStartWith_Throws()
    {
        var builder = Workflow<TestWorkflowState>.Create("diagnostic-fork-workflow");

        await Assert.That(() => builder.AllowDiagnosticFork(fork => fork
                .Anchor("StepA")
                .PermitTrigger(ForkTrigger.RatificationFailure, "eventId")
                .WithCompensationSeed("SeedA")
                .MaxForks(2)))
            .Throws<InvalidOperationException>();
    }

    /// <summary>
    /// A null configure delegate is refused.
    /// </summary>
    [Test]
    public async Task AllowDiagnosticFork_NullConfigure_Throws()
    {
        var builder = Workflow<TestWorkflowState>
            .Create("diagnostic-fork-workflow")
            .StartWith<ValidateStep>();

        await Assert.That(() => builder.AllowDiagnosticFork(null!))
            .Throws<ArgumentNullException>();
    }

    // =========================================================================
    // C. Sealed-type guard (INV-6) — the concrete staged builder is a leaf
    //    collaborator with no intended subclassing.
    // =========================================================================

    /// <summary>
    /// INV-6 sealed-by-default: the concrete diagnostic-fork builder and its fluent
    /// entrypoint host are sealed (a C# <c>static class</c> compiles to
    /// <c>abstract sealed</c>).
    /// </summary>
    [Test]
    public async Task DiagnosticForkBuilderTypes_AreSealed()
    {
        await Assert.That(typeof(DiagnosticForkBuilder<TestWorkflowState>).IsSealed).IsTrue();
        await Assert.That(typeof(DiagnosticForkWorkflowBuilderExtensions).IsSealed).IsTrue();
    }
}
