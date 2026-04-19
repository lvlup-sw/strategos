// -----------------------------------------------------------------------
// <copyright file="HandlerContextTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for the <see cref="HandlerContext"/> record.
/// </summary>
[Property("Category", "Unit")]
public class HandlerContextTests
{
    /// <summary>
    /// Verifies that HandlerContext can be created with required properties.
    /// </summary>
    [Test]
    public async Task Create_WithRequiredProperties_Succeeds()
    {
        // Act
        var context = new HandlerContext(
            StepIndex: 0,
            IsLastStep: false,
            IsTerminalStep: false,
            NextStepName: "NextStep",
            StepModel: null,
            LoopsAtStep: null,
            BranchAtStep: null,
            ApprovalAtStep: null,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);

        // Assert
        await Assert.That(context.StepIndex).IsEqualTo(0);
        await Assert.That(context.IsLastStep).IsFalse();
        await Assert.That(context.IsTerminalStep).IsFalse();
        await Assert.That(context.NextStepName).IsEqualTo("NextStep");
    }

    /// <summary>
    /// Verifies that HandlerContext stores StepModel correctly.
    /// </summary>
    [Test]
    public async Task Create_WithStepModel_StoresStepModel()
    {
        // Arrange
        var stepModel = StepModel.Create(
            stepName: "ValidateOrder",
            stepTypeName: "MyNamespace.ValidateOrder");

        // Act
        var context = new HandlerContext(
            StepIndex: 1,
            IsLastStep: false,
            IsTerminalStep: false,
            NextStepName: "ProcessOrder",
            StepModel: stepModel,
            LoopsAtStep: null,
            BranchAtStep: null,
            ApprovalAtStep: null,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);

        // Assert
        await Assert.That(context.StepModel).IsNotNull();
        await Assert.That(context.StepModel!.StepName).IsEqualTo("ValidateOrder");
    }

    /// <summary>
    /// Verifies that HandlerContext stores LoopsAtStep correctly.
    /// </summary>
    [Test]
    public async Task Create_WithLoops_StoresLoops()
    {
        // Arrange
        var loop = LoopModel.Create(
            loopName: "Refinement",
            conditionId: "TestWorkflow-Refinement",
            maxIterations: 5,
            firstBodyStepName: "Refinement_Analyze",
            lastBodyStepName: "Refinement_Refine");

        var loops = new List<LoopModel> { loop };

        // Act
        var context = new HandlerContext(
            StepIndex: 2,
            IsLastStep: false,
            IsTerminalStep: false,
            NextStepName: null,
            StepModel: null,
            LoopsAtStep: loops,
            BranchAtStep: null,
            ApprovalAtStep: null,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);

        // Assert
        await Assert.That(context.LoopsAtStep).IsNotNull();
        await Assert.That(context.LoopsAtStep).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that HandlerContext stores BranchAtStep correctly.
    /// </summary>
    [Test]
    public async Task Create_WithBranch_StoresBranch()
    {
        // Arrange
        var branchCase = BranchCaseModel.Create(
            caseValueLiteral: "OrderStatus.Approved",
            branchPathPrefix: "Approved",
            stepNames: ["Approved_Process"],
            isTerminal: false);

        var branch = BranchModel.Create(
            branchId: "TestWorkflow-Status",
            previousStepName: "ValidateOrder",
            discriminatorPropertyPath: "Status",
            discriminatorTypeName: "OrderStatus",
            isEnumDiscriminator: true,
            isMethodDiscriminator: false,
            cases: [branchCase]);

        // Act
        var context = new HandlerContext(
            StepIndex: 0,
            IsLastStep: false,
            IsTerminalStep: false,
            NextStepName: null,
            StepModel: null,
            LoopsAtStep: null,
            BranchAtStep: branch,
            ApprovalAtStep: null,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);

        // Assert
        await Assert.That(context.BranchAtStep).IsNotNull();
        await Assert.That(context.BranchAtStep!.BranchId).IsEqualTo("TestWorkflow-Status");
    }

    /// <summary>
    /// Verifies that HandlerContext for last step has IsLastStep true and null NextStepName.
    /// </summary>
    [Test]
    public async Task Create_ForLastStep_HasCorrectFlags()
    {
        // Act
        var context = new HandlerContext(
            StepIndex: 5,
            IsLastStep: true,
            IsTerminalStep: false,
            NextStepName: null,
            StepModel: null,
            LoopsAtStep: null,
            BranchAtStep: null,
            ApprovalAtStep: null,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);

        // Assert
        await Assert.That(context.IsLastStep).IsTrue();
        await Assert.That(context.NextStepName).IsNull();
    }

    /// <summary>
    /// Verifies that HandlerContext stores ApprovalAtStep correctly.
    /// </summary>
    [Test]
    public async Task Create_WithApproval_StoresApproval()
    {
        // Arrange
        var approval = ApprovalModel.Create(
            approvalPointName: "PostValidation",
            approverTypeName: "LegalReviewer",
            precedingStepName: "ValidateOrder");

        // Act
        var context = new HandlerContext(
            StepIndex: 0,
            IsLastStep: false,
            IsTerminalStep: false,
            NextStepName: "ProcessOrder",
            StepModel: null,
            LoopsAtStep: null,
            BranchAtStep: null,
            ApprovalAtStep: approval,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);

        // Assert
        await Assert.That(context.ApprovalAtStep).IsNotNull();
        await Assert.That(context.ApprovalAtStep!.ApprovalPointName).IsEqualTo("PostValidation");
        await Assert.That(context.ApprovalAtStep.ApproverTypeName).IsEqualTo("LegalReviewer");
    }

    /// <summary>
    /// Verifies that HandlerContext is a record with value equality.
    /// </summary>
    [Test]
    public async Task Equality_SameValues_AreEqual()
    {
        // Arrange
        var context1 = new HandlerContext(
            StepIndex: 0,
            IsLastStep: false,
            IsTerminalStep: false,
            NextStepName: "NextStep",
            StepModel: null,
            LoopsAtStep: null,
            BranchAtStep: null,
            ApprovalAtStep: null,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);

        var context2 = new HandlerContext(
            StepIndex: 0,
            IsLastStep: false,
            IsTerminalStep: false,
            NextStepName: "NextStep",
            StepModel: null,
            LoopsAtStep: null,
            BranchAtStep: null,
            ApprovalAtStep: null,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);

        // Assert
        await Assert.That(context1).IsEqualTo(context2);
    }
}
