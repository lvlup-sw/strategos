// =============================================================================
// <copyright file="ApprovalFailureConfigTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

using Strategos.Contracts.Generated;

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
        var compensationStepType = comp.GetProperty("properties").GetProperty("compensationStepType");
        await Assert.That(compensationStepType.GetProperty("type").GetString()).IsEqualTo("string")
            .Because("compensationStepType is a simple-name moniker (LB-2).");
        await Assert.That(compensationStepType.GetProperty("minLength").GetInt32()).IsEqualTo(1)
            .Because("the importer rejects empty compensation step monikers.");
        await Assert.That(compensationStepType.GetProperty("pattern").GetString()).IsEqualTo(@".*\S.*")
            .Because("the schema must reject whitespace-only compensation step monikers just like the importer.");
        await Assert.That(comp.GetProperty("properties").GetProperty("inverseAction")
            .GetProperty("$ref").GetString()).IsEqualTo("ActionReferenceV1.json")
            .Because("inverseAction is the same language-neutral ontology identity used by forward occurrences.");
    }

    /// <summary>
    /// The generated contract applies the same nonblank compensation-step moniker
    /// rule during serialization and deserialization as the workflow importer.
    /// </summary>
    /// <param name="compensationStepType">Invalid compensation-step moniker.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task CompensationConfiguration_GeneratedContract_RejectsBlankStepType(
        string compensationStepType)
    {
        var json = $$"""
            {
              "compensationStepType": "{{compensationStepType}}"
            }
            """;
        var value = new CompensationConfiguration
        {
            CompensationStepType = compensationStepType,
        };

        var read = () => JsonSerializer.Deserialize<CompensationConfiguration>(json, ContractsJson.Options);
        var write = () => JsonSerializer.Serialize(value, ContractsJson.Options);

        await Assert.That(read).Throws<JsonException>();
        await Assert.That(write).Throws<JsonException>();
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
