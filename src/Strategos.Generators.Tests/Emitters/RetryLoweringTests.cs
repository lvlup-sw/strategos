// -----------------------------------------------------------------------
// <copyright file="RetryLoweringTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Shape tests for lowering a step's <c>.WithRetry(...)</c> policy onto the
/// generated worker handler as a Wolverine per-handler error policy
/// (<c>public static void Configure(HandlerChain chain)</c>).
/// </summary>
/// <remarks>
/// <para>
/// Wolverine discovers a static <c>Configure(HandlerChain)</c> method on the
/// handler class at codegen time and applies it to that handler's chain,
/// scoping the error policy to this one step (DR-2). The handler's existing
/// <c>catch { throw; }</c> re-throw is what feeds the policy, so retry is a
/// pure additive on top of the happy-path handler body.
/// </para>
/// <para>
/// Maps <see cref="RetryModel"/> to the chain fluent API:
/// no delay → <c>chain.OnAnyException().RetryTimes(n)</c>;
/// an <c>InitialDelay</c> → <c>chain.OnAnyException().RetryWithCooldown(...)</c>.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public class RetryLoweringTests
{
    /// <summary>
    /// A bare <c>.WithRetry(2)</c> (no delay) lowers to a static
    /// <c>Configure(HandlerChain ...)</c> method that calls
    /// <c>RetryTimes(2)</c> on the handler chain.
    /// </summary>
    [Test]
    public async Task Emit_StepWithWithRetry2_GeneratesConfigureWithRetryTimes()
    {
        // Arrange
        var step = StepModel.Create(
            "FlakyStep",
            "TestNamespace.FlakyStep",
            retry: new RetryModel(MaxAttempts: 2));

        var model = ModelWithStep(step);

        // Act
        var source = WorkerHandlerEmitter.Emit(model);

        // Assert - the FlakyStepHandler carries the per-handler retry policy.
        await Assert.That(source).Contains("public static void Configure(HandlerChain");
        await Assert.That(source).Contains("RetryTimes(2)");
    }

    /// <summary>
    /// A <c>.WithRetry(n, delay)</c> lowers to <c>RetryWithCooldown(...)</c>
    /// (a TimeSpan-shaped backoff), not <c>RetryTimes</c>.
    /// </summary>
    [Test]
    public async Task Emit_StepWithWithRetryAndDelay_GeneratesRetryWithCooldown()
    {
        // Arrange
        var step = StepModel.Create(
            "FlakyStep",
            "TestNamespace.FlakyStep",
            retry: new RetryModel(MaxAttempts: 3, InitialDelay: TimeSpan.FromMilliseconds(50)));

        var model = ModelWithStep(step);

        // Act
        var source = WorkerHandlerEmitter.Emit(model);

        // Assert - a delayed retry uses the cooldown overload, not RetryTimes.
        await Assert.That(source).Contains("public static void Configure(HandlerChain");
        await Assert.That(source).Contains("RetryWithCooldown(");
        await Assert.That(source).DoesNotContain("RetryTimes(");
    }

    /// <summary>
    /// A step without any retry policy emits NO <c>Configure</c> method, so the
    /// generated handler is unchanged from the no-resilience baseline.
    /// </summary>
    [Test]
    public async Task Emit_StepWithoutRetry_GeneratesNoConfigureMethod()
    {
        // Arrange
        var step = StepModel.Create("PlainStep", "TestNamespace.PlainStep");
        var model = ModelWithStep(step);

        // Act
        var source = WorkerHandlerEmitter.Emit(model);

        // Assert - no per-handler policy hook is emitted when retry is absent.
        await Assert.That(source).DoesNotContain("Configure(HandlerChain");
    }

    private static WorkflowModel ModelWithStep(StepModel step)
    {
        return new WorkflowModel(
            WorkflowName: "resilience-flow",
            PascalName: "ResilienceFlow",
            Namespace: "TestNamespace",
            StepNames: [step.StepName],
            StateTypeName: "FlowState",
            Steps: [step]);
    }
}
