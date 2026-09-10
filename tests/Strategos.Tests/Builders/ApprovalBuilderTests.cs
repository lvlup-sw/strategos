// =============================================================================
// <copyright file="ApprovalBuilderTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="ApprovalBuilder{TState, TApprover}"/> fluent builder.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>WithContext sets static context message</description></item>
///   <item><description>WithContextFrom sets dynamic context factory</description></item>
///   <item><description>WithTimeout sets approval timeout</description></item>
///   <item><description>WithOption adds approval options</description></item>
///   <item><description>WithMetadata adds static metadata</description></item>
///   <item><description>WithMetadataFrom adds dynamic metadata</description></item>
///   <item><description>OnTimeout configures escalation handler</description></item>
///   <item><description>OnRejection configures rejection handler</description></item>
///   <item><description>Build creates ApprovalDefinition</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class ApprovalBuilderTests
{
    // =============================================================================
    // Test Marker Types
    // =============================================================================

    /// <summary>
    /// Marker type for test approver role.
    /// </summary>
    private sealed class TestApprover;

    /// <summary>
    /// Marker type for supervisor approver role.
    /// </summary>
    private sealed class SupervisorApprover;

    // =============================================================================
    // A. WithContext Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithContext returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task WithContext_ReturnsBuilder()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        var result = builder.WithContext("Review this request");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IApprovalBuilder<TestWorkflowState, TestApprover>>();
    }

    /// <summary>
    /// Verifies that WithContext sets the static context message.
    /// </summary>
    [Test]
    public async Task WithContext_SetsStaticContext()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        builder.WithContext("Review this request");
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.Configuration.StaticContext).IsEqualTo("Review this request");
    }

    /// <summary>
    /// Verifies that WithContext with null throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task WithContext_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act & Assert
        await Assert.That(() => builder.WithContext(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. WithContextFrom Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithContextFrom returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task WithContextFrom_ReturnsBuilder()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        var result = builder.WithContextFrom(state => $"Order {state.OrderId}");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IApprovalBuilder<TestWorkflowState, TestApprover>>();
    }

    /// <summary>
    /// Verifies that WithContextFrom sets the context factory expression.
    /// </summary>
    [Test]
    public async Task WithContextFrom_SetsContextFactoryExpression()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        builder.WithContextFrom(state => $"Order {state.OrderId}");
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.Configuration.ContextFactoryExpression).IsNotNull();
    }

    /// <summary>
    /// Verifies that WithContextFrom with null factory throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task WithContextFrom_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act & Assert
        await Assert.That(() => builder.WithContextFrom(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // C. WithTimeout Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithTimeout returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task WithTimeout_ReturnsBuilder()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        var result = builder.WithTimeout(TimeSpan.FromHours(2));

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IApprovalBuilder<TestWorkflowState, TestApprover>>();
    }

    /// <summary>
    /// Verifies that WithTimeout sets the timeout value.
    /// </summary>
    [Test]
    public async Task WithTimeout_SetsTimeout()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");
        var timeout = TimeSpan.FromMinutes(30);

        // Act
        builder.WithTimeout(timeout);
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.Configuration.Timeout).IsEqualTo(timeout);
    }

    /// <summary>
    /// Verifies that default timeout is 24 hours.
    /// </summary>
    [Test]
    public async Task Build_ByDefault_HasDefaultTimeout()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.Configuration.Timeout).IsEqualTo(TimeSpan.FromHours(24));
    }

    // =============================================================================
    // D. WithOption Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithOption returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task WithOption_ReturnsBuilder()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        var result = builder.WithOption("approve", "Approve", "Approve this request");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IApprovalBuilder<TestWorkflowState, TestApprover>>();
    }

    /// <summary>
    /// Verifies that WithOption adds option to the configuration.
    /// </summary>
    [Test]
    public async Task WithOption_AddsOptionToConfiguration()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        builder.WithOption("approve", "Approve", "Approve the request");
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.Configuration.Options).HasCount().EqualTo(1);
        await Assert.That(definition.Configuration.Options[0].OptionId).IsEqualTo("approve");
        await Assert.That(definition.Configuration.Options[0].Label).IsEqualTo("Approve");
        await Assert.That(definition.Configuration.Options[0].Description).IsEqualTo("Approve the request");
    }

    /// <summary>
    /// Verifies that WithOption can add multiple options.
    /// </summary>
    [Test]
    public async Task WithOption_MultipleCalls_AddsMultipleOptions()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        builder
            .WithOption("approve", "Approve", "Approve the request")
            .WithOption("reject", "Reject", "Reject the request");
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.Configuration.Options).HasCount().EqualTo(2);
    }

    /// <summary>
    /// Verifies that WithOption with isDefault flag sets the default option.
    /// </summary>
    [Test]
    public async Task WithOption_WithIsDefaultTrue_SetsDefaultOption()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        builder.WithOption("approve", "Approve", "Approve the request", isDefault: true);
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.Configuration.Options[0].IsDefault).IsTrue();
    }

    // =============================================================================
    // E. WithMetadata Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithMetadata returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task WithMetadata_ReturnsBuilder()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        var result = builder.WithMetadata("priority", "high");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IApprovalBuilder<TestWorkflowState, TestApprover>>();
    }

    /// <summary>
    /// Verifies that WithMetadata adds static metadata.
    /// </summary>
    [Test]
    public async Task WithMetadata_AddsStaticMetadata()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        builder.WithMetadata("priority", "high");
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.Configuration.StaticMetadata.ContainsKey("priority")).IsTrue();
        await Assert.That(definition.Configuration.StaticMetadata["priority"]).IsEqualTo("high");
    }

    // =============================================================================
    // F. WithMetadataFrom Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithMetadataFrom returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task WithMetadataFrom_ReturnsBuilder()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        var result = builder.WithMetadataFrom("orderId", state => state.OrderId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IApprovalBuilder<TestWorkflowState, TestApprover>>();
    }

    /// <summary>
    /// Verifies that WithMetadataFrom adds dynamic metadata expression.
    /// </summary>
    [Test]
    public async Task WithMetadataFrom_AddsDynamicMetadataExpression()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        builder.WithMetadataFrom("orderId", state => state.OrderId);
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.Configuration.DynamicMetadataExpressions.ContainsKey("orderId")).IsTrue();
    }

    // =============================================================================
    // G. OnTimeout Tests
    // =============================================================================

    /// <summary>
    /// Verifies that OnTimeout returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task OnTimeout_ReturnsBuilder()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        var result = builder.OnTimeout(escalation => escalation.Complete());

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IApprovalBuilder<TestWorkflowState, TestApprover>>();
    }

    /// <summary>
    /// Verifies that OnTimeout configures escalation handler.
    /// </summary>
    [Test]
    public async Task OnTimeout_ConfiguresEscalationHandler()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        builder.OnTimeout(escalation => escalation.Complete());
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.EscalationHandler).IsNotNull();
    }

    /// <summary>
    /// Verifies that OnTimeout with null configure action throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task OnTimeout_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act & Assert
        await Assert.That(() => builder.OnTimeout(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // H. OnRejection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that OnRejection returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task OnRejection_ReturnsBuilder()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        var result = builder.OnRejection(rejection => rejection.Complete());

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IApprovalBuilder<TestWorkflowState, TestApprover>>();
    }

    /// <summary>
    /// Verifies that OnRejection configures rejection handler.
    /// </summary>
    [Test]
    public async Task OnRejection_ConfiguresRejectionHandler()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        builder.OnRejection(rejection => rejection.Complete());
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.RejectionHandler).IsNotNull();
    }

    /// <summary>
    /// Verifies that OnRejection with null configure action throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task OnRejection_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act & Assert
        await Assert.That(() => builder.OnRejection(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // I. Build Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Build creates ApprovalDefinition with correct approver type.
    /// </summary>
    [Test]
    public async Task Build_CreatesDefinitionWithApproverType()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.ApproverType).IsEqualTo(typeof(TestApprover));
    }

    /// <summary>
    /// Verifies that Build creates ApprovalDefinition with correct preceding step.
    /// </summary>
    [Test]
    public async Task Build_CreatesDefinitionWithPrecedingStep()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.PrecedingStepId).IsEqualTo("step-1");
    }

    /// <summary>
    /// Verifies that Build creates ApprovalDefinition with generated ID.
    /// </summary>
    [Test]
    public async Task Build_CreatesDefinitionWithGeneratedId()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        var definition = builder.Build();

        // Assert
        await Assert.That(definition.ApprovalPointId).IsNotNull();
        await Assert.That(definition.ApprovalPointId).IsNotEmpty();
    }

    /// <summary>
    /// Verifies that Build with full configuration creates complete definition.
    /// </summary>
    [Test]
    public async Task Build_WithFullConfiguration_CreatesCompleteDefinition()
    {
        // Arrange
        var builder = new ApprovalBuilder<TestWorkflowState, TestApprover>("step-1");

        // Act
        var definition = builder
            .WithContext("Review request")
            .WithTimeout(TimeSpan.FromHours(4))
            .WithOption("approve", "Approve", "Approve the request", isDefault: true)
            .WithOption("reject", "Reject", "Reject the request")
            .WithMetadata("category", "urgent")
            .OnTimeout(escalation => escalation.Complete())
            .OnRejection(rejection => rejection.Complete())
            .Build();

        // Assert
        await Assert.That(definition.ApproverType).IsEqualTo(typeof(TestApprover));
        await Assert.That(definition.Configuration.StaticContext).IsEqualTo("Review request");
        await Assert.That(definition.Configuration.Timeout).IsEqualTo(TimeSpan.FromHours(4));
        await Assert.That(definition.Configuration.Options).HasCount().EqualTo(2);
        await Assert.That(definition.Configuration.StaticMetadata.ContainsKey("category")).IsTrue();
        await Assert.That(definition.EscalationHandler).IsNotNull();
        await Assert.That(definition.RejectionHandler).IsNotNull();
    }
}
