using System.Text;
using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.Npgsql.Schema;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.Schema;

/// <summary>
/// DR-11 (junction posture, #128): unit tests for the deterministic
/// identifier-length guard. PostgreSQL silently TRUNCATES any identifier at 63
/// BYTES (<c>NAMEDATALEN - 1</c>), so two distinct per-<c>(link, target-descriptor)</c>
/// junction names whose first 63 bytes coincide would collapse onto the SAME
/// physical table — a SILENT COLLISION, not an error. <see cref="JunctionIdentifier"/>
/// makes that failure mode mechanical: it truncates over-long names to
/// <c>(63 - suffix)</c> bytes and appends a deterministic hash suffix derived from
/// the FULL name, and it throws a typed <see cref="OntologySchemaIdentifierException"/>
/// when two distinct inputs would still derive the same identifier.
/// </summary>
/// <remarks>
/// These assert the DERIVED identifier strings only — no live database (INV-2:
/// raw Npgsql posture, pure/deterministic SQL-identity logic). The 63-byte limit
/// is measured in UTF-8 BYTES, not chars, mirroring Postgres' own
/// <c>NAMEDATALEN</c> accounting.
/// </remarks>
public class IdentifierGuardTests
{
    private const int PostgresIdentifierByteLimit = 63;

    [Test]
    public async Task IdentifierGuard_NameExceeds63Bytes_TruncatesWithDeterministicHashSuffix()
    {
        // A junction name comfortably over the 63-byte limit.
        var longName = new string('a', 80);

        var derived = JunctionIdentifier.Derive(longName);

        // 1. The derived identifier fits Postgres' 63-byte cap, so it can never be
        //    silently truncated (and thus never collide) by the server.
        await Assert.That(Encoding.UTF8.GetByteCount(derived))
            .IsLessThanOrEqualTo(PostgresIdentifierByteLimit);

        // 2. It is shorter than the input — the over-long name WAS truncated, not
        //    passed through unchanged.
        await Assert.That(derived.Length).IsLessThan(longName.Length);

        // 3. The suffix is a deterministic hash of the FULL name: deriving the same
        //    input twice yields the same identifier (so DDL and DML never drift).
        var again = JunctionIdentifier.Derive(longName);
        await Assert.That(derived).IsEqualTo(again);

        // 4. A name already within the limit passes through verbatim — short,
        //    honest identifiers are not gratuitously hashed.
        var shortName = "document_written_by";
        await Assert.That(JunctionIdentifier.Derive(shortName)).IsEqualTo(shortName);
    }

    [Test]
    public async Task IdentifierGuard_TwoLongDistinctDescriptors_DoNotCollide()
    {
        // Two DISTINCT over-long junction names whose first 63 bytes are IDENTICAL
        // — exactly the case Postgres' silent truncation would collapse into one
        // table. The deterministic hash suffix is derived from the FULL name, so
        // the two derived identifiers must differ.
        var sharedPrefix = new string('x', 70);
        var first = sharedPrefix + "_alpha";
        var second = sharedPrefix + "_beta";

        var derivedFirst = JunctionIdentifier.Derive(first);
        var derivedSecond = JunctionIdentifier.Derive(second);

        // Both fit the byte cap.
        await Assert.That(Encoding.UTF8.GetByteCount(derivedFirst))
            .IsLessThanOrEqualTo(PostgresIdentifierByteLimit);
        await Assert.That(Encoding.UTF8.GetByteCount(derivedSecond))
            .IsLessThanOrEqualTo(PostgresIdentifierByteLimit);

        // The two distinct inputs derive DISTINCT identifiers — no silent collision.
        await Assert.That(derivedFirst).IsNotEqualTo(derivedSecond);
    }

    [Test]
    public async Task IdentifierGuard_TwoInputsDerivingSameIdentifier_ThrowsTyped()
    {
        // F7: force the residual collision MECHANICALLY (no brute-force search). The
        // two-arg Derive throws OntologySchemaIdentifierException when two DISTINCT
        // inputs derive the SAME identifier. A deterministic such pair:
        //   N    = an over-long name (> 63 bytes), and
        //   D    = Derive(N), which is exactly 63 ASCII bytes (54-byte truncated
        //          prefix + '_' + 8 hex chars).
        // Derive(N) == D by construction, and because D already fits the 63-byte cap
        // Derive(D) returns D verbatim — so Derive(N) == Derive(D) == D while N != D.
        // That is precisely the (first != second) AND (derivedFirst == derivedSecond)
        // condition the guard rejects.
        var longName = new string('a', 80);
        var derivedLong = JunctionIdentifier.Derive(longName);

        // Sanity: the derived identifier is a DISTINCT input that sits within the cap
        // (so it passes through verbatim) yet collides with longName's derivation.
        await Assert.That(derivedLong).IsNotEqualTo(longName);
        await Assert.That(Encoding.UTF8.GetByteCount(derivedLong))
            .IsLessThanOrEqualTo(PostgresIdentifierByteLimit);
        await Assert.That(JunctionIdentifier.Derive(derivedLong)).IsEqualTo(derivedLong);

        // The two distinct inputs derive the same identifier — the guard makes it a
        // loud, typed, catchable error rather than a silent table merge.
        await Assert.That(() => JunctionIdentifier.Derive(longName, derivedLong))
            .Throws<OntologySchemaIdentifierException>();
    }

    // -----------------------------------------------------------------------
    // #130 R3a — the guard is applied at EVERY identifier-derivation site. The
    // DDL and the relate/unrelate/traversal DML must name the SAME physical
    // object for one logical (source, link) / descriptor / role, and no
    // generated identifier may exceed the cap. Each test pins a >63-byte
    // logical name against the production resolvers and SQL builders.
    // -----------------------------------------------------------------------

    private const string GuardDomain = "guard";
    private const string ShortTarget = "GuardTarget";

    [Test]
    public async Task LongMonomorphicJunction_DdlRelateUnrelateAndTraversal_NameTheSameDerivedTable()
    {
        // A long source descriptor + a long link: the raw {source}_{link} junction
        // name is over the 63-byte cap while both vertex names stay within it, so
        // ONLY the junction identifier is derived — and it must be derived
        // identically by the DDL, the relate/unrelate write path and the
        // traversal read path.
        var sourceName = "GuardSource" + new string('a', 29);
        var linkName = "GuardLink" + new string('b', 31);
        var graph = BuildLinkGraph(sourceName, linkName, ShortTarget);

        var sourceTable = TypeMapper.ToSnakeCase(sourceName);
        var targetTable = TypeMapper.ToSnakeCase(ShortTarget);
        var rawJunction = $"{sourceTable}_{TypeMapper.ToSnakeCase(linkName)}";
        await Assert.That(Encoding.UTF8.GetByteCount(sourceTable))
            .IsLessThanOrEqualTo(PostgresIdentifierByteLimit);
        await Assert.That(Encoding.UTF8.GetByteCount(rawJunction))
            .IsGreaterThan(PostgresIdentifierByteLimit);

        var expected = PgIdentifier.Derive(rawJunction);
        await Assert.That(expected).IsNotEqualTo(rawJunction);

        // DDL — both junction DDL builders create the DERIVED table.
        var ddl = SqlGenerator.BuildJunctionTableDdl("public", sourceTable, linkName, targetTable);
        await Assert.That(ddl).Contains($"CREATE TABLE IF NOT EXISTS \"public\".\"{expected}\"");
        await Assert.That(ddl).DoesNotContain(rawJunction);

        var junction = PgVectorObjectSetProvider.ResolveRelateJunction(
            graph, srcDescriptor: sourceName, linkName: linkName, tgtDescriptor: ShortTarget);
        await Assert.That(junction.IsPolymorphic).IsFalse();
        var resolvedDdl = SqlGenerator.BuildJunctionTableDdlForResolvedTargets("public", [junction]);
        await Assert.That(resolvedDdl[0]).Contains($"CREATE TABLE IF NOT EXISTS \"public\".\"{expected}\"");

        // Relate / unrelate DML — the write path names the SAME derived table.
        var relateName = SqlGenerator.JunctionTableNameFor(junction);
        await Assert.That(relateName).IsEqualTo(expected);

        var relateSql = SqlGenerator.BuildValidatingRelateInsertSql(
            "public", relateName, junction.SourceTable, "Id", junction.TargetTable, "Id");
        await Assert.That(relateSql).Contains($"\"public\".\"{expected}\"");

        var unrelateSql = SqlGenerator.BuildUnrelateDeleteSql(
            "public", relateName, junction.SourceTable, "Id", junction.TargetTable, "Id");
        await Assert.That(unrelateSql).Contains($"\"public\".\"{expected}\"");

        // Traversal DML — the read path (the pre-#130 drift site) names it too.
        await Assert.That(SqlGenerator.JunctionTableName(sourceTable, linkName)).IsEqualTo(expected);

        var hop = PgVectorObjectSetProvider.ResolveTraversalHop(
            graph, sourceDescriptorName: sourceName, linkName: linkName, targetDescriptorOverride: null);
        await Assert.That(hop.JunctionTable).IsEqualTo(expected);

        var traversalSql = SqlGenerator.BuildInstanceAnchoredTraversalSql(
            "public", hop.SourceTable, hop.SourceKeyProperty, hop.JunctionTable, hop.TargetTable);
        await Assert.That(traversalSql).Contains($"JOIN \"public\".\"{expected}\" j");
        await Assert.That(traversalSql).DoesNotContain(rawJunction);
    }

    [Test]
    public async Task LongAssociationDescriptor_DdlAndRelateDml_NameTheSameDerivedTableAndIndex()
    {
        // A long association descriptor AND a long endpoint descriptor: the
        // association-object table, its as-of-now index and the endpoint's FK
        // target are all over the cap and must all be derived — identically by
        // the DDL and by the attributed-relate plan the DML is built from.
        var assocName = "GuardEmployment" + new string('c', 55);
        var leftName = "GuardLeft" + new string('d', 61);
        var graph = BuildAssociationGraph(assocName, "From", leftName, "To", ShortTarget);
        var association = graph.ObjectTypes.Single(o => o.Name == assocName);

        var rawTable = TypeMapper.ToSnakeCase(assocName);
        var rawLeft = TypeMapper.ToSnakeCase(leftName);
        await Assert.That(Encoding.UTF8.GetByteCount(rawTable)).IsGreaterThan(PostgresIdentifierByteLimit);
        await Assert.That(Encoding.UTF8.GetByteCount(rawLeft)).IsGreaterThan(PostgresIdentifierByteLimit);

        var expectedTable = PgIdentifier.Derive(rawTable);
        var expectedLeft = PgIdentifier.Derive(rawLeft);
        var expectedIndex = PgIdentifier.Derive($"idx_{expectedTable}_as_of_now");
        await Assert.That(Encoding.UTF8.GetByteCount(expectedIndex))
            .IsLessThanOrEqualTo(PostgresIdentifierByteLimit);

        var ddl = SqlGenerator.BuildAssociationObjectTableDdl("public", association);
        await Assert.That(ddl).Contains($"CREATE TABLE IF NOT EXISTS \"public\".\"{expectedTable}\"");
        await Assert.That(ddl).Contains($"REFERENCES \"public\".\"{expectedLeft}\" (id)");
        await Assert.That(ddl).Contains($"CREATE INDEX IF NOT EXISTS \"{expectedIndex}\"");
        await Assert.That(ddl).DoesNotContain(rawTable);
        await Assert.That(ddl).DoesNotContain(rawLeft);

        var plan = PgVectorObjectSetProvider.ResolveAssociationRelate(
            graph, associationDescriptor: assocName, srcDescriptor: leftName, tgtDescriptor: ShortTarget);
        await Assert.That(plan.AssociationTable).IsEqualTo(expectedTable);
        await Assert.That(plan.SourceTable).IsEqualTo(expectedLeft);

        var insert = SqlGenerator.BuildAssociationRelateInsertSql(
            "public",
            plan.AssociationTable,
            plan.SourceColumn,
            plan.SourceTable,
            plan.SourceKeyProperty,
            plan.TargetColumn,
            plan.TargetTable,
            plan.TargetKeyProperty);
        await Assert.That(insert).Contains($"INSERT INTO \"public\".\"{expectedTable}\"");
        await Assert.That(insert).Contains($"\"public\".\"{expectedLeft}\"");

        var asOf = SqlGenerator.BuildAsOfTransactionTimeSql("public", plan.AssociationTable);
        await Assert.That(asOf).Contains($"FROM \"public\".\"{expectedTable}\"");
    }

    [Test]
    public async Task LongVertexDescriptor_EveryTableNameResolverAndTheDdl_UseTheSameDerivedTable()
    {
        // A long vertex descriptor: every table-name resolver the provider routes
        // through (read path, explicit-name write path, schema bootstrap, relate
        // endpoint, default overload) must agree on ONE derived identifier, and
        // the vertex DDL must create that table with capped index names.
        var vertexName = "GuardVertex" + new string('e', 60);
        var raw = TypeMapper.ToSnakeCase(vertexName);
        await Assert.That(Encoding.UTF8.GetByteCount(raw)).IsGreaterThan(PostgresIdentifierByteLimit);

        var expected = PgIdentifier.Derive(raw);
        await Assert.That(expected).IsNotEqualTo(raw);
        var graph = BuildLinkGraph(vertexName, "GuardLink", ShortTarget);

        await Assert.That(PgVectorObjectSetProvider.ResolveTableName(
                new RootExpression(typeof(GuardSourceNode), vertexName)))
            .IsEqualTo(expected);
        await Assert.That(PgVectorObjectSetProvider.ResolveTableNameForDescriptor(vertexName))
            .IsEqualTo(expected);
        await Assert.That(PgVectorObjectSetProvider.ResolveEnsureSchemaTableName<GuardSourceNode>(vertexName, graph: null))
            .IsEqualTo(expected);
        await Assert.That(PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload<GuardSourceNode>(graph))
            .IsEqualTo(expected);
        await Assert.That(PgVectorObjectSetProvider.ResolveRelateEndpoint(graph, vertexName).TableName)
            .IsEqualTo(expected);

        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            "public", expected, 3, PgVectorIndexType.Hnsw, keyPropertyName: "Id");
        await Assert.That(ddl).Contains($"CREATE TABLE IF NOT EXISTS \"public\".\"{expected}\"");

        // The embedding index is named from the (already-capped) table name and
        // must itself stay within the cap.
        var embeddingIndex = PgIdentifier.Derive($"idx_{expected}_embedding");
        await Assert.That(Encoding.UTF8.GetByteCount(embeddingIndex))
            .IsLessThanOrEqualTo(PostgresIdentifierByteLimit);
        await Assert.That(ddl).Contains($"CREATE INDEX IF NOT EXISTS \"{embeddingIndex}\"");
    }

    [Test]
    public async Task LongEndpointRoles_SharingA63BytePrefix_DeriveDistinctCappedColumnsInDdlAndDml()
    {
        // Two roles whose {role}_id columns are over the cap and IDENTICAL in their
        // first 63 bytes — exactly what PostgreSQL would silently collapse into a
        // duplicate column. The derived columns must be distinct, capped, and the
        // same in the DDL and the attributed-relate DML.
        var sharedRole = "GuardRole" + new string('f', 56);
        var roleA = sharedRole + "Alpha";
        var roleB = sharedRole + "Beta";
        var rawA = $"{TypeMapper.ToSnakeCase(roleA)}_id";
        var rawB = $"{TypeMapper.ToSnakeCase(roleB)}_id";
        await Assert.That(Encoding.UTF8.GetByteCount(rawA)).IsGreaterThan(PostgresIdentifierByteLimit);
        await Assert.That(rawA[..PostgresIdentifierByteLimit]).IsEqualTo(rawB[..PostgresIdentifierByteLimit]);

        var expectedA = PgIdentifier.Derive(rawA);
        var expectedB = PgIdentifier.Derive(rawB);
        await Assert.That(expectedA).IsNotEqualTo(expectedB);

        const string pairing = "GuardPairing";
        var graph = BuildAssociationGraph(pairing, roleA, "GuardLeft", roleB, ShortTarget);
        var association = graph.ObjectTypes.Single(o => o.Name == pairing);

        var ddl = SqlGenerator.BuildAssociationObjectTableDdl("public", association);
        await Assert.That(ddl).Contains($"\"{expectedA}\" uuid NOT NULL");
        await Assert.That(ddl).Contains($"\"{expectedB}\" uuid NOT NULL");
        await Assert.That(ddl).DoesNotContain(rawA);
        await Assert.That(ddl).DoesNotContain(rawB);

        var plan = PgVectorObjectSetProvider.ResolveAssociationRelate(
            graph, associationDescriptor: pairing, srcDescriptor: "GuardLeft", tgtDescriptor: ShortTarget);
        await Assert.That(plan.SourceColumn).IsEqualTo(expectedA);
        await Assert.That(plan.TargetColumn).IsEqualTo(expectedB);

        var insert = SqlGenerator.BuildAssociationRelateInsertSql(
            "public",
            plan.AssociationTable,
            plan.SourceColumn,
            plan.SourceTable,
            plan.SourceKeyProperty,
            plan.TargetColumn,
            plan.TargetTable,
            plan.TargetKeyProperty);
        await Assert.That(insert).Contains($"(id, data, \"{expectedA}\", \"{expectedB}\")");

        // Roles that normalize to the SAME column (a case-only difference) are
        // still refused with the typed normalization-collision error — the guard
        // compares DERIVED columns, so it can never let two roles silently merge.
        var colliding = new ObjectTypeDescriptor
        {
            Name = "GuardCollide",
            DomainName = GuardDomain,
            ClrType = typeof(GuardEdge),
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint("Owner", "GuardLeft"),
                new AssociationEndpoint("owner", ShortTarget),
            ],
        };
        await Assert.That(() => SqlGenerator.BuildAssociationObjectTableDdl("public", colliding))
            .Throws<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    // Graph fixtures — built through the internal OntologyGraph constructor so a
    // descriptor/link/role name of arbitrary length can be pinned directly.
    // -----------------------------------------------------------------------

    private static OntologyGraph BuildLinkGraph(string sourceName, string linkName, string targetName)
    {
        var source = new ObjectTypeDescriptor
        {
            Name = sourceName,
            DomainName = GuardDomain,
            ClrType = typeof(GuardSourceNode),
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            Links = [new LinkDescriptor(linkName, targetName, LinkCardinality.OneToMany)],
        };

        return BuildGraph(source, Vertex(targetName, typeof(GuardTargetNode)));
    }

    private static OntologyGraph BuildAssociationGraph(
        string associationName, string leftRole, string leftName, string rightRole, string rightName)
    {
        var association = new ObjectTypeDescriptor
        {
            Name = associationName,
            DomainName = GuardDomain,
            ClrType = typeof(GuardEdge),
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint(leftRole, leftName),
                new AssociationEndpoint(rightRole, rightName),
            ],
        };

        return BuildGraph(
            Vertex(leftName, typeof(GuardSourceNode)),
            Vertex(rightName, typeof(GuardTargetNode)),
            association);
    }

    private static ObjectTypeDescriptor Vertex(string name, Type clrType) => new()
    {
        Name = name,
        DomainName = GuardDomain,
        ClrType = clrType,
        KeyProperty = new PropertyDescriptor("Id", typeof(string)),
    };

    private static OntologyGraph BuildGraph(params ObjectTypeDescriptor[] objectTypes) => new(
        domains: [new DomainDescriptor(GuardDomain) { ObjectTypes = objectTypes }],
        objectTypes: objectTypes,
        interfaces: [],
        crossDomainLinks: [],
        workflowChains: [],
        objectTypeNamesByType: objectTypes
            .Where(o => o.ClrType is not null)
            .GroupBy(o => o.ClrType!)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(o => o.Name).ToList()));
}

// Distinct CLR carriers so the type->descriptor reverse index resolves each
// long-named descriptor unambiguously in the default-overload resolver.
public sealed record GuardSourceNode(string Id);

public sealed record GuardTargetNode(string Id);

public sealed record GuardEdge(string Id);
