// =============================================================================
// <copyright file="AgentStepBaseStreamingSourceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System;
using System.IO;
using System.Linq;

namespace Strategos.Agents.Tests.Unit;

/// <summary>
/// T-015 INV-1 guard: the streaming path is a non-durable side-channel. The orchestrator
/// source MUST NOT reference the durable progress-event machinery
/// (<c>IProgressEventStore</c>) or the durable <c>StreamingTokenReceived</c> event from
/// its streaming code. The only durable artifact is the terminal StepResult.
/// </summary>
[Property("Category", "Unit")]
public sealed class AgentStepBaseStreamingSourceTests
{
    [Test]
    public async Task StreamingPath_DoesNotReference_ProgressEventStoreOrStreamingTokenReceived()
    {
        var source = ReadAgentStepBaseSource();

        await Assert.That(source.Contains("IProgressEventStore", StringComparison.Ordinal))
            .IsFalse()
            .Because("INV-1: streaming tokens are non-durable; AgentStepBase must not touch IProgressEventStore.");

        await Assert.That(source.Contains("StreamingTokenReceived", StringComparison.Ordinal))
            .IsFalse()
            .Because("INV-1: AgentStepBase must not emit the durable StreamingTokenReceived event.");
    }

    [Test]
    public async Task StreamingPath_HasNoBareCatchOrCatchAllWithoutWrap()
    {
        // DR-4/DR-11: no bare `catch {` and no `catch (Exception` that doesn't rethrow or wrap.
        var source = ReadAgentStepBaseSource();
        var lines = source.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();

            // No bare catch.
            await Assert.That(trimmed.StartsWith("catch {", StringComparison.Ordinal)
                    || trimmed.Equals("catch", StringComparison.Ordinal)
                    || trimmed.StartsWith("catch (Exception)", StringComparison.Ordinal))
                .IsFalse()
                .Because($"Line {i + 1} is a bare/untyped catch: {trimmed}");
        }

        // Every `catch (Exception ` in the streaming methods must be followed (within the
        // block) by a throw — verified by ensuring the only Exception catches wrap as
        // AgentStreamingException.
        var exceptionCatchCount = lines.Count(l => l.TrimStart().StartsWith("catch (Exception ", StringComparison.Ordinal));
        var wrapCount = lines.Count(l => l.Contains("throw new AgentStreamingException", StringComparison.Ordinal));
        await Assert.That(wrapCount).IsGreaterThanOrEqualTo(exceptionCatchCount)
            .Because("Every catch (Exception ...) on the streaming path must wrap as AgentStreamingException.");
    }

    private static string ReadAgentStepBaseSource()
    {
        var path = LocateSource("AgentStepBase.cs");
        return File.ReadAllText(path);
    }

    private static string LocateSource(string fileName)
    {
        // Walk up from the test assembly location to the repo, then into the production project.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Strategos.Agents", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName} by walking up from {AppContext.BaseDirectory}.");
    }
}
