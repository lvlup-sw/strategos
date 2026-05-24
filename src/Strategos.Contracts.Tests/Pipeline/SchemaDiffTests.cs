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
}
