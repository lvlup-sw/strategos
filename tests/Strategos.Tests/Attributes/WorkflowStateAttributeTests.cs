// -----------------------------------------------------------------------
// <copyright file="WorkflowStateAttributeTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Attributes;

namespace Strategos.Tests.Attributes;

/// <summary>
/// Unit tests for <see cref="WorkflowStateAttribute"/>.
/// </summary>
[Property("Category", "Unit")]
public class WorkflowStateAttributeTests
{
    // =============================================================================
    // A. Construction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the attribute can be created with default constructor.
    /// </summary>
    [Test]
    public async Task WorkflowStateAttribute_CanBeCreatedWithDefaultConstructor()
    {
        // Arrange & Act
        var attribute = new WorkflowStateAttribute();

        // Assert
        await Assert.That(attribute).IsNotNull();
    }

    // =============================================================================
    // B. AttributeUsage Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the attribute can be applied to a class.
    /// </summary>
    [Test]
    public async Task WorkflowStateAttribute_CanApplyToClass()
    {
        // Arrange
        var attributeType = typeof(WorkflowStateAttribute);
        var usageAttribute = attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        await Assert.That(usageAttribute).IsNotNull();
        await Assert.That((usageAttribute!.ValidOn & AttributeTargets.Class) != 0).IsTrue();
    }

    /// <summary>
    /// Verifies that the attribute can be applied to a struct.
    /// </summary>
    [Test]
    public async Task WorkflowStateAttribute_CanApplyToStruct()
    {
        // Arrange
        var attributeType = typeof(WorkflowStateAttribute);
        var usageAttribute = attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        await Assert.That(usageAttribute).IsNotNull();
        await Assert.That((usageAttribute!.ValidOn & AttributeTargets.Struct) != 0).IsTrue();
    }

    /// <summary>
    /// Verifies that the attribute is not inheritable.
    /// </summary>
    [Test]
    public async Task WorkflowStateAttribute_IsNotInherited()
    {
        // Arrange
        var attributeType = typeof(WorkflowStateAttribute);
        var usageAttribute = attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        await Assert.That(usageAttribute).IsNotNull();
        await Assert.That(usageAttribute!.Inherited).IsFalse();
    }

    /// <summary>
    /// Verifies that only one attribute can be applied per type.
    /// </summary>
    [Test]
    public async Task WorkflowStateAttribute_DoesNotAllowMultiple()
    {
        // Arrange
        var attributeType = typeof(WorkflowStateAttribute);
        var usageAttribute = attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        await Assert.That(usageAttribute).IsNotNull();
        await Assert.That(usageAttribute!.AllowMultiple).IsFalse();
    }
}
