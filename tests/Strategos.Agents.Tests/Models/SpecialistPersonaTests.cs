// =============================================================================
// <copyright file="SpecialistPersonaTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Tests.Models;

/// <summary>
/// Unit tests for the <see cref="SpecialistPersona"/> record.
/// </summary>
[Property("Category", "Unit")]
public class SpecialistPersonaTests
{
    // =============================================================================
    // A. Construction Tests (4 tests)
    // =============================================================================

    [Test]
    public async Task Constructor_WithValidParameters_CreatesPersona()
    {
        // Arrange & Act
        var persona = new SpecialistPersona(
            SpecialistType.Coder,
            "Test system prompt",
            0.85);

        // Assert
        await Assert.That(persona.Type).IsEqualTo(SpecialistType.Coder);
        await Assert.That(persona.SystemPrompt).IsEqualTo("Test system prompt");
        await Assert.That(persona.SuccessConfidence).IsEqualTo(0.85);
    }

    [Test]
    public async Task Constructor_WithDefaultConfidence_Uses0Point9()
    {
        // Arrange & Act
        var persona = new SpecialistPersona(
            SpecialistType.Analyst,
            "Analysis prompt");

        // Assert
        await Assert.That(persona.SuccessConfidence).IsEqualTo(0.9);
    }

    [Test]
    public async Task Constructor_WithMinConfidence_Succeeds()
    {
        // Arrange & Act
        var persona = new SpecialistPersona(
            SpecialistType.Coder,
            "Test prompt",
            0.0);

        // Assert
        await Assert.That(persona.SuccessConfidence).IsEqualTo(0.0);
    }

    [Test]
    public async Task Constructor_WithMaxConfidence_Succeeds()
    {
        // Arrange & Act
        var persona = new SpecialistPersona(
            SpecialistType.Coder,
            "Test prompt",
            1.0);

        // Assert
        await Assert.That(persona.SuccessConfidence).IsEqualTo(1.0);
    }

    // =============================================================================
    // B. Validation Tests (3 tests)
    // =============================================================================

    [Test]
    public async Task Validate_WithValidPersona_DoesNotThrow()
    {
        // Arrange
        var persona = new SpecialistPersona(
            SpecialistType.Coder,
            "Valid prompt",
            0.9);

        // Act & Assert
        await Assert.That(() => persona.Validate()).ThrowsNothing();
    }

    [Test]
    public async Task Validate_WithNullSystemPrompt_ThrowsArgumentException()
    {
        // Arrange
        var persona = new SpecialistPersona(
            SpecialistType.Coder,
            null!,
            0.9);

        // Act & Assert
        await Assert.That(() => persona.Validate())
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Validate_WithEmptySystemPrompt_ThrowsArgumentException()
    {
        // Arrange
        var persona = new SpecialistPersona(
            SpecialistType.Coder,
            string.Empty,
            0.9);

        // Act & Assert
        await Assert.That(() => persona.Validate())
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Validate_WithConfidenceBelowZero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var persona = new SpecialistPersona(
            SpecialistType.Coder,
            "Valid prompt",
            -0.1);

        // Act & Assert
        await Assert.That(() => persona.Validate())
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validate_WithConfidenceAboveOne_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var persona = new SpecialistPersona(
            SpecialistType.Coder,
            "Valid prompt",
            1.1);

        // Act & Assert
        await Assert.That(() => persona.Validate())
            .Throws<ArgumentOutOfRangeException>();
    }

    // =============================================================================
    // C. Predefined Persona Tests (4 tests)
    // =============================================================================

    [Test]
    public async Task Coder_ReturnsValidPersona()
    {
        // Arrange & Act
        var persona = SpecialistPersona.Coder;

        // Assert
        await Assert.That(persona.Type).IsEqualTo(SpecialistType.Coder);
        await Assert.That(persona.SystemPrompt).IsNotNull().And.IsNotEmpty();
        await Assert.That(persona.SuccessConfidence).IsEqualTo(0.9);
        await Assert.That(() => persona.Validate()).ThrowsNothing();
    }

    [Test]
    public async Task Analyst_ReturnsValidPersona()
    {
        // Arrange & Act
        var persona = SpecialistPersona.Analyst;

        // Assert
        await Assert.That(persona.Type).IsEqualTo(SpecialistType.Analyst);
        await Assert.That(persona.SystemPrompt).IsNotNull().And.IsNotEmpty();
        await Assert.That(persona.SuccessConfidence).IsEqualTo(0.9);
        await Assert.That(() => persona.Validate()).ThrowsNothing();
    }

    [Test]
    public async Task Coder_SystemPromptContainsPythonInstructions()
    {
        // Arrange & Act
        var persona = SpecialistPersona.Coder;

        // Assert
        await Assert.That(persona.SystemPrompt).Contains("Python");
        await Assert.That(persona.SystemPrompt).Contains("print");
    }

    [Test]
    public async Task Analyst_SystemPromptContainsDataAnalysisInstructions()
    {
        // Arrange & Act
        var persona = SpecialistPersona.Analyst;

        // Assert
        await Assert.That(persona.SystemPrompt).Contains("data analyst");
        await Assert.That(persona.SystemPrompt).Contains("pandas");
        await Assert.That(persona.SystemPrompt).Contains("numpy");
    }

    // =============================================================================
    // D. Record Equality Tests (2 tests)
    // =============================================================================

    [Test]
    public async Task Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var persona1 = new SpecialistPersona(
            SpecialistType.Coder,
            "Test prompt",
            0.9);
        var persona2 = new SpecialistPersona(
            SpecialistType.Coder,
            "Test prompt",
            0.9);

        // Act & Assert
        await Assert.That(persona1).IsEqualTo(persona2);
    }

    [Test]
    public async Task Equals_WithDifferentPrompt_ReturnsFalse()
    {
        // Arrange
        var persona1 = new SpecialistPersona(
            SpecialistType.Coder,
            "Test prompt 1",
            0.9);
        var persona2 = new SpecialistPersona(
            SpecialistType.Coder,
            "Test prompt 2",
            0.9);

        // Act & Assert
        await Assert.That(persona1).IsNotEqualTo(persona2);
    }
}
