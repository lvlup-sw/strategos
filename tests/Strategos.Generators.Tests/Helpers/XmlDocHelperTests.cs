// -----------------------------------------------------------------------
// <copyright file="XmlDocHelperTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Helpers;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for the <see cref="XmlDocHelper"/> class.
/// </summary>
[Property("Category", "Unit")]
public class XmlDocHelperTests
{
    // =============================================================================
    // A. AppendSummary Tests
    // =============================================================================

    /// <summary>
    /// Verifies that AppendSummary emits opening summary tag.
    /// </summary>
    [Test]
    public async Task AppendSummary_EmitsOpeningTag()
    {
        // Arrange
        var sb = new StringBuilder();

        // Act
        XmlDocHelper.AppendSummary(sb, "Test summary");

        // Assert
        await Assert.That(sb.ToString()).Contains("/// <summary>");
    }

    /// <summary>
    /// Verifies that AppendSummary emits the summary text.
    /// </summary>
    [Test]
    public async Task AppendSummary_EmitsSummaryText()
    {
        // Arrange
        var sb = new StringBuilder();

        // Act
        XmlDocHelper.AppendSummary(sb, "Test summary");

        // Assert
        await Assert.That(sb.ToString()).Contains("/// Test summary");
    }

    /// <summary>
    /// Verifies that AppendSummary emits closing summary tag.
    /// </summary>
    [Test]
    public async Task AppendSummary_EmitsClosingTag()
    {
        // Arrange
        var sb = new StringBuilder();

        // Act
        XmlDocHelper.AppendSummary(sb, "Test summary");

        // Assert
        await Assert.That(sb.ToString()).Contains("/// </summary>");
    }

    /// <summary>
    /// Verifies that AppendSummary applies indent.
    /// </summary>
    [Test]
    public async Task AppendSummary_WithIndent_AppliesIndent()
    {
        // Arrange
        var sb = new StringBuilder();

        // Act
        XmlDocHelper.AppendSummary(sb, "Test summary", "    ");
        var result = sb.ToString();

        // Assert
        await Assert.That(result).Contains("    /// <summary>");
        await Assert.That(result).Contains("    /// Test summary");
        await Assert.That(result).Contains("    /// </summary>");
    }

    // =============================================================================
    // B. AppendParam Tests
    // =============================================================================

    /// <summary>
    /// Verifies that AppendParam emits param tag with name attribute.
    /// </summary>
    [Test]
    public async Task AppendParam_EmitsParamTagWithName()
    {
        // Arrange
        var sb = new StringBuilder();

        // Act
        XmlDocHelper.AppendParam(sb, "input", "The input parameter");

        // Assert
        await Assert.That(sb.ToString()).Contains("/// <param name=\"input\">");
    }

    /// <summary>
    /// Verifies that AppendParam emits param description.
    /// </summary>
    [Test]
    public async Task AppendParam_EmitsDescription()
    {
        // Arrange
        var sb = new StringBuilder();

        // Act
        XmlDocHelper.AppendParam(sb, "input", "The input parameter");

        // Assert
        await Assert.That(sb.ToString()).Contains("The input parameter</param>");
    }

    /// <summary>
    /// Verifies that AppendParam applies indent.
    /// </summary>
    [Test]
    public async Task AppendParam_WithIndent_AppliesIndent()
    {
        // Arrange
        var sb = new StringBuilder();

        // Act
        XmlDocHelper.AppendParam(sb, "input", "The input parameter", "    ");

        // Assert
        await Assert.That(sb.ToString()).Contains("    /// <param name=\"input\">The input parameter</param>");
    }

    // =============================================================================
    // C. AppendReturns Tests
    // =============================================================================

    /// <summary>
    /// Verifies that AppendReturns emits returns tag.
    /// </summary>
    [Test]
    public async Task AppendReturns_EmitsReturnsTag()
    {
        // Arrange
        var sb = new StringBuilder();

        // Act
        XmlDocHelper.AppendReturns(sb, "The result");

        // Assert
        await Assert.That(sb.ToString()).Contains("/// <returns>The result</returns>");
    }

    /// <summary>
    /// Verifies that AppendReturns applies indent.
    /// </summary>
    [Test]
    public async Task AppendReturns_WithIndent_AppliesIndent()
    {
        // Arrange
        var sb = new StringBuilder();

        // Act
        XmlDocHelper.AppendReturns(sb, "The result", "    ");

        // Assert
        await Assert.That(sb.ToString()).Contains("    /// <returns>The result</returns>");
    }

    // =============================================================================
    // D. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that AppendSummary throws when StringBuilder is null.
    /// </summary>
    [Test]
    public async Task AppendSummary_NullStringBuilder_ThrowsArgumentNullException()
    {
        await Assert.That(() => XmlDocHelper.AppendSummary(null!, "Test"))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that AppendSummary throws when summary is null.
    /// </summary>
    [Test]
    public async Task AppendSummary_NullSummary_ThrowsArgumentNullException()
    {
        var sb = new StringBuilder();
        await Assert.That(() => XmlDocHelper.AppendSummary(sb, null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that AppendParam throws when StringBuilder is null.
    /// </summary>
    [Test]
    public async Task AppendParam_NullStringBuilder_ThrowsArgumentNullException()
    {
        await Assert.That(() => XmlDocHelper.AppendParam(null!, "name", "desc"))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that AppendParam throws when name is null.
    /// </summary>
    [Test]
    public async Task AppendParam_NullName_ThrowsArgumentNullException()
    {
        var sb = new StringBuilder();
        await Assert.That(() => XmlDocHelper.AppendParam(sb, null!, "desc"))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that AppendParam throws when description is null.
    /// </summary>
    [Test]
    public async Task AppendParam_NullDescription_ThrowsArgumentNullException()
    {
        var sb = new StringBuilder();
        await Assert.That(() => XmlDocHelper.AppendParam(sb, "name", null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that AppendReturns throws when StringBuilder is null.
    /// </summary>
    [Test]
    public async Task AppendReturns_NullStringBuilder_ThrowsArgumentNullException()
    {
        await Assert.That(() => XmlDocHelper.AppendReturns(null!, "desc"))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that AppendReturns throws when description is null.
    /// </summary>
    [Test]
    public async Task AppendReturns_NullDescription_ThrowsArgumentNullException()
    {
        var sb = new StringBuilder();
        await Assert.That(() => XmlDocHelper.AppendReturns(sb, null!))
            .Throws<ArgumentNullException>();
    }
}
