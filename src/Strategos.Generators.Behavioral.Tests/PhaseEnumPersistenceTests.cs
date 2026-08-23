// -----------------------------------------------------------------------
// <copyright file="PhaseEnumPersistenceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json.Serialization;

using Marten;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// Establishes what a persisted saga actually stores for its phase, against the raw
/// <c>mt_doc_*</c> document rather than against a serializer's advertised default.
/// </summary>
/// <remarks>
/// <para>
/// Reordering the emitted phase enum's members is only safe if the stored representation is the
/// member NAME. If it is the member's position, every saga document written before the reorder
/// silently denotes a different phase after it, and the reorder is a data migration.
/// </para>
/// <para>
/// The saga documents here are written directly through Marten rather than by running a workflow.
/// The question is what the store does with the phase, not what a workflow does with the saga, and
/// a workflow run would couple this to whichever sagas happen to terminate.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<RoundTripHostFixture>(Shared = SharedType.PerClass)]
public sealed class PhaseEnumPersistenceTests
{
    /// <summary>Marten's document table for the branch saga used as the persistence sample.</summary>
    private const string SagaDocumentTable = "mt_doc_terminalbranchsaga";

    private readonly RoundTripHostFixture host;

    /// <summary>Initializes a new instance of the <see cref="PhaseEnumPersistenceTests"/> class.</summary>
    /// <param name="host">The shared real-host fixture, injected by TUnit.</param>
    public PhaseEnumPersistenceTests(RoundTripHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// The generated phase enum as it stood BEFORE the member reorder: the same member names in a
    /// different order, so every member's position differs from its position today.
    /// </summary>
    /// <remarks>
    /// Standing in for the previous revision of <see cref="TerminalBranchPhase"/> is what makes
    /// "loads after the reorder" testable at all — the emitted enum only ever exists in one order
    /// per build. It carries the same string-enum converter the generator emits on every phase
    /// enum, because that attribute is the whole reason the reorder is not a migration; a stand-in
    /// without it would be a different enum, not an earlier revision of this one.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    private enum PreReorderTerminalBranchPhase
    {
        /// <summary>Workflow failed.</summary>
        Failed,

        /// <summary>Executing ShipApprovedOrder step.</summary>
        ShipApprovedOrder,

        /// <summary>Executing RejectOrder step.</summary>
        RejectOrder,

        /// <summary>Workflow completed successfully.</summary>
        Completed,

        /// <summary>Executing ProcessApprovedOrder step.</summary>
        ProcessApprovedOrder,

        /// <summary>Executing ReviewOrder step.</summary>
        ReviewOrder,

        /// <summary>Workflow has not yet started.</summary>
        NotStarted,
    }

    /// <summary>
    /// Verifies that the raw persisted saga document stores its phase as the enum member's NAME.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PersistedSaga_PhaseColumn_StoresEnumName()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        await this.PersistSagaAsync(workflowId, TerminalBranchPhase.ShipApprovedOrder);

        // Act - read the stored document, NOT a deserialized saga
        var (jsonType, storedPhase) = await this.ReadRawPhaseAsync(workflowId);

        // Assert
        await Assert.That(jsonType).IsEqualTo("string")
            .Because("a phase stored as a number denotes its position, and a reorder moves positions.");
        await Assert.That(storedPhase).IsEqualTo(nameof(TerminalBranchPhase.ShipApprovedOrder));
    }

    /// <summary>
    /// Verifies that a saga document written while the phase enum was in its previous member order
    /// still loads as the same phase after the reorder.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PersistedSaga_WrittenBeforeReorder_LoadsAfterReorder()
    {
        // Arrange - the member whose position moved most between the two orders
        const TerminalBranchPhase phase = TerminalBranchPhase.RejectOrder;
        const PreReorderTerminalBranchPhase phaseBeforeReorder = PreReorderTerminalBranchPhase.RejectOrder;

        await Assert.That((int)phase).IsNotEqualTo((int)phaseBeforeReorder)
            .Because("the two orders must genuinely disagree on this member's position, or nothing is being proven.");

        var workflowId = Guid.NewGuid();

        // Act - write the document as the PRE-reorder build's enum would have written it, then
        // load it back through the store that now carries the reordered enum.
        await this.PersistPreReorderSagaAsync(workflowId, phaseBeforeReorder);
        var reloaded = await this.LoadSagaAsync(workflowId);

        // Assert
        await Assert.That(reloaded).IsNotNull()
            .Because("a document written by the previous build must still be loadable.");
        await Assert.That(reloaded!.Phase).IsEqualTo(phase)
            .Because("the phase is stored by name, so the member's new position cannot change what it denotes.");

        // Assert - and it is genuinely NOT reading the old position, which now names another member
        await Assert.That(reloaded.Phase).IsNotEqualTo((TerminalBranchPhase)(int)phaseBeforeReorder);
    }

    /// <summary>
    /// Verifies the counterfactual that makes the name representation load-bearing: the SAME
    /// document, written positionally instead, loads as whatever member now occupies that position.
    /// </summary>
    /// <remarks>
    /// Without this, "the phase is stored by name" reads as a fact about the serializer rather than
    /// as the thing that keeps the reorder off the migration path.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PersistedSaga_WrittenPositionally_LoadsTheWrongPhaseAfterReorder()
    {
        // Arrange
        const PreReorderTerminalBranchPhase phaseBeforeReorder = PreReorderTerminalBranchPhase.RejectOrder;
        var workflowId = Guid.NewGuid();

        // Act - the position the pre-reorder enum gave this member, written as a bare JSON number
        await this.PersistSagaAsync(workflowId, TerminalBranchPhase.NotStarted);
        await this.OverwriteStoredPhaseAsync(
            workflowId,
            ((int)phaseBeforeReorder).ToString(System.Globalization.CultureInfo.InvariantCulture));

        var reloaded = await this.LoadSagaAsync(workflowId);

        // Assert - the position now denotes a different member entirely
        await Assert.That(reloaded).IsNotNull();
        await Assert.That(reloaded!.Phase).IsEqualTo(TerminalBranchPhase.ProcessApprovedOrder);
        await Assert.That(reloaded.Phase).IsNotEqualTo(TerminalBranchPhase.RejectOrder)
            .Because("a positionally stored phase is exactly what a member reorder silently rewrites.");
    }

    private static TerminalBranchSaga BuildSaga(Guid workflowId, TerminalBranchPhase phase) =>
        new()
        {
            WorkflowId = workflowId,
            Phase = phase,
            State = new TerminalBranchState
            {
                WorkflowId = workflowId,
                Outcome = OrderReviewOutcome.Rejected,
                StepCount = 1,
            },
            StartedAt = DateTimeOffset.UtcNow,
        };

    private async Task PersistSagaAsync(Guid workflowId, TerminalBranchPhase phase)
    {
        var store = this.host.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        session.Store(BuildSaga(workflowId, phase));
        await session.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Writes a saga document the way the build that preceded the phase reorder would have written
    /// it: the same store, the same serializer, but the phase named by the OLD enum.
    /// </summary>
    /// <param name="workflowId">The saga identity to write.</param>
    /// <param name="phase">The phase, expressed in the pre-reorder enum.</param>
    /// <returns>A task that completes when the row is written.</returns>
    private async Task PersistPreReorderSagaAsync(Guid workflowId, PreReorderTerminalBranchPhase phase)
    {
        // Persist through the current enum first so Marten owns the row's shape, then overwrite the
        // phase with what the pre-reorder build serialized for the same member. Hand-writing the
        // whole row would sidestep the store's own document format and prove nothing about it.
        await this.PersistSagaAsync(workflowId, TerminalBranchPhase.NotStarted);

        var store = this.host.Services.GetRequiredService<IDocumentStore>();
        await this.OverwriteStoredPhaseAsync(workflowId, store.Options.Serializer().ToJson(phase));
    }

    /// <summary>
    /// Replaces the phase of an already-persisted saga document with a raw JSON value, leaving the
    /// rest of the row exactly as the store wrote it.
    /// </summary>
    /// <param name="workflowId">The saga identity whose row is rewritten.</param>
    /// <param name="phaseJson">The JSON value to store for the phase.</param>
    /// <returns>A task that completes when the row is rewritten.</returns>
    private async Task OverwriteStoredPhaseAsync(Guid workflowId, string phaseJson)
    {
        await using var connection = new NpgsqlConnection(this.host.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);

        await using var command = new NpgsqlCommand(
            $"update {SagaDocumentTable} set data = jsonb_set(data, '{{Phase}}', @phase::jsonb) where id = @id",
            connection);
        command.Parameters.AddWithValue("phase", phaseJson);
        command.Parameters.AddWithValue("id", workflowId);

        var updated = await command.ExecuteNonQueryAsync(CancellationToken.None);
        if (updated != 1)
        {
            throw new InvalidOperationException(
                $"Expected to rewrite exactly one {SagaDocumentTable} row for {workflowId}, rewrote {updated}.");
        }
    }

    private async Task<TerminalBranchSaga?> LoadSagaAsync(Guid workflowId)
    {
        var store = this.host.Services.GetRequiredService<IDocumentStore>();
        await using var query = store.QuerySession();
        return await query.LoadAsync<TerminalBranchSaga>(workflowId, CancellationToken.None);
    }

    private async Task<(string? JsonType, string? StoredPhase)> ReadRawPhaseAsync(Guid workflowId)
    {
        await using var connection = new NpgsqlConnection(this.host.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);

        await using var command = new NpgsqlCommand(
            $"select jsonb_typeof(data->'Phase'), data->>'Phase' from {SagaDocumentTable} where id = @id",
            connection);
        command.Parameters.AddWithValue("id", workflowId);

        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);

        if (!await reader.ReadAsync(CancellationToken.None))
        {
            throw new InvalidOperationException(
                $"No {SagaDocumentTable} row for {workflowId}; the saga document was not persisted.");
        }

        var jsonType = reader.IsDBNull(0) ? null : reader.GetString(0);
        var storedPhase = reader.IsDBNull(1) ? null : reader.GetString(1);

        return (jsonType, storedPhase);
    }
}
