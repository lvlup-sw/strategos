// =============================================================================
// <copyright file="WorkflowRefTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// S3 (issue #66) — the <c>WorkflowRef</c> discriminated union. A reference to a
/// workflow journey is either a <c>catalog</c> variant (a pinned
/// <c>workflowId</c> + <c>catalogVersion</c> resolvable against a
/// <c>WorkflowCatalog</c>) or an <c>authored</c> variant (a free-form
/// <c>journeyDescription</c>, operationally deferred). String monikers only — no
/// CLR type names cross the wire (INV-8 polyglot identity).
/// </summary>
[Property("Category", "Pipeline")]
public class WorkflowRefTests
{
    /// <summary>
    /// Asserts the <c>catalog</c> variant is a sealed record carrying
    /// <c>WorkflowId</c> and <c>CatalogVersion</c>, discriminated on
    /// <c>kind == "catalog"</c>, and round-trips through the polymorphic base.
    /// </summary>
    [Test]
    public async Task WorkflowRef_CatalogVariant_CarriesWorkflowIdAndCatalogVersion()
    {
        var baseType = IntentEnvelopeTests.ResolveGenerated("WorkflowRef");
        await Assert.That(baseType).IsNotNull()
            .Because("the WorkflowRef discriminated-union base must be generated.");
        await Assert.That(baseType!.IsAbstract).IsTrue()
            .Because("the union base is an abstract polymorphic record (INV-6).");

        var catalog = IntentEnvelopeTests.ResolveGenerated("CatalogWorkflowRef");
        await Assert.That(catalog).IsNotNull()
            .Because("the catalog variant CatalogWorkflowRef must be generated.");
        await Assert.That(catalog!.IsSealed).IsTrue();
        await Assert.That(baseType.IsAssignableFrom(catalog)).IsTrue()
            .Because("CatalogWorkflowRef must derive from WorkflowRef.");

        var workflowId = catalog.GetProperty("WorkflowId");
        await Assert.That(workflowId).IsNotNull();
        await Assert.That(workflowId!.PropertyType).IsEqualTo(typeof(string));
        await Assert.That(IntentEnvelopeTests.IsInitOnly(workflowId)).IsTrue();

        var catalogVersion = catalog.GetProperty("CatalogVersion");
        await Assert.That(catalogVersion).IsNotNull();
        await Assert.That(catalogVersion!.PropertyType).IsEqualTo(typeof(string));
        await Assert.That(IntentEnvelopeTests.IsInitOnly(catalogVersion)).IsTrue();

        // Round-trips polymorphically on the kind == "catalog" discriminator.
        var instance = Activator.CreateInstance(catalog)!;
        catalog.GetProperty("WorkflowId")!.SetValue(instance, "merge-gate");
        catalog.GetProperty("CatalogVersion")!.SetValue(instance, "1.4.0");
        var json = JsonSerializer.Serialize(instance, baseType, ContractsJson.Options);
        await Assert.That(json).Contains("\"kind\": \"catalog\"")
            .Because($"the catalog variant must carry its discriminator: {json}");

        var back = JsonSerializer.Deserialize(json, baseType, ContractsJson.Options);
        await Assert.That(back!.GetType()).IsEqualTo(catalog)
            .Because("deserialization must route to CatalogWorkflowRef by kind.");
    }

    /// <summary>
    /// Asserts the <c>authored</c> variant is a sealed record carrying a
    /// <c>JourneyDescription</c>, discriminated on <c>kind == "authored"</c>. The
    /// type exists now; no consumer exercises it (operationally deferred).
    /// </summary>
    [Test]
    public async Task WorkflowRef_AuthoredVariant_CarriesJourneyDescription()
    {
        var baseType = IntentEnvelopeTests.ResolveGenerated("WorkflowRef");
        await Assert.That(baseType).IsNotNull();

        var authored = IntentEnvelopeTests.ResolveGenerated("AuthoredWorkflowRef");
        await Assert.That(authored).IsNotNull()
            .Because("the authored variant AuthoredWorkflowRef must be generated.");
        await Assert.That(authored!.IsSealed).IsTrue();
        await Assert.That(baseType!.IsAssignableFrom(authored)).IsTrue();

        var description = authored.GetProperty("JourneyDescription");
        await Assert.That(description).IsNotNull();
        await Assert.That(description!.PropertyType).IsEqualTo(typeof(string));
        await Assert.That(IntentEnvelopeTests.IsInitOnly(description)).IsTrue();

        var instance = Activator.CreateInstance(authored)!;
        authored.GetProperty("JourneyDescription")!.SetValue(instance, "spike the new gate flow");
        var json = JsonSerializer.Serialize(instance, baseType, ContractsJson.Options);
        await Assert.That(json).Contains("\"kind\": \"authored\"")
            .Because($"the authored variant must carry its discriminator: {json}");

        var back = JsonSerializer.Deserialize(json, baseType, ContractsJson.Options);
        await Assert.That(back!.GetType()).IsEqualTo(authored);
    }
}
