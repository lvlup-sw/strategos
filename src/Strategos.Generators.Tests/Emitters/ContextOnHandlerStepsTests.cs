// -----------------------------------------------------------------------
// <copyright file="ContextOnHandlerStepsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Integration tests (DR-6, CodeRabbit / epic #135 F3 + F5) proving that a
/// <c>.WithContext(...)</c> declaration on a step that lives OFF the main linear
/// flow — a failure-handler step (<c>OnFailure</c>) or a low-confidence handler
/// step (<c>OnLowConfidence</c>) — is lowered end-to-end:
/// <list type="bullet">
///   <item><description>
///     F5: the context merge in <see cref="WorkflowIncrementalGenerator"/> runs
///     AFTER the handler/low-confidence step models are appended, so the context
///     actually attaches to the handler step model and the
///     <c>ContextAssemblerEmitter</c> emits its <c>{Step}ContextAssembler</c>.
///   </description></item>
///   <item><description>
///     F3: the DI registration in <see cref="Strategos.Generators.Emitters.ExtensionsEmitter"/>
///     registers the handler step's <c>{Step}ContextAssembler</c> so the worker
///     handler's constructor dependency resolves at runtime.
///   </description></item>
/// </list>
/// Together they close the gap where context on a handler step was parsed but
/// never attached, emitted, or registered (runtime missing-dependency).
/// </summary>
/// <remarks>
/// Context assembly is ontology-wired (INV-2): the retrieval source emits a
/// <c>SimilarityExpression</c> executed through <c>IObjectSetProvider</c>; no
/// <c>Strategos.Rag</c> type is introduced.
/// </remarks>
[Property("Category", "Integration")]
public class ContextOnHandlerStepsTests
{
    /// <summary>
    /// A workflow whose <c>OnFailure</c> handler step (<c>LogFailure</c>) declares
    /// <c>.WithContext(...)</c>. The main flow steps declare no context. Drives the
    /// generator to lower a <c>LogFailureContextAssembler</c> and register it.
    /// </summary>
    private const string FailureHandlerContextSource = @"
using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Steps;

namespace TestApp;

[WorkflowState]
public sealed record OrderState : IWorkflowState
{
    public global::System.Guid WorkflowId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
}

public sealed class ProductCatalog { }

public sealed class ValidateOrder : IWorkflowStep<OrderState>
{
    public global::System.Threading.Tasks.Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state, StepContext context, global::System.Threading.CancellationToken cancellationToken)
        => global::System.Threading.Tasks.Task.FromResult(StepResult<OrderState>.FromState(state));
}

public sealed class ProcessOrder : IWorkflowStep<OrderState>
{
    public global::System.Threading.Tasks.Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state, StepContext context, global::System.Threading.CancellationToken cancellationToken)
        => global::System.Threading.Tasks.Task.FromResult(StepResult<OrderState>.FromState(state));
}

public sealed class FinishStep : IWorkflowStep<OrderState>
{
    public global::System.Threading.Tasks.Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state, StepContext context, global::System.Threading.CancellationToken cancellationToken)
        => global::System.Threading.Tasks.Task.FromResult(StepResult<OrderState>.FromState(state));
}

public sealed class LogFailure : IWorkflowStep<OrderState>
{
    public global::System.Threading.Tasks.Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state, StepContext context, global::System.Threading.CancellationToken cancellationToken)
        => global::System.Threading.Tasks.Task.FromResult(StepResult<OrderState>.FromState(state));
}

[Workflow(""order-flow"")]
public static partial class OrderFlowWorkflow
{
    public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
        .Create(""order-flow"")
        .StartWith<ValidateOrder>()
        .Then<ProcessOrder>()
        .Finally<FinishStep>()
        .OnFailure(f => f
            .Then<LogFailure>(step => step
                .WithContext(ctx => ctx
                    .FromState(s => s.CustomerName)
                    .FromRetrieval<ProductCatalog>(r => r.Query(""incident"").TopK(3).MinRelevance(0.7m))
                    .FromLiteral(""Record the failure cause."")))
            .Complete());
}
";

    /// <summary>
    /// A workflow whose <c>OnLowConfidence</c> handler step (<c>HumanReview</c>)
    /// declares <c>.WithContext(...)</c>. Drives the generator to lower a
    /// <c>HumanReviewContextAssembler</c> and register it.
    /// </summary>
    private const string LowConfidenceHandlerContextSource = @"
using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Steps;

namespace TestApp;

[WorkflowState]
public sealed record OrderState : IWorkflowState
{
    public global::System.Guid WorkflowId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
}

public sealed class ProductCatalog { }

public sealed class ValidateOrder : IWorkflowStep<OrderState>
{
    public global::System.Threading.Tasks.Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state, StepContext context, global::System.Threading.CancellationToken cancellationToken)
        => global::System.Threading.Tasks.Task.FromResult(StepResult<OrderState>.FromState(state));
}

public sealed class ClassifyIntent : IWorkflowStep<OrderState>
{
    public global::System.Threading.Tasks.Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state, StepContext context, global::System.Threading.CancellationToken cancellationToken)
        => global::System.Threading.Tasks.Task.FromResult(StepResult<OrderState>.FromState(state));
}

public sealed class HumanReview : IWorkflowStep<OrderState>
{
    public global::System.Threading.Tasks.Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state, StepContext context, global::System.Threading.CancellationToken cancellationToken)
        => global::System.Threading.Tasks.Task.FromResult(StepResult<OrderState>.FromState(state));
}

public sealed class FinishStep : IWorkflowStep<OrderState>
{
    public global::System.Threading.Tasks.Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state, StepContext context, global::System.Threading.CancellationToken cancellationToken)
        => global::System.Threading.Tasks.Task.FromResult(StepResult<OrderState>.FromState(state));
}

[Workflow(""classify-flow"")]
public static partial class ClassifyFlowWorkflow
{
    public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
        .Create(""classify-flow"")
        .StartWith<ValidateOrder>()
        .Then<ClassifyIntent>(step => step
            .RequireConfidence(0.85)
            .OnLowConfidence(alt => alt.Then<HumanReview>(h => h
                .WithContext(ctx => ctx
                    .FromState(s => s.CustomerName)
                    .FromLiteral(""Human escalation context."")))))
        .Finally<FinishStep>();
}
";

    /// <summary>
    /// F5: <c>.WithContext(...)</c> on an <c>OnFailure</c> handler step must
    /// attach to that handler step model (because the merge now runs after the
    /// failure-handler step models are appended), so the
    /// <c>ContextAssemblerEmitter</c> emits its assembler and the worker handler
    /// wires it in.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generate_FailureHandlerStepWithContext_EmitsAssemblerAndWiresWorker()
    {
        // Act
        var result = GeneratorTestHelper.RunGenerator(FailureHandlerContextSource);
        var assemblerSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderFlowAssemblers.g.cs");
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderFlowHandlers.g.cs");

        // F5: the assembler for the failure-handler step is emitted (the context
        // attached to the LogFailure step model).
        await Assert.That(assemblerSource).IsNotNull().And.IsNotEmpty();
        await Assert.That(assemblerSource).Contains("LogFailureContextAssembler");
        await Assert.That(assemblerSource).Contains("IObjectSetProvider");
        await Assert.That(assemblerSource).DoesNotContain("Strategos.Rag");

        // F5: the assembler is wired into the LogFailure worker handler.
        await Assert.That(handlersSource).Contains("LogFailureContextAssembler");
        await Assert.That(handlersSource).Contains("AssembleAsync");
    }

    /// <summary>
    /// F3: the <c>OnFailure</c> handler step's <c>{Step}ContextAssembler</c> must
    /// be DI-registered (mirroring the existing main-flow assembler registration),
    /// or the worker handler's constructor dependency cannot resolve at runtime.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generate_FailureHandlerStepWithContext_RegistersAssemblerInDi()
    {
        // Act
        var result = GeneratorTestHelper.RunGenerator(FailureHandlerContextSource);
        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderFlowExtensions.g.cs");

        // F3: the assembler registration covers the failure-handler step.
        await Assert.That(extensionsSource).Contains(
            "services.AddTransient<LogFailureContextAssembler>();");
    }

    /// <summary>
    /// F5: <c>.WithContext(...)</c> on an <c>OnLowConfidence</c> handler step must
    /// attach to that handler step model (DR-5 handler steps are appended after the
    /// initial context merge), so its assembler is emitted and wired.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generate_LowConfidenceHandlerStepWithContext_EmitsAssemblerAndWiresWorker()
    {
        // Act
        var result = GeneratorTestHelper.RunGenerator(LowConfidenceHandlerContextSource);
        var assemblerSource = GeneratorTestHelper.GetGeneratedSource(result, "ClassifyFlowAssemblers.g.cs");
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(result, "ClassifyFlowHandlers.g.cs");

        await Assert.That(assemblerSource).IsNotNull().And.IsNotEmpty();
        await Assert.That(assemblerSource).Contains("HumanReviewContextAssembler");
        await Assert.That(handlersSource).Contains("HumanReviewContextAssembler");
        await Assert.That(handlersSource).Contains("AssembleAsync");
    }

    /// <summary>
    /// F3: the <c>OnLowConfidence</c> handler step's assembler must be DI-registered.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generate_LowConfidenceHandlerStepWithContext_RegistersAssemblerInDi()
    {
        // Act
        var result = GeneratorTestHelper.RunGenerator(LowConfidenceHandlerContextSource);
        var extensionsSource = GeneratorTestHelper.GetGeneratedSource(result, "ClassifyFlowExtensions.g.cs");

        await Assert.That(extensionsSource).Contains(
            "services.AddTransient<HumanReviewContextAssembler>();");
    }
}
