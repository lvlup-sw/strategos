using global::Npgsql;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests;

/// <summary>
/// DR-13 (R6, #130): the NPGSQL half of the <c>RelateBatchAsync</c> contract
/// reservation. The Npgsql provider reserves the batch surface but DEFERS the
/// set-based DML to #115 (bulk edge ingestion) — calling it throws
/// <see cref="NotSupportedException"/> rather than silently degrading to a
/// per-edge round-trip loop (which would mask the missing batched lowering). The
/// throw is unconditional and DB-free, so this needs no live Postgres.
/// </summary>
/// <remarks>
/// The in-memory loop half lives in
/// <c>Strategos.Ontology.Tests/ObjectSets/RelateBatchContractTests</c>; that
/// project does not reference the Npgsql provider, so the throw assertion lives
/// here.
/// </remarks>
public class RelateBatchContractTests
{
    [Test]
    public async Task RelateBatchAsync_Npgsql_ThrowsNotSupportedUntilIngestion()
    {
        var embedding = Substitute.For<IEmbeddingProvider>();
        embedding.Dimensions.Returns(3);

        // A bogus data source: the throw must happen BEFORE any connection is
        // opened, so the provider never reaches the (unreachable) database.
        await using var dataSource = NpgsqlDataSource.Create(
            "Host=unreachable.invalid;Database=none;Username=none;Password=none");

        var provider = new PgVectorObjectSetProvider(
            dataSource,
            embedding,
            Options.Create(new PgVectorOptions { Schema = "public" }),
            NullLogger<PgVectorObjectSetProvider>.Instance,
            graph: null);

        IObjectSetWriter writer = provider;

        var requests = new List<RelateRequest>
        {
            new()
            {
                SourceDescriptor = "a",
                SourceId = "1",
                LinkName = "links_to",
                TargetDescriptor = "a",
                TargetId = "2",
            },
        };

        await Assert.That(async () => await writer.RelateBatchAsync(requests))
            .Throws<NotSupportedException>();
    }
}
