// -----------------------------------------------------------------------
// <copyright file="MixedVersionRolloutTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using JasperFx.Resources;

using Marten;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

using Wolverine;
using Wolverine.Marten;
using Wolverine.Tracking;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// Reproduces the rolling-deploy hazard across the derived-runtime boundary on a real
/// Wolverine + Marten host.
/// </summary>
/// <remarks>
/// <para>
/// Marten's default serializer is <c>System.Text.Json</c> with the default
/// <c>JsonUnmappedMemberHandling.Skip</c>, so an instance running a build that predates typed
/// derived compensation does not fail when it loads a typed saga document — it drops the
/// derived members, and its next <c>Update</c> writes the row back without them. This test
/// performs exactly that write-back with one SQL statement, which is what the older build's
/// round-trip amounts to, then drives the next forward completion through the current build.
/// </para>
/// <para>
/// No in-repo test can run two builds against one database, so the older build is represented
/// by its only observable effect on the row. The control makes the difference load-bearing:
/// the same message against the same document, unstripped, is a quiet no-op.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
public sealed class MixedVersionRolloutTests
{
    private const string SagaDocumentTable = "public.mt_doc_derivedcompensationproofsaga";

    private const string CorruptJournalMessage =
        "Completion journal changed or became corrupt before a forward result; saga retained.";

    /// <summary>
    /// The derived compensation members a pre-#169 build cannot see, and therefore drops.
    /// This list is the fixture's copy of SagaPropertiesEmitter's derived member block; the
    /// test asserts every one of them is present in the row before it strips them.
    /// </summary>
    private static readonly string[] DerivedCompensationMembers =
    [
        "CompensationJournalSchemaVersion",
        "CompensationJournal",
        "ForwardDispatchClaims",
        "PendingPostCompletionFailureClaims",
        "ConsumedFailureTriggerClaims",
        "CompensationJournalSequence",
        "ActiveCompensationScopeKey",
        "FailedCompensationOccurrenceKey",
        "PendingCompensationForkId",
        "PendingCompensationScopeKey",
        "CompensationOutcomeUnknown",
        "CompensationFailureMessage",
        "CompensationRollbackFinished",
    ];

    /// <summary>
    /// A saga whose derived members were dropped by an older build is parked in
    /// <c>Failed</c> with the corruption message on its next forward completion, while the
    /// identical unstripped saga takes the same message as a no-op.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StrippedJournal_OnNextForwardCompletion_RetainsTheSagaInFailed()
    {
        await using var postgres = new PostgresFixture();
        await postgres.InitializeAsync();
        using var host = await BuildHostAsync(postgres.ConnectionString);

        var store = host.Services.GetRequiredService<IDocumentStore>();
        var forwardExecutionId = Guid.NewGuid();
        var strippedId = Guid.NewGuid();
        var controlId = Guid.NewGuid();

        // A saga document in the shape the derived runtime writes it: schema version 1 and one
        // completed journal entry for the first forward occurrence, matching the exact topology
        // the generated handler journals.
        await StoreAsync(store, BuildInFlightSaga(strippedId, forwardExecutionId));
        await StoreAsync(store, BuildInFlightSaga(controlId, forwardExecutionId));

        // The row really carries the derived members before the older build touches it.
        var keysBefore = await ReadDocumentKeysAsync(postgres.ConnectionString, strippedId);
        foreach (var member in DerivedCompensationMembers)
        {
            await Assert.That(keysBefore).Contains(member)
                .Because("the strip below is only meaningful if the member was persisted");
        }

        // The older build's round-trip: it cannot map these members, so they are absent from
        // the document it writes back.
        await StripDerivedMembersAsync(postgres.ConnectionString, strippedId);

        var keysAfter = await ReadDocumentKeysAsync(postgres.ConnectionString, strippedId);
        foreach (var member in DerivedCompensationMembers)
        {
            await Assert.That(keysAfter).DoesNotContain(member);
        }

        // Reloading the stripped row through the full type does not throw: the schema version
        // takes its CLR default and the journal's property initializer wins, so the loss is
        // silent rather than loud.
        var stripped = await LoadAsync(store, strippedId);
        await Assert.That(stripped).IsNotNull()
            .Because("unmapped-member handling is Skip, so a stripped row still deserializes");
        await Assert.That(stripped!.CompensationJournalSchemaVersion).IsEqualTo(0);
        await Assert.That(stripped.CompensationJournal).IsNotNull();
        await Assert.That(stripped.CompensationJournal).IsEmpty();

        // Drive the next forward completion through the current build.
        var completion = new DerivedForwardAStepCompleted(
            strippedId,
            forwardExecutionId,
            new DerivedCompensationState { WorkflowId = strippedId, Stage = 1 },
            null,
            DateTimeOffset.UtcNow);
        await PublishAsync(host, completion);

        var parked = await LoadAsync(store, strippedId);
        await Assert.That(parked).IsNotNull()
            .Because("the documented outcome is a retained saga, not a deleted one");
        await Assert.That(parked!.Phase).IsEqualTo(DerivedCompensationProofPhase.Failed);
        await Assert.That(parked.CompensationFailureMessage).IsEqualTo(CorruptJournalMessage)
            .Because(
                "the message an operator reads names the journal, not the deploy that stripped it");

        // Control: the same message against the same document, unstripped, is a no-op. Without
        // this the assertion above would also pass for a saga that was simply never journaled.
        var controlCompletion = new DerivedForwardAStepCompleted(
            controlId,
            forwardExecutionId,
            new DerivedCompensationState { WorkflowId = controlId, Stage = 1 },
            null,
            DateTimeOffset.UtcNow);
        await PublishAsync(host, controlCompletion);

        var control = await LoadAsync(store, controlId);
        await Assert.That(control).IsNotNull();
        await Assert.That(control!.CompensationFailureMessage).IsNull()
            .Because("only the stripped journal is corrupt; the difference is the deploy");
        await Assert.That(control.Phase).IsNotEqualTo(DerivedCompensationProofPhase.Failed);
        await Assert.That(control.CompensationJournalSchemaVersion).IsEqualTo(1);
    }

    /// <summary>
    /// Replicates the generated saga's rollback-identity permutation so the fixture's journal
    /// entry satisfies the structural-validity check the runtime applies to it.
    /// </summary>
    /// <param name="forwardExecutionId">The forward execution identity.</param>
    /// <returns>The rollback identity the runtime derives from it.</returns>
    private static Guid CreateRollbackId(Guid forwardExecutionId)
    {
        var bytes = forwardExecutionId.ToByteArray();
        if (bytes.All(static value => value == byte.MaxValue))
        {
            Array.Clear(bytes, 0, bytes.Length);
            bytes[0] = 1;
            return new Guid(bytes);
        }

        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index]++;
            if (bytes[index] != 0)
            {
                break;
            }
        }

        return new Guid(bytes);
    }

    private static DerivedCompensationProofSaga BuildInFlightSaga(
        Guid workflowId,
        Guid forwardExecutionId) => new()
        {
            WorkflowId = workflowId,
            Phase = DerivedCompensationProofPhase.DerivedForwardBStep,
            State = new DerivedCompensationState { WorkflowId = workflowId, Stage = 1 },
            StartedAt = DateTimeOffset.UtcNow,
            CompensationJournalSchemaVersion = 1,
            CompensationJournalSequence = 1,
            CompensationJournal =
            [
                new DerivedCompensationProofSaga.CompensationJournalEntry
                {
                    Sequence = 1,
                    ForwardExecutionId = forwardExecutionId,
                    RollbackId = CreateRollbackId(forwardExecutionId),
                    OccurrenceKey = "root/step:DerivedForwardAStep#0",
                    ScopeKey = "root",
                    ScopeKind = "Root",
                    ScopeOrdinal = 0,
                    ForwardStepName = "DerivedForwardAStep",
                    InverseStepName = "DerivedUndoAStep",
                    ForwardActionIdentity = "derived-compensation/Order/a",
                    InverseActionIdentity = "derived-compensation/Order/undo-a",
                    UsesIdentityInverse = false,
                    InverseTimeoutTicks = 3000000000L,
                    RollbackState = new DerivedCompensationState
                    {
                        WorkflowId = workflowId,
                        Stage = 1,
                    },
                    Status = "Completed",
                },
            ],
        };

    private static async Task<IHost> BuildHostAsync(string connectionString) =>
        await Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                opts.Services
                    .AddMarten(storeOptions =>
                    {
                        storeOptions.Connection(connectionString);
                        storeOptions.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
                    })
                    .IntegrateWithWolverine()
                    .ApplyAllDatabaseChangesOnStartup();

                opts.Services.AddDerivedCompensationProofWorkflow();
                opts.Services.AddSingleton(new WorkflowInvocationLog());
                opts.Services.AddResourceSetupOnStartup();
            })
            .StartAsync();

    private static async Task StoreAsync(IDocumentStore store, DerivedCompensationProofSaga saga)
    {
        await using var session = store.LightweightSession();
        session.Store(saga);
        await session.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<DerivedCompensationProofSaga?> LoadAsync(
        IDocumentStore store,
        Guid workflowId)
    {
        await using var session = store.QuerySession();
        return await session.LoadAsync<DerivedCompensationProofSaga>(workflowId);
    }

    private static async Task PublishAsync(IHost host, object message) =>
        await host
            .TrackActivity()
            .Timeout(TimeSpan.FromSeconds(30))
            .DoNotAssertOnExceptionsDetected()
            .PublishMessageAndWaitAsync(message);

    private static async Task<IReadOnlyList<string>> ReadDocumentKeysAsync(
        string connectionString,
        Guid workflowId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"select jsonb_object_keys(data::jsonb) from {SagaDocumentTable} where id = @id";
        command.Parameters.AddWithValue("id", workflowId);

        var keys = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        while (await reader.ReadAsync(CancellationToken.None))
        {
            keys.Add(reader.GetString(0));
        }

        return keys;
    }

    private static async Task StripDerivedMembersAsync(string connectionString, Guid workflowId)
    {
        var removals = string.Join(
            string.Empty,
            DerivedCompensationMembers.Select(static member => $" - '{member}'"));

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"update {SagaDocumentTable} set data = data::jsonb{removals} where id = @id";
        command.Parameters.AddWithValue("id", workflowId);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
