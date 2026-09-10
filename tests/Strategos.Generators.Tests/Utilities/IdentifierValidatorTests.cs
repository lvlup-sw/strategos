// -----------------------------------------------------------------------
// <copyright file="IdentifierValidatorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Utilities;

namespace Strategos.Generators.Tests.Utilities;

/// <summary>
/// Unit tests for the <see cref="IdentifierValidator"/> class.
/// </summary>
[Property("Category", "Unit")]
public class IdentifierValidatorTests
{
    // =============================================================================
    // A. IsValidIdentifier Tests
    // =============================================================================

    /// <summary>
    /// Verifies that IsValidIdentifier returns true for valid identifiers.
    /// </summary>
    [Test]
    [Arguments("MyClass")]
    [Arguments("_privateField")]
    [Arguments("ValidName123")]
    [Arguments("_")]
    public async Task IsValidIdentifier_ValidIdentifiers_ReturnsTrue(string identifier)
    {
        // Act
        var result = IdentifierValidator.IsValidIdentifier(identifier);

        // Assert
        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Verifies that IsValidIdentifier returns false for invalid identifiers.
    /// </summary>
    [Test]
    [Arguments("123Invalid")]
    [Arguments("my-class")]
    [Arguments("has space")]
    [Arguments("")]
    public async Task IsValidIdentifier_InvalidIdentifiers_ReturnsFalse(string identifier)
    {
        // Act
        var result = IdentifierValidator.IsValidIdentifier(identifier);

        // Assert
        await Assert.That(result).IsFalse();
    }

    /// <summary>
    /// Verifies that IsValidIdentifier returns false for null.
    /// </summary>
    [Test]
    public async Task IsValidIdentifier_Null_ReturnsFalse()
    {
        // Act
        var result = IdentifierValidator.IsValidIdentifier(null!);

        // Assert
        await Assert.That(result).IsFalse();
    }

    // =============================================================================
    // B. ValidateIdentifier Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ValidateIdentifier does not throw for valid identifiers.
    /// </summary>
    [Test]
    public async Task ValidateIdentifier_ValidIdentifier_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await Assert.That(() => IdentifierValidator.ValidateIdentifier("ValidName", "paramName"))
            .ThrowsNothing();
    }

    /// <summary>
    /// Verifies that ValidateIdentifier throws ArgumentNullException for null.
    /// </summary>
    [Test]
    public async Task ValidateIdentifier_Null_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => IdentifierValidator.ValidateIdentifier(null!, "paramName"))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ValidateIdentifier throws ArgumentException for invalid identifier.
    /// </summary>
    [Test]
    public async Task ValidateIdentifier_InvalidIdentifier_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => IdentifierValidator.ValidateIdentifier("123Invalid", "paramName"))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that ValidateIdentifier includes identifier in exception message.
    /// </summary>
    [Test]
    public async Task ValidateIdentifier_InvalidIdentifier_ExceptionIncludesIdentifier()
    {
        // Act
        ArgumentException? exception = null;
        try
        {
            IdentifierValidator.ValidateIdentifier("123Invalid", "paramName");
        }
        catch (ArgumentException ex)
        {
            exception = ex;
        }

        // Assert
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("123Invalid");
    }

    // =============================================================================
    // C. ValidatePropertyPath Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ValidatePropertyPath does not throw for valid paths.
    /// </summary>
    [Test]
    [Arguments("PropertyName")]
    [Arguments("State.Value")]
    [Arguments("Nested.Deep.Path")]
    public async Task ValidatePropertyPath_ValidPath_DoesNotThrow(string path)
    {
        // Act & Assert - Should not throw
        await Assert.That(() => IdentifierValidator.ValidatePropertyPath(path, "paramName"))
            .ThrowsNothing();
    }

    /// <summary>
    /// Verifies that ValidatePropertyPath throws ArgumentNullException for null.
    /// </summary>
    [Test]
    public async Task ValidatePropertyPath_Null_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => IdentifierValidator.ValidatePropertyPath(null!, "paramName"))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ValidatePropertyPath throws for empty string.
    /// </summary>
    [Test]
    public async Task ValidatePropertyPath_Empty_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => IdentifierValidator.ValidatePropertyPath("", "paramName"))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that ValidatePropertyPath throws for whitespace string.
    /// </summary>
    [Test]
    public async Task ValidatePropertyPath_Whitespace_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => IdentifierValidator.ValidatePropertyPath("   ", "paramName"))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that ValidatePropertyPath throws for path with invalid segment.
    /// </summary>
    [Test]
    public async Task ValidatePropertyPath_InvalidSegment_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => IdentifierValidator.ValidatePropertyPath("Valid.123Invalid.Path", "paramName"))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that ValidatePropertyPath exception includes invalid segment.
    /// </summary>
    [Test]
    public async Task ValidatePropertyPath_InvalidSegment_ExceptionIncludesSegment()
    {
        // Act
        ArgumentException? exception = null;
        try
        {
            IdentifierValidator.ValidatePropertyPath("Valid.123Invalid.Path", "paramName");
        }
        catch (ArgumentException ex)
        {
            exception = ex;
        }

        // Assert
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("123Invalid");
    }
}
