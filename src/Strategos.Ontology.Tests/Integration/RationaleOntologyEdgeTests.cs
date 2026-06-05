using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.Integration;

// ---------------------------------------------------------------------------
// DR-9 / DR-10 (#114, #128): the comprehensive SymbolKey-only zero-reflection
// proof. An edge-centric RATIONALE ontology — Decisions and Constraints linked
// by reified associations (Supersedes / Motivates / ConflictsWith) — is authored
// with NO CLR descriptor types and NO reflection: every ObjectTypeDescriptor in
// the corpus is SymbolKey-only (ClrType == null, SymbolKey == <moniker>), and
// every id projection rides the reflection-free DR-1 IdAccessor.
//
// The test exercises the full public-primitive path: RELATE (materialized
// relation rows in the relate-store shape) -> TRAVERSE (InMemoryExpressionEvaluator
// over the frozen OntologyGraph) -> VALIDATE (the traversal resolves the correct
// related instances AND the reified association objects carry their own edge
// properties). Because every descriptor is ClrType == null, the hop-target
// partition can ONLY be resolved via the graph's SymbolKey -> descriptor-name
// reverse index (DR-10) — the legacy typeof(TLinked) path is structurally
// impossible here, which is exactly the point (INV-8).
//
// Bitemporal validity is explicitly OUT of scope for v2.9.0 (tracked #126); the
// associations carry a plain status/rationale rather than a valid-time interval.
//
// The corpus + relate/traverse SCRIPT live in the provider-agnostic
// RationaleOntologyFixture so a later cross-provider task (T13) can replay the
// SAME ontology and the SAME relate rows against the Npgsql provider — the
// fixture knows nothing about which executor runs it.
// ---------------------------------------------------------------------------
public class RationaleOntologyEdgeTests
{
    [Test]
    public async Task RationaleOntology_SymbolKeyOnly_RelatesTraversesValidates_InMemory()
    {
        var fixture = RationaleOntologyFixture.Build();

        // INV-8 guard: assert the corpus itself is CLR-free. If any descriptor
        // ever regains a ClrType this proof silently degrades into the legacy
        // CLR path, so we pin it mechanically rather than by convention.
        foreach (var descriptor in fixture.Graph.ObjectTypes)
        {
            await Assert.That(descriptor.ClrType).IsNull();
            await Assert.That(descriptor.SymbolKey).IsNotNull();
        }

        var evaluator = new InMemoryExpressionEvaluator(
            fixture.Graph,
            fixture.RelationResolver,
            idProjector: null);

        // --- RELATE -> TRAVERSE (edge view): Decision D1 --Supersedes--> ? ---
        // The link target is named ONLY by the association's SymbolKey, so the hop
        // resolves the "Supersedes" partition through the graph's reverse index.
        // The edge view surfaces the reified association object, NOT the far node.
        var supersedesEdges = evaluator.Evaluate<RationaleNode>(
            fixture.TraverseSupersedesEdges("D1"),
            fixture.ResolveItems);

        await Assert.That(supersedesEdges).HasCount().EqualTo(1);
        // The association object carries its OWN edge properties (rationale).
        await Assert.That(supersedesEdges[0].Id).IsEqualTo("sup-1");
        await Assert.That(supersedesEdges[0].Get("rationale"))
            .IsEqualTo("D1 obsoletes the earlier choice");

        // --- TRAVERSE (far node): Decision D1 --supersededDecision--> Decision D0 ---
        // A SEPARATE SymbolKey-only link whose target is the Decision NODE (not the
        // association) resolves the superseded decision via the SAME reverse index.
        // Far-node identity also rides the IdAccessor only — no reflection.
        var superseded = evaluator.Evaluate<RationaleNode>(
            fixture.TraverseSupersededDecision("D1"),
            fixture.ResolveItems);

        await Assert.That(superseded).HasCount().EqualTo(1);
        await Assert.That(superseded[0].Id).IsEqualTo("D0");
        await Assert.That(superseded[0].Get("title")).IsEqualTo("Use a monolith");

        // --- RELATE -> TRAVERSE (edge view): Constraint C1 --Motivates--> ? ---
        // A DIFFERENT association partition (Motivates), proving the reverse index
        // routes each link to its own SymbolKey-named edge, not the first match.
        var motivatesEdges = evaluator.Evaluate<RationaleNode>(
            fixture.TraverseMotivatesEdges("C1"),
            fixture.ResolveItems);

        await Assert.That(motivatesEdges).HasCount().EqualTo(1);
        await Assert.That(motivatesEdges[0].Id).IsEqualTo("mot-1");
        await Assert.That(motivatesEdges[0].Get("weight")).IsEqualTo("high");

        // --- VALIDATE: an instance with no relation rows yields an empty set ---
        // (no silent type-level fetch of all Supersedes objects — #114).
        var unrelated = evaluator.Evaluate<RationaleNode>(
            fixture.TraverseSupersedesEdges("D0"),
            fixture.ResolveItems);

        await Assert.That(unrelated).IsEmpty();

        // --- VALIDATE: ConflictsWith is its own partition, distinctly resolved ---
        var conflictEdges = evaluator.Evaluate<RationaleNode>(
            fixture.TraverseConflictsWithEdges("D1"),
            fixture.ResolveItems);

        await Assert.That(conflictEdges).HasCount().EqualTo(1);
        await Assert.That(conflictEdges[0].Id).IsEqualTo("cfl-1");
        await Assert.That(conflictEdges[0].Get("severity")).IsEqualTo("blocking");
    }
}
