// -----------------------------------------------------------------------
// <copyright file="AllowDiagnosticForkClosureTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Proof;

/// <summary>
/// Pins the composition restriction that <c>AllowDiagnosticFork</c> now carries.
/// </summary>
/// <remarks>
/// <para>
/// <c>AllowDiagnosticFork</c> shipped in v2.10.0 (#151) and still lowers unchanged in a
/// workflow that is neither bound nor typed-compensated. It is not represented in the closed
/// workflow proof, so it is an unconditional topology-closure failure: <c>AGWF042</c> for a
/// workflow that a <c>BoundToWorkflow</c> action names, and <c>AGWF045</c> — which carries
/// <c>NotConfigurable</c> — for a workflow that declares typed compensation.
/// </para>
/// <para>
/// Without these fixtures the arm is both undocumented and unpinned: it lives in one
/// <c>case</c> of a switch whose effect is delivered two files away through a string
/// collection, so a later refactor could broaden or drop it unobserved.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public sealed class AllowDiagnosticForkClosureTests
{
    private const string ClosureReason =
        "diagnostic fork paths are not admitted in a bound or typed-compensation workflow";

    /// <summary>A bound workflow declaring a diagnostic fork cannot be proved closed.</summary>
    /// <returns>A task representing the assertion.</returns>
    [Test]
    public async Task BoundWorkflowWithDiagnosticFork_ReportsAgwf042()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(
            Source(bound: true, typedCompensation: false),
            "AGWF042");

        var diagnostics = result.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "AGWF042")
            .ToArray();

        await Assert.That(diagnostics).IsNotEmpty()
            .Because("a workflow a BoundToWorkflow action names must fail closed on the fork edge");
        await Assert.That(diagnostics[0].GetMessage()).Contains("AllowDiagnosticFork");
        await Assert.That(diagnostics[0].GetMessage()).Contains(ClosureReason);
    }

    /// <summary>Typed compensation raises the same closure failure to the unsuppressible arm.</summary>
    /// <returns>A task representing the assertion.</returns>
    [Test]
    public async Task TypedCompensationWorkflowWithDiagnosticFork_ReportsAgwf045()
    {
        var result = GeneratorTestHelper.RunRejectedTopologyWithValidInput(
            Source(bound: false, typedCompensation: true),
            "AGWF042",
            "AGWF045");

        var diagnostics = result.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "AGWF045")
            .ToArray();

        await Assert.That(diagnostics).IsNotEmpty()
            .Because("typed compensation over an unclosed topology is a NotConfigurable refusal");
        await Assert.That(diagnostics[0].GetMessage()).Contains("AllowDiagnosticFork");
        await Assert.That(diagnostics[0].GetMessage()).Contains(ClosureReason);
    }

    /// <summary>
    /// Control: the same fork in a workflow that is neither bound nor typed-compensated is
    /// still legal, so the restriction is a composition rule and not a withdrawal.
    /// </summary>
    /// <returns>A task representing the assertion.</returns>
    [Test]
    public async Task UnboundWorkflowWithDiagnosticFork_ReportsNoBindingDiagnostics()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(
            Source(bound: false, typedCompensation: false));

        var diagnostics = result.Diagnostics
            .Where(static diagnostic => diagnostic.Id is
                "AGWF039" or "AGWF040" or "AGWF041" or "AGWF042" or "AGWF044" or "AGWF045")
            .ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because("AllowDiagnosticFork shipped in v2.10.0 and still lowers where nothing binds it");
    }

    private static string Source(bool bound, bool typedCompensation)
    {
        var binding = bound ? "\n                        .BoundToWorkflow(\"fork-flow\")" : string.Empty;
        var compensation = typedCompensation
            ? "\n                    .Compensate<UndoLeaf>(new WorkflowActionReference(\"orders\", \"Order\", \"undo\"))"
            : string.Empty;

        return $$"""
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

        using ForkTrigger = Strategos.Contracts.Generated.ForkTrigger;

        namespace DiagnosticForkClosureProbe;

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
                        .Modifies(order => order.Stage){{binding}};

                    obj.Action("leaf")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);

                    obj.Action("undo")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 0)
                        .Modifies(order => order.Stage);

                    obj.Action("done")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);
                });
            }
        }

        [WorkflowState]
        public sealed record ForkFlowState : IWorkflowState { public Guid WorkflowId { get; init; } }

        public sealed class Leaf : IWorkflowStep<ForkFlowState>
        {
            public Task<StepResult<ForkFlowState>> ExecuteAsync(
                ForkFlowState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<ForkFlowState>.FromState(state));
        }

        public sealed class UndoLeaf : IWorkflowStep<ForkFlowState>
        {
            public Task<StepResult<ForkFlowState>> ExecuteAsync(
                ForkFlowState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<ForkFlowState>.FromState(state));
        }

        public sealed class Done : IWorkflowStep<ForkFlowState>
        {
            public Task<StepResult<ForkFlowState>> ExecuteAsync(
                ForkFlowState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<ForkFlowState>.FromState(state));
        }

        [Workflow("fork-flow")]
        public static partial class ForkFlowWorkflowDefinition
        {
            public static WorkflowDefinition<ForkFlowState> Definition => Workflow<ForkFlowState>
                .Create("fork-flow")
                .StartWith<Leaf>(step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "leaf")){{compensation}})
                .AllowDiagnosticFork(fork => fork
                    .Anchor("Leaf")
                    .PermitTrigger(ForkTrigger.RatificationFailure, "provisionalStampEventId")
                    .WithCompensationSeed("Leaf")
                    .MaxForks(1))
                .Finally<Done>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "done")));
        }
        """;
    }
}
