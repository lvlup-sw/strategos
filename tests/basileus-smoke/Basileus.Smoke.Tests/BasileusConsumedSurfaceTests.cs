// =============================================================================
// <copyright file="BasileusConsumedSurfaceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================
//
// DR-11 / T-022 — basileus-consumed surface smoke tests.
//
// Solution-layout decision: this project lives in a STANDALONE
// tests/basileus-smoke/Basileus.Smoke.sln rather than being added to
// src/strategos.sln. Two reasons:
//
//   1) Chicken-and-egg with CI: the smoke project's <PackageReference>
//      to LevelUp.Strategos.Agents 2.7.0 only resolves AFTER
//      `dotnet pack` populates the sibling local-feed/. The main
//      build-test job restores src/strategos.sln before any pack step,
//      so including this csproj there would break that job.
//
//   2) Directory.Build.props isolation: src/Directory.Build.props
//      installs MinVer, coverlet thresholds, analyzer packages and a
//      net10.0-only target. The smoke project is intentionally outside
//      that hierarchy so nothing leaks in — keeping it a clean
//      "external consumer" simulation of what basileus sees.
//
// What these tests prove:
//
//   * BasileusConsumedSurface_AfterMeai105Adoption_StillCompiles
//       Compile-time assertion. Declaring a class that implements all
//       three interfaces forces the package's public surface to still
//       expose them with their current shape. If a future change
//       deletes / renames a method, removes an interface, or alters a
//       parameter type, this file fails to BUILD (not just at runtime).
//
//   * BasileusConsumedSurface_OldSingleArityIAgentStepAndAgentStepBase_AreAbsent
//       Reflection over the *packed* assembly (loaded via
//       PackageReference, not ProjectReference). Mirrors T-021's
//       MigrationInvariantTests but executed against the artifact that
//       will actually ship — so a regression that resurrects the
//       single-arity types inside the production csproj but somehow
//       slips past the in-repo test sweep is still caught here.
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.AI;

using Strategos.Agents.Abstractions;
using Strategos.Agents.Models;

namespace Basileus.Smoke.Tests;

/// <summary>
/// Mock implementations of every basileus-consumed interface. Bodies
/// return <c>null!</c> / <c>default</c> — we only need this class to
/// COMPILE; runtime behavior is irrelevant.
/// </summary>
internal sealed class BasileusSurfaceProbe
    : IConversationThreadManager,
      IWorkflowAgentFactory,
      IStreamingHandler
{
    // IConversationThreadManager
    public Task<IChatClient> CreateAgentWithThreadAsync(
        string agentType,
        string? serializedThread,
        CancellationToken cancellationToken = default) => Task.FromResult<IChatClient>(null!);

    public Task<string> SerializeThreadAsync(
        string agentType,
        CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);

    // IWorkflowAgentFactory
    public Task<WorkflowAgentContext> CreateAgentWithThreadAsync(
        SpecialistType specialistType,
        string? serializedThreadJson,
        CancellationToken cancellationToken = default) => Task.FromResult<WorkflowAgentContext>(null!);

    public Task<string> SerializeThreadAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);

    // IStreamingHandler
    public Task OnTokenReceivedAsync(
        string token,
        System.Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task OnResponseCompletedAsync(
        string fullResponse,
        System.Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

[Property("Category", "Smoke")]
public sealed class BasileusConsumedSurfaceTests
{
    [Test]
    public async Task BasileusConsumedSurface_AfterMeai105Adoption_StillCompiles()
    {
        // The very existence of BasileusSurfaceProbe (above) is the
        // compile-time guarantee. The runtime assertion below just
        // anchors the test in the TUnit runner so it shows up in the
        // CI report.
        var probe = new BasileusSurfaceProbe();
        await Assert.That(probe).IsNotNull();
        await Assert.That(probe).IsAssignableTo<IConversationThreadManager>();
        await Assert.That(probe).IsAssignableTo<IWorkflowAgentFactory>();
        await Assert.That(probe).IsAssignableTo<IStreamingHandler>();
    }

    [Test]
    public async Task BasileusConsumedSurface_OldSingleArityIAgentStepAndAgentStepBase_AreAbsent()
    {
        // Reach the production assembly via an interface guaranteed to
        // be present in the package. Going through the packed assembly
        // (PackageReference, not ProjectReference) is the load-bearing
        // detail: this proves the artifact basileus restores is clean.
        var assembly = typeof(IConversationThreadManager).Assembly;

        var singleArityInterfaces = assembly.GetTypes()
            .Where(t => t.IsGenericTypeDefinition
                && t.Name.StartsWith("IAgentStep", System.StringComparison.Ordinal)
                && t.GetGenericArguments().Length == 1)
            .ToArray();
        await Assert.That(singleArityInterfaces).IsEmpty();

        var singleArityBases = assembly.GetTypes()
            .Where(t => t.IsGenericTypeDefinition
                && t.Name.StartsWith("AgentStepBase", System.StringComparison.Ordinal)
                && t.GetGenericArguments().Length == 1)
            .ToArray();
        await Assert.That(singleArityBases).IsEmpty();
    }
}
