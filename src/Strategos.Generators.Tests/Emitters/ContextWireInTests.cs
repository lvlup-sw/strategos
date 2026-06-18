// -----------------------------------------------------------------------
// <copyright file="ContextWireInTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Integration tests (DR-6 T015) proving that a step's <c>.WithContext(...)</c>
/// declaration is actually LOWERED by the full generator pipeline: the
/// <see cref="Strategos.Generators.Emitters.ContextAssemblerEmitter"/> is
/// invoked from <see cref="WorkflowIncrementalGenerator"/> so the
/// <c>{Step}ContextAssembler</c> is emitted AND wired into the step's worker
/// handler execution path.
/// </summary>
/// <remarks>
/// <para>
/// Before this task the emitter existed (with unit tests) but was never invoked
/// by the generator — <c>.WithContext(...)</c> parsed into a
/// <c>ContextModel</c> that was discarded, so no assembler reached the
/// compilation and no step ever received assembled context at runtime.
/// </para>
/// <para>
/// The assembler is ontology-wired (INV-2), not RAG: a retrieval source emits a
/// <c>SimilarityExpression</c> executed through <c>IObjectSetProvider</c>'s
/// <c>ExecuteSimilarityAsync</c>; no <c>Strategos.Rag</c> type is introduced.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public class ContextWireInTests
{
    /// <summary>
    /// A linear workflow whose first step declares <c>.WithContext(...)</c> with
    /// all three source kinds (state, ontology retrieval, literal). Drives the
    /// generator to lower a <c>ProcessStepContextAssembler</c> and wire it into
    /// the <c>ProcessStep</c> worker handler.
    /// </summary>
    private const string ContextWorkflowSource = @"
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

public sealed class ProcessStep : IWorkflowStep<OrderState>
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

[Workflow(""context-flow"")]
public static partial class ContextFlowWorkflow
{
    public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
        .Create(""context-flow"")
        .StartWith<ProcessStep>(step => step
            .WithContext(ctx => ctx
                .FromState(s => s.CustomerName)
                .FromRetrieval<ProductCatalog>(r => r.Query(""widgets"").TopK(5).MinRelevance(0.8m))
                .FromLiteral(""Follow brand guidelines."")))
        .Finally<FinishStep>();
}
";

    /// <summary>
    /// The wire-in proof: running the full generator over a workflow whose step
    /// declares <c>.WithContext(...)</c> must emit the
    /// <c>ProcessStepContextAssembler</c> with the ontology retrieval path
    /// (<c>IObjectSetProvider</c> / <c>ExecuteSimilarityAsync</c> /
    /// <c>SimilarityExpression</c>), AND wire the assembler into the
    /// <c>ProcessStep</c> worker handler so it assembles context before the step
    /// executes. No <c>Strategos.Rag</c> type may appear.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generate_StepWithWithContext_EmitsContextAssembler()
    {
        // Act
        var result = GeneratorTestHelper.RunGenerator(ContextWorkflowSource);

        var assemblerSource = GeneratorTestHelper.GetGeneratedSource(
            result, "ContextFlowAssemblers.g.cs");
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(
            result, "ContextFlowHandlers.g.cs");

        // Assert — the assembler class was emitted with the ontology retrieval
        // path (INV-2: ontology, not RAG).
        await Assert.That(assemblerSource).IsNotNull().And.IsNotEmpty();
        await Assert.That(assemblerSource).Contains("ProcessStepContextAssembler");
        await Assert.That(assemblerSource).Contains("IObjectSetProvider");
        await Assert.That(assemblerSource).Contains("ExecuteSimilarityAsync");
        await Assert.That(assemblerSource).Contains("new SimilarityExpression(");
        await Assert.That(assemblerSource).DoesNotContain("Strategos.Rag");
        await Assert.That(assemblerSource).DoesNotContain("IVectorSearchAdapter");

        // Assert — the assembler is WIRED into the ProcessStep worker handler:
        // the handler depends on the assembler and assembles context before the
        // step executes.
        await Assert.That(handlersSource).Contains("ProcessStepContextAssembler");
        await Assert.That(handlersSource).Contains("AssembleAsync");
    }

    /// <summary>
    /// Steps WITHOUT <c>.WithContext(...)</c> must NOT get an assembler and their
    /// worker handler must keep the no-assembler baseline shape (no assembler
    /// dependency, no <c>AssembleAsync</c> call). Guards against the wire-in
    /// leaking assembler wiring onto unrelated steps.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generate_StepWithoutContext_KeepsNoAssemblerBaseline()
    {
        // Act
        var result = GeneratorTestHelper.RunGenerator(ContextWorkflowSource);

        var assemblerSource = GeneratorTestHelper.GetGeneratedSource(
            result, "ContextFlowAssemblers.g.cs");
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(
            result, "ContextFlowHandlers.g.cs");

        // The context-less FinishStep gets no assembler...
        await Assert.That(assemblerSource).DoesNotContain("FinishStepContextAssembler");

        // ...and its handler must not reference any assembler nor assemble context.
        await Assert.That(handlersSource).Contains("FinishStepHandler");
        await Assert.That(handlersSource).DoesNotContain("FinishStepContextAssembler");
    }
}
