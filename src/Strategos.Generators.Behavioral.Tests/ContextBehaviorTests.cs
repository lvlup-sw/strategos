// -----------------------------------------------------------------------
// <copyright file="ContextBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Agents.Models;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof (DR-6 T016) that a step's <c>.WithContext(...)</c>
/// declaration is actually honored at runtime: the generated
/// <c>EnrichStepContextAssembler</c> is wired into the step's worker handler, so
/// when the saga runs on a real PostgreSQL-backed host the handler assembles the
/// declared context (state value, ontology retrieval, literal) BEFORE the step
/// executes, runs the lowered <c>SimilarityExpression</c> through the registered
/// <c>IObjectSetProvider</c>, and delivers the assembled context to the
/// context-aware step.
/// </summary>
/// <remarks>
/// <para>
/// Before the DR-6 wire-in the <c>ContextAssemblerEmitter</c> was never invoked
/// by the generator, so no assembler reached the compilation: the step ran with
/// no assembled context and the object-set provider was never called. Asserting
/// that the stub provider's <c>ExecuteSimilarityAsync</c> was invoked with the
/// declared <c>TopK</c>/<c>MinRelevance</c>, and that the step received a
/// state + retrieval + literal context, is the wire-in proof.
/// </para>
/// <para>
/// Ontology-wired, not RAG (INV-2): the assembler's only retrieval dependency is
/// <c>IObjectSetProvider</c> (Strategos.Ontology.ObjectSets), backed here by the
/// recording <see cref="StubObjectSetProvider"/>.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single
/// process-wide container + host and observes process-shared probes.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<ContextHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class ContextBehaviorTests
{
    private readonly ContextHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextBehaviorTests"/> class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared
    /// across the entire test session.
    /// </param>
    public ContextBehaviorTests(ContextHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Runs the generated context workflow saga whose enrich step declares
    /// <c>.WithContext(...)</c>. Asserts the generated assembler assembled the
    /// step's context (state + retrieval + literal segments) and delivered it to
    /// the step, and that the lowered retrieval ran through the stub
    /// <c>IObjectSetProvider</c> with the declared <c>TopK</c>/<c>MinRelevance</c>.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_StepWithContext_AssemblesContextAndInvokesExecuteSimilarity()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();
        this.host.Probe.Reset();

        var initialState = new ContextState { WorkflowId = workflowId };
        var startCommand = new StartContextFlowCommand(workflowId, initialState);

        var completed = await this.host.RunWorkflowAsync<ContextFlowSaga>(workflowId, startCommand);

        // The saga reached its terminal phase via the finish step's MarkCompleted().
        await Assert.That(completed).IsTrue();

        // Every step ran exactly once, including the context-declaring enrich step.
        await Assert.That(this.host.Invocations.CountFor(nameof(ContextPrepareStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(EnrichStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(ContextFinishStep))).IsEqualTo(1);

        // Retrieval proof: the lowered SimilarityExpression reached the provider
        // with the declared TopK / MinRelevance / query exactly once.
        await Assert.That(this.host.Probe.SimilarityCallCount).IsEqualTo(1);
        var similarity = this.host.Probe.LastSimilarity;
        await Assert.That(similarity).IsNotNull();
        await Assert.That(similarity!.TopK).IsEqualTo(5);
        await Assert.That(similarity.MinRelevance).IsEqualTo(0.8);
        await Assert.That(similarity.QueryText).IsEqualTo("recommended widgets");

        // Assembly proof: the worker handler assembled context BEFORE the step ran
        // and delivered it to the context-aware step.
        var assembled = this.host.Probe.LastAssembledContext;
        await Assert.That(assembled).IsNotNull();
        await Assert.That(assembled!.IsEmpty).IsFalse();

        // All three declared sources are present, in order: state, retrieval, literal.
        await Assert.That(assembled.Segments.Count).IsEqualTo(3);
        await Assert.That(assembled.Segments[0]).IsTypeOf<StateContextSegment>();
        await Assert.That(assembled.Segments[1]).IsTypeOf<RetrievalContextSegment>();
        await Assert.That(assembled.Segments[2]).IsTypeOf<LiteralContextSegment>();

        // State segment carries the value the prepare step folded into state.
        var stateSegment = (StateContextSegment)assembled.Segments[0];
        await Assert.That(stateSegment.Value as string).IsEqualTo("Ada Lovelace");

        // Retrieval segment carries the single canned stub document.
        var retrievalSegment = (RetrievalContextSegment)assembled.Segments[1];
        await Assert.That(retrievalSegment.CollectionName).IsEqualTo(nameof(ProductCatalog));
        await Assert.That(retrievalSegment.Results.Count).IsEqualTo(1);
        await Assert.That(retrievalSegment.Results[0].Score).IsEqualTo(StubObjectSetProvider.StubScore);

        // Literal segment carries the declared brand-guidelines literal.
        var literalSegment = (LiteralContextSegment)assembled.Segments[2];
        await Assert.That(literalSegment.Value).IsEqualTo("Follow brand guidelines.");
    }
}
