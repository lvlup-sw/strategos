// =============================================================================
// <copyright file="SchemaDiffTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Contracts.SchemaDiff;

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// T30 — breaking-change JSON Schema structural diff (design §Resilience item 3:
/// "additive-only minors; breaking change ⇒ major bump"). A structural diff over
/// two emitted JSON Schema documents must classify a removed (or newly-required,
/// or type-narrowed) property as <see cref="ChangeSeverity.Breaking"/>, and an
/// added optional property as <see cref="ChangeSeverity.NonBreaking"/>. CI uses
/// the same harness against the previous tag's schemas; the tests compare
/// in-test fixtures so they are deterministic and offline.
/// </summary>
[Property("Category", "Pipeline")]
public class SchemaDiffTests
{
    private const string BaseSchema =
        """
        {
          "$id": "Widget.json",
          "type": "object",
          "properties": {
            "id": { "type": "string" },
            "size": { "type": "integer" }
          },
          "required": ["id"]
        }
        """;

    /// <summary>
    /// Removing a required field is BREAKING: consumers that depend on the field
    /// can no longer rely on it being present in the producer's output.
    /// </summary>
    [Test]
    public async Task SchemaDiff_DetectsBreakingChange_FailsCi()
    {
        const string next =
            """
            {
              "$id": "Widget.json",
              "type": "object",
              "properties": {
                "size": { "type": "integer" }
              },
              "required": []
            }
            """;

        var result = JsonSchemaDiff.Compare(BaseSchema, next);

        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.Breaking);
        await Assert.That(result.HasBreakingChanges).IsTrue();
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.Breaking
                && c.Description.Contains("id", StringComparison.Ordinal));
    }

    /// <summary>
    /// Adding an optional field is NON-BREAKING: existing producers/consumers are
    /// unaffected (additive-only minor). This is the green path that lets a minor
    /// bump ship without a major.
    /// </summary>
    [Test]
    public async Task SchemaDiff_AddedOptionalField_IsNonBreaking()
    {
        const string next =
            """
            {
              "$id": "Widget.json",
              "type": "object",
              "properties": {
                "id": { "type": "string" },
                "size": { "type": "integer" },
                "color": { "type": "string" }
              },
              "required": ["id"]
            }
            """;

        var result = JsonSchemaDiff.Compare(BaseSchema, next);

        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.NonBreaking);
        await Assert.That(result.HasBreakingChanges).IsFalse();
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.NonBreaking
                && c.Description.Contains("color", StringComparison.Ordinal));
    }

    /// <summary>
    /// Promoting an existing optional field to <c>required</c> is BREAKING:
    /// producers that omitted it now emit invalid documents against the new schema.
    /// </summary>
    [Test]
    public async Task SchemaDiff_NewlyRequiredField_IsBreaking()
    {
        const string next =
            """
            {
              "$id": "Widget.json",
              "type": "object",
              "properties": {
                "id": { "type": "string" },
                "size": { "type": "integer" }
              },
              "required": ["id", "size"]
            }
            """;

        var result = JsonSchemaDiff.Compare(BaseSchema, next);

        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.Breaking);
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.Breaking
                && c.Description.Contains("size", StringComparison.Ordinal));
    }

    /// <summary>
    /// Narrowing a property's type (e.g. <c>string</c> → <c>integer</c>) is
    /// BREAKING: previously-valid values are now rejected.
    /// </summary>
    [Test]
    public async Task SchemaDiff_NarrowedFieldType_IsBreaking()
    {
        const string next =
            """
            {
              "$id": "Widget.json",
              "type": "object",
              "properties": {
                "id": { "type": "integer" },
                "size": { "type": "integer" }
              },
              "required": ["id"]
            }
            """;

        var result = JsonSchemaDiff.Compare(BaseSchema, next);

        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.Breaking);
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.Breaking
                && c.Description.Contains("id", StringComparison.Ordinal)
                && c.Description.Contains("type", StringComparison.Ordinal));
    }

    /// <summary>
    /// Identical schemas produce no changes and are NON-BREAKING.
    /// </summary>
    [Test]
    public async Task SchemaDiff_IdenticalSchemas_NoChanges()
    {
        var result = JsonSchemaDiff.Compare(BaseSchema, BaseSchema);

        await Assert.That(result.Changes).IsEmpty();
        await Assert.That(result.HasBreakingChanges).IsFalse();
        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.NonBreaking);
    }

    // -------------------------------------------------------------------------
    // DR-18 enum-evolution policy. TypeSpec closed enums emit as a top-level
    // `{ "type": "string", "enum": [...] }` schema (referenced by $ref elsewhere),
    // so the differ diffs the schema's own enum member list. Removal or rename ⇒
    // BREAKING; addition ⇒ flagged NOTICE (permitted on a minor, but surfaced).
    // -------------------------------------------------------------------------

    private const string EnumBaseSchema =
        """
        {
          "$id": "TriggerKind.json",
          "type": "string",
          "enum": ["manual", "scheduled", "signal"],
          "description": "How a workflow run is triggered."
        }
        """;

    /// <summary>
    /// Removing an enum member is BREAKING: a producer may still emit the removed
    /// token and a consumer may still switch on it, yet it is gone from the closed set.
    /// </summary>
    [Test]
    public async Task SchemaDiff_RemovedEnumMember_IsBreaking()
    {
        const string next =
            """
            {
              "$id": "TriggerKind.json",
              "type": "string",
              "enum": ["manual", "scheduled"],
              "description": "How a workflow run is triggered."
            }
            """;

        var result = JsonSchemaDiff.Compare(EnumBaseSchema, next);

        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.Breaking);
        await Assert.That(result.HasBreakingChanges).IsTrue();
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.Breaking
                && c.Description.Contains("signal", StringComparison.Ordinal)
                && c.Description.Contains("removed", StringComparison.Ordinal));
    }

    /// <summary>
    /// Renaming an enum member (drop the old token, add a new one) is BREAKING: it
    /// surfaces as a removal (⇒ BREAKING) plus an addition (⇒ NOTICE), and the removal
    /// dominates the overall severity.
    /// </summary>
    [Test]
    public async Task SchemaDiff_RenamedEnumMember_IsBreaking()
    {
        const string next =
            """
            {
              "$id": "TriggerKind.json",
              "type": "string",
              "enum": ["manual", "cron", "signal"],
              "description": "How a workflow run is triggered."
            }
            """;

        var result = JsonSchemaDiff.Compare(EnumBaseSchema, next);

        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.Breaking);
        await Assert.That(result.HasBreakingChanges).IsTrue();
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.Breaking
                && c.Description.Contains("scheduled", StringComparison.Ordinal)
                && c.Description.Contains("removed", StringComparison.Ordinal));
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.Notice
                && c.Description.Contains("cron", StringComparison.Ordinal)
                && c.Description.Contains("added", StringComparison.Ordinal));
    }

    /// <summary>
    /// Adding an enum member is a flagged NOTICE, not BREAKING: additive on a minor
    /// bump, but surfaced so a consumer-notice release-notes line is written (strict
    /// converters reject unknown members until consumers upgrade). The CI gate stays
    /// green on NOTICE.
    /// </summary>
    [Test]
    public async Task SchemaDiff_AddedEnumMember_IsNoticeNotBreaking()
    {
        const string next =
            """
            {
              "$id": "TriggerKind.json",
              "type": "string",
              "enum": ["manual", "scheduled", "signal", "webhook"],
              "description": "How a workflow run is triggered."
            }
            """;

        var result = JsonSchemaDiff.Compare(EnumBaseSchema, next);

        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.Notice);
        await Assert.That(result.HasBreakingChanges).IsFalse();
        await Assert.That(result.HasNotices).IsTrue();
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.Notice
                && c.Description.Contains("webhook", StringComparison.Ordinal)
                && c.Description.Contains("added", StringComparison.Ordinal));
    }
}
