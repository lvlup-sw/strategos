using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using global::Npgsql;
using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.Tests.Integration;

namespace Strategos.Ontology.Npgsql.Tests.Integration;

/// <summary>
/// DR-9 (t13) DB-GATED execution harness for the cross-provider rationale-ontology
/// parity test. It replays T12's provider-agnostic
/// <see cref="RationaleOntologyFixture"/> against a REAL Postgres so the SAME
/// SymbolKey-only corpus + relate rows can be asserted to produce identical
/// observable results across the in-memory evaluator and the Npgsql provider.
///
/// <para>
/// The harness drives the PRODUCTION Npgsql SQL path throughout (INV-2: raw
/// Npgsql/pgvector, no Marten/Wolverine):
/// </para>
/// <list type="bullet">
///   <item><description>
///     Schema is created from the fixture graph via the SAME
///     <see cref="SqlGenerator"/> DDL builders the provider's schema path uses
///     (junction tables + association-object tables).
///   </description></item>
///   <item><description>
///     Plain (DR-2) relates replay through the SAME junction-insert SQL the
///     provider emits, resolved via the provider's
///     <see cref="PgVectorObjectSetProvider.ResolveTraversalHop"/>.
///   </description></item>
///   <item><description>
///     Attributed (DR-4) relates replay through the SAME association-object
///     INSERT the provider's attributed <c>RelateAsync&lt;TRel&gt;</c> emits,
///     resolved via <see cref="PgVectorObjectSetProvider.ResolveAssociationRelate"/>.
///   </description></item>
/// </list>
///
/// <para>
/// Seeding writes each carrier's full attribute bag as <c>data jsonb</c>: the
/// SymbolKey-only <see cref="RationaleNode"/> exposes attributes through a method
/// (not serializable CLR properties), so the harness shapes the JSONB itself and
/// the read-back maps it back the same way — preserving the exact observable
/// shape the in-memory oracle exposes.
/// </para>
/// </summary>
internal sealed class NpgsqlRationaleHarness : IAsyncDisposable
{
    private const string Schema = "public";

    private readonly NpgsqlDataSource _dataSource;
    private readonly OntologyGraph _graph;

    // The seeded corpus, retained so the attributed-relate replay can recover an
    // association object's attribute bag by its business id.
    private IReadOnlyDictionary<string, IReadOnlyList<RationaleNode>> _seeded =
        new Dictionary<string, IReadOnlyList<RationaleNode>>(StringComparer.Ordinal);

    private NpgsqlRationaleHarness(NpgsqlDataSource dataSource, OntologyGraph graph)
    {
        _dataSource = dataSource;
        _graph = graph;
    }

    /// <summary>The Npgsql-backed read-back evaluator used by the parity
    /// assertion. It runs the corpus's traversals against the live store and maps
    /// <c>data jsonb</c> back into <see cref="RationaleNode"/> carriers — the SAME
    /// shape the in-memory evaluator yields.</summary>
    public IRationaleEvaluator Evaluator => new NpgsqlRationaleEvaluator(_dataSource, _graph, Schema);

    /// <summary>
    /// Opens a data source against <paramref name="connectionString"/> and
    /// provisions a CLEAN schema for the rationale corpus: an object table per
    /// node/association descriptor, a junction table per plain link, and an
    /// association-object table per association descriptor.
    /// </summary>
    public static async Task<NpgsqlRationaleHarness> CreateAsync(
        string connectionString, OntologyGraph graph)
    {
        var dataSource = NpgsqlDataSource.Create(connectionString);
        var harness = new NpgsqlRationaleHarness(dataSource, graph);
        await harness.CreateSchemaAsync().ConfigureAwait(false);
        return harness;
    }

    /// <summary>
    /// Seeds the corpus into the object tables under the SAME descriptor
    /// partitions the in-memory store uses, writing each carrier's full attribute
    /// bag as <c>data jsonb</c> (Id plus the node/edge attributes).
    /// </summary>
    public async Task SeedAsync(
        IReadOnlyDictionary<string, IReadOnlyList<RationaleNode>> instancesByDescriptor)
    {
        _seeded = instancesByDescriptor;

        foreach (var (descriptorName, nodes) in instancesByDescriptor)
        {
            var table = TypeMapper.ToSnakeCase(descriptorName);
            foreach (var node in nodes)
            {
                var json = ToJson(descriptorName, node);
                await ExecuteAsync(
                    $"INSERT INTO \"{Schema}\".\"{table}\" (data) VALUES (@data::jsonb);",
                    ("data", json)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Replays the corpus relate SCRIPT against Npgsql. A row with an association
    /// object id is an ATTRIBUTED relate (association-object INSERT); a row without
    /// is a plain (DR-2) relate (junction INSERT). Both resolve via the SAME
    /// provider resolvers production RelateAsync walks.
    /// </summary>
    public async Task ReplayRelationsAsync(
        IReadOnlyList<RationaleOntologyFixture.RelateRow> relations)
    {
        foreach (var row in relations)
        {
            if (row.AssociationObjectId is { } associationId)
            {
                await ReplayAttributedRelateAsync(row, associationId).ConfigureAwait(false);
            }
            else
            {
                await ReplayPlainRelateAsync(row).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask DisposeAsync() =>
        await _dataSource.DisposeAsync().ConfigureAwait(false);

    private async Task CreateSchemaAsync()
    {
        // Drop + recreate every rationale table so each run starts clean (the
        // parity assertion compares exact result sets, so residue would corrupt
        // it). Object tables for nodes AND associations; junction tables for the
        // plain links; association-object tables for the reified associations.
        foreach (var descriptor in _graph.ObjectTypes)
        {
            var table = TypeMapper.ToSnakeCase(descriptor.Name);
            await ExecuteAsync($"DROP TABLE IF EXISTS \"{Schema}\".\"{table}\" CASCADE;").ConfigureAwait(false);
        }

        foreach (var descriptor in _graph.ObjectTypes)
        {
            var table = TypeMapper.ToSnakeCase(descriptor.Name);
            // A minimal object table (id + data jsonb) — the same id/data shape the
            // provider's object tables carry, sans the optional embedding.
            await ExecuteAsync(
                $"CREATE TABLE \"{Schema}\".\"{table}\" "
                + "(id uuid PRIMARY KEY DEFAULT gen_random_uuid(), data jsonb NOT NULL);")
                .ConfigureAwait(false);
        }

        // Junction tables for the plain (DR-2) links — those whose corpus relate
        // rows carry NO association object id.
        foreach (var (sourceDescriptor, link, targetDescriptor) in PlainLinks())
        {
            var ddl = SqlGenerator.BuildJunctionTableDdl(
                Schema,
                TypeMapper.ToSnakeCase(sourceDescriptor),
                link,
                TypeMapper.ToSnakeCase(targetDescriptor));
            await ExecuteAsync(ddl).ConfigureAwait(false);
        }

        // Association-object tables for the reified associations.
        foreach (var association in _graph.ObjectTypes.Where(d => d.Kind == ObjectKind.Association))
        {
            var ddl = SqlGenerator.BuildAssociationObjectTableDdl(Schema, association);
            await ExecuteAsync(ddl).ConfigureAwait(false);
        }
    }

    // The plain (junction) links present in the corpus — supersededDecision is the
    // lone far-node (DR-2) link; the rest are attributed (association-object).
    private static IEnumerable<(string SourceDescriptor, string LinkName, string TargetDescriptor)> PlainLinks()
    {
        yield return (
            RationaleOntologyFixture.Decision,
            RationaleOntologyFixture.LinkSupersededDecision,
            RationaleOntologyFixture.Decision);
    }

    private async Task ReplayPlainRelateAsync(RationaleOntologyFixture.RelateRow row)
    {
        // Resolve the hop the SAME way the provider's traversal/relate path does,
        // then emit the SAME junction-insert SQL the provider's RelateAsync emits.
        var hop = PgVectorObjectSetProvider.ResolveTraversalHop(
            _graph, row.SourceDescriptor, row.LinkName, targetDescriptorOverride: null);

        var sql = SqlGenerator.BuildRelateInsertSql(
            Schema,
            hop.JunctionTable,
            hop.SourceTable,
            hop.SourceKeyProperty,
            hop.TargetTable,
            ResolveKey(row.TargetDescriptor));

        await ExecuteAsync(sql, ("srcId", row.SourceId), ("tgtId", row.TargetId)).ConfigureAwait(false);
    }

    private async Task ReplayAttributedRelateAsync(
        RationaleOntologyFixture.RelateRow row, string associationId)
    {
        var association = _graph.ObjectTypes.Single(d => d.Name == row.TargetDescriptor);
        var sourceEndpoint = association.AssociationEndpoints
            .First(e => e.DescriptorName == row.SourceDescriptor);
        var targetEndpoint = association.AssociationEndpoints
            .First(e => !ReferenceEquals(e, sourceEndpoint));

        var plan = PgVectorObjectSetProvider.ResolveAssociationRelate(
            _graph, association.Name, sourceEndpoint.DescriptorName, targetEndpoint.DescriptorName);

        var sql = SqlGenerator.BuildAssociationRelateInsertSql(
            Schema,
            plan.AssociationTable,
            plan.SourceColumn,
            plan.SourceTable,
            plan.SourceKeyProperty,
            plan.TargetColumn,
            plan.TargetTable,
            plan.TargetKeyProperty);

        // The corpus's edge-view relate rows do not name the far node; the parity
        // oracle only reads the association object's edge attributes, so any
        // existing far-endpoint row satisfies the FK.
        var farId = ResolveFarEndpointId(row.SourceId, sourceEndpoint, targetEndpoint);
        var data = AssociationJson(association.Name, associationId);

        await ExecuteAsync(
            sql,
            ("id", Guid.NewGuid()),
            ("data", data),
            ("srcId", row.SourceId),
            ("tgtId", farId)).ConfigureAwait(false);
    }

    private string ResolveFarEndpointId(
        string sourceId, AssociationEndpoint sourceEndpoint, AssociationEndpoint targetEndpoint)
    {
        var farTable = TypeMapper.ToSnakeCase(targetEndpoint.DescriptorName);
        var farKey = ResolveKey(targetEndpoint.DescriptorName);
        var selfAssociation = sourceEndpoint.DescriptorName == targetEndpoint.DescriptorName;

        // For a self-association pick a far row distinct from the source; otherwise
        // any far-endpoint row.
        var sql =
            $"SELECT data->>'{farKey}' FROM \"{Schema}\".\"{farTable}\" "
            + (selfAssociation ? $"WHERE data->>'{farKey}' <> @srcId " : string.Empty)
            + "LIMIT 1;";

        using var cmd = _dataSource.CreateCommand(sql);
        if (selfAssociation)
        {
            cmd.Parameters.AddWithValue("srcId", sourceId);
        }

        return cmd.ExecuteScalar() as string
            ?? throw new InvalidOperationException(
                $"No far endpoint row available for association endpoint "
                + $"'{targetEndpoint.DescriptorName}'.");
    }

    private string ResolveKey(string descriptorName)
    {
        var descriptor = _graph.ObjectTypes.Single(d => d.Name == descriptorName);
        return descriptor.KeyProperty?.Name
            ?? throw new InvalidOperationException(
                $"Descriptor '{descriptorName}' declares no key property.");
    }

    private string AssociationJson(string descriptorName, string associationId)
    {
        var node = _seeded[descriptorName].Single(n => n.Id == associationId);
        return ToJson(descriptorName, node);
    }

    private static string ToJson(string descriptorName, RationaleNode node)
    {
        var bag = new Dictionary<string, string> { ["Id"] = node.Id };
        foreach (var key in AttributeKeysFor(descriptorName))
        {
            var value = node.Get(key);
            if (value is not null)
            {
                bag[key] = value;
            }
        }

        return JsonSerializer.Serialize(bag);
    }

    // The attribute key each descriptor partition carries in the corpus, kept in
    // lockstep with RationaleOntologyFixture's seeded instances.
    private static IEnumerable<string> AttributeKeysFor(string descriptorName) => descriptorName switch
    {
        RationaleOntologyFixture.Decision => ["title"],
        RationaleOntologyFixture.Constraint => ["title"],
        RationaleOntologyFixture.Supersedes => ["rationale"],
        RationaleOntologyFixture.Motivates => ["weight"],
        RationaleOntologyFixture.ConflictsWith => ["severity"],
        _ => [],
    };

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var cmd = _dataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
