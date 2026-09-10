// -----------------------------------------------------------------------
// <copyright file="DefinitionShapeExtractionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Pins the pre-2.13 extraction contract for workflows that are not bound to an ontology action:
/// the shape of the <c>Definition</c> member must not change what the generator emits.
/// </summary>
/// <remarks>
/// 2.13 scopes extraction to the <c>Definition</c> value so that unrelated Strategos calls in the
/// attributed type cannot leak into the proof graph. That scoping is only sound when the value is a
/// closed direct <c>Finally&lt;TStep&gt;</c> chain. For every other shape the whole-type walk is the
/// established behaviour; narrowing it silently produced an empty saga for a helper-method
/// <c>Definition</c> that generated a complete saga on 2.12.
/// </remarks>
[Property("Category", "Integration")]
public sealed class DefinitionShapeExtractionTests
{
    private const string Prelude = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace DefinitionShapes;

        [WorkflowState]
        public sealed record ShapeState : IWorkflowState { public Guid WorkflowId { get; init; } }

        public sealed class StepA : IWorkflowStep<ShapeState>
        {
            public Task<StepResult<ShapeState>> ExecuteAsync(
                ShapeState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<ShapeState>.FromState(state));
        }

        public sealed class StepB : IWorkflowStep<ShapeState>
        {
            public Task<StepResult<ShapeState>> ExecuteAsync(
                ShapeState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<ShapeState>.FromState(state));
        }

        public sealed class StepC : IWorkflowStep<ShapeState>
        {
            public Task<StepResult<ShapeState>> ExecuteAsync(
                ShapeState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<ShapeState>.FromState(state));
        }

        """;

    private const string DirectChain = """
        [Workflow("shape-flow")]
        public static partial class ShapeFlowWorkflowDefinition
        {
            public static WorkflowDefinition<ShapeState> Definition => Workflow<ShapeState>
                .Create("shape-flow")
                .StartWith<StepA>()
                .Then<StepB>()
                .Finally<StepC>();
        }
        """;

    private const string HelperMethod = """
        [Workflow("shape-flow")]
        public static partial class ShapeFlowWorkflowDefinition
        {
            public static WorkflowDefinition<ShapeState> Definition => Build();

            private static WorkflowDefinition<ShapeState> Build() => Workflow<ShapeState>
                .Create("shape-flow")
                .StartWith<StepA>()
                .Then<StepB>()
                .Finally<StepC>();
        }
        """;

    /// <summary>
    /// Kill fixture: a <c>Definition</c> that delegates to a helper method emits exactly the saga
    /// the equivalent direct chain emits. Before the fix the helper shape produced AGWF002 and an
    /// empty saga whose generated <c>Start…Command</c> did not compile.
    /// </summary>
    [Test]
    public async Task UnboundWorkflow_HelperMethodDefinition_EmitsSameSagaAsDirectChain()
    {
        var direct = GeneratorTestHelper.RunGeneratorWithValidInput(Prelude + DirectChain);
        var helper = GeneratorTestHelper.RunGeneratorWithValidInput(Prelude + HelperMethod);

        await Assert.That(helper.Diagnostics.Any(diagnostic => diagnostic.Id == "AGWF002")).IsFalse()
            .Because("the helper body carries every step of the workflow");

        var directSources = GeneratedSourcesByHint(direct);
        var helperSources = GeneratedSourcesByHint(helper);

        await Assert.That(helperSources.Keys.Order(StringComparer.Ordinal))
            .IsEquivalentTo(directSources.Keys.Order(StringComparer.Ordinal));
        foreach (var (hint, directText) in directSources)
        {
            await Assert.That(helperSources[hint]).IsEqualTo(directText)
                .Because($"generated '{hint}' must not depend on the Definition shape");
        }

        var saga = GeneratorTestHelper.GetGeneratedSource(helper, "Saga.g.cs");
        await Assert.That(saga).Contains("StepA");
        await Assert.That(saga).Contains("StepB");
        await Assert.That(saga).Contains("StepC");
    }

    /// <summary>
    /// The proof side of the same contract: a bound workflow whose <c>Definition</c> is not a
    /// closed direct chain still fails closed with AGWF042, because extraction cannot prove that
    /// the helper body is the whole runtime topology.
    /// </summary>
    [Test]
    public async Task BoundWorkflow_HelperMethodDefinition_ReportsAgwf042ClosureFailure()
    {
        var source = BoundPrelude + HelperMethodBound;

        var result = GeneratorTestHelper.RunGeneratorWithValidInput(source, "AGWF042");

        var errors = result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        await Assert.That(errors).HasCount().EqualTo(1);
        await Assert.That(errors[0].Id).IsEqualTo("AGWF042");
        await Assert.That(errors[0].GetMessage())
            .Contains("workflow Definition must end in one direct Strategos Finally<TStep> call");
    }

    private const string BoundPrelude = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;
        using Strategos.Ontology.Descriptors;
        using Strategos.Steps;

        namespace BoundDefinitionShapes;

        public sealed class Order { public int Stage { get; set; } }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("fulfill")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage)
                        .BoundToWorkflow("flow");

                    obj.Action("leaf")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);

                    obj.Action("done")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);
                });
            }
        }

        [WorkflowState]
        public sealed record FlowState : IWorkflowState { public Guid WorkflowId { get; init; } }

        public sealed class Leaf : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public sealed class Done : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        """;

    private const string HelperMethodBound = """
        [Workflow("flow")]
        public static partial class FlowWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Build();

            private static WorkflowDefinition<FlowState> Build() => Workflow<FlowState>
                .Create("flow")
                .StartWith<Leaf>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "leaf")))
                .Finally<Done>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "done")));
        }
        """;

    private static Dictionary<string, string> GeneratedSourcesByHint(GeneratorDriverRunResult result) =>
        result.GeneratedTrees.ToDictionary(
            tree => Path.GetFileName(tree.FilePath),
            tree => tree.GetText().ToString(),
            StringComparer.Ordinal);
}
