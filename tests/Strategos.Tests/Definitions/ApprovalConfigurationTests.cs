// =============================================================================
// <copyright file="ApprovalConfigurationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Models;

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="ApprovalConfiguration"/>.
/// </summary>
[Property("Category", "Unit")]
public class ApprovalConfigurationTests
{
    // =============================================================================
    // A. Default Value Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Default returns a valid configuration.
    /// </summary>
    [Test]
    public async Task Default_ReturnsValidConfiguration()
    {
        // Act
        var config = ApprovalConfiguration.Default;

        // Assert
        await Assert.That(config).IsNotNull();
    }

    /// <summary>
    /// Verifies that Default type is GeneralApproval.
    /// </summary>
    [Test]
    public async Task Default_Type_IsGeneralApproval()
    {
        // Act
        var config = ApprovalConfiguration.Default;

        // Assert
        await Assert.That(config.Type).IsEqualTo(ApprovalType.GeneralApproval);
    }

    /// <summary>
    /// Verifies that Default timeout is 24 hours.
    /// </summary>
    [Test]
    public async Task Default_Timeout_Is24Hours()
    {
        // Act
        var config = ApprovalConfiguration.Default;

        // Assert
        await Assert.That(config.Timeout).IsEqualTo(TimeSpan.FromHours(24));
    }

    /// <summary>
    /// Verifies that Default has no static context.
    /// </summary>
    [Test]
    public async Task Default_StaticContext_IsNull()
    {
        // Act
        var config = ApprovalConfiguration.Default;

        // Assert
        await Assert.That(config.StaticContext).IsNull();
    }

    /// <summary>
    /// Verifies that Default has empty options.
    /// </summary>
    [Test]
    public async Task Default_Options_IsEmpty()
    {
        // Act
        var config = ApprovalConfiguration.Default;

        // Assert
        await Assert.That(config.Options).IsEmpty();
    }

    // =============================================================================
    // B. Property Initialization Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Type can be set.
    /// </summary>
    [Test]
    public async Task Type_CanBeSet()
    {
        // Act
        var config = new ApprovalConfiguration { Type = ApprovalType.SafetyCheck };

        // Assert
        await Assert.That(config.Type).IsEqualTo(ApprovalType.SafetyCheck);
    }

    /// <summary>
    /// Verifies that Timeout can be set.
    /// </summary>
    [Test]
    public async Task Timeout_CanBeSet()
    {
        // Act
        var config = new ApprovalConfiguration { Timeout = TimeSpan.FromHours(4) };

        // Assert
        await Assert.That(config.Timeout).IsEqualTo(TimeSpan.FromHours(4));
    }

    /// <summary>
    /// Verifies that StaticContext can be set.
    /// </summary>
    [Test]
    public async Task StaticContext_CanBeSet()
    {
        // Act
        var config = new ApprovalConfiguration { StaticContext = "Please approve this request" };

        // Assert
        await Assert.That(config.StaticContext).IsEqualTo("Please approve this request");
    }

    /// <summary>
    /// Verifies that ContextFactoryExpression can be set.
    /// </summary>
    [Test]
    public async Task ContextFactoryExpression_CanBeSet()
    {
        // Act
        var config = new ApprovalConfiguration
        {
            ContextFactoryExpression = "state => $\"Claim {state.ClaimId}\""
        };

        // Assert
        await Assert.That(config.ContextFactoryExpression).IsEqualTo("state => $\"Claim {state.ClaimId}\"");
    }

    /// <summary>
    /// Verifies that Options can be set.
    /// </summary>
    [Test]
    public async Task Options_CanBeSet()
    {
        // Arrange
        var options = new List<ApprovalOptionDefinition>
        {
            new("approve", "Approve", "Approve the request", true),
            new("reject", "Reject", "Reject the request"),
        };

        // Act
        var config = new ApprovalConfiguration { Options = options };

        // Assert
        await Assert.That(config.Options.Count).IsEqualTo(2);
        await Assert.That(config.Options[0].OptionId).IsEqualTo("approve");
    }

    // =============================================================================
    // C. Metadata Tests
    // =============================================================================

    /// <summary>
    /// Verifies that static metadata defaults to empty dictionary.
    /// </summary>
    [Test]
    public async Task Default_StaticMetadata_IsEmptyDictionary()
    {
        // Act
        var config = ApprovalConfiguration.Default;

        // Assert
        await Assert.That(config.StaticMetadata).IsEmpty();
    }

    /// <summary>
    /// Verifies that StaticMetadata can be set.
    /// </summary>
    [Test]
    public async Task StaticMetadata_CanBeSet()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["claimAmount"] = 1000m,
            ["claimType"] = "Auto"
        };

        // Act
        var config = new ApprovalConfiguration { StaticMetadata = metadata };

        // Assert
        await Assert.That(config.StaticMetadata.Count).IsEqualTo(2);
        await Assert.That(config.StaticMetadata["claimAmount"]).IsEqualTo(1000m);
    }

    /// <summary>
    /// Verifies that DynamicMetadataExpressions defaults to empty dictionary.
    /// </summary>
    [Test]
    public async Task Default_DynamicMetadataExpressions_IsEmptyDictionary()
    {
        // Act
        var config = ApprovalConfiguration.Default;

        // Assert
        await Assert.That(config.DynamicMetadataExpressions).IsEmpty();
    }

    // =============================================================================
    // D. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ApprovalConfiguration is an immutable record.
    /// </summary>
    [Test]
    public async Task ApprovalConfiguration_IsImmutableRecord()
    {
        // Arrange
        var original = new ApprovalConfiguration { Type = ApprovalType.SafetyCheck };

        // Act - Use record with syntax
        var modified = original with { Type = ApprovalType.DataRequest };

        // Assert
        await Assert.That(original.Type).IsEqualTo(ApprovalType.SafetyCheck);
        await Assert.That(modified.Type).IsEqualTo(ApprovalType.DataRequest);
        await Assert.That(original).IsNotEqualTo(modified);
    }
}
