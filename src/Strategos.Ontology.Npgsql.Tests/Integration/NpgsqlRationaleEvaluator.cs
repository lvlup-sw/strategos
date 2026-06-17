using System.Collections.Generic;
using System.Text.Json;
using global::Npgsql;
using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.Tests.Integration;

namespace Strategos.Ontology.Npgsql.Tests.Integration;

/// <summary>
/// DR-9 (t13) DB-gated Npgsql read-back evaluator. Executes the corpus's
/// traversals against the live store and maps <c>data jsonb</c> rows back into
/// <see cref="RationaleNode"/> carriers — the SAME observable shape
/// <c>InMemoryExpressionEvaluator</c> yields — so the parity assertion compares
/// like-for-like across providers.
///
/// <para>
/// Each traversal is resolved through the provider's GRAPH-driven hop resolver
/// (DR-10/INV-8, never <c>typeof</c>) and dispatched on the link's resolved
/// target kind:
/// </para>
/// <list type="bullet">
///   <item><description>
///     an ASSOCIATION target (edge-view) projects the reified association
///     object's <c>data</c> by its source endpoint FK column;
///   </description></item>
///   <item><description>
///     a NODE target (far-node) runs the SAME production junction-traversal SQL
///     the Npgsql provider emits (<see cref="SqlGenerator.BuildInstanceAnchoredTraversalSql"/>).
///   </description></item>
/// </list>
/// </summary>
internal sealed class NpgsqlRationaleEvaluator : IRationaleEvaluator
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly OntologyGraph _graph;
    private readonly string _schema;

    public NpgsqlRationaleEvaluator(NpgsqlDataSource dataSource, OntologyGraph graph, string schema)
    {
        _dataSource = dataSource;
        _graph = graph;
        _schema = schema;
    }

    public IReadOnlyList<RationaleNode> Evaluate(RationaleTraversal traversal)
    {
        var hop = PgVectorObjectSetProvider.ResolveTraversalHop(
            _graph, traversal.SourceDescriptor, traversal.LinkName, targetDescriptorOverride: null);

        var target = _graph.ObjectTypes.Single(d => d.Name == hop.TargetDescriptorName);

        return target.Kind == ObjectKind.Association
            ? ReadEdgeView(traversal.SourceDescriptor, traversal.SourceId, target)
            : ReadFarNode(hop, traversal.SourceId);
    }

    // Edge-view read-back: the reified association objects whose SOURCE endpoint
    // FK references the anchor source row's surrogate id.
    private IReadOnlyList<RationaleNode> ReadEdgeView(
        string sourceDescriptor, string sourceId, ObjectTypeDescriptor association)
    {
        var sourceEndpoint = association.AssociationEndpoints
            .First(e => e.DescriptorName == sourceDescriptor);
        // QuoteIdentifier the {role}_id FK column to stay identifier-identical
        // with the quoted DDL column (review M1).
        var sourceColumn = SqlGenerator.QuoteIdentifier($"{TypeMapper.ToSnakeCase(sourceEndpoint.Role)}_id");

        var assocTable = TypeMapper.ToSnakeCase(association.Name);
        var sourceTable = TypeMapper.ToSnakeCase(sourceDescriptor);
        // Escape single quotes so a key carrying an apostrophe cannot break (or
        // inject into) the JSON-path string literal embedded in the SQL below.
        var sourceKey = ResolveKey(sourceDescriptor).Replace("'", "''");

        var sql =
            $"SELECT a.id, a.data FROM \"{_schema}\".\"{assocTable}\" a "
            + $"JOIN \"{_schema}\".\"{sourceTable}\" s ON s.id = a.{sourceColumn} "
            + $"WHERE s.data->>'{sourceKey}' = @srcId";

        return ReadNodes(sql, sourceId);
    }

    // Far-node read-back: the SAME production junction-traversal SQL the provider
    // emits for an instance-anchored hop.
    private IReadOnlyList<RationaleNode> ReadFarNode(
        PgVectorObjectSetProvider.TraversalHop hop, string sourceId)
    {
        var sql = SqlGenerator.BuildInstanceAnchoredTraversalSql(
            _schema, hop.SourceTable, hop.SourceKeyProperty, hop.JunctionTable, hop.TargetTable);
        return ReadNodes(sql, sourceId);
    }

    private IReadOnlyList<RationaleNode> ReadNodes(string sql, string sourceId)
    {
        var results = new List<RationaleNode>();
        using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("srcId", sourceId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var dataJson = reader.GetString(1);
            results.Add(FromJson(dataJson));
        }

        return results;
    }

    private string ResolveKey(string descriptorName)
    {
        var descriptor = _graph.ObjectTypes.Single(d => d.Name == descriptorName);
        return descriptor.KeyProperty?.Name ?? "Id";
    }

    private static RationaleNode FromJson(string json)
    {
        var bag = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var id = bag.TryGetValue("Id", out var value) ? value : string.Empty;
        return new RationaleNode(id, bag);
    }
}
