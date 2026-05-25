// =============================================================================
// <copyright file="WorkflowCatalogTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Collections;

using Strategos.Contracts;
using Strategos.Contracts.Generated;

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// S4 (issue #65/#66) — the <c>WorkflowCatalog</c> manifest and the consumer-time
/// catalog-ref resolver. The manifest pins a <c>CatalogVersion</c> and a list of
/// entries, each pairing a <c>(WorkflowId, CatalogVersion)</c> with a
/// <c>WorkflowDefinitionV1</c> IR payload. The resolver validates a catalog
/// <c>WorkflowRef</c> against a given manifest.
/// </summary>
[Property("Category", "Pipeline")]
public sealed class WorkflowCatalogTests
{
    /// <summary>
    /// Asserts the manifest carries <c>CatalogVersion</c> and a read-only list of
    /// <c>Entries</c>, each entry pairing identity fields with a
    /// <c>WorkflowDefinitionV1</c>.
    /// </summary>
    [Test]
    public async Task WorkflowCatalog_Manifest_CarriesVersionAndEntries()
    {
        var manifest = IntentEnvelopeTests.ResolveGenerated("WorkflowCatalog");
        await Assert.That(manifest).IsNotNull()
            .Because("the WorkflowCatalog manifest must be generated.");
        await Assert.That(manifest!.IsSealed).IsTrue();

        var version = manifest.GetProperty("CatalogVersion");
        await Assert.That(version).IsNotNull();
        await Assert.That(version!.PropertyType).IsEqualTo(typeof(string));
        await Assert.That(IntentEnvelopeTests.IsInitOnly(version)).IsTrue();

        var entries = manifest.GetProperty("Entries");
        await Assert.That(entries).IsNotNull();
        await Assert.That(typeof(IEnumerable).IsAssignableFrom(entries!.PropertyType)).IsTrue();
        await Assert.That(entries.PropertyType.IsGenericType
                && entries.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            .IsTrue()
            .Because("Entries must be an IReadOnlyList<T> (INV-7).");

        var entryType = entries.PropertyType.GetGenericArguments()[0];
        await Assert.That(entryType.Name).IsEqualTo("WorkflowCatalogEntry");

        var entryWorkflowId = entryType.GetProperty("WorkflowId");
        await Assert.That(entryWorkflowId).IsNotNull();
        await Assert.That(entryWorkflowId!.PropertyType).IsEqualTo(typeof(string));

        var entryVersion = entryType.GetProperty("CatalogVersion");
        await Assert.That(entryVersion).IsNotNull();
        await Assert.That(entryVersion!.PropertyType).IsEqualTo(typeof(string));

        var entryDef = entryType.GetProperty("Definition");
        await Assert.That(entryDef).IsNotNull()
            .Because("each entry must carry a WorkflowDefinitionV1 IR payload.");
        await Assert.That(entryDef!.PropertyType.Name).IsEqualTo("WorkflowDefinitionV1");
    }

    /// <summary>
    /// Asserts the consumer-time resolver validates a catalog <c>WorkflowRef</c>
    /// against a manifest: a present <c>(WorkflowId, CatalogVersion)</c> pair
    /// resolves to its entry; an absent pair (unknown version) fails resolution.
    /// </summary>
    [Test]
    public async Task CatalogRef_ValidatesAgainstManifest_RejectsUnknownVersion()
    {
        var def = new WorkflowDefinitionV1
        {
            SchemaVersion = "1.0",
            Name = "merge-gate",
            Steps = [],
            Transitions = [],
            BranchPoints = [],
            Loops = [],
            ForkPoints = [],
            FailureHandlers = [],
            ApprovalPoints = [],
        };

        var catalog = new WorkflowCatalog
        {
            CatalogVersion = "1.4.0",
            Entries =
            [
                new WorkflowCatalogEntry
                {
                    WorkflowId = "merge-gate",
                    CatalogVersion = "1.4.0",
                    Definition = def,
                },
            ],
        };

        // Present pair resolves to the entry.
        var present = new CatalogWorkflowRef { WorkflowId = "merge-gate", CatalogVersion = "1.4.0" };
        var hit = WorkflowCatalogResolver.Resolve(catalog, present);
        await Assert.That(hit.Resolved).IsTrue()
            .Because("a (workflowId, catalogVersion) pair present in the manifest must resolve.");
        await Assert.That(hit.Entry).IsNotNull();
        await Assert.That(hit.Entry!.Definition.Name).IsEqualTo("merge-gate");

        // Unknown version fails resolution.
        var unknownVersion = new CatalogWorkflowRef { WorkflowId = "merge-gate", CatalogVersion = "9.9.9" };
        var miss = WorkflowCatalogResolver.Resolve(catalog, unknownVersion);
        await Assert.That(miss.Resolved).IsFalse()
            .Because("a catalogVersion absent from the manifest must NOT resolve.");
        await Assert.That(miss.Entry).IsNull();

        // Unknown workflowId fails resolution too.
        var unknownId = new CatalogWorkflowRef { WorkflowId = "nope", CatalogVersion = "1.4.0" };
        await Assert.That(WorkflowCatalogResolver.Resolve(catalog, unknownId).Resolved).IsFalse();
    }
}
