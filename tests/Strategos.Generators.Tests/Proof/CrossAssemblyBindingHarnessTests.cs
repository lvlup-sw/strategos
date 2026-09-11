// -----------------------------------------------------------------------
// <copyright file="CrossAssemblyBindingHarnessTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Proof;

/// <summary>
/// The cross-assembly binding proof (#204): what happens when a contract and the
/// workflow it binds are compiled into different assemblies.
/// </summary>
/// <remarks>
/// <para>
/// Both halves of a workflow binding are legal, ordinary C#. Which half lives in
/// which assembly is a layout choice, invisible to the author of either half — and
/// before #204 it silently decided whether the #167 guarantee applied at all: the
/// consuming compilation walked its own syntax trees, found no binding, and built
/// clean with nothing proved and nothing said.
/// </para>
/// <para>
/// The obligation now travels with the contract. The declaring assembly exports a
/// portable catalog and defers; the assembly that lowers the workflow reads it and
/// discharges the obligation there. Every test below is the same two assemblies —
/// what changes is which one is behind the boundary, and what is wrong.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public sealed class CrossAssemblyBindingHarnessTests
{
    /// <summary>The harness produces a real referenceable image whose types the consumer binds against.</summary>
    /// <remarks>
    /// Asserted first and on its own, because several tests here read a NEGATIVE
    /// result. If the producer silently emitted nothing, or the consumer silently
    /// failed to reference it, "no diagnostic was reported" would be true for the
    /// wrong reason and those tests would pass while proving nothing.
    /// </remarks>
    [Test]
    public async Task Harness_CompilesAProducerTheConsumerActuallyBindsAgainst()
    {
        var producer = TwoAssemblyHarness.CompileProducer("OntologyProducer", OntologyAssembly());

        await Assert.That(producer.Image.Length).IsGreaterThan(0)
            .Because("a producer that emitted no image cannot be referenced by anything.");

        var symbol = TwoAssemblyHarness.ResolveSymbol(producer);
        await Assert.That(symbol.GetTypeByMetadataName("CrossAssembly.Ontology.OrdersOntology"))
            .IsNotNull()
            .Because("the consumer must see the producer's ontology type through metadata alone.");

        var consumer = TwoAssemblyHarness.CompileConsumer(WorkflowAssembly, producer);
        await Assert.That(consumer.OutputCompilation.GetTypeByMetadataName("CrossAssembly.Ontology.Order"))
            .IsNotNull()
            .Because("the consumer compilation must genuinely resolve the producer's types.");
    }

    /// <summary>
    /// The declaring assembly exports its contracts and says nothing about a workflow
    /// it cannot see.
    /// </summary>
    /// <remarks>
    /// Before #204 this compilation reported AGWF039 — "resolves to 0 workflow
    /// definitions" — which the project file then silenced, because there was no
    /// other way to ship an ontology whose workflows live elsewhere. Exporting the
    /// contract is that other way: the binding is deferred to whoever lowers the
    /// workflow, not dropped, so nothing needs silencing and the id no longer has to
    /// stay configurable.
    /// </remarks>
    [Test]
    public async Task DeclaringAssembly_ExportsItsContracts_AndDefersTheBinding()
    {
        var producer = TwoAssemblyHarness.CompileProducer("OntologyProducer", OntologyAssembly());

        await Assert.That(producer.GeneratedHintNames).Contains("StrategosProofCatalog.g.cs")
            .Because("an assembly that declares action contracts exports them.");
        await Assert.That(producer.GeneratorDiagnostics.Select(diagnostic => diagnostic.Id))
            .DoesNotContain("AGWF039")
            .Because("a binding whose contract is exported is deferred, not unresolved.");

        var catalog = producer.Generated("StrategosProofCatalog.g.cs")!;
        await Assert.That(catalog).Contains("boundWorkflow")
            .Because("the catalog must carry the binding, or a reader learns the contract but "
                + "not the claim the proof discharges.");
        await Assert.That(catalog).Contains("contentHash")
            .Because("the catalog is stamped, so a consumer can tell skew from agreement.");
        await Assert.That(catalog).Contains("fulfill-order");
    }

    /// <summary>
    /// The consuming assembly proves the binding it could not previously see.
    /// </summary>
    [Test]
    public async Task ConsumingAssembly_ProvesABindingDeclaredBehindTheBoundary()
    {
        var producer = TwoAssemblyHarness.CompileProducer("OntologyProducer", OntologyAssembly());
        var consumer = TwoAssemblyHarness.CompileConsumer(WorkflowAssembly, producer);

        await Assert.That(consumer.ErrorIds()).IsEmpty()
            .Because("the workflow refines the imported contract, so the binding is proved.");

        var saga = consumer.RunResult.GeneratedTrees
            .Where(tree => tree.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal))
            .ToArray();
        await Assert.That(saga).IsNotEmpty()
            .Because("a proved binding still lowers; proof and emission are not alternatives.");
    }

    /// <summary>
    /// The proof is real across the boundary: a workflow that does NOT refine the
    /// imported contract is refuted, with the same diagnostic a local refutation gets.
    /// </summary>
    /// <remarks>
    /// This is the acceptance sentence of #204. Without it every other test here is
    /// consistent with an import path that reads the catalog and then proves nothing:
    /// a binding that can only ever succeed is not a proof.
    /// </remarks>
    [Test]
    public async Task ConsumingAssembly_RefutesAWorkflowThatDoesNotRefineTheImportedContract()
    {
        // The entry step's action now needs Stage == 9, which the bound action's
        // precondition (Stage == 0) does not imply.
        var producer = TwoAssemblyHarness.CompileProducer(
            "OntologyProducer", OntologyAssembly(receiveRequires: 9));
        var consumer = TwoAssemblyHarness.CompileConsumer(WorkflowAssembly, producer);

        var refutation = consumer.WithId("AGWF041");
        await Assert.That(refutation).IsNotEmpty()
            .Because("a cross-assembly binding that does not refine must be refuted here, "
                + "because no other compilation can refute it.");
        await Assert.That(refutation[0].GetMessage()).Contains("orders/Order/fulfill");
        await Assert.That(refutation[0].Descriptor.CustomTags)
            .Contains(WellKnownDiagnosticTags.NotConfigurable)
            .Because("a refuted binding is not silenceable, wherever the contract was declared.");
    }

    /// <summary>
    /// A referenced catalog from an unknown schema version is refused, not read on a guess.
    /// </summary>
    [Test]
    public async Task ReferencedCatalog_WithAnUnknownSchemaVersion_IsRefused()
    {
        var consumer = ConsumeHandWrittenCatalog(
            "SkewedProducer", Catalog(schemaVersion: "2.0"));

        var refusal = consumer.WithId("AGWF047");
        await Assert.That(refusal).IsNotEmpty()
            .Because("an unknown manifest version is refused; reading it on a guess would prove "
                + "the wrong thing quietly.");
        await Assert.That(refusal[0].GetMessage()).Contains("2.0");
    }

    /// <summary>
    /// A referenced catalog whose content hash does not match its bytes is refused.
    /// </summary>
    /// <remarks>
    /// The hash is taken over the catalog written without its own hash member, so this
    /// is a genuine integrity check rather than a re-serialization comparison: a
    /// catalog edited after emission no longer matches, whatever was edited.
    /// </remarks>
    [Test]
    public async Task ReferencedCatalog_WhoseContentHashDoesNotMatch_IsRefused()
    {
        var consumer = ConsumeHandWrittenCatalog(
            "TamperedProducer",
            Catalog(contentHash: new string('0', 64)));

        var refusal = consumer.WithId("AGWF047");
        await Assert.That(refusal).IsNotEmpty()
            .Because("a catalog that does not hash to its stamp has been altered since emission.");
        await Assert.That(refusal[0].GetMessage()).Contains("does not match its content");
    }

    /// <summary>
    /// A predicate discriminator this compiler does not know is refused, not skipped.
    /// </summary>
    [Test]
    public async Task ReferencedCatalog_WithAnUnknownPredicateKind_IsRefused()
    {
        var consumer = ConsumeHandWrittenCatalog(
            "UnknownPredicateProducer",
            Catalog(requirePredicate: Escaped("{\"kind\":\"eventually\"}")));

        var refusal = consumer.WithId("AGWF047");
        await Assert.That(refusal).IsNotEmpty()
            .Because("an unknown predicate kind is refused; skipping it would weaken every proof "
                + "that predicate takes part in.");
        await Assert.That(refusal[0].GetMessage()).Contains("eventually");
    }

    /// <summary>
    /// A member of the wrong SHAPE is refused with the same discipline as a missing
    /// one, because each of these narrows a proof input rather than breaking it.
    /// </summary>
    /// <remarks>
    /// This is the failure mode a reader is most likely to get wrong, because the
    /// tolerant spelling reads as defensive: filter the malformed entry out and carry
    /// on. What carries on is a proof against less than the catalog declared, and it
    /// goes green. Each case below names what would silently shrink.
    /// </remarks>
    /// <param name="label">What the malformed member would narrow.</param>
    /// <param name="catalog">The catalog literal carrying it.</param>
    /// <param name="reason">The refusal this member must produce.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(NarrowingCatalogs))]
    public async Task ReferencedCatalog_WithAMemberOfTheWrongShape_IsRefused(
        string label,
        string catalog,
        string reason)
    {
        var consumer = ConsumeHandWrittenCatalog("NarrowingProducer", catalog);

        var refusal = consumer.WithId("AGWF047");
        await Assert.That(refusal).IsNotEmpty()
            .Because($"a malformed member that would silently {label} is refused, not dropped: "
                + "the build would otherwise stay green on a weaker proof than the catalog declared.");

        // Named, not just counted: AGWF047 has many causes, and a case that refused for
        // some other one would pin nothing about the member under test.
        await Assert.That(refusal[0].GetMessage()).Contains(reason)
            .Because("the refusal must be the one this malformed member causes.");
    }

    /// <summary>The four members whose tolerant reading would narrow a proof input.</summary>
    /// <returns>A label, the catalog carrying that malformed member, and its refusal.</returns>
    public static IEnumerable<(string Label, string Catalog, string Reason)> NarrowingCatalogs()
    {
        yield return (
            "shift the rank of every later authority level",
            Catalog(authorities: "[{\"domainName\":\"orders\",\"lattice\":{\"axes\":"
                + "[{\"name\":\"scope\",\"levels\":[\"team\",7,\"org\"]}]}}]"),
            "non-string level on axis 'scope'");

        yield return (
            "prove the guarantee against a narrower frame",
            Catalog(touches: "[{\"kind\":\"property\"}]"),
            "declares a frame entry with no name");

        yield return (
            "drop the authority obligation entirely",
            Catalog(authority: "{\"sourceAuthorities\":[17]}"),
            "declares a non-string required authority");

        yield return (
            "hide an atom from the frame intersection",
            Catalog(requirePredicate: Escaped(
                "{\"kind\":\"custom\",\"evaluatorKey\":\"k\",\"readSet\":[{\"kind\":\"property\"}]}")),
            "read-set entry with no name");
    }

    /// <summary>
    /// Two assemblies declaring one ordinal action identity is refused rather than
    /// resolved by reference order.
    /// </summary>
    [Test]
    public async Task TwoCatalogs_DeclaringTheSameActionIdentity_AreRefused()
    {
        var first = TwoAssemblyHarness.CompileProducer("FirstProducer", OntologyAssembly());
        var second = TwoAssemblyHarness.CompileProducer(
            "SecondProducer", OntologyAssembly(ontologyTypeName: "OtherOrdersOntology"));

        var consumer = TwoAssemblyHarness.CompileConsumer(WorkflowAssembly, first, second);

        var refusal = consumer.WithId("AGWF048");
        await Assert.That(refusal).IsNotEmpty()
            .Because("one ordinal identity must have one declaring assembly; picking a winner "
                + "would make the proved contract depend on reference order.");

        // Every colliding identity is named, not just the first. A merge that reported
        // one collision and quietly resolved the rest would leave the same ambiguity
        // behind under a diagnostic that looks like it covered it.
        var named = refusal.Select(diagnostic => diagnostic.GetMessage()).ToArray();
        foreach (var identity in new[]
        {
            "orders/Order/fulfill",
            "orders/Order/receive",
            "orders/Order/complete",
        })
        {
            await Assert.That(named.Any(message => message.Contains(identity, StringComparison.Ordinal)))
                .IsTrue()
                .Because($"the collision on '{identity}' must be reported, not resolved.");
        }
    }

    /// <summary>
    /// One domain declared on both sides of the seam still resolves to one lattice.
    /// </summary>
    /// <remarks>
    /// A domain can legitimately be split across assemblies — different actions of the
    /// same domain — and each side then contributes that domain's authority lattice.
    /// Every authority-bearing obligation requires the domain to resolve to exactly
    /// one, so without structural deduplication the merge would hand the proof two
    /// identical lattices and it would refuse every authority in that domain: a
    /// correct layout failing on an ambiguity that is not one.
    /// </remarks>
    [Test]
    public async Task DomainDeclaredOnBothSidesOfTheSeam_ResolvesToOneAuthorityLattice()
    {
        var producer = TwoAssemblyHarness.CompileProducer(
            "AuthorityProducer",
            OntologyAssembly(withAuthority: true));
        var consumer = TwoAssemblyHarness.CompileConsumer(
            WorkflowAssemblyWithLocalOrdersOntology,
            producer);

        var unprovable = consumer.WithId("AGWF042")
            .Select(diagnostic => diagnostic.GetMessage())
            .Where(message => message.Contains("authority lattices", StringComparison.Ordinal))
            .ToArray();

        await Assert.That(unprovable).IsEmpty()
            .Because("two identical declarations of one domain's lattice are one lattice; "
                + "counting them as two would refuse a layout the design calls normal.");
        await Assert.That(consumer.ErrorIds()).IsEmpty()
            .Because("the binding proves, authority and all.");
    }

    /// <summary>
    /// A contract the portable form cannot carry is refused at the DECLARING assembly,
    /// and its binding stays unresolved there.
    /// </summary>
    /// <remarks>
    /// A link predicate is a boolean atom whose atom key and declared read set are
    /// different strings, and the portable contract has one name and no read set for
    /// it. Exporting it would drop the read set the frame check projects a guarantee
    /// through, so the contract would be proved downstream against a frame it never
    /// declared. Both diagnostics are reported together: AGWF046 says why the export
    /// failed, AGWF039 says the binding is therefore still unresolved here.
    /// </remarks>
    [Test]
    public async Task ContractThePortableFormCannotCarry_IsRefusedAtTheDeclaringAssembly()
    {
        var producer = TwoAssemblyHarness.CompileProducer(
            "UnexportableProducer",
            OntologyAssembly(fulfillExtra: ".RequiresLink(\"customer\")"),
            runGenerators: true,
            "AGWF046",
            "AGWF039");

        var reported = producer.GeneratorDiagnostics
            .Select(diagnostic => diagnostic.Id)
            .ToArray();

        await Assert.That(reported).Contains("AGWF046")
            .Because("the declaring assembly is the only place that knows why the contract "
                + "cannot travel.");
        await Assert.That(reported).Contains("AGWF039")
            .Because("a binding whose contract was NOT exported is still unresolved here; "
                + "deferral is per action, not per assembly.");

        await Assert.That(producer.GeneratedHintNames).Contains("StrategosProofCatalog.g.cs")
            .Because("the refusal is per action: this assembly's other two contracts still "
                + "travel, and only the one the gate refused stays behind.");
    }

    /// <summary>
    /// An assembly whose EVERY contract the gate refuses still exports, carrying nothing.
    /// </summary>
    /// <remarks>
    /// Absence of the attribute is the only signal AONT222 has, and it reads absence as
    /// "the workflow generator never ran here". Emitting nothing because every contract
    /// was refused would make absence mean two things at once: AONT222 would fire beside
    /// the AGWF046 that already named the real fault, and blame a generator that is
    /// installed and did run. An empty catalog says "nothing to contribute", which is a
    /// different statement from saying nothing at all.
    /// </remarks>
    [Test]
    public async Task AssemblyWhoseEveryContractIsRefused_StillExportsAnEmptyCatalog()
    {
        var producer = TwoAssemblyHarness.CompileProducer(
            "WhollyUnexportableProducer",
            SingleUnexportableActionAssembly,
            runGenerators: true,
            "AGWF046",
            "AGWF039");

        await Assert.That(producer.GeneratorDiagnostics.Select(diagnostic => diagnostic.Id))
            .Contains("AGWF046")
            .Because("the only contract this assembly declares cannot travel.");
        await Assert.That(producer.GeneratedHintNames).Contains("StrategosProofCatalog.g.cs")
            .Because("an assembly that declared a contract and exported none of them must "
                + "still look like an assembly whose generator ran, or AONT222 blames the "
                + "wrong thing.");
        await Assert.That(producer.Generated("StrategosProofCatalog.g.cs")!)
            .Contains("\\\"actions\\\":[]")
            .Because("the catalog carries an empty action list, not a missing one.");
    }

    private static TwoAssemblyHarness.ConsumerCompilation ConsumeHandWrittenCatalog(
        string assemblyName,
        string catalogJson)
    {
        var producer = TwoAssemblyHarness.CompileProducer(
            assemblyName,
            HandWrittenCatalogAssembly(catalogJson),
            runGenerators: false);
        return TwoAssemblyHarness.CompileConsumer(WorkflowAssembly, producer);
    }

    /// <summary>The authority lattice both sides of the seam declare, word for word.</summary>
    private const string OrdersLattice = """
        builder.AuthorityAxis("scope", "read", "write");
                builder.Authority("order.writer").At("scope", "write");
        """;

    /// <summary>
    /// An ontology declaring exactly one action, bound, whose contract cannot travel.
    /// </summary>
    private const string SingleUnexportableActionAssembly = """
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;
        using Strategos.Ontology.Descriptors;

        namespace CrossAssembly.Ontology;

        public sealed class Order
        {
            public int Stage { get; set; }
        }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("fulfill")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage)
                        .RequiresLink("customer")
                        .BoundToWorkflow("fulfill-order");
                });
            }
        }
        """;

    /// <summary>
    /// The ontology half: a domain whose action binds a workflow declared elsewhere.
    /// </summary>
    /// <param name="ontologyTypeName">The ontology class name, so two producers can differ.</param>
    /// <param name="fulfillExtra">Extra contract clauses on the bound action.</param>
    /// <param name="receiveRequires">
    /// The stage the entry step's action requires. 0 refines the bound contract; any
    /// other value does not.
    /// </param>
    /// <returns>The producer source.</returns>
    private static string OntologyAssembly(
        string ontologyTypeName = "OrdersOntology",
        string fulfillExtra = "",
        int receiveRequires = 0,
        bool withAuthority = false) => $$"""
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;
        using Strategos.Ontology.Descriptors;

        namespace CrossAssembly.Ontology;

        public sealed class Order
        {
            public int Stage { get; set; }
        }

        public sealed class {{ontologyTypeName}} : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                {{(withAuthority ? OrdersLattice : string.Empty)}}
                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("fulfill")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage)
                        {{(withAuthority ? ".RequiresAuthority(\"order.writer\")" : string.Empty)}}
                        {{fulfillExtra}}
                        .BoundToWorkflow("fulfill-order");

                    obj.Action("receive")
                        .Requires(order => order.Stage == {{receiveRequires}})
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);

                    obj.Action("complete")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage);
                });
            }
        }
        """;

    /// <summary>
    /// The workflow half: the implementation the bound action names, every step
    /// carrying the occurrence-scoped action reference the proof reads.
    /// </summary>
    private const string WorkflowAssembly = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace CrossAssembly.Flow;

        [WorkflowState]
        public sealed record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class TestStep : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state,
                StepContext context,
                CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public sealed class ReceiveStep : TestStep { }
        public sealed class CompleteStep : TestStep { }

        [Workflow("fulfill-order")]
        public static partial class FulfillOrderWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("fulfill-order")
                .StartWith<ReceiveStep>(step =>
                    step.Performs(new WorkflowActionReference("orders", "Order", "receive")))
                .Finally<CompleteStep>(step =>
                    step.Performs(new WorkflowActionReference("orders", "Order", "complete")));
        }
        """;

    /// <summary>
    /// The workflow half, plus a local ontology for the SAME domain the producer
    /// declares — the layout that makes one domain's lattice arrive from both sides.
    /// </summary>
    private static string WorkflowAssemblyWithLocalOrdersOntology =>
        WorkflowAssembly.Replace(
            "namespace CrossAssembly.Flow;",
            """
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;
            using Strategos.Ontology.Descriptors;

            namespace CrossAssembly.Flow;
            """,
            StringComparison.Ordinal)
        + $$"""

        public sealed class Invoice
        {
            public int Stage { get; set; }
        }

        public sealed class LocalOrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                {{OrdersLattice}}
                builder.Object<Invoice>("Invoice", obj =>
                {
                    obj.Action("audit")
                        .Requires(invoice => invoice.Stage == 0)
                        .Ensures(invoice => invoice.Stage == 1)
                        .Modifies(invoice => invoice.Stage);
                });
            }
        }
        """;

    /// <summary>
    /// An assembly carrying a hand-written catalog attribute, so a test can put a
    /// catalog the emitter would never produce in front of the reader.
    /// </summary>
    /// <param name="catalogJson">The catalog JSON, escaped for a C# literal.</param>
    /// <returns>The producer source.</returns>
    /// <remarks>
    /// Compiled WITHOUT the generators. A tampered or version-skewed catalog is
    /// exactly what the emitter cannot make, and reaching the reader's refusals any
    /// other way would mean weakening the emitter in order to test the reader.
    /// </remarks>
    private static string HandWrittenCatalogAssembly(string catalogJson) => $$"""
        [assembly: global::Strategos.Generated.StrategosProofCatalogAttribute("{{catalogJson}}")]

        namespace Strategos.Generated
        {
            [global::System.AttributeUsage(global::System.AttributeTargets.Assembly, AllowMultiple = false)]
            internal sealed class StrategosProofCatalogAttribute : global::System.Attribute
            {
                public StrategosProofCatalogAttribute(string catalog)
                {
                    this.Catalog = catalog;
                }

                public string Catalog { get; }
            }
        }
        """;

    /// <summary>Builds a catalog literal, escaped for a C# string, with one knob turned.</summary>
    /// <param name="schemaVersion">The declared manifest version.</param>
    /// <param name="contentHash">A stamped hash, or null for none.</param>
    /// <param name="requirePredicate">The requirement predicate JSON, already escaped.</param>
    /// <param name="touches">The frame array JSON.</param>
    /// <param name="authority">The action's authority object JSON, or null for none.</param>
    /// <param name="authorities">The catalog's lattice array JSON, or null for none.</param>
    /// <returns>The escaped catalog literal.</returns>
    private static string Catalog(
        string schemaVersion = "1.0",
        string? contentHash = null,
        string? requirePredicate = null,
        string touches = "[]",
        string? authority = null,
        string? authorities = null)
    {
        var hash = contentHash is null ? string.Empty : $",\"contentHash\":\"{contentHash}\"";
        var predicate = requirePredicate ?? Escaped("{\"kind\":\"true\"}");
        var declaredAuthority = authority is null ? string.Empty : ",\"authority\":" + authority;
        var lattices = authorities is null ? string.Empty : ",\"authorities\":" + authorities;
        var json = "{\"schemaVersion\":\"" + schemaVersion + "\",\"catalogId\":\"HandWritten\""
            + hash
            + ",\"actions\":{\"actions\":[{"
            + "\"subject\":{\"domainName\":\"orders\",\"objectTypeName\":\"Order\"},"
            + "\"name\":\"fulfill\","
            + "\"requires\":[{\"predicate\":__PREDICATE__,"
            + "\"expression\":\"true\",\"strength\":\"hard\"}],"
            + "\"ensures\":[{\"predicate\":{\"kind\":\"true\"},\"expression\":\"true\"}],"
            + "\"touches\":" + touches + ",\"idempotent\":false"
            + declaredAuthority
            + ",\"boundWorkflow\":\"fulfill-order\"}]}"
            + lattices
            + "}";
        return Escaped(json).Replace("__PREDICATE__", predicate, StringComparison.Ordinal);
    }

    /// <summary>Escapes JSON for embedding in a C# string literal.</summary>
    /// <param name="json">The raw JSON.</param>
    /// <returns>The escaped text.</returns>
    private static string Escaped(string json) =>
        json.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
