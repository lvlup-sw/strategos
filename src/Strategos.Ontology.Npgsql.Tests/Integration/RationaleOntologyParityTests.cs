using System.Collections.Generic;
using System.Threading.Tasks;
using Strategos.Ontology;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Tests.Integration;

namespace Strategos.Ontology.Npgsql.Tests.Integration;

// ---------------------------------------------------------------------------
// DR-9 / DR-10 (#114) cross-provider parity (t13): replay T12's SymbolKey-only
// rationale corpus against BOTH the in-memory evaluator and the Npgsql provider
// and assert IDENTICAL observable results. The corpus + relate/traverse SCRIPT
// live in the provider-agnostic RationaleOntologyFixture (T12), reused here via
// a <ProjectReference> to Strategos.Ontology.Tests — the fixture binds to NO
// executor, so the SAME ontology and the SAME relate rows drive both backends.
//
// Parity is proven on two seams:
//
//   1. SQL-SHAPE parity (DB-FREE, runs everywhere): the Npgsql provider's
//      graph-driven resolvers (ResolveTraversalHop / ResolveAssociationRelate)
//      + SqlGenerator must GENERATE the relate/traverse SQL whose
//      junction/association/endpoint shapes correspond exactly to the corpus's
//      operations. Crucially, the hop targets are resolved from the graph's
//      SymbolKey -> descriptor-name reverse index (DR-10) — the corpus's links
//      name their targets ONLY by SymbolKey (ClrType == null), so the legacy
//      typeof(TLinked) path is structurally impossible (INV-8). This is the
//      locally-verifiable half of "parity".
//
//   2. EXECUTION parity (DB-GATED, skips without Postgres): seed the corpus into
//      a real Npgsql store, replay the relate rows, read them back through the
//      SAME traversal expressions, and assert the resolved instances + reified-
//      association edge attributes are byte-identical to the in-memory replay.
//      There is no local Postgres, so this test SKIPS unless STRATEGOS_PG_TEST_CONN
//      names a reachable database (provisioned CI lane); it is correctly
//      structured to RUN and ASSERT parity when a DB is present.
//
// INV-2: raw Npgsql/pgvector throughout — no Marten/Wolverine. INV-8: every
// descriptor in the corpus is SymbolKey-only (ClrType == null).
// ---------------------------------------------------------------------------
public class RationaleOntologyParityTests
{
    // -----------------------------------------------------------------------
    // (1) SQL-SHAPE parity — DB-FREE. Drives the Npgsql provider's resolvers +
    //     SqlGenerator from the SAME fixture graph the in-memory evaluator uses,
    //     and asserts the generated relate/traverse SQL matches the expected
    //     junction/association shapes for the corpus's operations.
    // -----------------------------------------------------------------------

    [Test]
    public async Task RationaleOntology_NpgsqlGeneratesExpectedRelateTraverseSql_ForCorpus()
    {
        var fixture = RationaleOntologyFixture.Build();
        var graph = fixture.Graph;

        // INV-8 guard: the corpus is CLR-free, so every Npgsql hop/endpoint below
        // can ONLY resolve via the graph (SymbolKey reverse index), never typeof.
        foreach (var descriptor in graph.ObjectTypes)
        {
            await Assert.That(descriptor.ClrType).IsNull();
            await Assert.That(descriptor.SymbolKey).IsNotNull();
        }

        // --- Edge-view TRAVERSE: Decision --supersedesEdge--> Supersedes ---
        // The link target is named ONLY by the Supersedes association's SymbolKey,
        // so the Npgsql resolver routes the hop to the "Supersedes" partition via
        // the SAME reverse index the in-memory evaluator walks (DR-10).
        var supersedesHop = PgVectorObjectSetProvider.ResolveTraversalHop(
            graph,
            sourceDescriptorName: RationaleOntologyFixture.Decision,
            linkName: RationaleOntologyFixture.LinkSupersedesEdge,
            targetDescriptorOverride: null);

        await Assert.That(supersedesHop.TargetDescriptorName)
            .IsEqualTo(RationaleOntologyFixture.Supersedes);
        await Assert.That(supersedesHop.SourceTable).IsEqualTo("decision");
        await Assert.That(supersedesHop.TargetTable).IsEqualTo("supersedes");
        await Assert.That(supersedesHop.JunctionTable).IsEqualTo("decision_supersedes_edge");

        var supersedesSql = SqlGenerator.BuildInstanceAnchoredTraversalSql(
            "public",
            supersedesHop.SourceTable,
            supersedesHop.SourceKeyProperty,
            supersedesHop.JunctionTable,
            supersedesHop.TargetTable);

        await Assert.That(supersedesSql).Contains("FROM \"public\".\"decision\" s");
        await Assert.That(supersedesSql)
            .Contains("JOIN \"public\".\"decision_supersedes_edge\" j ON j.source_id = s.id");
        await Assert.That(supersedesSql)
            .Contains("JOIN \"public\".\"supersedes\" t ON t.id = j.target_id");
        await Assert.That(supersedesSql).Contains("s.data->>'Id' = @srcId");
        // INV-2: raw Npgsql — no event-store machinery leaks into the SQL.
        await Assert.That(supersedesSql).DoesNotContain("Marten");
        await Assert.That(supersedesSql).DoesNotContain("Wolverine");

        // --- Far-node TRAVERSE: Decision --supersededDecision--> Decision ---
        // A SEPARATE SymbolKey-only link whose target is the Decision NODE; the
        // Npgsql resolver routes it to the "Decision" partition via the same index.
        var farNodeHop = PgVectorObjectSetProvider.ResolveTraversalHop(
            graph,
            sourceDescriptorName: RationaleOntologyFixture.Decision,
            linkName: RationaleOntologyFixture.LinkSupersededDecision,
            targetDescriptorOverride: null);

        await Assert.That(farNodeHop.TargetDescriptorName)
            .IsEqualTo(RationaleOntologyFixture.Decision);
        await Assert.That(farNodeHop.TargetTable).IsEqualTo("decision");
        await Assert.That(farNodeHop.JunctionTable).IsEqualTo("decision_superseded_decision");

        // --- Edge-view TRAVERSE: Constraint --motivatesEdge--> Motivates ---
        // A DIFFERENT association partition (Motivates), proving the reverse index
        // routes each link to its own SymbolKey-named edge, not the first match.
        var motivatesHop = PgVectorObjectSetProvider.ResolveTraversalHop(
            graph,
            sourceDescriptorName: RationaleOntologyFixture.Constraint,
            linkName: RationaleOntologyFixture.LinkMotivatesEdge,
            targetDescriptorOverride: null);

        await Assert.That(motivatesHop.TargetDescriptorName)
            .IsEqualTo(RationaleOntologyFixture.Motivates);
        await Assert.That(motivatesHop.SourceTable).IsEqualTo("constraint");
        await Assert.That(motivatesHop.TargetTable).IsEqualTo("motivates");
        await Assert.That(motivatesHop.JunctionTable).IsEqualTo("constraint_motivates_edge");

        // --- Edge-view TRAVERSE: Decision --conflictsWithEdge--> ConflictsWith ---
        var conflictHop = PgVectorObjectSetProvider.ResolveTraversalHop(
            graph,
            sourceDescriptorName: RationaleOntologyFixture.Decision,
            linkName: RationaleOntologyFixture.LinkConflictsWithEdge,
            targetDescriptorOverride: null);

        await Assert.That(conflictHop.TargetDescriptorName)
            .IsEqualTo(RationaleOntologyFixture.ConflictsWith);
        await Assert.That(conflictHop.TargetTable).IsEqualTo("conflicts_with");
        await Assert.That(conflictHop.JunctionTable).IsEqualTo("decision_conflicts_with_edge");

        // --- Attributed RELATE: the reified associations lower to association-
        //     OBJECT tables with role-disambiguated endpoint FK columns. Drive the
        //     SAME ResolveAssociationRelate the production attributed RelateAsync
        //     walks, for each attributed relate row in the corpus. ---

        // Supersedes: Decision (From) -> Decision (To), a self-association.
        var supersedesPlan = PgVectorObjectSetProvider.ResolveAssociationRelate(
            graph,
            associationDescriptor: RationaleOntologyFixture.Supersedes,
            srcDescriptor: RationaleOntologyFixture.Decision,
            tgtDescriptor: RationaleOntologyFixture.Decision);

        await Assert.That(supersedesPlan.AssociationTable).IsEqualTo("supersedes");
        // Self-association: distinct role columns so each surrogate id routes home.
        await Assert.That(supersedesPlan.SourceColumn).IsEqualTo("from_id");
        await Assert.That(supersedesPlan.TargetColumn).IsEqualTo("to_id");
        await Assert.That(supersedesPlan.SourceTable).IsEqualTo("decision");
        await Assert.That(supersedesPlan.TargetTable).IsEqualTo("decision");

        var supersedesInsert = SqlGenerator.BuildAssociationRelateInsertSql(
            "public",
            supersedesPlan.AssociationTable,
            supersedesPlan.SourceColumn,
            supersedesPlan.SourceTable,
            supersedesPlan.SourceKeyProperty,
            supersedesPlan.TargetColumn,
            supersedesPlan.TargetTable,
            supersedesPlan.TargetKeyProperty);

        await Assert.That(supersedesInsert)
            .Contains("INSERT INTO \"public\".\"supersedes\" (id, data, from_id, to_id)");
        await Assert.That(supersedesInsert).Contains("s.data->>'Id' = @srcId");
        await Assert.That(supersedesInsert).Contains("t.data->>'Id' = @tgtId");

        // Motivates: Constraint (From) -> Decision (To), heterogeneous endpoints.
        var motivatesPlan = PgVectorObjectSetProvider.ResolveAssociationRelate(
            graph,
            associationDescriptor: RationaleOntologyFixture.Motivates,
            srcDescriptor: RationaleOntologyFixture.Constraint,
            tgtDescriptor: RationaleOntologyFixture.Decision);

        await Assert.That(motivatesPlan.AssociationTable).IsEqualTo("motivates");
        await Assert.That(motivatesPlan.SourceTable).IsEqualTo("constraint");
        await Assert.That(motivatesPlan.TargetTable).IsEqualTo("decision");
        await Assert.That(motivatesPlan.SourceColumn).IsEqualTo("from_id");
        await Assert.That(motivatesPlan.TargetColumn).IsEqualTo("to_id");

        // ConflictsWith: Decision (From) -> Decision (To), self-association.
        var conflictPlan = PgVectorObjectSetProvider.ResolveAssociationRelate(
            graph,
            associationDescriptor: RationaleOntologyFixture.ConflictsWith,
            srcDescriptor: RationaleOntologyFixture.Decision,
            tgtDescriptor: RationaleOntologyFixture.Decision);

        await Assert.That(conflictPlan.AssociationTable).IsEqualTo("conflicts_with");
        await Assert.That(conflictPlan.SourceColumn).IsEqualTo("from_id");
        await Assert.That(conflictPlan.TargetColumn).IsEqualTo("to_id");
    }

    // -----------------------------------------------------------------------
    // (2) EXECUTION parity — DB-GATED. Skips unless STRATEGOS_PG_TEST_CONN names
    //     a reachable Postgres. Seeds the corpus, replays the relate rows, reads
    //     them back through the SAME traversal expressions, and asserts byte-
    //     identical results vs. the in-memory replay.
    // -----------------------------------------------------------------------

    [Test]
    [SkipIfNoPostgres]
    public async Task RationaleOntology_SameCorpus_InMemoryAndNpgsql_ProduceIdenticalResults()
    {
        var connectionString = Environment.GetEnvironmentVariable(SkipIfNoPostgresAttribute.ConnectionEnvVar);

        // The skip attribute guarantees this only runs with a connection string;
        // assert it defensively so a misconfigured CI lane fails loud, not silent.
        await Assert.That(connectionString).IsNotNull();

        var fixture = RationaleOntologyFixture.Build();

        // The corpus's traversals, keyed for cross-backend comparison. Each
        // carries the provider-agnostic expression (for the in-memory oracle) plus
        // the source/link coordinates the Npgsql read-back resolves against.
        var traversals = new Dictionary<string, RationaleTraversal>(StringComparer.Ordinal)
        {
            ["supersedesEdge"] = new(
                fixture.TraverseSupersedesEdges("D1"),
                RationaleOntologyFixture.Decision,
                "D1",
                RationaleOntologyFixture.LinkSupersedesEdge),
            ["supersededDecision"] = new(
                fixture.TraverseSupersededDecision("D1"),
                RationaleOntologyFixture.Decision,
                "D1",
                RationaleOntologyFixture.LinkSupersededDecision),
            ["motivatesEdge"] = new(
                fixture.TraverseMotivatesEdges("C1"),
                RationaleOntologyFixture.Constraint,
                "C1",
                RationaleOntologyFixture.LinkMotivatesEdge),
            ["conflictsWithEdge"] = new(
                fixture.TraverseConflictsWithEdges("D1"),
                RationaleOntologyFixture.Decision,
                "D1",
                RationaleOntologyFixture.LinkConflictsWithEdge),
        };

        // --- Provider-agnostic ORACLE: the in-memory replay of the corpus. ---
        var inMemory = new InMemoryRationaleEvaluator(fixture);
        var oracle = Project(inMemory, traversals);

        // --- The Npgsql replay of the SAME corpus + relate rows. ---
        await using var harness = await NpgsqlRationaleHarness.CreateAsync(connectionString!, fixture.Graph);
        await harness.SeedAsync(fixture.InstancesByDescriptor);
        await harness.ReplayRelationsAsync(fixture.Relations);

        var actual = Project(harness.Evaluator, traversals);

        // --- Parity: identical observable results across providers. ---
        foreach (var (key, expected) in oracle)
        {
            await Assert.That(actual[key]).IsEquivalentTo(expected);
        }
    }

    private static Dictionary<string, IReadOnlyList<(string Id, string? Title, string? Rationale, string? Weight, string? Severity)>>
        Project(
            IRationaleEvaluator evaluator,
            IReadOnlyDictionary<string, RationaleTraversal> traversals)
    {
        var projected = new Dictionary<string, IReadOnlyList<(string, string?, string?, string?, string?)>>(StringComparer.Ordinal);
        foreach (var (key, traversal) in traversals)
        {
            projected[key] = evaluator.Evaluate(traversal)
                .Select(n => (
                    n.Id,
                    n.Get("title"),
                    n.Get("rationale"),
                    n.Get("weight"),
                    n.Get("severity")))
                .ToList();
        }

        return projected;
    }
}
