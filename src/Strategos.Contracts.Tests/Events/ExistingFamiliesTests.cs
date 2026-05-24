// =============================================================================
// <copyright file="ExistingFamiliesTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Events;

/// <summary>
/// T7 — consolidation of the in-repo event families: the coding-attempt
/// lifecycle (<c>CodingAttemptStarted/Completed</c>, <c>TaskProgressed</c>,
/// <c>TestResult</c>, <c>ContainerDestroyed</c>) and the task lifecycle
/// (<c>task.completed/failed/escalation</c>). Asserts each data model emits a
/// JSON Schema with its defining fields.
/// </summary>
[Property("Category", "Events")]
[NotInParallel("tsp-compile")]
public class ExistingFamiliesTests
{
    /// <summary>
    /// Compiles the contracts <c>.tsp</c> and asserts the coding-attempt and
    /// task lifecycle data models are emitted with their characteristic fields.
    /// </summary>
    [Test]
    public async Task ExistingFamilies_RoundTrip_FromInRepoSchemas()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        // ── Coding-attempt lifecycle ──────────────────────────────────────
        await AssertHasRequired("CodingAttemptStartedData", "taskId", "attemptNumber", "containerId");
        await AssertHasRequired("CodingAttemptCompletedData", "taskId", "attemptNumber", "outcome");
        await AssertHasRequired("TaskProgressedData", "taskId", "tddPhase");
        await AssertHasRequired("TaskTestResultData", "taskId", "passed", "failed");
        await AssertHasRequired("ContainerDestroyedData", "taskId", "containerId", "totalDuration", "totalTokens");

        // ── Task lifecycle ────────────────────────────────────────────────
        await AssertHasRequired("TaskCompletedData", "taskId");
        await AssertHasRequired("TaskFailedData", "taskId", "error");
        await AssertHasRequired("TaskEscalationData", "taskId", "escalationTarget", "suggestedAction");

        // The coding-attempt outcome is a closed set (union of literals).
        var completed = await EventSchemas.LoadAsync("CodingAttemptCompletedData");
        var outcome = completed.GetProperty("properties").GetProperty("outcome");
        var outcomeVals = EventSchemas.EnumValues(outcome);
        foreach (var v in new[] { "success", "tests_failed", "budget_exhausted", "loop_detected" })
        {
            await Assert.That(outcomeVals).Contains(v)
                .Because($"CodingAttemptCompleted.outcome must admit '{v}'.");
        }
    }

    private static async Task AssertHasRequired(string model, params string[] fields)
    {
        var root = await EventSchemas.LoadAsync(model);
        await Assert.That(root.GetProperty("type").GetString()).IsEqualTo("object")
            .Because($"{model} must be an object schema.");
        var props = root.GetProperty("properties");
        var required = root.TryGetProperty("required", out var req)
            ? req.EnumerateArray().Select(e => e.GetString()).ToHashSet()
            : new HashSet<string?>();
        foreach (var f in fields)
        {
            await Assert.That(props.TryGetProperty(f, out _)).IsTrue()
                .Because($"{model}.{f} must exist.");
            await Assert.That(required.Contains(f)).IsTrue()
                .Because($"{model}.{f} must be required.");
        }
    }
}
