// -----------------------------------------------------------------------
// <copyright file="SagaEmitterForkConfigTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// DR-17 (Task A) — end-to-end generator tests proving that step configuration
/// declared on a <b>fork-path</b> step via the
/// <c>Then&lt;TStep&gt;(Action&lt;IStepConfiguration&lt;TState&gt;&gt;)</c> overload
/// actually lowers into the generated Wolverine saga.
/// </summary>
/// <remarks>
/// <para>
/// Of the <c>IStepConfiguration</c> surface, only <c>ValidateState</c> (the Guard-Then-
/// Dispatch guard) is lowered end-to-end by the generator pipeline today — for top-level
/// and loop steps. Fork-path steps used to drop it because
/// <c>StepExtractor.ParseForkPathStepModels</c> populated each fork-path <c>StepModel</c>
/// with <c>null</c> validation. These tests run the full parse → emit pipeline from
/// source text and assert the guard appears in the generated saga, closing that gap.
/// </para>
/// <para>
/// Retry/timeout/compensation (tracked by #135) <b>and</b> <c>WithContext</c> (the
/// <c>ContextAssemblerEmitter</c> / <c>ContextModelExtractor</c> are wired into unit
/// tests only, never the production <c>WorkflowIncrementalGenerator</c> pipeline) remain
/// "declared, not yet enforced" for every step kind, so they are intentionally not
/// asserted here.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public class SagaEmitterForkConfigTests
{
    private const string ForkWithValidatedBranchStep = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record OrderState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public int ItemCount { get; init; }
        }

        public class ValidateOrderStep : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class ProcessPaymentStep : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class ReserveInventoryStep : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class SynthesizeStep : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class CompleteStep : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        [Workflow("fork-config")]
        public static partial class ForkConfigWorkflow
        {
            public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
                .Create("fork-config")
                .StartWith<ValidateOrderStep>()
                .Fork(
                    path => path.Then<ProcessPaymentStep>(step => step
                        .ValidateState(state => state.ItemCount > 0, "Order must have items")),
                    path => path.Then<ReserveInventoryStep>())
                .Join<SynthesizeStep>()
                .Finally<CompleteStep>();
        }
        """;

    /// <summary>
    /// A fork-path step configured with <c>.ValidateState(...)</c> must emit its
    /// Guard-Then-Dispatch validation guard in the generated saga, exactly as a
    /// top-level or loop step would.
    /// </summary>
    [Test]
    public async Task SagaEmitter_ConfiguredForkPathStep_AppliesValidationGuard()
    {
        // Act
        var result = GeneratorTestHelper.RunGenerator(ForkWithValidatedBranchStep);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "ForkConfigSaga.g.cs");

        // Assert - the fork-path step's validation guard lowers into the saga
        await Assert.That(saga).Contains("// Validation guard");
        await Assert.That(saga).Contains("State.ItemCount > 0");
        await Assert.That(saga).Contains("Order must have items");
    }

    /// <summary>
    /// Threading config onto the configured fork-path step must not disturb the
    /// fork machinery: the phase enum still carries a forking phase and the path-status
    /// checkpoint properties remain emitted for both branches.
    /// </summary>
    [Test]
    public async Task SagaEmitter_ConfiguredForkPathStep_PreservesPhaseAndCheckpoint()
    {
        // Act
        var result = GeneratorTestHelper.RunGenerator(ForkWithValidatedBranchStep);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "ForkConfigSaga.g.cs");
        var phase = GeneratorTestHelper.GetGeneratedSource(result, "ForkConfigPhase.g.cs");

        // Assert - the fork phase enum still carries a forking phase value
        await Assert.That(phase).Contains("Forking_");

        // Assert - fork dispatch sets the forking phase and the durable per-path
        // checkpoint status properties survive the config threading
        await Assert.That(saga).Contains("Phase = ForkConfigPhase.Forking_");
        await Assert.That(saga).Contains("_Path0Status = Strategos.Definitions.ForkPathStatus.InProgress");
        await Assert.That(saga).Contains("_Path1Status = Strategos.Definitions.ForkPathStatus.InProgress");

        // Assert - both branch start commands are still dispatched
        await Assert.That(saga).Contains("StartProcessPaymentStepCommand");
        await Assert.That(saga).Contains("StartReserveInventoryStepCommand");
    }
}
