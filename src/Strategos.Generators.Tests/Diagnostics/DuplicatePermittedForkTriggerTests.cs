// -----------------------------------------------------------------------
// <copyright file="DuplicatePermittedForkTriggerTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Diagnostics;

/// <summary>
/// #156.2 / AGWF037 — two <c>PermitTrigger(ForkTrigger.X)</c> declarations on one
/// diagnostic-fork edge fail with a dedicated diagnostic on the C# authoring path.
/// The JSON-import twin lives in <c>ImportRejectionTests</c>.
/// </summary>
[Property("Category", "Unit")]
public sealed class DuplicatePermittedForkTriggerTests
{
    private const string DuplicateTriggerId = "AGWF037";

    /// <summary>
    /// C# twin: two <c>PermitTrigger</c> calls for the same closed trigger (different
    /// evidence schemas) fire AGWF037 and emit no saga.
    /// </summary>
    [Test]
    public async Task CsharpTwin_DuplicatePermitTrigger_FiresAgwf037AndEmitsNoSaga()
    {
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithForkTriggers(
            """
            .PermitTrigger(ForkTrigger.RatificationFailure, "stampId")
            .PermitTrigger(ForkTrigger.RatificationFailure, "otherStampId")
            """));

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == DuplicateTriggerId);
        await Assert.That(diagnostic).IsNotNull()
            .Because("two PermitTrigger(ForkTrigger.X) calls on one edge must fire AGWF037.");

        var message = diagnostic!.GetMessage();
        await Assert.That(message).Contains("RatificationFailure");
        await Assert.That(message).Contains("duplicate-fork-trigger");

        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a workflow rejected by AGWF037 must not emit a saga.");
    }

    /// <summary>
    /// Distinct triggers on one edge stay clean: no AGWF037, and a saga still lowers.
    /// </summary>
    [Test]
    public async Task CsharpTwin_DistinctPermitTriggers_DoesNotFireAgwf037()
    {
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithForkTriggers(
            """
            .PermitTrigger(ForkTrigger.RatificationFailure, "stampId")
            .PermitTrigger(ForkTrigger.GateContradiction, "leftGateId", "rightGateId")
            """));

        await Assert.That(result.Diagnostics.Any(d => d.Id == DuplicateTriggerId)).IsFalse()
            .Because("distinct PermitTrigger declarations on one edge must stay silent.");

        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a diagnostic-fork edge with distinct triggers must still lower a saga.");
    }

    private static string WorkflowWithForkTriggers(string permitTriggerCalls) => $$"""
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

        [Workflow("duplicate-fork-trigger")]
        public static partial class DuplicateForkTriggerWorkflow
        {
            public static WorkflowDefinition<RatifyState> Definition => Workflow<RatifyState>
                .Create("duplicate-fork-trigger")
                .StartWith<RatifyStep>()
                .AllowDiagnosticFork(fork => fork
                    .Anchor("RatifyStep")
                    {{permitTriggerCalls}}
                    .WithCompensationSeed("CompleteStep")
                    .MaxForks(2))
                .Finally<CompleteStep>();
        }
        """;
}
