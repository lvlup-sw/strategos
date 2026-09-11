// =============================================================================
// <copyright file="KernelSlotSchemaTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Workflow;

/// <summary>
/// #209 (#193 stage a) — the workflow-definition kernel, frozen as additive
/// slots on the shapes that already exist.
/// <para>
/// This family follows <see cref="GateSlotSchemaTests"/> rather than the #53
/// builder-fixture corpus, and for the same reason: every slot below is
/// wire-only. No builder call sets one, so the builder corpus cannot produce a
/// fixture that carries one, and a corpus entry would assert nothing. The
/// schema is the artifact consumers read, so the schema is what is pinned.
/// </para>
/// <para>
/// Every assertion here is a NEGATIVE as much as a positive: each slot must be
/// absent from <c>required</c>. A kernel slot that became required would narrow
/// the wire, and a narrowing in this issue is a design error rather than an
/// allowlist entry.
/// </para>
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class KernelSlotSchemaTests
{
    private const string WorkflowRootSchema = "WorkflowDefinitionV1";

    /// <summary>The five step arms that spread the shared step common.</summary>
    private static readonly string[] StepArms =
    [
        "SkillStep",
        "HandlerStep",
        "GateStep",
        "DelegateStep",
        "ApprovalStep",
    ];

    /// <summary>
    /// The kernel's <c>identity.contentHash</c>. A lowercase-hex SHA-256 over the
    /// definition's structural fields, constrained by pattern so a producer
    /// cannot stamp an arbitrary string and call it a hash.
    /// </summary>
    [Test]
    public async Task WorkflowRoot_HasOptionalContentHash_PinnedToLowercaseSha256()
    {
        var root = await LoadAsync(WorkflowRootSchema);
        var slot = await RequireOptionalPropertyAsync(root, "contentHash", WorkflowRootSchema);

        await Assert.That(slot.GetProperty("type").GetString()).IsEqualTo("string")
            .Because("`contentHash` is a hex digest string, not a structured object.");

        await Assert.That(slot.TryGetProperty("pattern", out var pattern)).IsTrue()
            .Because("an unconstrained `contentHash` would accept any string as a digest.");
        await Assert.That(pattern.GetString()).IsEqualTo("^[0-9a-f]{64}$")
            .Because("the digest is SHA-256 in LOWERCASE hex, matching Convert.ToHexStringLower "
                + "in OntologyGraphHasher — a mixed-case digest of the same bytes must not "
                + "compare as a different hash.");
    }

    /// <summary>
    /// The kernel's <c>authority { … }</c> block. Carried, not proved: the slot
    /// exists so the semantic plane shares one vocabulary from birth.
    /// </summary>
    [Test]
    public async Task WorkflowRoot_HasOptionalAuthorityFrame_RefsWorkflowAuthorityV1()
    {
        var root = await LoadAsync(WorkflowRootSchema);
        var slot = await RequireOptionalPropertyAsync(root, "authority", WorkflowRootSchema);

        await Assert.That(RefName(slot)).IsEqualTo("WorkflowAuthorityV1")
            .Because("the frame must $ref the shared model, not inline a loose object.");
    }

    /// <summary>
    /// Every member of the authority frame is optional. The frame is unproved in
    /// 3.0, so a definition that declares one member carries only that member.
    /// </summary>
    [Test]
    public async Task WorkflowAuthority_HasFiveMembers_AllOptional()
    {
        var frame = await LoadAsync("WorkflowAuthorityV1");

        string[] members =
        [
            "invariants",
            "goals",
            "assumptions",
            "delegatedDecisions",
            "escalationBoundaries",
        ];

        foreach (var member in members)
        {
            var slot = await RequireOptionalPropertyAsync(frame, member, "WorkflowAuthorityV1");
            await Assert.That(slot.GetProperty("type").GetString()).IsEqualTo("array")
                .Because($"`{member}` is a list of statements.");
            await Assert.That(RefName(slot.GetProperty("items")))
                .IsEqualTo("WorkflowAuthorityStatementV1")
                .Because($"`{member}` items must $ref the shared statement model. A bare string "
                    + "array could not gain a provenance or check field later without narrowing.");
        }
    }

    /// <summary>
    /// The four step slots land on EVERY arm, because they are spread through the
    /// shared step common. A slot present on one arm and missing from another is
    /// the failure this test exists to catch.
    /// </summary>
    [Test]
    public async Task EveryStepArm_CarriesTheFourKernelSlots_AllOptional()
    {
        foreach (var armName in StepArms)
        {
            var arm = await LoadAsync(armName);

            var completion = await RequireOptionalPropertyAsync(arm, "completion", armName);
            await Assert.That(RefName(completion)).IsEqualTo("ActionGuaranteeV1")
                .Because($"{armName}.completion must reuse the #168 predicate vocabulary. A second "
                    + "predicate vocabulary is the drift #193 exists to prevent.");

            var authority = await RequireOptionalPropertyAsync(arm, "authority", armName);
            await Assert.That(RefName(authority)).IsEqualTo("AuthorityRequirementV1")
                .Because($"{armName}.authority is a lattice COORDINATE, not a capability name list.");

            var inputs = await RequireOptionalPropertyAsync(arm, "inputs", armName);
            await Assert.That(RefName(inputs)).IsEqualTo("TypedContractRefV1")
                .Because($"{armName}.inputs references a schema by $id and never inlines a CLR type (LB-2).");

            var outputs = await RequireOptionalPropertyAsync(arm, "outputs", armName);
            await Assert.That(RefName(outputs)).IsEqualTo("TypedContractRefV1")
                .Because($"{armName}.outputs references a schema by $id and never inlines a CLR type (LB-2).");
        }
    }

    /// <summary>
    /// The authority lattice crosses the wire. Before 0.13.0 only the
    /// <c>x-strategos-authority</c> NAME string did, so a consumer holding two
    /// authorities could not tell whether one dominated the other.
    /// </summary>
    [Test]
    public async Task AuthorityLattice_CarriesAxesAndAuthorities_WithOrderedLevels()
    {
        var lattice = await LoadAsync("AuthorityLatticeV1");
        var properties = lattice.GetProperty("properties");

        await Assert.That(RefName(properties.GetProperty("axes").GetProperty("items")))
            .IsEqualTo("AuthorityAxisV1");
        await Assert.That(RefName(properties.GetProperty("authorities").GetProperty("items")))
            .IsEqualTo("AuthorityDescriptorV1");

        // Both are REQUIRED: a lattice missing either half decides nothing.
        var required = RequiredNames(lattice);
        await Assert.That(required).Contains("axes")
            .Because("a lattice with no axes has no order to read.");
        await Assert.That(required).Contains("authorities")
            .Because("a lattice with no authorities places nothing in the order.");

        // A coordinate is an ORDERED ARRAY of pairs, never a map. A JSON object has
        // no canonical member order, so a map lets two producers emit two different
        // contentHash values for one definition — and #204 compares those digests.
        var requirement = await LoadAsync("AuthorityRequirementV1");
        var coordinates = requirement.GetProperty("properties").GetProperty("coordinates");
        await Assert.That(coordinates.GetProperty("type").GetString()).IsEqualTo("array")
            .Because("a coordinate map would make contentHash non-deterministic across producers.");
        await Assert.That(RefName(coordinates.GetProperty("items")))
            .IsEqualTo("AuthorityCoordinateV1")
            .Because("coordinate items must be the shared axis/level pair model.");

        // The asymmetry is deliberate. A REQUIREMENT that demands nothing is spelled
        // by omitting the optional `authority` field, so an empty array would be a
        // second spelling of one fact. A DESCRIPTOR sitting at the bottom of every
        // axis is meaningful, so its list is legitimately empty.
        await Assert.That(coordinates.GetProperty("minItems").GetInt32()).IsEqualTo(1)
            .Because("an empty requirement coordinate duplicates an omitted authority field.");
        var descriptor = await LoadAsync("AuthorityDescriptorV1");
        await Assert.That(descriptor.GetProperty("properties").GetProperty("coordinates")
            .TryGetProperty("minItems", out _)).IsFalse()
            .Because("an authority at the bottom of every axis has an empty coordinate list.");

        var axis = await LoadAsync("AuthorityAxisV1");
        await Assert.That(axis.GetProperty("properties").GetProperty("levels")
            .GetProperty("type").GetString()).IsEqualTo("array")
            .Because("levels are an ORDERED array (weakest to strongest), never a set — "
                + "the order is the total order the dominance test reads.");
    }

    /// <summary>
    /// The action contract as a document, carrying the action calculus's own
    /// seven members. <c>AcceptsType</c> / <c>ReturnsType</c> stay unprojected.
    /// </summary>
    [Test]
    public async Task ActionContract_CarriesTheCalculusVocabulary_AndNoClrTypes()
    {
        var contract = await LoadAsync("ActionContractV1");
        var properties = contract.GetProperty("properties");

        await Assert.That(RefName(properties.GetProperty("subject"))).IsEqualTo("ActionSubjectV1");
        await Assert.That(RefName(properties.GetProperty("requires").GetProperty("items")))
            .IsEqualTo("ActionRequirementV1");
        await Assert.That(RefName(properties.GetProperty("ensures").GetProperty("items")))
            .IsEqualTo("ActionGuaranteeV1");
        await Assert.That(RefName(properties.GetProperty("touches").GetProperty("items")))
            .IsEqualTo("ActionResourceV1");
        await Assert.That(RefName(properties.GetProperty("authority")))
            .IsEqualTo("AuthorityRequirementV1");
        await Assert.That(RefName(properties.GetProperty("inverse")))
            .IsEqualTo("ActionReferenceV1");
        await Assert.That(properties.GetProperty("idempotent").GetProperty("type").GetString())
            .IsEqualTo("boolean");

        // INV-8 / LB-2: no CLR handle is projected, under any name.
        foreach (var forbidden in new[] { "acceptsType", "returnsType", "clrType" })
        {
            await Assert.That(properties.TryGetProperty(forbidden, out _)).IsFalse()
                .Because($"`{forbidden}` would put a CLR type handle on a language-neutral wire.");
        }
    }

    /// <summary>
    /// The #204 manifest root. Frozen here so #204 is emission, consumption and
    /// proof with no further Contracts change.
    /// </summary>
    [Test]
    public async Task ProofCatalog_PinsSchemaVersion_AndCarriesTheThreeCatalogs()
    {
        var catalog = await LoadAsync("ProofCatalogV1");
        var properties = catalog.GetProperty("properties");

        // schemaVersion is a pinned const, matching the WorkflowDefinitionV1 posture:
        // an unknown version must be a rejection, not a best-effort parse.
        var schemaVersion = properties.GetProperty("schemaVersion");
        await Assert.That(schemaVersion.TryGetProperty("const", out var pinned)).IsTrue()
            .Because("an unpinned schemaVersion cannot fail closed on an unknown manifest.");
        await Assert.That(pinned.GetString()).IsEqualTo("1.0");

        await Assert.That(RequiredNames(catalog)).Contains("catalogId")
            .Because("a manifest with no ordinal identity cannot be merged or de-duplicated.");

        await Assert.That(RefName(properties.GetProperty("actions"))).IsEqualTo("ActionCatalogV1");
        await Assert.That(RefName(properties.GetProperty("authorities").GetProperty("items")))
            .IsEqualTo("DomainAuthorityLatticeV1");
        await Assert.That(RefName(properties.GetProperty("workflows").GetProperty("items")))
            .IsEqualTo("WorkflowDefinitionV1");

        await Assert.That(properties.GetProperty("contentHash").GetProperty("pattern").GetString())
            .IsEqualTo("^[0-9a-f]{64}$")
            .Because("the manifest digest uses the same lowercase-hex SHA-256 form as the "
                + "definition digest — #204 compares them for skew.");
    }

    /// <summary>
    /// The reserved composition-combinator kinds add NO enum tokens. An enum
    /// token with no implementation is a consumer-breaking payload under the
    /// strict converter: a producer emits it, every 0.13.0 consumer throws.
    /// </summary>
    [Test]
    public async Task ReservedCombinatorKinds_AddNoInertEnumTokens()
    {
        var bundlePath = Path.Combine(
            RepoLayout.ContractsProjectDir, "schemas", "workflow-definition-v1.schema.json");
        var bundle = await File.ReadAllTextAsync(bundlePath);

        foreach (var reserved in new[]
        {
            "\"host-continuation\"",
            "\"hostContinuation\"",
            "\"compensationCombinator\"",
        })
        {
            await Assert.That(bundle.Contains(reserved, StringComparison.Ordinal)).IsFalse()
                .Because($"{reserved} is RESERVED structurally in docs/architecture/kernel-v1.md, "
                    + "not as a wire token. An inert token would be emitted by a producer and "
                    + "rejected by every strict consumer converter.");
        }
    }

    private static async Task<JsonElement> LoadAsync(string schemaName)
    {
        await Assert.That(EventSchemas.Exists(schemaName)).IsTrue()
            .Because($"`tsp compile` must emit {schemaName}.json (run scripts/contracts-codegen.sh).");

        return await EventSchemas.LoadAsync(schemaName);
    }

    private static async Task<JsonElement> RequireOptionalPropertyAsync(
        JsonElement schema,
        string propertyName,
        string schemaName)
    {
        await Assert.That(schema.TryGetProperty("properties", out var properties)).IsTrue()
            .Because($"{schemaName} must declare properties.");
        await Assert.That(properties.TryGetProperty(propertyName, out var slot)).IsTrue()
            .Because($"{schemaName} must carry the kernel slot `{propertyName}`.");

        await Assert.That(RequiredNames(schema)).DoesNotContain(propertyName)
            .Because($"`{propertyName}` is additive and OPTIONAL. Requiring it would narrow the "
                + "wire, and a narrowing in #209 is a design error, not an allowlist entry.");

        return slot;
    }

    private static string? RefName(JsonElement slot) =>
        slot.TryGetProperty("$ref", out var reference)
            ? Path.GetFileNameWithoutExtension(reference.GetString())
            : null;

    private static IReadOnlyList<string> RequiredNames(JsonElement root)
    {
        if (root.TryGetProperty("required", out var required)
            && required.ValueKind == JsonValueKind.Array)
        {
            return required.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
        }

        return Array.Empty<string>();
    }
}
