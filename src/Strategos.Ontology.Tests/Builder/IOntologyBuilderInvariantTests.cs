using System.Collections.Immutable;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Events;
using Strategos.Ontology.Actions;

namespace Strategos.Ontology.Tests.Builder;

/// <summary>
/// DR-6 + DR-10 (Task 16): the AONT205 invariant fires at delta-apply
/// time when a mechanical ingester contributes to one of the intent-only
/// fields (<c>Actions</c>, <c>Events</c>, <c>Lifecycle</c>).
/// <see cref="OntologyBuilder.ApplyDelta"/> aborts with an
/// <see cref="OntologyCompositionException"/> whose
/// <see cref="OntologyCompositionException.Diagnostics"/> contains
/// the offending diagnostic, identifying the violated field. Hand-authored
/// descriptors are unaffected.
/// </summary>
public class IOntologyBuilderInvariantTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    private const string SourceId = "marten-typescript";

    [Test]
    public async Task ApplyDelta_AddIngestedDescriptorWithActions_ThrowsOntologyCompositionException()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var descriptor = new ObjectTypeDescriptor
        {
            Name = "Position",
            DomainName = "Trading",
            SymbolKey = "scip-typescript ./pos.ts#Position",
            Source = DescriptorSource.Ingested,
            SourceId = SourceId,
            Actions = new List<ActionDescriptor>
            {
                new("Trade", "Trade action"),
            },
        };

        var delta = new OntologyDelta.AddObjectType(descriptor)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        OntologyCompositionException? caught = null;
        try
        {
            builder.ApplyDelta(delta);
        }
        catch (OntologyCompositionException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Diagnostics.Any(d => d.Id == "AONT205")).IsTrue();
        // Offending field is identified — the diagnostic message or its
        // structured fields must name the violated intent field.
        var aont205 = caught.Diagnostics.First(d => d.Id == "AONT205");
        await Assert.That(aont205.Message).Contains("Actions");
    }

    [Test]
    public async Task ApplyDelta_AddIngestedDescriptorWithLifecycle_ThrowsAONT205()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var descriptor = new ObjectTypeDescriptor
        {
            Name = "Position",
            DomainName = "Trading",
            SymbolKey = "scip-typescript ./pos.ts#Position",
            Source = DescriptorSource.Ingested,
            SourceId = SourceId,
            Lifecycle = new LifecycleDescriptor
            {
                PropertyName = "State",
                StateEnumTypeName = "PositionState",
                States = new List<LifecycleStateDescriptor>
                {
                    new() { Name = "Open", IsInitial = true },
                    new() { Name = "Closed", IsTerminal = true },
                },
                Transitions = new List<LifecycleTransitionDescriptor>(),
            },
        };

        var delta = new OntologyDelta.AddObjectType(descriptor)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        OntologyCompositionException? caught = null;
        try
        {
            builder.ApplyDelta(delta);
        }
        catch (OntologyCompositionException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        var aont205 = caught!.Diagnostics.FirstOrDefault(d => d.Id == "AONT205");
        await Assert.That(aont205).IsNotNull();
        await Assert.That(aont205!.Message).Contains("Lifecycle");
    }

    [Test]
    public async Task ApplyDelta_AddIngestedDescriptorWithEvents_ThrowsAONT205()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var descriptor = new ObjectTypeDescriptor
        {
            Name = "Position",
            DomainName = "Trading",
            SymbolKey = "scip-typescript ./pos.ts#Position",
            Source = DescriptorSource.Ingested,
            SourceId = SourceId,
            Events = new List<EventDescriptor>
            {
                new(typeof(string), "Position opened"),
            },
        };

        var delta = new OntologyDelta.AddObjectType(descriptor)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        OntologyCompositionException? caught = null;
        try
        {
            builder.ApplyDelta(delta);
        }
        catch (OntologyCompositionException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        var aont205 = caught!.Diagnostics.FirstOrDefault(d => d.Id == "AONT205");
        await Assert.That(aont205).IsNotNull();
        await Assert.That(aont205!.Message).Contains("Events");
    }

    [Test]
    public async Task ApplyDelta_AddHandAuthoredDescriptorWithActions_DoesNotThrow()
    {
        // Negative: hand-authored descriptors are the home of Actions/
        // Events/Lifecycle. AONT205 must only fire when Source == Ingested.
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Source = DescriptorSource.HandAuthored,
            Actions = new List<ActionDescriptor>
            {
                new("Trade", "Trade action"),
            },
        };

        var delta = new OntologyDelta.AddObjectType(descriptor)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        Exception? caught = null;
        try
        {
            builder.ApplyDelta(delta);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNull();
        await Assert.That(((OntologyBuilder)builder).ObjectTypes.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ApplyDelta_UpdateIngestedDescriptorWithActions_ThrowsAONT205()
    {
        // Covers the UpdateObjectType branch — same invariant must apply
        // on update as on add. The builder is first seeded with a clean
        // ingested descriptor; the update then introduces an Actions
        // contribution, which must fail.
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var seed = new ObjectTypeDescriptor
        {
            Name = "Position",
            DomainName = "Trading",
            SymbolKey = "scip-typescript ./pos.ts#Position",
            Source = DescriptorSource.Ingested,
            SourceId = SourceId,
        };
        builder.ApplyDelta(new OntologyDelta.AddObjectType(seed)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        var updated = new ObjectTypeDescriptor
        {
            Name = "Position",
            DomainName = "Trading",
            SymbolKey = "scip-typescript ./pos.ts#Position",
            Source = DescriptorSource.Ingested,
            SourceId = SourceId,
            Actions = new List<ActionDescriptor>
            {
                new("Trade", "Trade action"),
            },
        };

        OntologyCompositionException? caught = null;
        try
        {
            builder.ApplyDelta(new OntologyDelta.UpdateObjectType(updated)
            {
                SourceId = SourceId,
                Timestamp = Timestamp,
            });
        }
        catch (OntologyCompositionException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        var aont205 = caught!.Diagnostics.FirstOrDefault(d => d.Id == "AONT205");
        await Assert.That(aont205).IsNotNull();
        await Assert.That(aont205!.Message).Contains("Actions");
    }
}
