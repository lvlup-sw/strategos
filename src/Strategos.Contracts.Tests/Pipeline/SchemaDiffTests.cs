// =============================================================================
// <copyright file="SchemaDiffTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Contracts.SchemaDiff;

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// T30 — breaking-change JSON Schema structural diff. A structural diff over
/// two emitted JSON Schema documents must classify a removed (or newly-required,
/// or type-narrowed) property as <see cref="ChangeSeverity.Breaking"/>, and an
/// added optional property as <see cref="ChangeSeverity.NonBreaking"/>. CI uses
/// the same harness against the latest package actually published to NuGet. A
/// breaking change advances the minor before 1.0 and the major after 1.0. The
/// tests compare in-test fixtures so they are deterministic and offline.
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

    /// <summary>
    /// Adding a closed-enum constraint to a previously unconstrained string is
    /// narrowing, unlike adding one member to an already-closed enum.
    /// </summary>
    [Test]
    public async Task SchemaDiff_AddedEnumConstraint_IsBreaking()
    {
        const string previous =
            """
            {
              "type": "string"
            }
            """;
        const string next =
            """
            {
              "type": "string",
              "enum": ["manual"]
            }
            """;

        var result = JsonSchemaDiff.Compare(previous, next);

        await Assert.That(result.HasBreakingChanges).IsTrue();
        await Assert.That(result.Changes)
            .Contains(change => change.Description.Contains(
                "enum constraint",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// Adding nonblank string constraints below a retained object property is a
    /// narrowing even though the root property/type shape is unchanged.
    /// </summary>
    [Test]
    public async Task SchemaDiff_NestedStringConstraints_AreBreaking()
    {
        const string previous =
            """
            {
              "type": "object",
              "properties": {
                "action": {
                  "type": "object",
                  "properties": {
                    "domainName": { "type": "string" }
                  }
                }
              }
            }
            """;
        const string next =
            """
            {
              "type": "object",
              "properties": {
                "action": {
                  "type": "object",
                  "properties": {
                    "domainName": {
                      "type": "string",
                      "minLength": 1,
                      "pattern": ".*\\S.*"
                    }
                  }
                }
              }
            }
            """;

        var result = JsonSchemaDiff.Compare(previous, next);

        await Assert.That(result.HasBreakingChanges).IsTrue();
        await Assert.That(result.Changes)
            .Contains(change => change.Severity == ChangeSeverity.Breaking
                && change.Description.Contains("minLength", StringComparison.Ordinal));
        await Assert.That(result.Changes)
            .Contains(change => change.Severity == ChangeSeverity.Breaking
                && change.Description.Contains("pattern", StringComparison.Ordinal));
    }

    /// <summary>A retained property's reference target cannot change on a minor.</summary>
    [Test]
    public async Task SchemaDiff_ChangedReference_IsBreaking()
    {
        const string previous =
            """
            {
              "type": "object",
              "properties": {
                "action": { "$ref": "ActionReferenceV1.json" }
              }
            }
            """;
        const string next =
            """
            {
              "type": "object",
              "properties": {
                "action": { "$ref": "ActionReferenceV2.json" }
              }
            }
            """;

        var result = JsonSchemaDiff.Compare(previous, next);

        await Assert.That(result.HasBreakingChanges).IsTrue();
        await Assert.That(result.Changes)
            .Contains(change => change.Description.Contains("$ref", StringComparison.Ordinal));
    }

    /// <summary>Removing an accepted union arm narrows the wire contract.</summary>
    [Test]
    public async Task SchemaDiff_RemovedUnionArm_IsBreaking()
    {
        const string previous =
            """
            {
              "anyOf": [
                { "$ref": "CatalogWorkflowRef.json" },
                { "$ref": "AuthoredWorkflowRef.json" }
              ]
            }
            """;
        const string next =
            """
            {
              "anyOf": [
                { "$ref": "CatalogWorkflowRef.json" }
              ]
            }
            """;

        var result = JsonSchemaDiff.Compare(previous, next);

        await Assert.That(result.HasBreakingChanges).IsTrue();
        await Assert.That(result.Changes)
            .Contains(change => change.Description.Contains("union arm", StringComparison.Ordinal)
                && change.Description.Contains("removed", StringComparison.Ordinal));
    }

    /// <summary>
    /// Adding an <c>anyOf</c> variant is wire-additive but receives the same
    /// consumer-upgrade notice as adding a closed-enum member.
    /// </summary>
    [Test]
    public async Task SchemaDiff_AddedAnyOfArm_IsNotice()
    {
        const string previous =
            """
            {
              "anyOf": [
                { "$ref": "CatalogWorkflowRef.json" }
              ]
            }
            """;
        const string next =
            """
            {
              "anyOf": [
                { "$ref": "CatalogWorkflowRef.json" },
                { "$ref": "AuthoredWorkflowRef.json" }
              ]
            }
            """;

        var result = JsonSchemaDiff.Compare(previous, next);

        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.Notice);
        await Assert.That(result.HasBreakingChanges).IsFalse();
    }

    /// <summary>Changing discriminator metadata can redirect variant decoding.</summary>
    [Test]
    public async Task SchemaDiff_ChangedDiscriminator_IsBreaking()
    {
        const string previous =
            """
            {
              "discriminator": {
                "propertyName": "kind"
              }
            }
            """;
        const string next =
            """
            {
              "discriminator": {
                "propertyName": "type"
              }
            }
            """;

        var result = JsonSchemaDiff.Compare(previous, next);

        await Assert.That(result.HasBreakingChanges).IsTrue();
        await Assert.That(result.Changes)
            .Contains(change => change.Description.Contains("discriminator", StringComparison.Ordinal));
    }

    /// <summary>Array item constraints are compared recursively.</summary>
    [Test]
    public async Task SchemaDiff_NarrowedItemSchema_IsBreaking()
    {
        const string previous =
            """
            {
              "type": "array",
              "items": {
                "type": "string"
              }
            }
            """;
        const string next =
            """
            {
              "type": "array",
              "items": {
                "type": "string",
                "minLength": 1
              }
            }
            """;

        var result = JsonSchemaDiff.Compare(previous, next);

        await Assert.That(result.HasBreakingChanges).IsTrue();
        await Assert.That(result.Changes)
            .Contains(change => change.Description.Contains("$.items", StringComparison.Ordinal)
                && change.Description.Contains("minLength", StringComparison.Ordinal));
    }

    /// <summary>
    /// A changed validation keyword without a compatibility rule fails closed.
    /// </summary>
    [Test]
    public async Task SchemaDiff_UnknownValidationKeywordChange_IsBreaking()
    {
        const string previous =
            """
            {
              "type": "object",
              "unevaluatedProperties": true
            }
            """;
        const string next =
            """
            {
              "type": "object",
              "unevaluatedProperties": false
            }
            """;

        var result = JsonSchemaDiff.Compare(previous, next);

        await Assert.That(result.HasBreakingChanges).IsTrue();
        await Assert.That(result.Changes)
            .Contains(change => change.Description.Contains(
                "safety cannot be proven",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// JSON Schema permits a required name without a sibling properties entry;
    /// adding that name still narrows the accepted object set.
    /// </summary>
    [Test]
    public async Task SchemaDiff_RequiredNameWithoutPropertyEntry_IsBreakingInBothClassifiers()
    {
        const string previous = """{ "type": "object" }""";
        const string next = """{ "type": "object", "required": ["externalName"] }""";

        var authoritative = JsonSchemaDiff.Compare(previous, next);
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), previous);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), next);
            var gate = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "1.2.3",
                "1.2.4");

            await Assert.That(authoritative.HasBreakingChanges).IsTrue();
            await Assert.That(authoritative.Changes)
                .Contains(change => change.Description.Contains("externalName", StringComparison.Ordinal));
            await Assert.That(gate.ExitCode).IsEqualTo(1).Because(gate.Output);
            await Assert.That(gate.Output).Contains("externalName");
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>Schema dialect and identity changes are semantic, not annotations.</summary>
    [Test]
    [Arguments("$id", "Widget.json", "RenamedWidget.json")]
    [Arguments(
        "$schema",
        "https://json-schema.org/draft/2020-12/schema",
        "http://json-schema.org/draft-04/schema#")]
    public async Task SchemaDiff_SchemaIdentityAndDialectChanges_AreBreakingInBothClassifiers(
        string keyword,
        string previousValue,
        string nextValue)
    {
        var previous = $$"""{ "{{keyword}}": "{{previousValue}}", "type": "object" }""";
        var next = $$"""{ "{{keyword}}": "{{nextValue}}", "type": "object" }""";
        var authoritative = JsonSchemaDiff.Compare(previous, next);
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), previous);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), next);
            var gate = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "1.2.3",
                "1.2.4");

            await Assert.That(authoritative.HasBreakingChanges).IsTrue();
            await Assert.That(authoritative.Changes).HasCount().EqualTo(1);
            await Assert.That(authoritative.Changes)
                .Contains(change => change.Description.Contains(keyword, StringComparison.Ordinal));
            await Assert.That(gate.ExitCode).IsEqualTo(1).Because(gate.Output);
            await Assert.That(gate.Output).Contains(keyword);
            await Assert.That(gate.Output.Split($"'{keyword}' constraint changed").Length - 1)
                .IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>
    /// The Node gate must not round JSON integers above IEEE-754's safe range and
    /// miss a minimum increase that the exact C# classifier detects.
    /// </summary>
    [Test]
    public async Task SchemaDiff_UnsafeJsonIntegerMinimum_IsExactInBothClassifiers()
    {
        const string previous = """{ "type": "string", "minLength": 9007199254740992 }""";
        const string next = """{ "type": "string", "minLength": 9007199254740993 }""";
        var authoritative = JsonSchemaDiff.Compare(previous, next);
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), previous);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), next);
            var gate = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "1.2.3",
                "1.2.4");

            await Assert.That(authoritative.HasBreakingChanges).IsTrue();
            await Assert.That(authoritative.Changes).HasCount().EqualTo(1);
            await Assert.That(gate.ExitCode).IsEqualTo(1).Because(gate.Output);
            await Assert.That(gate.Output).Contains("9007199254740992");
            await Assert.That(gate.Output).Contains("9007199254740993");
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>
    /// The actual Node gate must reject an empty baseline as indeterminate rather
    /// than treating every current schema as an additive first release.
    /// </summary>
    [Test]
    public async Task SchemaDiff_NodeGate_EmptyBaselineIsIndeterminate()
    {
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), BaseSchema);
            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "0.4.0",
                "0.5.0");

            await Assert.That(result.ExitCode).IsEqualTo(2).Because(result.Output);
            await Assert.That(result.Output).Contains("INDETERMINATE");
            await Assert.That(result.Output).Contains("contains no JSON schemas");
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>
    /// The Node gate's recursive classifier must kill the same nested
    /// reference/string/union/discriminator/item narrowing fixture as the
    /// authoritative C# classifier.
    /// </summary>
    [Test]
    public async Task SchemaDiff_NodeGate_NestedNarrowingIsBreaking()
    {
        const string previous =
            """
            {
              "type": "object",
              "properties": {
                "name": {
                  "type": "string"
                },
                "actions": {
                  "type": "array",
                  "items": {
                    "$ref": "ActionReferenceV1.json"
                  }
                },
                "target": {
                  "anyOf": [
                    { "$ref": "CatalogWorkflowRef.json" },
                    { "$ref": "AuthoredWorkflowRef.json" }
                  ],
                  "discriminator": {
                    "propertyName": "kind"
                  }
                }
              }
            }
            """;
        const string next =
            """
            {
              "type": "object",
              "properties": {
                "name": {
                  "type": "string",
                  "minLength": 1,
                  "pattern": ".*\\S.*"
                },
                "actions": {
                  "type": "array",
                  "items": {
                    "$ref": "ActionReferenceV2.json"
                  }
                },
                "target": {
                  "anyOf": [
                    { "$ref": "CatalogWorkflowRef.json" }
                  ],
                  "discriminator": {
                    "propertyName": "type"
                  }
                }
              }
            }
            """;
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), previous);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), next);

            var authoritative = JsonSchemaDiff.Compare(previous, next);
            var gate = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "1.2.3",
                "1.2.4");

            await Assert.That(authoritative.HasBreakingChanges).IsTrue();
            await Assert.That(gate.ExitCode).IsEqualTo(1).Because(gate.Output);
            await Assert.That(gate.Output).Contains("BREAKING");
            await Assert.That(gate.Output).Contains(".items");
            await Assert.That(gate.Output).Contains("$ref");
            await Assert.That(gate.Output).Contains("minLength");
            await Assert.That(gate.Output).Contains("pattern");
            await Assert.That(gate.Output).Contains("union arm");
            await Assert.That(gate.Output).Contains("discriminator");
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>
    /// The process gate applies the documented pre-1.0-minor/post-1.0-major
    /// breaking-version policy, and rejects non-forward versions.
    /// </summary>
    [Test]
    [Arguments("0.4.0", "0.4.1", 1, "requires a MINOR")]
    [Arguments("0.4.0", "0.5.0", 0, "BREAKING change(s) allowed")]
    [Arguments("0.4.0", "1.0.0", 0, "BREAKING change(s) allowed")]
    [Arguments("1.2.3", "1.3.0", 1, "requires a MAJOR")]
    [Arguments("1.2.3", "2.0.0", 0, "BREAKING change(s) allowed")]
    public async Task SchemaDiff_NodeGate_EnforcesBreakingVersionPolicy(
        string previousVersion,
        string candidateVersion,
        int expectedExitCode,
        string expectedOutput)
    {
        const string narrowed =
            """
            {
              "$id": "Widget.json",
              "type": "object",
              "properties": {
                "id": { "type": "string" }
              },
              "required": ["id"]
            }
            """;
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), narrowed);

            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                previousVersion,
                candidateVersion);

            await Assert.That(result.ExitCode).IsEqualTo(expectedExitCode).Because(result.Output);
            await Assert.That(result.Output).Contains(expectedOutput);
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>Equal, downgraded, and malformed candidate versions cannot pass.</summary>
    [Test]
    [Arguments("1.2.3", "1.2.3", 1, "must be greater")]
    [Arguments("1.2.3", "1.2.2", 1, "must be greater")]
    [Arguments("1.2.3", "1.2.4-alpha.1", 2, "INDETERMINATE")]
    public async Task SchemaDiff_NodeGate_RejectsInvalidVersionProgression(
        string previousVersion,
        string candidateVersion,
        int expectedExitCode,
        string expectedOutput)
    {
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), BaseSchema);

            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                previousVersion,
                candidateVersion);

            await Assert.That(result.ExitCode).IsEqualTo(expectedExitCode).Because(result.Output);
            await Assert.That(result.Output).Contains(expectedOutput);
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>The file-set gate includes packaged schemas below nested directories.</summary>
    [Test]
    public async Task SchemaDiff_NodeGate_RecursesThroughCompleteSchemaTree()
    {
        const string narrowed =
            """
            {
              "$id": "Widget.json",
              "type": "object",
              "properties": {
                "id": { "type": "string" }
              },
              "required": ["id"]
            }
            """;
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            var previousNested = Directory.CreateDirectory(
                Path.Combine(previousDirectory, "bundled"));
            var nextNested = Directory.CreateDirectory(Path.Combine(nextDirectory, "bundled"));
            await File.WriteAllTextAsync(Path.Combine(previousNested.FullName, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(Path.Combine(nextNested.FullName, "Widget.json"), narrowed);

            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "1.2.3",
                "1.2.4");

            await Assert.That(result.ExitCode).IsEqualTo(1).Because(result.Output);
            await Assert.That(result.Output).Contains("bundled/Widget.json");
            await Assert.That(result.Output).Contains("BREAKING");
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>The workflow must bind its baseline to the published package.</summary>
    [Test]
    public async Task SchemaDiff_Workflow_UsesPublishedPackageAndCompleteSchemaTree()
    {
        var workflowPath = Path.Combine(
            RepoLayout.RepoRoot,
            ".github",
            "workflows",
            "contracts-schema-diff.yml");
        var workflow = await File.ReadAllTextAsync(workflowPath);

        await Assert.That(workflow).Contains("api.nuget.org/v3-flatcontainer");
        await Assert.That(workflow).Contains("contentFiles/any/any/schemas");
        await Assert.That(workflow).Contains("src/Strategos.Contracts/schemas");
        await Assert.That(workflow).Contains("previous_version");
        await Assert.That(workflow).Contains("candidate_version");
        await Assert.That(workflow).Contains("schema compatibility is indeterminate");
        await Assert.That(workflow).Contains("permissions:\n  contents: read");
        await Assert.That(workflow).Contains(
            "actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020");
        await Assert.That(workflow).Contains("persist-credentials: false");
    }

    private static Task<CliResult> RunNodeSchemaDiff(
        string previousDirectory,
        string nextDirectory,
        string previousVersion,
        string candidateVersion)
    {
        var scriptPath = Path.Combine(
            RepoLayout.RepoRoot,
            "scripts",
            "contracts-schema-diff.mjs");
        return Cli.RunAsync(
            "node",
            $"\"{scriptPath}\" \"{previousDirectory}\" \"{nextDirectory}\" "
            + $"\"{previousVersion}\" \"{candidateVersion}\"");
    }
}
