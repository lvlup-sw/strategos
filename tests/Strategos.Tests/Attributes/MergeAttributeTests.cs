// -----------------------------------------------------------------------
// <copyright file="MergeAttributeTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Attributes;

namespace Strategos.Tests.Attributes;

/// <summary>
/// Unit tests for <see cref="MergeAttribute"/>.
/// </summary>
[Property("Category", "Unit")]
public class MergeAttributeTests
{
    // =============================================================================
    // A. Construction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the attribute can be created with default constructor.
    /// </summary>
    [Test]
    public async Task MergeAttribute_CanBeCreatedWithDefaultConstructor()
    {
        // Arrange & Act
        var attribute = new MergeAttribute();

        // Assert
        await Assert.That(attribute).IsNotNull();
    }

    // =============================================================================
    // B. AttributeUsage Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the attribute can only be applied to properties.
    /// </summary>
    [Test]
    public async Task MergeAttribute_CanApplyToProperty()
    {
        // Arrange
        var attributeType = typeof(MergeAttribute);
        var usageAttribute = attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        await Assert.That(usageAttribute).IsNotNull();
        await Assert.That((usageAttribute!.ValidOn & AttributeTargets.Property) != 0).IsTrue();
    }

    /// <summary>
    /// Verifies that the attribute cannot be applied to classes.
    /// </summary>
    [Test]
    public async Task MergeAttribute_CannotApplyToClass()
    {
        // Arrange
        var attributeType = typeof(MergeAttribute);
        var usageAttribute = attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        await Assert.That(usageAttribute).IsNotNull();
        await Assert.That((usageAttribute!.ValidOn & AttributeTargets.Class) == 0).IsTrue();
    }

    /// <summary>
    /// Verifies that the attribute is not inheritable.
    /// </summary>
    [Test]
    public async Task MergeAttribute_IsNotInherited()
    {
        // Arrange
        var attributeType = typeof(MergeAttribute);
        var usageAttribute = attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        await Assert.That(usageAttribute).IsNotNull();
        await Assert.That(usageAttribute!.Inherited).IsFalse();
    }

    /// <summary>
    /// Verifies that only one attribute can be applied per property.
    /// </summary>
    [Test]
    public async Task MergeAttribute_DoesNotAllowMultiple()
    {
        // Arrange
        var attributeType = typeof(MergeAttribute);
        var usageAttribute = attributeType
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        await Assert.That(usageAttribute).IsNotNull();
        await Assert.That(usageAttribute!.AllowMultiple).IsFalse();
    }
}
