// -----------------------------------------------------------------------
// <copyright file="ContextWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Models;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

/// <summary>
/// Immutable state for the context-assembly fixture workflow (DR-6 T016).
/// </summary>
/// <remarks>
/// Marked <see cref="WorkflowStateAttribute"/> so the source generator emits a
/// reducer used by the saga to fold every step's returned state.
/// </remarks>
[WorkflowState]
public sealed record ContextState : IWorkflowState
{
    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets the customer name folded into the assembled state context.
    /// </summary>
    public string CustomerName { get; init; } = string.Empty;
}

/// <summary>
/// Marker type for the ontology collection the enrich step retrieves from. The
/// generated assembler dispatches a <c>SimilarityExpression</c> rooted on this
/// type through <c>IObjectSetProvider.ExecuteSimilarityAsync&lt;ProductCatalog&gt;</c>;
/// the stub provider ignores the root and returns a canned scored result.
/// </summary>
public sealed class ProductCatalog
{
    /// <summary>
    /// Gets a stub document body; the assembler maps each item via
    /// <c>item.ToString()</c> into a retrieval segment.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => this.Text;
}

/// <summary>
/// Entry step of the context fixture. Deterministic; records its invocation so
/// the test can confirm the saga started.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ContextPrepareStep(WorkflowInvocationLog log) : IWorkflowStep<ContextState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ContextState>> ExecuteAsync(
        ContextState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ContextPrepareStep));

        // Seed a state value so the assembler's FromState source has content.
        var updated = state with { CustomerName = "Ada Lovelace" };
        return Task.FromResult(StepResult<ContextState>.FromState(updated));
    }
}

/// <summary>
/// The context-declaring step. Its <c>.WithContext(...)</c> declaration lowers a
/// generated <c>EnrichStepContextAssembler</c> whose retrieval source runs
/// through the stub <c>IObjectSetProvider</c>. Implements
/// <see cref="IContextAwareStep"/> so the generated worker handler delivers the
/// assembled context here, where it is captured on the shared
/// <see cref="ContextProbe"/> for the test to assert.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
/// <param name="probe">The shared observation probe injected by the host.</param>
public sealed class EnrichStep(WorkflowInvocationLog log, ContextProbe probe)
    : IWorkflowStep<ContextState>, IContextAwareStep
{
    private readonly WorkflowInvocationLog log = log;
    private readonly ContextProbe probe = probe;

    /// <inheritdoc />
    public void ReceiveContext(AssembledContext context)
    {
        // The handler assembles context BEFORE ExecuteAsync and delivers it here.
        this.probe.RecordAssembledContext(context);
    }

    /// <inheritdoc />
    public Task<StepResult<ContextState>> ExecuteAsync(
        ContextState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(EnrichStep));
        return Task.FromResult(StepResult<ContextState>.FromState(state));
    }
}

/// <summary>
/// Terminal step of the context fixture. As the <c>Finally</c> step its
/// completion drives the saga to its terminal phase and <c>MarkCompleted()</c>.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ContextFinishStep(WorkflowInvocationLog log) : IWorkflowStep<ContextState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ContextState>> ExecuteAsync(
        ContextState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ContextFinishStep));
        return Task.FromResult(StepResult<ContextState>.FromState(state));
    }
}

/// <summary>
/// The context fixture workflow definition. The enrich step declares
/// <c>.WithContext(ctx =&gt; ctx.FromState(...).FromRetrieval&lt;ProductCatalog&gt;(...).FromLiteral(...))</c>,
/// driving the generator to emit <c>EnrichStepContextAssembler</c>, wire it into
/// <c>EnrichStepHandler</c>, and register it via <c>AddContextFlowWorkflow()</c>.
/// </summary>
[Workflow("context-flow")]
public static partial class ContextFlowWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: a prepare step, a context-declaring enrich
    /// step whose assembled context draws from state, ontology retrieval
    /// (TopK 5, MinRelevance 0.8) and a literal, and a terminal finish step.
    /// </summary>
    public static WorkflowDefinition<ContextState> Definition => Workflow<ContextState>
        .Create("context-flow")
        .StartWith<ContextPrepareStep>()
        .Then<EnrichStep>(step => step
            .WithContext(ctx => ctx
                .FromState(s => s.CustomerName)
                .FromRetrieval<ProductCatalog>(r => r
                    .Query("recommended widgets")
                    .TopK(5)
                    .MinRelevance(0.8m))
                .FromLiteral("Follow brand guidelines.")))
        .Finally<ContextFinishStep>();
}
