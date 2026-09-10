// =============================================================================
// <copyright file="RagConfigurationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Configuration;

namespace Strategos.Agents.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="RagConfiguration"/> covering defaults and property setters.
/// </summary>
[Property("Category", "Unit")]
public class RagConfigurationTests
{
    /// <summary>
    /// Verifies that RagConfiguration has correct default TopK value.
    /// </summary>
    [Test]
    public async Task TopK_DefaultValue_IsFive()
    {
        // Act
        var config = new RagConfiguration();

        // Assert
        await Assert.That(config.TopK).IsEqualTo(5);
    }

    /// <summary>
    /// Verifies that RagConfiguration has correct default MinRelevance value.
    /// </summary>
    [Test]
    public async Task MinRelevance_DefaultValue_IsSeventyPercent()
    {
        // Act
        var config = new RagConfiguration();

        // Assert
        await Assert.That(config.MinRelevance).IsEqualTo(0.7);
    }

    /// <summary>
    /// Verifies that RagConfiguration has correct default IncludeMetadata value.
    /// </summary>
    [Test]
    public async Task IncludeMetadata_DefaultValue_IsFalse()
    {
        // Act
        var config = new RagConfiguration();

        // Assert
        await Assert.That(config.IncludeMetadata).IsFalse();
    }

    /// <summary>
    /// Verifies that RagConfiguration has correct default ResultFormat value.
    /// </summary>
    [Test]
    public async Task ResultFormat_DefaultValue_IsContentPlaceholder()
    {
        // Act
        var config = new RagConfiguration();

        // Assert
        await Assert.That(config.ResultFormat).IsEqualTo("{Content}");
    }

    /// <summary>
    /// Verifies that RagConfiguration has correct default SectionHeader value.
    /// </summary>
    [Test]
    public async Task SectionHeader_DefaultValue_IsCorrect()
    {
        // Act
        var config = new RagConfiguration();

        // Assert
        await Assert.That(config.SectionHeader).IsEqualTo("### Relevant Background Information");
    }

    /// <summary>
    /// Verifies that TopK can be set to a custom value.
    /// </summary>
    [Test]
    public async Task TopK_SetCustomValue_ReturnsCustomValue()
    {
        // Arrange
        var config = new RagConfiguration();

        // Act
        config.TopK = 10;

        // Assert
        await Assert.That(config.TopK).IsEqualTo(10);
    }

    /// <summary>
    /// Verifies that MinRelevance can be set to a custom value.
    /// </summary>
    [Test]
    public async Task MinRelevance_SetCustomValue_ReturnsCustomValue()
    {
        // Arrange
        var config = new RagConfiguration();

        // Act
        config.MinRelevance = 0.5;

        // Assert
        await Assert.That(config.MinRelevance).IsEqualTo(0.5);
    }

    /// <summary>
    /// Verifies that IncludeMetadata can be enabled.
    /// </summary>
    [Test]
    public async Task IncludeMetadata_SetTrue_ReturnsTrue()
    {
        // Arrange
        var config = new RagConfiguration();

        // Act
        config.IncludeMetadata = true;

        // Assert
        await Assert.That(config.IncludeMetadata).IsTrue();
    }

    /// <summary>
    /// Verifies that ResultFormat can be set to a custom template.
    /// </summary>
    [Test]
    public async Task ResultFormat_SetCustomTemplate_ReturnsCustomTemplate()
    {
        // Arrange
        var config = new RagConfiguration();
        const string customFormat = "Score: {Score} - {Content}";

        // Act
        config.ResultFormat = customFormat;

        // Assert
        await Assert.That(config.ResultFormat).IsEqualTo(customFormat);
    }

    /// <summary>
    /// Verifies that SectionHeader can be set to a custom value.
    /// </summary>
    [Test]
    public async Task SectionHeader_SetCustomValue_ReturnsCustomValue()
    {
        // Arrange
        var config = new RagConfiguration();
        const string customHeader = "## Context";

        // Act
        config.SectionHeader = customHeader;

        // Assert
        await Assert.That(config.SectionHeader).IsEqualTo(customHeader);
    }
}
