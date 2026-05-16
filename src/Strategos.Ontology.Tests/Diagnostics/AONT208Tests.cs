using System.Collections.Immutable;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests.Diagnostics;

/// <summary>
/// DR-7 (Task 30): AONT208 is an error-severity graph-freeze diagnostic
/// emitted when MergeTwo's two inputs disagree on <c>LanguageId</c>
/// where the hand side has opted into a non-default value (i.e. the
/// hand-authored descriptor was explicitly set to a polyglot language).
/// Hand descriptors that default to <c>"dotnet"</c> do not fire this
/// diagnostic against ingested polyglot contributions — that is the
/// expected polyglot composition path.
/// </summary>
public class AONT208Tests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Hand-side ontology that publishes a descriptor with an explicit
    /// non-default LanguageId. The ingested source then contributes a
    /// disagreeing LanguageId — AONT208 fires.
    /// </summary>
    private sealed class HandRustAuthoringOntology : DomainOntology
    {
        public override string DomainName => "Trading";

        protected override void Define(IOntologyBuilder builder)
        {
            // Hand-feed a descriptor whose LanguageId is explicitly
            // "rust" (the hand-author has opted into polyglot identity).
            // The merge happens when an ingested AddObjectType arrives
            // for the same (Domain, Name) with a different LanguageId.
            if (builder is OntologyBuilder concrete)
            {
                concrete.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
                {
                    Name = "Position",
                    DomainName = "Trading",
                    SymbolKey = "rust ./pos.rs::Position",
                    LanguageId = "rust",
                    Source = DescriptorSource.HandAuthored,
                });
            }
        }
    }

    [Test]
    public async Task Build_TwoSourcesDisagreeLanguageId_AONT208Error()
    {
        var ingested = new ObjectTypeDescriptor
        {
            Name = "Position",
            DomainName = "Trading",
            ClrType = null,
            SymbolKey = "scip-typescript ./pos.ts#Position",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "marten-typescript",
        };

        var source = new TestOntologySource
        {
            SourceId = "marten-typescript",
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(ingested)
                {
                    SourceId = "marten-typescript",
                    Timestamp = Timestamp,
                }),
        };

        var graphBuilder = new OntologyGraphBuilder()
            .AddDomain<HandRustAuthoringOntology>()
            .AddSources(new IOntologySource[] { source });

        OntologyCompositionException? caught = null;
        try
        {
            graphBuilder.Build();
        }
        catch (OntologyCompositionException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        var aont208 = caught!.Diagnostics.FirstOrDefault(d => d.Id == "AONT208");
        await Assert.That(aont208).IsNotNull();
        await Assert.That(aont208!.DomainName).IsEqualTo("Trading");
        await Assert.That(aont208.TypeName).IsEqualTo("Position");
        await Assert.That(aont208.Message).Contains("rust");
        await Assert.That(aont208.Message).Contains("typescript");
    }
}
