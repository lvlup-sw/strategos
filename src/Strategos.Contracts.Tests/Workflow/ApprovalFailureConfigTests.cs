// =============================================================================
// <copyright file="ApprovalFailureConfigTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Workflow;

/// <summary>
/// T16 — the approval, failure, and configuration sub-definitions of the
/// workflow wire IR. Asserts the JSON Schema for the approval gate (with its
/// escalation / rejection handlers), the failure handler (scoped), and the step
/// configuration tree (retry / compensation / validation / low-confidence).
/// </summary>
[Property("Category", "WorkflowIr")]
[NotInParallel("tsp-compile")]
public class ApprovalFailureConfigTests
{
    /// <summary>
    /// Asserts the approval / failure / configuration sub-definitions emit with
    /// their identifying fields and that CLR <c>Type</c> members project to
    /// simple-name string monikers (LB-2).
    /// </summary>
    [Test]
    public async Task ApprovalFailureConfig_Schema_MatchDefinitions()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        // Approval gate carries the approver moniker + optional handlers.
        var approval = await EventSchemas.LoadAsync("ApprovalDefinition");
        var aprops = approval.GetProperty("properties");
        await Assert.That(aprops.GetProperty("approverType").GetProperty("type").GetString())
            .IsEqualTo("string").Because("approverType is a simple-name moniker, not a CLR Type (LB-2).");
        await Assert.That(aprops.TryGetProperty("escalationHandler", out _)).IsTrue();
        await Assert.That(aprops.TryGetProperty("rejectionHandler", out _)).IsTrue();

        await AssertRequiredProps("ApprovalEscalationDefinition", "escalationId");
        await AssertRequiredProps("ApprovalRejectionDefinition", "rejectionHandlerId");

        // Failure handler is scoped (workflow | step | fork-path) and carries steps.
        var failure = await EventSchemas.LoadAsync("FailureHandlerDefinition");
        var fprops = failure.GetProperty("properties");
        await AssertRequiredProps("FailureHandlerDefinition", "handlerId", "scope");
        var scopeValues = EventSchemas.EnumValues(fprops.GetProperty("scope"));
        await Assert.That(scopeValues.Count).IsEqualTo(3)
            .Because("failure-handler scope is workflow | step | forkPath.");

        // Step configuration tree.
        var config = await EventSchemas.LoadAsync("StepConfigurationDefinition");
        var cprops = config.GetProperty("properties");
        foreach (var name in new[] { "confidenceThreshold", "onLowConfidence", "compensation", "retry", "validation" })
        {
            await Assert.That(cprops.TryGetProperty(name, out _)).IsTrue()
                .Because($"StepConfigurationDefinition must expose {name}.");
        }

        await AssertRequiredProps("RetryConfiguration", "maxAttempts");
        await AssertRequiredProps("ValidationDefinition", "predicateExpression", "errorMessage");
        await AssertRequiredProps("LowConfidenceHandlerDefinition", "handlerId");

        // Compensation carries a simple-name moniker, not a CLR Type (LB-2).
        var comp = await EventSchemas.LoadAsync("CompensationConfiguration");
        await Assert.That(comp.GetProperty("properties").GetProperty("compensationStepType")
            .GetProperty("type").GetString()).IsEqualTo("string")
            .Because("compensationStepType is a simple-name moniker (LB-2).");
    }

    private static async Task AssertRequiredProps(string model, params string[] requiredNames)
    {
        var root = await EventSchemas.LoadAsync(model);
        var required = root.TryGetProperty("required", out var reqEl)
            ? reqEl.EnumerateArray().Select(e => e.GetString()).ToHashSet()
            : new HashSet<string?>();
        foreach (var name in requiredNames)
        {
            await Assert.That(required.Contains(name)).IsTrue()
                .Because($"{model} must require {name}.");
        }
    }
}
