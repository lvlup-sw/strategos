// =============================================================================
// <copyright file="ApprovalModelTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="ApprovalModel"/> record.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Create factory method validates parameters</description></item>
///   <item><description>ApprovalPointName and ApproverTypeName are captured</description></item>
///   <item><description>Optional escalation and rejection handlers are captured</description></item>
///   <item><description>PhaseName property derives correct value</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class ApprovalModelTests
{
    // =============================================================================
    // A. Create Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with valid parameters creates ApprovalModel.
    /// </summary>
    [Test]
    public async Task Create_WithValidParameters_CreatesApprovalModel()
    {
        // Act
        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder");

        // Assert
        await Assert.That(model).IsNotNull();
        await Assert.That(model.ApprovalPointName).IsEqualTo("ManagerReview");
        await Assert.That(model.ApproverTypeName).IsEqualTo("MyApp.Approvers.ManagerApprover");
        await Assert.That(model.PrecedingStepName).IsEqualTo("ValidateOrder");
    }

    /// <summary>
    /// Verifies that Create with null approvalPointName throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Create_WithNullApprovalPointName_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ApprovalModel.Create(
            approvalPointName: null!,
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder"))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create with invalid identifier throws ArgumentException.
    /// </summary>
    [Test]
    public async Task Create_WithInvalidIdentifier_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => ApprovalModel.Create(
            approvalPointName: "123Invalid",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder"))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create with null approverTypeName throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Create_WithNullApproverTypeName_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: null!,
            precedingStepName: "ValidateOrder"))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create with empty approverTypeName throws ArgumentException.
    /// </summary>
    [Test]
    public async Task Create_WithEmptyApproverTypeName_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "   ",
            precedingStepName: "ValidateOrder"))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create with null precedingStepName throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Create_WithNullPrecedingStepName_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. PhaseName Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that PhaseName returns approval point name with AwaitApproval prefix.
    /// </summary>
    [Test]
    public async Task PhaseName_ReturnsAwaitApprovalPrefix()
    {
        // Arrange
        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder");

        // Act & Assert
        await Assert.That(model.PhaseName).IsEqualTo("AwaitApproval_ManagerReview");
    }

    // =============================================================================
    // C. Optional Handler Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with escalation handler steps captures them.
    /// </summary>
    [Test]
    public async Task Create_WithEscalationSteps_CapturesSteps()
    {
        // Arrange
        var escalationSteps = new List<StepModel>
        {
            StepModel.Create("NotifyAdmin", "MyApp.Steps.NotifyAdmin"),
        };

        // Act
        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder",
            escalationSteps: escalationSteps);

        // Assert
        await Assert.That(model.EscalationSteps).HasCount().EqualTo(1);
        await Assert.That(model.EscalationSteps![0].StepName).IsEqualTo("NotifyAdmin");
    }

    /// <summary>
    /// Verifies that Create with rejection handler steps captures them.
    /// </summary>
    [Test]
    public async Task Create_WithRejectionSteps_CapturesSteps()
    {
        // Arrange
        var rejectionSteps = new List<StepModel>
        {
            StepModel.Create("LogRejection", "MyApp.Steps.LogRejection"),
        };

        // Act
        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder",
            rejectionSteps: rejectionSteps);

        // Assert
        await Assert.That(model.RejectionSteps).HasCount().EqualTo(1);
        await Assert.That(model.RejectionSteps![0].StepName).IsEqualTo("LogRejection");
    }

    /// <summary>
    /// Verifies that Create with nested escalation approvals captures them.
    /// </summary>
    [Test]
    public async Task Create_WithNestedApprovals_CapturesNestedApprovals()
    {
        // Arrange
        var nestedApproval = ApprovalModel.Create(
            approvalPointName: "DirectorReview",
            approverTypeName: "MyApp.Approvers.DirectorApprover",
            precedingStepName: "ManagerReview");

        var nestedApprovals = new List<ApprovalModel> { nestedApproval };

        // Act
        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder",
            nestedEscalationApprovals: nestedApprovals);

        // Assert
        await Assert.That(model.NestedEscalationApprovals).HasCount().EqualTo(1);
        await Assert.That(model.NestedEscalationApprovals![0].ApprovalPointName).IsEqualTo("DirectorReview");
    }

    // =============================================================================
    // D. HasEscalation and HasRejection Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that HasEscalation returns true when escalation steps exist.
    /// </summary>
    [Test]
    public async Task HasEscalation_WithSteps_ReturnsTrue()
    {
        // Arrange
        var escalationSteps = new List<StepModel>
        {
            StepModel.Create("NotifyAdmin", "MyApp.Steps.NotifyAdmin"),
        };

        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder",
            escalationSteps: escalationSteps);

        // Act & Assert
        await Assert.That(model.HasEscalation).IsTrue();
    }

    /// <summary>
    /// Verifies that HasEscalation returns true when nested approvals exist.
    /// </summary>
    [Test]
    public async Task HasEscalation_WithNestedApprovals_ReturnsTrue()
    {
        // Arrange
        var nestedApproval = ApprovalModel.Create(
            approvalPointName: "DirectorReview",
            approverTypeName: "MyApp.Approvers.DirectorApprover",
            precedingStepName: "ManagerReview");

        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder",
            nestedEscalationApprovals: [nestedApproval]);

        // Act & Assert
        await Assert.That(model.HasEscalation).IsTrue();
    }

    /// <summary>
    /// Verifies that HasEscalation returns false when no escalation configured.
    /// </summary>
    [Test]
    public async Task HasEscalation_WithoutEscalation_ReturnsFalse()
    {
        // Arrange
        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder");

        // Act & Assert
        await Assert.That(model.HasEscalation).IsFalse();
    }

    /// <summary>
    /// Verifies that HasRejection returns true when rejection steps exist.
    /// </summary>
    [Test]
    public async Task HasRejection_WithSteps_ReturnsTrue()
    {
        // Arrange
        var rejectionSteps = new List<StepModel>
        {
            StepModel.Create("LogRejection", "MyApp.Steps.LogRejection"),
        };

        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder",
            rejectionSteps: rejectionSteps);

        // Act & Assert
        await Assert.That(model.HasRejection).IsTrue();
    }

    /// <summary>
    /// Verifies that HasRejection returns false when no rejection configured.
    /// </summary>
    [Test]
    public async Task HasRejection_WithoutRejection_ReturnsFalse()
    {
        // Arrange
        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder");

        // Act & Assert
        await Assert.That(model.HasRejection).IsFalse();
    }

    // =============================================================================
    // E. IsTerminal Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that IsEscalationTerminal defaults to false.
    /// </summary>
    [Test]
    public async Task IsEscalationTerminal_ByDefault_IsFalse()
    {
        // Arrange
        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder");

        // Act & Assert
        await Assert.That(model.IsEscalationTerminal).IsFalse();
    }

    /// <summary>
    /// Verifies that IsRejectionTerminal defaults to false.
    /// </summary>
    [Test]
    public async Task IsRejectionTerminal_ByDefault_IsFalse()
    {
        // Arrange
        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder");

        // Act & Assert
        await Assert.That(model.IsRejectionTerminal).IsFalse();
    }

    /// <summary>
    /// Verifies that Create with isEscalationTerminal true captures the flag.
    /// </summary>
    [Test]
    public async Task Create_WithEscalationTerminal_CapturesFlag()
    {
        // Act
        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder",
            isEscalationTerminal: true);

        // Assert
        await Assert.That(model.IsEscalationTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that Create with isRejectionTerminal true captures the flag.
    /// </summary>
    [Test]
    public async Task Create_WithRejectionTerminal_CapturesFlag()
    {
        // Act
        var model = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "MyApp.Approvers.ManagerApprover",
            precedingStepName: "ValidateOrder",
            isRejectionTerminal: true);

        // Assert
        await Assert.That(model.IsRejectionTerminal).IsTrue();
    }
}
