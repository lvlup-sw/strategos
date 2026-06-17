using Strategos.Identity.Abstractions;
using Strategos.Ontology.Provenance;

namespace Strategos.Ontology.Tests.Provenance;

/// <summary>
/// DR-16 (T23, #126): W3C PROV-DM CORE provenance on a reified association — the
/// 3 types (Entity / Activity / Agent) and the 7 core relations, with the reified
/// association ≅ the qualified-influence node and the asserting agent sourced from
/// the G1 <c>CurrentAgentIdentity</c> seam (<see cref="IAgentIdentityAccessor.CurrentAgent"/>).
/// Bundles / Collections are deliberately out of scope.
/// </summary>
/// <remarks>
/// The core ontology stays self-contained (INV-2): the PROV model carries the
/// agent as an opaque header-safe STRING, and the binding to
/// <see cref="IAgentIdentityAccessor"/> happens at the seam exercised here — the
/// test reads <c>accessor.CurrentAgent?.Value</c> into the recorder. INV-7: the
/// provenance record is immutable; INV-8: subjects/objects are addressed by id,
/// never a CLR type.
/// </remarks>
public class ProvCoreTests
{
    [Test]
    public async Task Provenance_AttachAndQuery_AgentActivityReason_FromCurrentAgentIdentity()
    {
        // The G1 seam: a Wolverine-step agent identity is active.
        var accessor = Substitute.For<IAgentIdentityAccessor>();
        accessor.CurrentAgent.Returns(new AgentIdentity("spiffe://td/workflow/wf-1/step/Drafting"));

        // Attach provenance to a reified association: the agent (from the seam) and
        // the activity that generated it, with a human reason.
        var recorder = new AssociationProvenanceRecorder(() => accessor.CurrentAgent?.Value);

        var provenance = recorder.Attach(
            associationId: "emp-1",
            activityId: "wf-1/step/Drafting",
            reason: "promoted from candidate after interview loop");

        // The reified association IS the PROV Entity (the qualified-influence node).
        await Assert.That(provenance.Entity.Id).IsEqualTo("emp-1");

        // The asserting agent was sourced from CurrentAgentIdentity (not invented).
        await Assert.That(provenance.Agent).IsNotNull();
        await Assert.That(provenance.Agent!.Id).IsEqualTo("spiffe://td/workflow/wf-1/step/Drafting");

        // The activity that generated the assertion.
        await Assert.That(provenance.Activity.Id).IsEqualTo("wf-1/step/Drafting");

        // The reason rides on the qualified influence.
        await Assert.That(provenance.Reason).IsEqualTo("promoted from candidate after interview loop");

        // The qualified-influence node carries the PROV-DM core relations that bind
        // the entity to its activity (wasGeneratedBy) and agent (wasAttributedTo),
        // plus the activity's responsible agent (wasAssociatedWith).
        await Assert.That(provenance.Influences.Select(i => i.Relation))
            .Contains(ProvRelation.WasGeneratedBy);
        await Assert.That(provenance.Influences.Select(i => i.Relation))
            .Contains(ProvRelation.WasAttributedTo);
        await Assert.That(provenance.Influences.Select(i => i.Relation))
            .Contains(ProvRelation.WasAssociatedWith);
    }

    [Test]
    public async Task Provenance_NoActiveAgent_AgentIsNull_NotInvented()
    {
        // No envelope / no agent context active: the seam returns null. Provenance
        // must NOT fabricate an agent — wasAttributedTo is simply absent.
        var recorder = new AssociationProvenanceRecorder(() => null);

        var provenance = recorder.Attach(
            associationId: "emp-2",
            activityId: "wf-1/step/Ingest",
            reason: "bulk import");

        await Assert.That(provenance.Agent).IsNull();
        await Assert.That(provenance.Influences.Select(i => i.Relation))
            .DoesNotContain(ProvRelation.WasAttributedTo);
        // The activity-generation relation still holds (no agent required for it).
        await Assert.That(provenance.Influences.Select(i => i.Relation))
            .Contains(ProvRelation.WasGeneratedBy);
    }

    [Test]
    public async Task ProvRelation_CoversTheSevenPrimDmCoreRelations()
    {
        // PROV-DM core is exactly seven relations (Bundles/Collections excluded).
        // Guard the enum surface so a future edit that adds a non-core relation or
        // drops a core one is caught.
        var relations = Enum.GetValues<ProvRelation>();

        await Assert.That(relations.Length).IsEqualTo(7);
        await Assert.That(relations).Contains(ProvRelation.WasGeneratedBy);
        await Assert.That(relations).Contains(ProvRelation.Used);
        await Assert.That(relations).Contains(ProvRelation.WasInformedBy);
        await Assert.That(relations).Contains(ProvRelation.WasDerivedFrom);
        await Assert.That(relations).Contains(ProvRelation.WasAttributedTo);
        await Assert.That(relations).Contains(ProvRelation.WasAssociatedWith);
        await Assert.That(relations).Contains(ProvRelation.ActedOnBehalfOf);
    }

    [Test]
    public async Task Provenance_IsImmutable_StructurallyEqual()
    {
        // INV-7: two attachments with identical inputs (same fixed agent seam)
        // produce structurally-equal provenance — the record carries no hidden
        // mutable / wall-clock state.
        var recorder = new AssociationProvenanceRecorder(() => "agent-x");

        var a = recorder.Attach("emp-1", "act-1", "r");
        var b = recorder.Attach("emp-1", "act-1", "r");

        await Assert.That(a).IsEqualTo(b);
    }
}
