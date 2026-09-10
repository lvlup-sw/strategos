// -----------------------------------------------------------------------
// <copyright file="WorkflowAttributeTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Attributes;

namespace Strategos.Tests.Attributes;

/// <summary>
/// Unit tests for <see cref="WorkflowAttribute"/>.
/// </summary>
[Property("Category", "Unit")]
public class WorkflowAttributeTests
{
    // =============================================================================
    // A. Construction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the attribute can be created with a valid name.
    /// </summary>
    [Test]
    public async Task WorkflowAttribute_CanBeCreatedWithName()
    {
        // Arrange & Act
        var attribute = new WorkflowAttribute("test-workflow");

        // Assert
        await Assert.That(attribute).IsNotNull();
    }

    /// <summary>
    /// Verifies that the Name property returns the constructor value.
    /// </summary>
    [Test]
    public async Task WorkflowAttribute_NamePropertyReturnsConstructorValue()
    {
        // Arrange
        const string expectedName = "process-order";

        // Act
        var attribute = new WorkflowAttribute(expectedName);

        // Assert
        await Assert.That(attribute.Name).IsEqualTo(expectedName);
    }

    /// <summary>
    /// Verifies that the attribute can be applied to a class.
    /// </summary>
    [Test]
    public async Task WorkflowAttribute_CanApplyToClass()
    {
        // Arrange
        var attributeType = typeof(WorkflowAttribute);
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
    public async Task WorkflowAttribute_CanApplyToStruct()
    {
        // Arrange
        var attributeType = typeof(WorkflowAttribute);
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
    public async Task WorkflowAttribute_IsNotInherited()
    {
        // Arrange
        var attributeType = typeof(WorkflowAttribute);
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
    public async Task WorkflowAttribute_DoesNotAllowMultiple()
    {
        // Arrange
        var attributeType = typeof(WorkflowAttribute);
        var usageAttribute = attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        await Assert.That(usageAttribute).IsNotNull();
        await Assert.That(usageAttribute!.AllowMultiple).IsFalse();
    }
}
