// -----------------------------------------------------------------------
// <copyright file="BranchModelTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="BranchModel"/> and <see cref="BranchCaseModel"/> records.
/// </summary>
[Property("Category", "Unit")]
public class BranchModelTests
{
    // =============================================================================
    // A. BranchCaseModel Constructor Tests
    // =============================================================================

    /// <summary>
    /// Verifies that BranchCaseModel can be created with valid parameters.
    /// </summary>
    [Test]
    public async Task BranchCaseModel_Constructor_WithValidParameters_CreatesModel()
    {
        // Arrange & Act
        var model = new BranchCaseModel(
            CaseValueLiteral: "OrderStatus.Approved",
            BranchPathPrefix: "Approved",
            StepNames: ["ProcessPayment", "ShipOrder"],
            IsTerminal: false);

        // Assert
        await Assert.That(model.CaseValueLiteral).IsEqualTo("OrderStatus.Approved");
        await Assert.That(model.BranchPathPrefix).IsEqualTo("Approved");
        await Assert.That(model.StepNames).HasCount().EqualTo(2);
        await Assert.That(model.IsTerminal).IsFalse();
    }

    /// <summary>
    /// Verifies that terminal BranchCaseModel has IsTerminal set to true.
    /// </summary>
    [Test]
    public async Task BranchCaseModel_Constructor_TerminalCase_SetsIsTerminal()
    {
        // Arrange & Act
        var model = new BranchCaseModel(
            CaseValueLiteral: "OrderStatus.Rejected",
            BranchPathPrefix: "Rejected",
            StepNames: ["NotifyRejection"],
            IsTerminal: true);

        // Assert
        await Assert.That(model.IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that BranchCaseModel supports string discriminator values.
    /// </summary>
    [Test]
    public async Task BranchCaseModel_Constructor_StringDiscriminator_CapturesLiteral()
    {
        // Arrange & Act
        var model = new BranchCaseModel(
            CaseValueLiteral: "\"premium\"",
            BranchPathPrefix: "Premium",
            StepNames: ["ApplyPremiumDiscount"],
            IsTerminal: false);

        // Assert
        await Assert.That(model.CaseValueLiteral).IsEqualTo("\"premium\"");
    }

    /// <summary>
    /// Verifies that BranchCaseModel supports integer discriminator values.
    /// </summary>
    [Test]
    public async Task BranchCaseModel_Constructor_IntDiscriminator_CapturesLiteral()
    {
        // Arrange & Act
        var model = new BranchCaseModel(
            CaseValueLiteral: "1",
            BranchPathPrefix: "Priority1",
            StepNames: ["ProcessUrgent"],
            IsTerminal: false);

        // Assert
        await Assert.That(model.CaseValueLiteral).IsEqualTo("1");
    }

    /// <summary>
    /// Verifies that BranchCaseModel supports boolean discriminator values.
    /// </summary>
    [Test]
    public async Task BranchCaseModel_Constructor_BoolDiscriminator_CapturesLiteral()
    {
        // Arrange & Act
        var model = new BranchCaseModel(
            CaseValueLiteral: "true",
            BranchPathPrefix: "Enabled",
            StepNames: ["ProcessEnabled"],
            IsTerminal: false);

        // Assert
        await Assert.That(model.CaseValueLiteral).IsEqualTo("true");
    }

    // =============================================================================
    // B. BranchCaseModel Computed Properties Tests
    // =============================================================================

    /// <summary>
    /// Verifies that FirstStepName returns the first step in the branch path.
    /// </summary>
    [Test]
    public async Task BranchCaseModel_FirstStepName_ReturnsFirstStep()
    {
        // Arrange
        var model = new BranchCaseModel(
            CaseValueLiteral: "OrderStatus.Approved",
            BranchPathPrefix: "Approved",
            StepNames: ["ProcessPayment", "ShipOrder", "Complete"],
            IsTerminal: false);

        // Act & Assert
        await Assert.That(model.FirstStepName).IsEqualTo("ProcessPayment");
    }

    /// <summary>
    /// Verifies that LastStepName returns the last step in the branch path.
    /// </summary>
    [Test]
    public async Task BranchCaseModel_LastStepName_ReturnsLastStep()
    {
        // Arrange
        var model = new BranchCaseModel(
            CaseValueLiteral: "OrderStatus.Approved",
            BranchPathPrefix: "Approved",
            StepNames: ["ProcessPayment", "ShipOrder", "Complete"],
            IsTerminal: false);

        // Act & Assert
        await Assert.That(model.LastStepName).IsEqualTo("Complete");
    }

    /// <summary>
    /// Verifies that FirstStepName equals LastStepName for single-step branches.
    /// </summary>
    [Test]
    public async Task BranchCaseModel_SingleStep_FirstAndLastAreEqual()
    {
        // Arrange
        var model = new BranchCaseModel(
            CaseValueLiteral: "OrderStatus.Rejected",
            BranchPathPrefix: "Rejected",
            StepNames: ["NotifyRejection"],
            IsTerminal: true);

        // Act & Assert
        await Assert.That(model.FirstStepName).IsEqualTo("NotifyRejection");
        await Assert.That(model.LastStepName).IsEqualTo("NotifyRejection");
    }

    /// <summary>
    /// Verifies that FirstStepName throws when StepNames is empty.
    /// </summary>
    [Test]
    public async Task BranchCaseModel_EmptyStepNames_FirstStepName_ThrowsInvalidOperationException()
    {
        // Arrange
        var model = new BranchCaseModel(
            CaseValueLiteral: "OrderStatus.Approved",
            BranchPathPrefix: "Approved",
            StepNames: [],
            IsTerminal: false);

        // Act & Assert
        await Assert.That(() => _ = model.FirstStepName)
            .Throws<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that LastStepName throws when StepNames is empty.
    /// </summary>
    [Test]
    public async Task BranchCaseModel_EmptyStepNames_LastStepName_ThrowsInvalidOperationException()
    {
        // Arrange
        var model = new BranchCaseModel(
            CaseValueLiteral: "OrderStatus.Approved",
            BranchPathPrefix: "Approved",
            StepNames: [],
            IsTerminal: false);

        // Act & Assert
        await Assert.That(() => _ = model.LastStepName)
            .Throws<InvalidOperationException>();
    }

    // =============================================================================
    // C. BranchModel Constructor Tests
    // =============================================================================

    /// <summary>
    /// Verifies that BranchModel can be created with valid enum discriminator.
    /// </summary>
    [Test]
    public async Task BranchModel_Constructor_WithEnumDiscriminator_CreatesModel()
    {
        // Arrange
        var cases = new List<BranchCaseModel>
        {
            new("OrderStatus.Approved", "Approved", ["ProcessPayment"], false),
            new("OrderStatus.Rejected", "Rejected", ["NotifyRejection"], true),
        };

        // Act
        var model = new BranchModel(
            BranchId: "ProcessOrder-OrderStatus",
            PreviousStepName: "ValidateOrder",
            DiscriminatorPropertyPath: "Status",
            DiscriminatorTypeName: "OrderStatus",
            IsEnumDiscriminator: true,
            IsMethodDiscriminator: false,
            Cases: cases,
            RejoinStepName: "Complete");

        // Assert
        await Assert.That(model.BranchId).IsEqualTo("ProcessOrder-OrderStatus");
        await Assert.That(model.PreviousStepName).IsEqualTo("ValidateOrder");
        await Assert.That(model.DiscriminatorPropertyPath).IsEqualTo("Status");
        await Assert.That(model.DiscriminatorTypeName).IsEqualTo("OrderStatus");
        await Assert.That(model.IsEnumDiscriminator).IsTrue();
        await Assert.That(model.Cases).HasCount().EqualTo(2);
        await Assert.That(model.RejoinStepName).IsEqualTo("Complete");
    }

    /// <summary>
    /// Verifies that BranchModel supports string discriminator type.
    /// </summary>
    [Test]
    public async Task BranchModel_Constructor_WithStringDiscriminator_CreatesModel()
    {
        // Arrange
        var cases = new List<BranchCaseModel>
        {
            new("\"premium\"", "Premium", ["ApplyPremiumDiscount"], false),
            new("\"standard\"", "Standard", ["ApplyStandardRate"], false),
        };

        // Act
        var model = new BranchModel(
            BranchId: "ProcessOrder-CustomerType",
            PreviousStepName: "IdentifyCustomer",
            DiscriminatorPropertyPath: "CustomerType",
            DiscriminatorTypeName: "string",
            IsEnumDiscriminator: false,
            IsMethodDiscriminator: false,
            Cases: cases,
            RejoinStepName: "Checkout");

        // Assert
        await Assert.That(model.DiscriminatorTypeName).IsEqualTo("string");
        await Assert.That(model.IsEnumDiscriminator).IsFalse();
    }

    /// <summary>
    /// Verifies that BranchModel supports nested property path discriminator.
    /// </summary>
    [Test]
    public async Task BranchModel_Constructor_WithNestedPropertyPath_CreatesModel()
    {
        // Arrange
        var cases = new List<BranchCaseModel>
        {
            new("ShippingMethod.Express", "Express", ["ProcessExpress"], false),
            new("ShippingMethod.Standard", "Standard", ["ProcessStandard"], false),
        };

        // Act
        var model = new BranchModel(
            BranchId: "ProcessOrder-ShippingMethod",
            PreviousStepName: "SelectShipping",
            DiscriminatorPropertyPath: "Order.ShippingMethod",
            DiscriminatorTypeName: "ShippingMethod",
            IsEnumDiscriminator: true,
            IsMethodDiscriminator: false,
            Cases: cases,
            RejoinStepName: "Ship");

        // Assert
        await Assert.That(model.DiscriminatorPropertyPath).IsEqualTo("Order.ShippingMethod");
    }

    /// <summary>
    /// Verifies that BranchModel with null RejoinStepName indicates no convergence.
    /// </summary>
    [Test]
    public async Task BranchModel_Constructor_WithNullRejoinStep_IsTerminalBranch()
    {
        // Arrange
        var cases = new List<BranchCaseModel>
        {
            new("true", "Enabled", ["ProcessEnabled"], true),
            new("false", "Disabled", ["Skip"], true),
        };

        // Act
        var model = new BranchModel(
            BranchId: "ProcessOrder-IsEnabled",
            PreviousStepName: "CheckEnabled",
            DiscriminatorPropertyPath: "IsEnabled",
            DiscriminatorTypeName: "bool",
            IsEnumDiscriminator: false,
            IsMethodDiscriminator: false,
            Cases: cases,
            RejoinStepName: null);

        // Assert
        await Assert.That(model.RejoinStepName).IsNull();
    }

    // =============================================================================
    // D. BranchModel Computed Properties Tests
    // =============================================================================

    /// <summary>
    /// Verifies that BranchHandlerMethodName is derived correctly from property path.
    /// </summary>
    [Test]
    public async Task BranchModel_BranchHandlerMethodName_ReturnsCorrectName()
    {
        // Arrange
        var model = new BranchModel(
            BranchId: "ProcessOrder-OrderStatus",
            PreviousStepName: "ValidateOrder",
            DiscriminatorPropertyPath: "Status",
            DiscriminatorTypeName: "OrderStatus",
            IsEnumDiscriminator: true,
            IsMethodDiscriminator: false,
            Cases: [],
            RejoinStepName: "Complete");

        // Act & Assert
        await Assert.That(model.BranchHandlerMethodName).IsEqualTo("RouteByStatus");
    }

    /// <summary>
    /// Verifies that BranchHandlerMethodName for nested path removes dots.
    /// </summary>
    [Test]
    public async Task BranchModel_BranchHandlerMethodName_NestedPath_RemovesDots()
    {
        // Arrange
        var model = new BranchModel(
            BranchId: "ProcessOrder-OrderShippingMethod",
            PreviousStepName: "SelectShipping",
            DiscriminatorPropertyPath: "Order.ShippingMethod",
            DiscriminatorTypeName: "ShippingMethod",
            IsEnumDiscriminator: true,
            IsMethodDiscriminator: false,
            Cases: [],
            RejoinStepName: "Ship");

        // Act & Assert
        await Assert.That(model.BranchHandlerMethodName).IsEqualTo("RouteByOrderShippingMethod");
    }

    /// <summary>
    /// Verifies that HasRejoinPoint returns true when RejoinStepName is set.
    /// </summary>
    [Test]
    public async Task BranchModel_HasRejoinPoint_WhenRejoinStepSet_ReturnsTrue()
    {
        // Arrange
        var model = new BranchModel(
            BranchId: "ProcessOrder-OrderStatus",
            PreviousStepName: "ValidateOrder",
            DiscriminatorPropertyPath: "Status",
            DiscriminatorTypeName: "OrderStatus",
            IsEnumDiscriminator: true,
            IsMethodDiscriminator: false,
            Cases: [],
            RejoinStepName: "Complete");

        // Act & Assert
        await Assert.That(model.HasRejoinPoint).IsTrue();
    }

    /// <summary>
    /// Verifies that HasRejoinPoint returns false when RejoinStepName is null.
    /// </summary>
    [Test]
    public async Task BranchModel_HasRejoinPoint_WhenRejoinStepNull_ReturnsFalse()
    {
        // Arrange
        var model = new BranchModel(
            BranchId: "ProcessOrder-IsEnabled",
            PreviousStepName: "CheckEnabled",
            DiscriminatorPropertyPath: "IsEnabled",
            DiscriminatorTypeName: "bool",
            IsEnumDiscriminator: false,
            IsMethodDiscriminator: false,
            Cases: [],
            RejoinStepName: null);

        // Act & Assert
        await Assert.That(model.HasRejoinPoint).IsFalse();
    }

    /// <summary>
    /// Verifies that AllCasesTerminal returns true when all cases are terminal.
    /// </summary>
    [Test]
    public async Task BranchModel_AllCasesTerminal_WhenAllTerminal_ReturnsTrue()
    {
        // Arrange
        var cases = new List<BranchCaseModel>
        {
            new("OrderStatus.Approved", "Approved", ["ProcessPayment"], true),
            new("OrderStatus.Rejected", "Rejected", ["NotifyRejection"], true),
        };

        var model = new BranchModel(
            BranchId: "ProcessOrder-OrderStatus",
            PreviousStepName: "ValidateOrder",
            DiscriminatorPropertyPath: "Status",
            DiscriminatorTypeName: "OrderStatus",
            IsEnumDiscriminator: true,
            IsMethodDiscriminator: false,
            Cases: cases,
            RejoinStepName: null);

        // Act & Assert
        await Assert.That(model.AllCasesTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that AllCasesTerminal returns false when any case is non-terminal.
    /// </summary>
    [Test]
    public async Task BranchModel_AllCasesTerminal_WhenSomeNonTerminal_ReturnsFalse()
    {
        // Arrange
        var cases = new List<BranchCaseModel>
        {
            new("OrderStatus.Approved", "Approved", ["ProcessPayment"], false),
            new("OrderStatus.Rejected", "Rejected", ["NotifyRejection"], true),
        };

        var model = new BranchModel(
            BranchId: "ProcessOrder-OrderStatus",
            PreviousStepName: "ValidateOrder",
            DiscriminatorPropertyPath: "Status",
            DiscriminatorTypeName: "OrderStatus",
            IsEnumDiscriminator: true,
            IsMethodDiscriminator: false,
            Cases: cases,
            RejoinStepName: "Complete");

        // Act & Assert
        await Assert.That(model.AllCasesTerminal).IsFalse();
    }

    // =============================================================================
    // E. Record Equality Tests
    // =============================================================================

    /// <summary>
    /// Verifies that BranchCaseModel has value equality for primitive properties.
    /// </summary>
    /// <remarks>
    /// Records compare collection properties by reference, not content.
    /// This test verifies equality when sharing the same collection instance.
    /// </remarks>
    [Test]
    public async Task BranchCaseModel_IsValueEqual()
    {
        // Arrange - use same list instance for value equality
        IReadOnlyList<string> sharedSteps = ["ProcessPayment"];

        var model1 = new BranchCaseModel(
            CaseValueLiteral: "OrderStatus.Approved",
            BranchPathPrefix: "Approved",
            StepNames: sharedSteps,
            IsTerminal: false);

        var model2 = new BranchCaseModel(
            CaseValueLiteral: "OrderStatus.Approved",
            BranchPathPrefix: "Approved",
            StepNames: sharedSteps,
            IsTerminal: false);

        // Assert
        await Assert.That(model1).IsEqualTo(model2);
    }

    /// <summary>
    /// Verifies that BranchModel is a record with value equality.
    /// </summary>
    [Test]
    public async Task BranchModel_IsValueEqual()
    {
        // Arrange
        var cases = new List<BranchCaseModel>
        {
            new("OrderStatus.Approved", "Approved", ["ProcessPayment"], false),
        };

        var model1 = new BranchModel(
            BranchId: "ProcessOrder-OrderStatus",
            PreviousStepName: "ValidateOrder",
            DiscriminatorPropertyPath: "Status",
            DiscriminatorTypeName: "OrderStatus",
            IsEnumDiscriminator: true,
            IsMethodDiscriminator: false,
            Cases: cases,
            RejoinStepName: "Complete");

        var model2 = new BranchModel(
            BranchId: "ProcessOrder-OrderStatus",
            PreviousStepName: "ValidateOrder",
            DiscriminatorPropertyPath: "Status",
            DiscriminatorTypeName: "OrderStatus",
            IsEnumDiscriminator: true,
            IsMethodDiscriminator: false,
            Cases: cases,
            RejoinStepName: "Complete");

        // Assert
        await Assert.That(model1).IsEqualTo(model2);
    }

    /// <summary>
    /// Verifies that BranchModels with different values are not equal.
    /// </summary>
    [Test]
    public async Task BranchModel_DifferentValues_AreNotEqual()
    {
        // Arrange
        var model1 = new BranchModel(
            BranchId: "ProcessOrder-OrderStatus",
            PreviousStepName: "ValidateOrder",
            DiscriminatorPropertyPath: "Status",
            DiscriminatorTypeName: "OrderStatus",
            IsEnumDiscriminator: true,
            IsMethodDiscriminator: false,
            Cases: [],
            RejoinStepName: "Complete");

        var model2 = new BranchModel(
            BranchId: "ProcessOrder-ShippingMethod",
            PreviousStepName: "SelectShipping",
            DiscriminatorPropertyPath: "Method",
            DiscriminatorTypeName: "ShippingMethod",
            IsEnumDiscriminator: true,
            IsMethodDiscriminator: false,
            Cases: [],
            RejoinStepName: "Ship");

        // Assert
        await Assert.That(model1).IsNotEqualTo(model2);
    }
}
