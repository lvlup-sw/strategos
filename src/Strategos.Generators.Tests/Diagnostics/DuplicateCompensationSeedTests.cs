// -----------------------------------------------------------------------
// <copyright file="DuplicateCompensationSeedTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Diagnostics;

/// <summary>
/// #156.3 / AGWF038 — two <c>AllowDiagnosticFork</c> edges that share a
/// compensation seed fail with a dedicated diagnostic on the C# authoring path.
/// The JSON-import twin lives in <c>ImportRejectionTests</c>.
/// </summary>
[Property("Category", "Unit")]
public sealed class DuplicateCompensationSeedTests
{
    private const string DuplicateSeedId = "AGWF038";

    /// <summary>
    /// C# twin: two edges with the same compensation seed fire AGWF038 and emit no saga.
    /// </summary>
    [Test]
    public async Task CsharpTwin_DuplicateCompensationSeed_FiresAgwf038AndEmitsNoSaga()
    {
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithTwoForkEdges(
            seedA: "CompleteStep",
            seedB: "CompleteStep"));

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == DuplicateSeedId);
        await Assert.That(diagnostic).IsNotNull()
            .Because("two AllowDiagnosticFork edges that share a seed must fire AGWF038.");

        var message = diagnostic!.GetMessage();
        await Assert.That(message).Contains("CompleteStep");
        await Assert.That(message).Contains("duplicate-fork-seed");

        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a workflow rejected by AGWF038 must not emit a saga.");
    }

    /// <summary>
    /// Distinct seeds on two edges stay clean: no AGWF038, and a saga still lowers
    /// with seed-keyed counters.
    /// </summary>
    [Test]
    public async Task CsharpTwin_DistinctCompensationSeeds_DoesNotFireAgwf038()
    {
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithTwoForkEdges(
            seedA: "RatifyStep",
            seedB: "CompleteStep"));

        await Assert.That(result.Diagnostics.Any(d => d.Id == DuplicateSeedId)).IsFalse()
            .Because("distinct compensation seeds on two edges must stay silent.");

        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a workflow with distinct diagnostic-fork seeds must still lower a saga.");

        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "DuplicateForkSeedSaga.g.cs");
        await Assert.That(sagaSource).Contains("public int DiagnosticForkCount_RatifyStep");
        await Assert.That(sagaSource).Contains("public int DiagnosticForkCount_CompleteStep");
    }

    /// <summary>
    /// Seeds that differ only by hyphen vs underscore sanitize to the same key and
    /// still fire AGWF038 (they would share <c>DiagnosticForkCount_foo_bar</c>).
    /// </summary>
    [Test]
    public async Task CsharpTwin_SanitizedSeedCollision_FiresAgwf038()
    {
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithTwoForkEdges(
            seedA: "foo-bar",
            seedB: "foo_bar"));

        await Assert.That(result.Diagnostics.Any(d => d.Id == DuplicateSeedId)).IsTrue()
            .Because("seeds that sanitize to the same identifier must fire AGWF038, not share a counter.");
        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a sanitized-seed collision must reject the workflow.");
    }

    private static string WorkflowWithTwoForkEdges(string seedA, string seedB) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum ForkTrigger
        {
            RatificationFailure,
            GateContradiction,
        }

        [WorkflowState]
        public record RatifyState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class RatifyStep : IWorkflowStep<RatifyState>
        {
            public Task<StepResult<RatifyState>> ExecuteAsync(
                RatifyState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RatifyState>.FromState(state));
        }

        public class CompleteStep : IWorkflowStep<RatifyState>
        {
            public Task<StepResult<RatifyState>> ExecuteAsync(
                RatifyState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RatifyState>.FromState(state));
        }

        [Workflow("duplicate-fork-seed")]
        public static partial class DuplicateForkSeedWorkflow
        {
            public static WorkflowDefinition<RatifyState> Definition => Workflow<RatifyState>
                .Create("duplicate-fork-seed")
                .StartWith<RatifyStep>()
                .AllowDiagnosticFork(fork => fork
                    .Anchor("RatifyStep")
                    .PermitTrigger(ForkTrigger.RatificationFailure, "stampId")
                    .WithCompensationSeed("{{seedA}}")
                    .MaxForks(2))
                .AllowDiagnosticFork(fork => fork
                    .Anchor("CompleteStep")
                    .PermitTrigger(ForkTrigger.GateContradiction, "leftGateId", "rightGateId")
                    .WithCompensationSeed("{{seedB}}")
                    .MaxForks(1))
                .Finally<CompleteStep>();
        }
        """;
}
