// -----------------------------------------------------------------------
// <copyright file="DiagnosticForkModelTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="DiagnosticForkModel"/> and <see cref="PermittedForkTriggerModel"/>.
/// </summary>
[Property("Category", "Unit")]
public class DiagnosticForkModelTests
{
    private static PermittedForkTriggerModel Trigger(string name, params string[] fields)
        => PermittedForkTriggerModel.Create(name, fields);

    // =============================================================================
    // A. DiagnosticForkModel factory tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with valid params returns a fully populated model.
    /// </summary>
    [Test]
    public async Task Create_WithValidParams_ReturnsModel()
    {
        // Act
        var model = DiagnosticForkModel.Create(
            anchorStepMonikers: ["RatifyDeployment", "SecondAnchor"],
            permittedTriggers:
            [
                Trigger("RatificationFailure", "provisionalStampEventId"),
                Trigger("GateContradiction", "leftGateId", "rightGateId"),
            ],
            compensationSeedMoniker: "RollbackProvisionalStamp",
            maxForks: 3);

        // Assert
        await Assert.That(model.AnchorStepMonikers).IsEquivalentTo(new[] { "RatifyDeployment", "SecondAnchor" });
        await Assert.That(model.AnchorCount).IsEqualTo(2);
        await Assert.That(model.PermittedTriggerCount).IsEqualTo(2);
        await Assert.That(model.CompensationSeedMoniker).IsEqualTo("RollbackProvisionalStamp");
        await Assert.That(model.MaxForks).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that Create throws for null anchors.
    /// </summary>
    [Test]
    public async Task Create_WithNullAnchors_ThrowsArgumentNullException()
    {
        await Assert.That(() => DiagnosticForkModel.Create(
            anchorStepMonikers: null!,
            permittedTriggers: [Trigger("RatificationFailure", "e")],
            compensationSeedMoniker: "Seed",
            maxForks: 1))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create throws when no anchor is declared.
    /// </summary>
    [Test]
    public async Task Create_WithNoAnchor_ThrowsArgumentException()
    {
        await Assert.That(() => DiagnosticForkModel.Create(
            anchorStepMonikers: [],
            permittedTriggers: [Trigger("RatificationFailure", "e")],
            compensationSeedMoniker: "Seed",
            maxForks: 1))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create throws when an anchor is whitespace.
    /// </summary>
    [Test]
    public async Task Create_WithWhitespaceAnchor_ThrowsArgumentException()
    {
        await Assert.That(() => DiagnosticForkModel.Create(
            anchorStepMonikers: ["  "],
            permittedTriggers: [Trigger("RatificationFailure", "e")],
            compensationSeedMoniker: "Seed",
            maxForks: 1))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create throws when no permitted trigger is declared.
    /// </summary>
    [Test]
    public async Task Create_WithNoPermittedTrigger_ThrowsArgumentException()
    {
        await Assert.That(() => DiagnosticForkModel.Create(
            anchorStepMonikers: ["Anchor"],
            permittedTriggers: [],
            compensationSeedMoniker: "Seed",
            maxForks: 1))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Two same-trigger declarations on one edge are rejected at the IR floor — not
    /// first-wins-deduped — because they can carry different evidence schemas (#156.2).
    /// </summary>
    [Test]
    public async Task Create_WithDuplicateTrigger_ThrowsArgumentException()
    {
        await Assert.That(() => DiagnosticForkModel.Create(
            anchorStepMonikers: ["Anchor"],
            permittedTriggers:
            [
                Trigger("RatificationFailure", "stampId"),
                Trigger("RatificationFailure", "otherStampId"),
            ],
            compensationSeedMoniker: "Seed",
            maxForks: 1))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Distinct trigger names on one edge stay unique; only a repeated name is reported.
    /// </summary>
    [Test]
    public async Task FindDuplicateTriggerNames_ReportsOnlyRepeatedNames()
    {
        var duplicates = DiagnosticForkModel.FindDuplicateTriggerNames(
            ["RatificationFailure", "GateContradiction", "RatificationFailure"]);

        await Assert.That(duplicates).IsEquivalentTo(new[] { "RatificationFailure" });
    }

    /// <summary>
    /// Distinct seeds stay unique; a repeated seed and a hyphen/underscore pair that
    /// sanitize to the same key are both reported (#156.3).
    /// </summary>
    [Test]
    public async Task FindDuplicateCompensationSeeds_ReportsSanitizedCollisions()
    {
        var duplicates = DiagnosticForkModel.FindDuplicateCompensationSeeds(
            ["RollbackOne", "RollbackTwo", "RollbackOne", "foo-bar", "foo_bar"]);

        await Assert.That(duplicates).IsEquivalentTo(new[] { "RollbackOne", "foo_bar" });
    }

    /// <summary>
    /// The count property is keyed by the hyphen-sanitized compensation seed.
    /// </summary>
    [Test]
    public async Task CountPropertyName_UsesSanitizedCompensationSeed()
    {
        var model = DiagnosticForkModel.Create(
            ["Anchor"],
            [Trigger("RatificationFailure", "e")],
            "rollback-provisional",
            2);

        await Assert.That(model.CountPropertyName).IsEqualTo("DiagnosticForkCount_rollback_provisional");
        await Assert.That(DiagnosticForkModel.SanitizeCompensationSeedMoniker("StampStep"))
            .IsEqualTo("StampStep");
    }

    /// <summary>
    /// Verifies that Create throws when the compensation seed is whitespace.
    /// </summary>
    [Test]
    public async Task Create_WithWhitespaceCompensationSeed_ThrowsArgumentException()
    {
        await Assert.That(() => DiagnosticForkModel.Create(
            anchorStepMonikers: ["Anchor"],
            permittedTriggers: [Trigger("RatificationFailure", "e")],
            compensationSeedMoniker: "   ",
            maxForks: 1))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create throws when maxForks is below 1.
    /// </summary>
    [Test]
    public async Task Create_WithMaxForksBelowOne_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() => DiagnosticForkModel.Create(
            anchorStepMonikers: ["Anchor"],
            permittedTriggers: [Trigger("RatificationFailure", "e")],
            compensationSeedMoniker: "Seed",
            maxForks: 0))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that Create copies the input collections so later mutation of the caller's
    /// list does not leak into the immutable IR.
    /// </summary>
    [Test]
    public async Task Create_CopiesInputCollections()
    {
        // Arrange
        var anchors = new List<string> { "Anchor" };
        var triggers = new List<PermittedForkTriggerModel> { Trigger("RatificationFailure", "e") };

        var model = DiagnosticForkModel.Create(anchors, triggers, "Seed", 2);

        // Act — mutate the caller's lists after construction
        anchors.Add("Sneaky");
        triggers.Add(Trigger("GateContradiction", "g"));

        // Assert — the model is unaffected
        await Assert.That(model.AnchorCount).IsEqualTo(1);
        await Assert.That(model.PermittedTriggerCount).IsEqualTo(1);
    }

    // =============================================================================
    // B. PermittedForkTriggerModel factory tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with valid params returns a populated trigger model.
    /// </summary>
    [Test]
    public async Task TriggerCreate_WithValidParams_ReturnsModel()
    {
        // Act
        var trigger = PermittedForkTriggerModel.Create(
            "GateContradiction",
            ["leftGateId", "rightGateId"]);

        // Assert
        await Assert.That(trigger.TriggerName).IsEqualTo("GateContradiction");
        await Assert.That(trigger.RequiredEvidenceFields).IsEquivalentTo(new[] { "leftGateId", "rightGateId" });
    }

    /// <summary>
    /// Verifies that Create throws when the trigger name is whitespace.
    /// </summary>
    [Test]
    public async Task TriggerCreate_WithWhitespaceTriggerName_ThrowsArgumentException()
    {
        await Assert.That(() => PermittedForkTriggerModel.Create("  ", ["e"]))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create throws when no evidence field is declared.
    /// </summary>
    [Test]
    public async Task TriggerCreate_WithNoEvidenceField_ThrowsArgumentException()
    {
        await Assert.That(() => PermittedForkTriggerModel.Create("RatificationFailure", []))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create throws when an evidence field is whitespace.
    /// </summary>
    [Test]
    public async Task TriggerCreate_WithWhitespaceEvidenceField_ThrowsArgumentException()
    {
        await Assert.That(() => PermittedForkTriggerModel.Create("RatificationFailure", ["ok", "  "]))
            .Throws<ArgumentException>();
    }
}
