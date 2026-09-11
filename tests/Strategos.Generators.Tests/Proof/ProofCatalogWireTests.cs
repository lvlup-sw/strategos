// -----------------------------------------------------------------------
// <copyright file="ProofCatalogWireTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Strategos.Analyzers.Proof;
using Strategos.Generators.Proof;
using Strategos.Generators.Tests.Fixtures;
using Strategos.Generators.Tests.Import;

namespace Strategos.Generators.Tests.Proof;

/// <summary>
/// The portable proof catalog is a wire document and a lossless projection (#204).
/// </summary>
/// <remarks>
/// <para>
/// Two claims, and they fail differently. If the emitted bytes do not validate
/// against <c>ProofCatalogV1</c>, the catalog is not the contract it says it is and
/// no other consumer can read it. If a contract does not survive the round trip, the
/// catalog IS the contract but a different one — which is worse, because everything
/// downstream still typechecks and proves the wrong thing.
/// </para>
/// <para>
/// The round trip is asserted on <c>StableKey</c>, not on logical equivalence.
/// Equivalence would pass for a projection that normalized a formula on the way
/// through, and a normalization the two arms disagree about is exactly the drift this
/// pins against.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public sealed class ProofCatalogWireTests
{
    /// <summary>The emitted catalog validates against the Contracts-emitted schema.</summary>
    [Test]
    public async Task EmittedCatalog_ValidatesAgainstProofCatalogV1()
    {
        var json = ExportCatalog(RichOntology);

        var schema = await NJsonSchema.JsonSchema.FromFileAsync(
            Path.Combine(ContractsSchemaPaths.SchemaDir, "ProofCatalogV1.json"));
        var errors = schema.Validate(json);

        await Assert.That(errors).IsEmpty()
            .Because("the catalog is a ProofCatalogV1 document, not a private encoding: "
                + string.Join("; ", errors.Select(error => $"{error.Path}: {error.Kind}")));
    }

    /// <summary>
    /// Every exportable contract shape survives the round trip unchanged.
    /// </summary>
    /// <remarks>
    /// The fixture covers each formula kind the export admits and each literal kind
    /// reachable from the authoring surface: a conjunction, a disjunction, a negation,
    /// comparisons on integer, string, boolean and enum properties, and the six
    /// comparison operators. A kind added to the algebra and not to the writer is
    /// caught by the export gate; a kind the writer renders and the reader misreads is
    /// caught here.
    /// </remarks>
    [Test]
    public async Task EveryExportableContract_RoundTripsToTheSameFormula()
    {
        var original = BuildLocalCatalog(RichOntology);
        var exported = ProofCatalogDocument.FromLocalCatalog("RoundTrip", original, out var failures);

        await Assert.That(failures).IsEmpty()
            .Because("the fixture is written to be exportable end to end: "
                + string.Join("; ", failures.Select(failure => failure.Reason)));
        await Assert.That(exported.Actions.Length).IsGreaterThan(3)
            .Because("a round trip over one trivial action would prove nothing.");

        var (_, json) = ProofCatalogWriter.WriteStamped(exported);
        var read = ProofCatalogReader.Read(json);

        await Assert.That(read.Succeeded).IsTrue()
            .Because($"the writer's own output must be readable: {read.FailureReason}");

        var readBack = read.Document!.Actions.ToDictionary(
            action => action.Identity.ToString(),
            StringComparer.Ordinal);

        foreach (var action in exported.Actions)
        {
            var identity = action.Identity.ToString();
            await Assert.That(readBack.ContainsKey(identity)).IsTrue()
                .Because($"'{identity}' must survive the round trip at all.");

            var round = readBack[identity];
            await Assert.That(round.Requirement.Formula.StableKey)
                .IsEqualTo(action.Requirement.Formula.StableKey)
                .Because($"'{identity}' precondition must be the same formula, not an equivalent one.");
            await Assert.That(round.Guarantee.Formula.StableKey)
                .IsEqualTo(action.Guarantee.Formula.StableKey)
                .Because($"'{identity}' guarantee must be the same formula, not an equivalent one.");
            await Assert.That(round.Frame.OrderBy(name => name, StringComparer.Ordinal))
                .IsEquivalentTo(action.Frame.OrderBy(name => name, StringComparer.Ordinal))
                .Because($"'{identity}' frame is what the realizability check projects through.");
            await Assert.That(round.BoundWorkflowName).IsEqualTo(action.BoundWorkflowName);
            await Assert.That(round.RequiredAuthority).IsEqualTo(action.RequiredAuthority);
            await Assert.That(round.CompensatingActionName).IsEqualTo(action.CompensatingActionName);
        }
    }

    /// <summary>
    /// The atom read set is rebuilt on import, because the frame check reads it.
    /// </summary>
    /// <remarks>
    /// This is not bookkeeping. <c>AtomReads</c> is what a guarantee is projected
    /// through before the realizability check runs; an imported contract with an empty
    /// map is proved against an empty frame, which refutes every action that changes
    /// anything. That failure looks exactly like a real refutation, which is why it
    /// gets its own test rather than being left to the end-to-end path.
    /// </remarks>
    [Test]
    public async Task ImportedContract_RebuildsTheAtomReadSetTheFrameCheckReads()
    {
        var local = BuildLocalCatalog(RichOntology);
        var exported = ProofCatalogDocument.FromLocalCatalog("ReadSet", local, out _);
        var (_, json) = ProofCatalogWriter.WriteStamped(exported);
        var read = ProofCatalogReader.Read(json);

        var fulfill = read.Document!.Actions
            .Single(action => string.Equals(action.Identity.ActionName, "fulfill", StringComparison.Ordinal));

        await Assert.That(fulfill.Guarantee.AtomReads).IsNotEmpty()
            .Because("a guarantee over a property must declare that the atom reads it.");
        await Assert.That(fulfill.Guarantee.AtomReads.Keys.Any(fulfill.Frame.Contains)).IsTrue()
            .Because("the frame check selects atoms whose reads intersect the frame; an imported "
                + "contract whose map misses that intersection is proved against an empty frame.");
    }

    /// <summary>
    /// The content hash is over the catalog's own canonical bytes, and is stable
    /// across builds of the same declarations.
    /// </summary>
    [Test]
    public async Task ContentHash_IsDeterministicAcrossBuildsOfTheSameDeclarations()
    {
        var first = ExportCatalog(RichOntology);
        var second = ExportCatalog(RichOntology);

        await Assert.That(second).IsEqualTo(first)
            .Because("two builds of the same declarations must produce the same bytes, or the "
                + "hash records the build rather than the contract and skew detection is noise.");
    }

    /// <summary>
    /// The generator emits the attribute name the ontology analyzer matches on.
    /// </summary>
    /// <remarks>
    /// The other half of this pin lives in <c>AONT222BindingExportTests</c>. The two
    /// analyzer assemblies reference neither each other nor a common one, so the name
    /// is a literal on both sides; a rename on one side alone silently breaks the
    /// declaring-side diagnostic.
    /// </remarks>
    [Test]
    public async Task ExportedAttributeName_MatchesTheNameTheOntologyAnalyzerMatches()
    {
        await Assert.That(ProofCatalogExport.AttributeFullName)
            .IsEqualTo("Strategos.Generated.StrategosProofCatalogAttribute")
            .Because("BindingExportAnalyzer.ProofCatalogAttributeMetadataName asserts the same "
                + "literal from the ontology analyzer, which cannot reference this assembly.");
    }

    private static string ExportCatalog(string source)
    {
        var local = BuildLocalCatalog(source);
        var document = ProofCatalogDocument.FromLocalCatalog("WireProbe", local, out _);
        return ProofCatalogWriter.WriteStamped(document).Json;
    }

    private static OntologyActionCatalog BuildLocalCatalog(string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "WireProbe",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: GeneratorTestHelper.GetCompileGraphReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        if (!errors.IsEmpty)
        {
            throw new InvalidOperationException(
                "the wire fixture does not compile: "
                + string.Join(" | ", errors.Select(diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        }

        return OntologyActionCatalog.Build(compilation, CancellationToken.None);
    }

    /// <summary>
    /// An ontology exercising the exportable formula algebra: conjunction, disjunction,
    /// negation, every comparison operator, and integer, string, boolean and enum
    /// comparisons — plus an authority, a lattice that defines it, and an inverse.
    /// </summary>
    private const string RichOntology = """
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;

        namespace WireProbe;

        public enum Tier { Standard, Priority }

        public sealed class Order
        {
            public string Id { get; set; } = string.Empty;
            public int Stage { get; set; }
            public int Attempts { get; set; }
            public string Region { get; set; } = string.Empty;
            public bool Held { get; set; }
            public Tier Tier { get; set; }
        }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.AuthorityAxis("scope", "read", "write");
                builder.Authority("order.writer").At("scope", "write");

                builder.Object<Order>(obj =>
                {
                    obj.Key(order => order.Id);
                    obj.Property(order => order.Stage);
                    obj.Property(order => order.Attempts);
                    obj.Property(order => order.Region);
                    obj.Property(order => order.Held);
                    obj.Property(order => order.Tier);

                    obj.Action("fulfill")
                        .Requires(order => order.Stage == 0 && order.Attempts < 3)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage)
                        .RequiresAuthority("order.writer")
                        .BoundToWorkflow("fulfill-order");

                    obj.Action("escalate")
                        .Requires(order => order.Region != "eu" || order.Tier == Tier.Priority)
                        .Ensures(order => order.Attempts >= 1)
                        .Modifies(order => order.Attempts);

                    obj.Action("hold")
                        .Requires(order => !order.Held)
                        .Ensures(order => order.Held == true)
                        .Modifies(order => order.Held)
                        .CompensatedBy("release");

                    obj.Action("release")
                        .Requires(order => order.Held == true)
                        .Ensures(order => order.Held == false)
                        .Modifies(order => order.Held);

                    obj.Action("expedite")
                        .Requires(order => order.Stage <= 1 && order.Attempts > 0)
                        .Ensures(order => order.Tier == Tier.Priority)
                        .Modifies(order => order.Tier);
                });
            }
        }
        """;
}
