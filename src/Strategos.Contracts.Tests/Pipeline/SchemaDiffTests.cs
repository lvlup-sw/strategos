// =============================================================================
// <copyright file="SchemaDiffTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;
using System.Text.RegularExpressions;
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

            // The narrowing is allowlisted, so this fixture isolates the version
            // policy: the rows that fail do so because the increment is wrong, not
            // because the change is unaccounted for.
            var allowlist = await WriteAllowlistAsync(
                nextDirectory,
                (SizeRemoved.File, SizeRemoved.Path, SizeRemoved.Kind, candidateVersion));

            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                previousVersion,
                candidateVersion,
                allowlist);

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

        // Second arm: the published baseline can be several minors behind, so a
        // pre-1.0 minor bump already in ContractsVersion permits every narrowing
        // there. The merge-base arm compares the versions this PR actually moves
        // between.
        await Assert.That(workflow).Contains("github.event.pull_request.base.sha")
            .Because("the merge-base arm must materialise the PR base's schema tree");
        await Assert.That(workflow).Contains("--allow-equal-versions")
            .Because("base and head ContractsVersion are usually equal; the arm must still evaluate");
        await Assert.That(workflow).Contains("Structural breaking-change diff vs merge base");
        await Assert.That(Regex.Matches(workflow, @"--allowlist").Count).IsEqualTo(2)
            .Because("both arms must pass the breaking-change allowlist");
        await Assert.That(workflow).Contains("breaking-changes.allowlist.json");
    }

    /// <summary>
    /// The publish gate runs the same script and must carry the same allowlist:
    /// a release must not be able to ship a narrowing the PR gate would reject.
    /// </summary>
    [Test]
    public async Task SchemaDiff_PublishWorkflow_PassesTheBreakingChangeAllowlist()
    {
        var workflowPath = Path.Combine(
            RepoLayout.RepoRoot,
            ".github",
            "workflows",
            "publish-contracts.yml");
        var workflow = await File.ReadAllTextAsync(workflowPath);

        await Assert.That(workflow).Contains("scripts/contracts-schema-diff.mjs");
        await Assert.That(workflow).Contains("--allowlist");
        await Assert.That(workflow).Contains(
            "src/Strategos.Contracts/schemas/breaking-changes.allowlist.json");
    }

    private const string DefinitionsBaseSchema =
        """
        {
          "$id": "WorkflowDefinitionV1.json",
          "type": "object",
          "properties": {
            "steps": {
              "type": "array",
              "items": { "$ref": "#/definitions/StepV1" }
            }
          },
          "definitions": {
            "StepV1": {
              "type": "object",
              "properties": {
                "name": { "type": "string" }
              },
              "required": ["name"]
            }
          }
        }
        """;

    /// <summary>
    /// A draft-07 <c>definitions</c> entry (and its 2019-09 twin <c>$defs</c>) is a
    /// schema container, not an unsupported validation keyword: adding a definition
    /// that nothing existing references narrows nothing, so it is NON-BREAKING in
    /// both classifiers. This is the shape the bundled
    /// <c>workflow-definition-v1.schema.json</c> takes when a new <c>$ref</c>
    /// target such as <c>ActionReferenceV1</c> is introduced.
    /// </summary>
    [Test]
    public async Task SchemaDiff_AddedDefinition_IsNonBreakingInBothClassifiers()
    {
        const string next =
            """
            {
              "$id": "WorkflowDefinitionV1.json",
              "type": "object",
              "properties": {
                "steps": {
                  "type": "array",
                  "items": { "$ref": "#/definitions/StepV1" }
                }
              },
              "definitions": {
                "StepV1": {
                  "type": "object",
                  "properties": {
                    "name": { "type": "string" },
                    "action": { "$ref": "#/definitions/ActionReferenceV1" }
                  },
                  "required": ["name"]
                },
                "ActionReferenceV1": {
                  "type": "object",
                  "properties": {
                    "actionName": { "type": "string", "minLength": 1 }
                  },
                  "required": ["actionName"]
                }
              }
            }
            """;

        var authoritative = JsonSchemaDiff.Compare(DefinitionsBaseSchema, next);
        var gate = await RunNodeSchemaDiffOnPair(DefinitionsBaseSchema, next, "1.2.3", "1.2.4");

        await Assert.That(authoritative.HasBreakingChanges).IsFalse();
        await Assert.That(authoritative.Changes)
            .Contains(change => change.Severity == ChangeSeverity.NonBreaking
                && change.Description.Contains("definition 'ActionReferenceV1' was added", StringComparison.Ordinal));
        await Assert.That(authoritative.Changes)
            .Contains(change => change.Severity == ChangeSeverity.NonBreaking
                && change.Description.Contains("$.definitions['StepV1']", StringComparison.Ordinal)
                && change.Description.Contains("optional property 'action' was added", StringComparison.Ordinal));
        await Assert.That(authoritative.Changes.Any(change =>
                change.Description.Contains("safety cannot be proven", StringComparison.Ordinal)))
            .IsFalse();

        await Assert.That(gate.ExitCode).IsEqualTo(0).Because(gate.Output);
        await Assert.That(gate.Output).Contains("definition 'ActionReferenceV1' was added");
        await Assert.That(gate.Output.Contains("[BREAKING]", StringComparison.Ordinal)).IsFalse().Because(gate.Output);
        await Assert.That(gate.Output.Contains("safety cannot be proven", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>
    /// A constraint tightened inside a definition is classified by what changed
    /// inside it, exactly as a nested property would be — the recursion attributes
    /// the breaking change to the definition path rather than to the container.
    /// </summary>
    [Test]
    public async Task SchemaDiff_ChangedConstraintInsideDefinition_IsBreakingInBothClassifiers()
    {
        const string next =
            """
            {
              "$id": "WorkflowDefinitionV1.json",
              "type": "object",
              "properties": {
                "steps": {
                  "type": "array",
                  "items": { "$ref": "#/definitions/StepV1" }
                }
              },
              "definitions": {
                "StepV1": {
                  "type": "object",
                  "properties": {
                    "name": { "type": "string", "pattern": ".*\\S.*" }
                  },
                  "required": ["name"]
                }
              }
            }
            """;

        var authoritative = JsonSchemaDiff.Compare(DefinitionsBaseSchema, next);
        var gate = await RunNodeSchemaDiffOnPair(DefinitionsBaseSchema, next, "1.2.3", "1.2.4");

        await Assert.That(authoritative.HasBreakingChanges).IsTrue();
        await Assert.That(authoritative.Changes)
            .Contains(change => change.Severity == ChangeSeverity.Breaking
                && change.Description.Contains("$.definitions['StepV1'].properties['name']", StringComparison.Ordinal)
                && change.Description.Contains("'pattern' constraint changed", StringComparison.Ordinal));
        await Assert.That(authoritative.Changes.Any(change =>
                change.Description.Contains("safety cannot be proven", StringComparison.Ordinal)))
            .IsFalse();

        await Assert.That(gate.ExitCode).IsEqualTo(1).Because(gate.Output);
        await Assert.That(gate.Output).Contains(
            "$.definitions[\"StepV1\"].properties[\"name\"]: 'pattern' constraint changed");
        await Assert.That(gate.Output.Contains("safety cannot be proven", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>A removed definition invalidates every <c>$ref</c> to it: BREAKING.</summary>
    [Test]
    public async Task SchemaDiff_RemovedDefinition_IsBreakingInBothClassifiers()
    {
        const string next =
            """
            {
              "$id": "WorkflowDefinitionV1.json",
              "type": "object",
              "properties": {
                "steps": {
                  "type": "array",
                  "items": { "$ref": "#/definitions/StepV1" }
                }
              },
              "definitions": {}
            }
            """;

        var authoritative = JsonSchemaDiff.Compare(DefinitionsBaseSchema, next);
        var gate = await RunNodeSchemaDiffOnPair(DefinitionsBaseSchema, next, "1.2.3", "1.2.4");

        await Assert.That(authoritative.HasBreakingChanges).IsTrue();
        await Assert.That(authoritative.Changes)
            .Contains(change => change.Severity == ChangeSeverity.Breaking
                && change.Description.Contains("definition 'StepV1' was removed", StringComparison.Ordinal));

        await Assert.That(gate.ExitCode).IsEqualTo(1).Because(gate.Output);
        await Assert.That(gate.Output).Contains("[BREAKING] Widget.json: $: definition 'StepV1' was removed");
    }

    /// <summary>
    /// The Node gate (the classifier CI actually runs) and the C# classifier each
    /// carry a literal keyword list. Policy is data: this test parses the two
    /// arrays out of <c>scripts/contracts-schema-diff.mjs</c> and requires them to
    /// equal the C# sets, so a keyword added to one classifier without the other
    /// fails here rather than diverging silently on the release gate.
    /// </summary>
    [Test]
    public async Task SchemaDiff_NodeGateKeywordLists_MatchAuthoritativeClassifier()
    {
        var scriptPath = Path.Combine(RepoLayout.RepoRoot, "scripts", "contracts-schema-diff.mjs");
        var script = await File.ReadAllTextAsync(scriptPath);

        var nodeHandled = ReadNodeKeywordSet(script, "handledKeywords");
        var nodeAnnotations = ReadNodeKeywordSet(script, "annotationKeywords");

        await Assert.That(nodeHandled.Count).IsGreaterThan(0)
            .Because("handledKeywords array must parse out of the Node gate");
        await Assert.That(nodeAnnotations.Count).IsGreaterThan(0)
            .Because("annotationKeywords array must parse out of the Node gate");
        await Assert.That(nodeHandled.Order(StringComparer.Ordinal).ToArray())
            .IsEquivalentTo(JsonSchemaDiff.SupportedKeywords.Order(StringComparer.Ordinal).ToArray());
        await Assert.That(nodeAnnotations.Order(StringComparer.Ordinal).ToArray())
            .IsEquivalentTo(JsonSchemaDiff.AnnotationOnlyKeywords.Order(StringComparer.Ordinal).ToArray());
        await Assert.That(JsonSchemaDiff.SupportedKeywords).Contains("definitions");
        await Assert.That(JsonSchemaDiff.SupportedKeywords).Contains("$defs");
    }

    private static HashSet<string> ReadNodeKeywordSet(string script, string constName)
    {
        var array = Regex.Match(
            script,
            $@"const\s+{Regex.Escape(constName)}\s*=\s*new\s+Set\(\s*\[(?<body>[^\]]*)\]\s*\)",
            RegexOptions.Singleline);
        if (!array.Success)
        {
            return [];
        }

        return Regex.Matches(array.Groups["body"].Value, "\"(?<keyword>[^\"]+)\"")
            .Select(match => match.Groups["keyword"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    // -------------------------------------------------------------------------
    // Breaking-change allowlist. A pre-1.0 minor bump permits EVERY narrowing, so
    // the version increment on its own makes the gate unfailable: a PR that
    // narrows the wire contract sails through as long as ContractsVersion is
    // already ahead of the published baseline. The increment is therefore
    // necessary but not sufficient — each BREAKING change must also be named by
    // an allowlist entry.
    // -------------------------------------------------------------------------

    /// <summary>The schema that removes <c>size</c> from <see cref="BaseSchema"/> —
    /// one BREAKING change under a permitting version increment.</summary>
    private const string NarrowedSchema =
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

    /// <summary>The exact triple the classifier reports for
    /// <see cref="NarrowedSchema"/>. An allowlist entry must match all three.</summary>
    private static (string File, string Path, string Kind) SizeRemoved =>
        ("Widget.json", "$", "property 'size' was removed");

    /// <summary>
    /// A narrowing under a permitting pre-1.0 minor bump with NO allowlist entry
    /// must FAIL. This is the kill fixture for the defect: before the allowlist
    /// gate this fixture exited 0 and the gate could not fail for a product reason.
    /// </summary>
    [Test]
    public async Task SchemaDiff_NodeGate_PermittedNarrowingWithoutAllowlistEntry_Fails()
    {
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), NarrowedSchema);
            var allowlist = await WriteAllowlistAsync(nextDirectory);

            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "0.4.0",
                "0.5.0",
                allowlist);

            await Assert.That(result.ExitCode).IsEqualTo(1).Because(result.Output);
            await Assert.That(result.Output).Contains("NOT accepted by the breaking-change");
            await Assert.That(result.Output).Contains("and a Contracts CHANGELOG line");
            await Assert.That(result.Output).Contains(SizeRemoved.Kind);
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Omitting <c>--allowlist</c> entirely is the same posture as an empty one:
    /// nothing can match, so a permitted narrowing still fails. The gate is
    /// fail-closed rather than opt-in.
    /// </summary>
    [Test]
    public async Task SchemaDiff_NodeGate_PermittedNarrowingWithNoAllowlistFlag_Fails()
    {
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), NarrowedSchema);

            var result = await RunNodeSchemaDiff(previousDirectory, nextDirectory, "0.4.0", "0.5.0");

            await Assert.That(result.ExitCode).IsEqualTo(1).Because(result.Output);
            await Assert.That(result.Output)
                .Contains("src/Strategos.Contracts/schemas/breaking-changes.allowlist.json");
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>A matching entry accepts the narrowing and is echoed in the log.</summary>
    [Test]
    public async Task SchemaDiff_NodeGate_PermittedNarrowingWithMatchingAllowlistEntry_Passes()
    {
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), NarrowedSchema);
            var allowlist = await WriteAllowlistAsync(
                nextDirectory,
                (SizeRemoved.File, SizeRemoved.Path, SizeRemoved.Kind, "0.5.0"));

            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "0.4.0",
                "0.5.0",
                allowlist);

            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
            await Assert.That(result.Output).Contains("[ALLOWED]");
            await Assert.That(result.Output).Contains(SizeRemoved.Kind);
            await Assert.That(result.Output).Contains("accepted by an entry in");
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Matching is exact, not substring: an entry that names the right file and
    /// version but a near-miss <c>path</c> or <c>kind</c> accepts nothing. Without
    /// this the allowlist would be a wildcard.
    /// </summary>
    [Test]
    [Arguments("$.properties[\"size\"]", "property 'size' was removed")]
    [Arguments("$", "property 'size' was")]
    [Arguments("$", "'size' was removed")]
    public async Task SchemaDiff_NodeGate_NearMissAllowlistEntry_DoesNotMatch(
        string entryPath,
        string entryKind)
    {
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), NarrowedSchema);
            var allowlist = await WriteAllowlistAsync(
                nextDirectory,
                (SizeRemoved.File, entryPath, entryKind, "0.5.0"));

            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "0.4.0",
                "0.5.0",
                allowlist);

            await Assert.That(result.ExitCode).IsEqualTo(1).Because(result.Output);
            await Assert.That(result.Output).Contains("NOT accepted by the breaking-change");
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>
    /// An entry whose <c>version</c> falls outside the compared
    /// <c>(previous, candidate]</c> window is stale: it describes a narrowing that
    /// is already baked into the baseline (or not yet shipped), so it accepts
    /// nothing and is reported as a NOTICE. A stale entry on its own does not fail
    /// the gate — it tells the maintainer the line can be pruned.
    /// </summary>
    [Test]
    public async Task SchemaDiff_NodeGate_StaleAllowlistEntry_IsNoticedAndAcceptsNothing()
    {
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), BaseSchema);

            // 0.3.0 is below the compared window's lower bound: already published.
            var allowlist = await WriteAllowlistAsync(
                nextDirectory,
                (SizeRemoved.File, SizeRemoved.Path, SizeRemoved.Kind, "0.3.0"));

            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "0.4.0",
                "0.5.0",
                allowlist);

            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
            await Assert.That(result.Output).Contains("stale allowlist entry");
            await Assert.That(result.Output).Contains("outside the compared window");
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>A stale entry cannot accept a live narrowing.</summary>
    [Test]
    public async Task SchemaDiff_NodeGate_StaleAllowlistEntry_CannotAcceptANarrowing()
    {
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), NarrowedSchema);
            var allowlist = await WriteAllowlistAsync(
                nextDirectory,
                (SizeRemoved.File, SizeRemoved.Path, SizeRemoved.Kind, "0.3.0"));

            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "0.4.0",
                "0.5.0",
                allowlist);

            await Assert.That(result.ExitCode).IsEqualTo(1).Because(result.Output);
            await Assert.That(result.Output).Contains("stale allowlist entry");
            await Assert.That(result.Output).Contains("NOT accepted by the breaking-change");
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>
    /// The merge-base arm compares a base and head <c>ContractsVersion</c> that are
    /// usually equal. <c>--allow-equal-versions</c> lets that comparison run rather
    /// than short-circuiting on "must be greater"; it does NOT relax the breaking
    /// rule — with equal versions no increment permits a narrowing, so a BREAKING
    /// change still exits 1 even with a matching allowlist entry.
    /// </summary>
    [Test]
    public async Task SchemaDiff_NodeGate_EqualVersionsEvaluateChangesAndFailOnBreaking()
    {
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), NarrowedSchema);
            var allowlist = await WriteAllowlistAsync(
                nextDirectory,
                (SizeRemoved.File, SizeRemoved.Path, SizeRemoved.Kind, "0.12.0"));

            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "0.12.0",
                "0.12.0",
                allowlist,
                allowEqualVersions: true);

            await Assert.That(result.ExitCode).IsEqualTo(1).Because(result.Output);
            await Assert.That(result.Output).Contains(SizeRemoved.Kind);
            await Assert.That(result.Output).Contains("requires a MINOR");
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>An additive change at an unmoved version passes the merge-base arm.</summary>
    [Test]
    public async Task SchemaDiff_NodeGate_EqualVersionsPassWhenNothingNarrows()
    {
        const string widened =
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
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), widened);

            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "0.12.0",
                "0.12.0",
                allowlistPath: null,
                allowEqualVersions: true);

            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
            await Assert.That(result.Output).Contains("all non-breaking");
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>A malformed allowlist is INDETERMINATE, never a silent pass.</summary>
    [Test]
    [Arguments("{ \"entries\": [ { \"file\": \"Widget.json\" } ] }", "missing a non-empty string")]
    [Arguments("{ \"entries\": \"nope\" }", "must be a JSON array of entries")]
    [Arguments("not json", "is not valid JSON")]
    public async Task SchemaDiff_NodeGate_MalformedAllowlist_IsIndeterminate(
        string allowlistBody,
        string expectedOutput)
    {
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), NarrowedSchema);
            var allowlistPath = Path.Combine(nextDirectory, "breaking-changes.allowlist.json");
            await File.WriteAllTextAsync(allowlistPath, allowlistBody);

            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "0.4.0",
                "0.5.0",
                allowlistPath);

            await Assert.That(result.ExitCode).IsEqualTo(2).Because(result.Output);
            await Assert.That(result.Output).Contains("INDETERMINATE");
            await Assert.That(result.Output).Contains(expectedOutput);
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>
    /// The allowlist ships inside <c>schemas/</c> (and therefore inside the package
    /// content the baseline is extracted from), but it is policy data, not a
    /// schema: diffing it would make every allowlist edit register as a schema
    /// change and eventually deadlock the gate against itself.
    /// </summary>
    [Test]
    public async Task SchemaDiff_NodeGate_AllowlistFileIsNotDiffedAsASchema()
    {
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), BaseSchema);
            await File.WriteAllTextAsync(
                Path.Combine(previousDirectory, "breaking-changes.allowlist.json"),
                "{ \"entries\": [] }");
            var allowlist = await WriteAllowlistAsync(
                nextDirectory,
                (SizeRemoved.File, SizeRemoved.Path, SizeRemoved.Kind, "0.5.0"));

            var result = await RunNodeSchemaDiff(
                previousDirectory,
                nextDirectory,
                "0.4.0",
                "0.5.0",
                allowlist);

            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
            await Assert.That(result.Output).DoesNotContain("breaking-changes.allowlist.json: $:");
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    /// <summary>The repository's own allowlist parses and every entry is well-formed.</summary>
    [Test]
    public async Task SchemaDiff_RepositoryAllowlist_IsWellFormed()
    {
        var allowlistPath = Path.Combine(
            RepoLayout.ContractsProjectDir,
            "schemas",
            "breaking-changes.allowlist.json");

        await Assert.That(File.Exists(allowlistPath)).IsTrue()
            .Because($"the gate's allowlist must exist at {allowlistPath}");

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(allowlistPath));
        var entries = document.RootElement.GetProperty("entries");
        await Assert.That(entries.GetArrayLength()).IsGreaterThan(0);

        foreach (var entry in entries.EnumerateArray())
        {
            foreach (var field in new[] { "file", "path", "kind", "version", "reason" })
            {
                await Assert.That(entry.TryGetProperty(field, out var value)).IsTrue()
                    .Because($"every allowlist entry needs a '{field}'");
                await Assert.That(value.GetString()).IsNotNullOrWhiteSpace();
            }

            await Assert.That(entry.GetProperty("version").GetString())
                .Matches(@"^\d+\.\d+\.\d+$");
        }
    }

    // -------------------------------------------------------------------------
    // Conditional applicators. `if` / `then` / `else` / `dependentSchemas` /
    // `dependentRequired` make a document's validity depend on its own shape, so
    // adding or changing one can only reject documents the previous schema
    // accepted. This is the shape CompensationConfiguration takes when the
    // AGWF044 rule (a typed inverseAction requires requiredOnFailure = true) moves
    // from the C# analyzer onto the wire.
    // -------------------------------------------------------------------------

    private const string ConditionalBaseSchema =
        """
        {
          "$id": "CompensationConfiguration.json",
          "type": "object",
          "properties": {
            "compensationStepType": { "type": "string" },
            "inverseAction": { "$ref": "ActionReferenceV1.json" },
            "requiredOnFailure": { "type": "boolean" }
          },
          "required": ["compensationStepType"]
        }
        """;

    private const string ConditionalNarrowedSchema =
        """
        {
          "$id": "CompensationConfiguration.json",
          "type": "object",
          "properties": {
            "compensationStepType": { "type": "string" },
            "inverseAction": { "$ref": "ActionReferenceV1.json" },
            "requiredOnFailure": { "type": "boolean" }
          },
          "required": ["compensationStepType"],
          "if": { "required": ["inverseAction"] },
          "then": { "properties": { "requiredOnFailure": { "const": true } } }
        }
        """;

    /// <summary>Adding a conditional rejects documents the previous schema accepted.</summary>
    [Test]
    public async Task SchemaDiff_AddedConditional_IsBreakingInBothClassifiers()
    {
        var result = JsonSchemaDiff.Compare(ConditionalBaseSchema, ConditionalNarrowedSchema);

        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.Breaking);
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.Breaking
                && c.Description.Contains("'if' conditional was added or narrowed", StringComparison.Ordinal));
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.Breaking
                && c.Description.Contains("'then' conditional was added or narrowed", StringComparison.Ordinal));

        var gate = await RunNodeSchemaDiffOnPair(
            ConditionalBaseSchema,
            ConditionalNarrowedSchema,
            "1.2.3",
            "1.2.4");

        await Assert.That(gate.ExitCode).IsEqualTo(1).Because(gate.Output);
        await Assert.That(gate.Output).Contains("'if' conditional was added or narrowed");
        await Assert.That(gate.Output).Contains("'then' conditional was added or narrowed");
        await Assert.That(gate.Output).DoesNotContain("safety cannot be proven");
    }

    /// <summary>Removing a conditional only widens the accepted set.</summary>
    [Test]
    public async Task SchemaDiff_RemovedConditional_IsNonBreakingInBothClassifiers()
    {
        var result = JsonSchemaDiff.Compare(ConditionalNarrowedSchema, ConditionalBaseSchema);

        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.NonBreaking);
        await Assert.That(result.Changes)
            .Contains(c => c.Severity == ChangeSeverity.NonBreaking
                && c.Description.Contains("'if' conditional was removed", StringComparison.Ordinal));

        var gate = await RunNodeSchemaDiffOnPair(
            ConditionalNarrowedSchema,
            ConditionalBaseSchema,
            "1.2.3",
            "1.2.4");

        await Assert.That(gate.ExitCode).IsEqualTo(0).Because(gate.Output);
        await Assert.That(gate.Output).Contains("'if' conditional was removed");
    }

    /// <summary>Changing a conditional's body is a narrowing, not an annotation.</summary>
    [Test]
    public async Task SchemaDiff_ChangedConditional_IsBreakingInBothClassifiers()
    {
        const string retightened =
            """
            {
              "$id": "CompensationConfiguration.json",
              "type": "object",
              "properties": {
                "compensationStepType": { "type": "string" },
                "inverseAction": { "$ref": "ActionReferenceV1.json" },
                "requiredOnFailure": { "type": "boolean" }
              },
              "required": ["compensationStepType"],
              "if": { "required": ["inverseAction", "requiredOnFailure"] },
              "then": { "properties": { "requiredOnFailure": { "const": true } } }
            }
            """;

        var result = JsonSchemaDiff.Compare(ConditionalNarrowedSchema, retightened);

        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.Breaking);

        var gate = await RunNodeSchemaDiffOnPair(
            ConditionalNarrowedSchema,
            retightened,
            "1.2.3",
            "1.2.4");

        await Assert.That(gate.ExitCode).IsEqualTo(1).Because(gate.Output);
        await Assert.That(gate.Output).Contains("'if' conditional was added or narrowed");
    }

    /// <summary>The whole conditional family is classified, not just if/then.</summary>
    [Test]
    [Arguments("else", "{ \"properties\": { \"requiredOnFailure\": { \"const\": false } } }")]
    [Arguments("dependentSchemas", "{ \"inverseAction\": { \"required\": [\"requiredOnFailure\"] } }")]
    [Arguments("dependentRequired", "{ \"inverseAction\": [\"requiredOnFailure\"] }")]
    public async Task SchemaDiff_ConditionalFamily_IsClassifiedInBothClassifiers(
        string keyword,
        string body)
    {
        var next = $$"""
            {
              "$id": "CompensationConfiguration.json",
              "type": "object",
              "properties": {
                "compensationStepType": { "type": "string" },
                "inverseAction": { "$ref": "ActionReferenceV1.json" },
                "requiredOnFailure": { "type": "boolean" }
              },
              "required": ["compensationStepType"],
              "{{keyword}}": {{body}}
            }
            """;

        var result = JsonSchemaDiff.Compare(ConditionalBaseSchema, next);

        await Assert.That(result.Severity).IsEqualTo(ChangeSeverity.Breaking);
        await Assert.That(result.Changes)
            .Contains(c => c.Description.Contains(
                $"'{keyword}' conditional was added or narrowed",
                StringComparison.Ordinal));

        var gate = await RunNodeSchemaDiffOnPair(ConditionalBaseSchema, next, "1.2.3", "1.2.4");

        await Assert.That(gate.ExitCode).IsEqualTo(1).Because(gate.Output);
        await Assert.That(gate.Output).Contains($"'{keyword}' conditional was added or narrowed");
        await Assert.That(gate.Output).DoesNotContain("safety cannot be proven");
    }

    private static async Task<CliResult> RunNodeSchemaDiffOnPair(
        string previous,
        string next,
        string previousVersion,
        string candidateVersion)
    {
        var previousDirectory = Directory.CreateTempSubdirectory("schema-diff-prev-").FullName;
        var nextDirectory = Directory.CreateTempSubdirectory("schema-diff-next-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(previousDirectory, "Widget.json"), previous);
            await File.WriteAllTextAsync(Path.Combine(nextDirectory, "Widget.json"), next);
            return await RunNodeSchemaDiff(previousDirectory, nextDirectory, previousVersion, candidateVersion);
        }
        finally
        {
            Directory.Delete(previousDirectory, recursive: true);
            Directory.Delete(nextDirectory, recursive: true);
        }
    }

    private static Task<CliResult> RunNodeSchemaDiff(
        string previousDirectory,
        string nextDirectory,
        string previousVersion,
        string candidateVersion,
        string? allowlistPath = null,
        bool allowEqualVersions = false)
    {
        var scriptPath = Path.Combine(
            RepoLayout.RepoRoot,
            "scripts",
            "contracts-schema-diff.mjs");
        var arguments =
            $"\"{scriptPath}\" \"{previousDirectory}\" \"{nextDirectory}\" "
            + $"\"{previousVersion}\" \"{candidateVersion}\"";
        if (allowlistPath is not null)
        {
            arguments += $" --allowlist \"{allowlistPath}\"";
        }

        if (allowEqualVersions)
        {
            arguments += " --allow-equal-versions";
        }

        return Cli.RunAsync("node", arguments);
    }

    /// <summary>Writes a breaking-change allowlist document and returns its path.
    /// The caller owns the containing directory.</summary>
    private static async Task<string> WriteAllowlistAsync(
        string directory,
        params (string File, string Path, string Kind, string Version)[] entries)
    {
        var body = string.Join(
            ",\n",
            entries.Select(entry =>
                $$"""
                    {
                      "file": {{JsonSerializer.Serialize(entry.File)}},
                      "path": {{JsonSerializer.Serialize(entry.Path)}},
                      "kind": {{JsonSerializer.Serialize(entry.Kind)}},
                      "version": {{JsonSerializer.Serialize(entry.Version)}},
                      "reason": "fixture"
                    }
                """));
        var allowlistPath = Path.Combine(directory, "breaking-changes.allowlist.json");
        await File.WriteAllTextAsync(allowlistPath, $"{{\n  \"entries\": [\n{body}\n  ]\n}}\n");
        return allowlistPath;
    }
}
