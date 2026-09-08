// =============================================================================
// <copyright file="WorkflowActionReferenceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// Tests occurrence-scoped ontology action identity on workflow steps.
/// </summary>
[Property("Category", "Unit")]
public sealed class WorkflowActionReferenceTests
{
    /// <summary>
    /// The language-neutral identity is a sealed immutable value object.
    /// </summary>
    [Test]
    public async Task Reference_IsSealedAndReadOnly()
    {
        var type = typeof(WorkflowActionReference);
        await Assert.That(type.IsSealed).IsTrue();

        foreach (var property in type.GetProperties())
        {
            await Assert.That(property.SetMethod).IsNull();
        }
    }

    /// <summary>
    /// Entry, ordinary, and terminal configure overloads attach the action identity to
    /// the exact step occurrence while preserving ordinary configuration.
    /// </summary>
    [Test]
    public async Task Performs_LinearConfiguredSteps_CarriesActionAndConfiguration()
    {
        var entryAction = Action("validate");
        var middleAction = Action("process");
        var terminalAction = Action("complete");

        var workflow = Workflow<TestWorkflowState>
            .Create("action-linear")
            .StartWith<ValidateStep>(step => step.Performs(entryAction))
            .Then<ProcessStep>(step => step
                .Performs(middleAction)
                .WithRetry(3))
            .Finally<CompleteStep>(step => step.Performs(terminalAction));

        await Assert.That(workflow.Steps[0].Action).IsEqualTo(entryAction);
        await Assert.That(workflow.Steps[1].Action).IsEqualTo(middleAction);
        await Assert.That(workflow.Steps[1].Configuration!.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(workflow.Steps[2].Action).IsEqualTo(terminalAction);
    }

    /// <summary>
    /// Branch, loop, fork, and failure-handler configure paths all retain their
    /// occurrence-scoped action identity in the builder IR.
    /// </summary>
    [Test]
    public async Task Performs_StructuralPathConfiguredSteps_CarriesActionWithoutLoss()
    {
        var branchAction = Action("branch");
        var branchWorkflow = Workflow<TestWorkflowState>
            .Create("action-branch")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path => path.Then<AutoProcessStep>(step => step.Performs(branchAction))))
            .Finally<CompleteStep>();

        var loopAction = Action("loop");
        var loopWorkflow = Workflow<TestWorkflowState>
            .Create("action-loop")
            .StartWith<ValidateStep>()
            .RepeatUntil(
                state => state.QualityScore >= 0.9m,
                "Refinement",
                loop => loop.Then<CritiqueStep>(step => step.Performs(loopAction)),
                maxIterations: 2)
            .Finally<CompleteStep>();

        var leftAction = Action("fork-left");
        var rightAction = Action("fork-right");
        var forkWorkflow = Workflow<TestWorkflowState>
            .Create("action-fork")
            .StartWith<ValidateStep>()
            .Fork(
                path => path.Then<ProcessStep>(step => step.Performs(leftAction)),
                path => path.Then<ProcessStep>(step => step.Performs(rightAction)))
            .Join<NotifyStep>()
            .Finally<CompleteStep>();

        var failureAction = Action("recover");
        var failureWorkflow = Workflow<TestWorkflowState>
            .Create("action-failure")
            .StartWith<ValidateStep>()
            .OnFailure(handler => handler
                .Then<LogFailureStep>(step => step.Performs(failureAction))
                .Complete())
            .Finally<CompleteStep>();

        await Assert.That(branchWorkflow.BranchPoints[0].Paths[0].Steps[0].Action)
            .IsEqualTo(branchAction);
        await Assert.That(loopWorkflow.Loops[0].BodySteps[0].Action).IsEqualTo(loopAction);
        await Assert.That(forkWorkflow.ForkPoints[0].Paths[0].Steps[0].Action).IsEqualTo(leftAction);
        await Assert.That(forkWorkflow.ForkPoints[0].Paths[1].Steps[0].Action).IsEqualTo(rightAction)
            .Because("two uses of one CLR step type retain distinct occurrence identities.");
        await Assert.That(failureWorkflow.FailureHandlers[0].Steps[0].Action)
            .IsEqualTo(failureAction);
    }

    /// <summary>
    /// Top-level and loop fork joins retain the same occurrence-scoped action and
    /// ordinary step configuration as every other class-based step position.
    /// </summary>
    [Test]
    public async Task Performs_ConfiguredForkJoins_CarryActionAndConfiguration()
    {
        var topLevelAction = Action("merge");
        var topLevel = Workflow<TestWorkflowState>
            .Create("action-fork-join")
            .StartWith<ValidateStep>()
            .Fork(
                path => path.Then<ProcessStep>(),
                path => path.Then<CritiqueStep>())
            .Join<NotifyStep>(step => step
                .Performs(topLevelAction)
                .WithRetry(3))
            .Finally<CompleteStep>();

        var loopAction = Action("merge-loop");
        var inLoop = Workflow<TestWorkflowState>
            .Create("action-loop-fork-join")
            .StartWith<ValidateStep>()
            .RepeatUntil(
                state => state.QualityScore >= 0.9m,
                "Refinement",
                loop => loop
                    .Fork(
                        path => path.Then<ProcessStep>(),
                        path => path.Then<CritiqueStep>())
                    .Join<NotifyStep>(step => step
                        .Performs(loopAction)
                        .WithTimeout(TimeSpan.FromMinutes(2))),
                maxIterations: 2)
            .Finally<CompleteStep>();

        var topLevelJoin = topLevel.Steps.Single(step => step.StepType == typeof(NotifyStep));
        await Assert.That(topLevelJoin.Action).IsEqualTo(topLevelAction);
        await Assert.That(topLevelJoin.Configuration!.Retry!.MaxAttempts).IsEqualTo(3);

        var loopJoin = inLoop.Loops[0].BodySteps.Single(step => step.StepType == typeof(NotifyStep));
        await Assert.That(loopJoin.Action).IsEqualTo(loopAction);
        await Assert.That(loopJoin.Configuration!.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
        await Assert.That(loopJoin.IsLoopBodyStep).IsTrue();
    }

    /// <summary>The configured join overload rejects a null callback at both DSL levels.</summary>
    [Test]
    public async Task ConfiguredForkJoin_NullConfigure_ThrowsArgumentNullException()
    {
        var topLevelFork = Workflow<TestWorkflowState>
            .Create("action-fork-null")
            .StartWith<ValidateStep>()
            .Fork(
                path => path.Then<ProcessStep>(),
                path => path.Then<CritiqueStep>());

        await Assert.That(() => topLevelFork.Join<NotifyStep>(null!))
            .Throws<ArgumentNullException>();

        await Assert.That(() => Workflow<TestWorkflowState>
            .Create("action-loop-fork-null")
            .StartWith<ValidateStep>()
            .RepeatUntil(
                state => state.QualityScore >= 0.9m,
                "Refinement",
                loop => loop
                    .Fork(
                        path => path.Then<ProcessStep>(),
                        path => path.Then<CritiqueStep>())
                    .Join<NotifyStep>(null!),
                maxIterations: 2))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// A null action reference is rejected at the fluent API boundary.
    /// </summary>
    [Test]
    public async Task Performs_NullReference_ThrowsArgumentNullException()
    {
        await Assert.That(() => Workflow<TestWorkflowState>
            .Create("action-null")
            .StartWith<ValidateStep>(step => step.Performs(null!)))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Every name is part of the action identity and therefore rejects null, empty,
    /// and whitespace-only values at construction.
    /// </summary>
    /// <param name="invalidName">The invalid identity component.</param>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" \t")]
    public async Task Constructor_InvalidIdentityComponent_ThrowsArgumentException(
        string? invalidName)
    {
        await Assert.That(() => new WorkflowActionReference(invalidName!, "Order", "submit"))
            .Throws<ArgumentException>();
        await Assert.That(() => new WorkflowActionReference("orders", invalidName!, "submit"))
            .Throws<ArgumentException>();
        await Assert.That(() => new WorkflowActionReference("orders", "Order", invalidName!))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// An occurrence cannot silently choose between competing action declarations.
    /// </summary>
    [Test]
    public async Task Performs_RepeatedDeclaration_ThrowsInvalidOperationException()
    {
        await Assert.That(() => Workflow<TestWorkflowState>
            .Create("action-repeated")
            .StartWith<ValidateStep>(step => step
                .Performs(Action("validate"))
                .Performs(Action("also-validate"))))
            .Throws<InvalidOperationException>();
    }

    private static WorkflowActionReference Action(string actionName) =>
        new("orders", "Order", actionName);
}
