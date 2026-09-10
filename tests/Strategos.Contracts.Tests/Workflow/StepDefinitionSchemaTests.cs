// =============================================================================
// <copyright file="StepDefinitionSchemaTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

using Strategos.Contracts.Generated;

namespace Strategos.Contracts.Tests.Workflow;

/// <summary>
/// T14 — the discriminated <c>StepDefinition</c>. Compiles the canonical
/// <c>.tsp</c> and asserts the wire step is a 5-kind discriminated union
/// (<c>skill | handler | gate | delegate | approval</c>) where every arm pins
/// its <c>kind</c> discriminator and reserves the optional
/// <c>runtime: exarchos | strategos | remote</c> federation slot.
/// </summary>
[Property("Category", "WorkflowIr")]
[NotInParallel("tsp-compile")]
public class StepDefinitionSchemaTests
{
    private static readonly string[] ExpectedKinds =
        ["skill", "handler", "gate", "delegate", "approval"];

    /// <summary>
    /// Asserts <c>StepDefinition</c> emits an <c>anyOf</c> union over the five
    /// kind arms, each pinning <c>kind</c> to its const and reserving
    /// <c>runtime</c>.
    /// </summary>
    [Test]
    public async Task StepDefinition_Discriminates_FiveKinds_ReservesRuntime()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        var root = await EventSchemas.LoadAsync("StepDefinition");

        // The union root is an anyOf over the five kind arms.
        await Assert.That(root.TryGetProperty("anyOf", out var anyOf)).IsTrue()
            .Because("StepDefinition must be a discriminated union (anyOf of arms).");

        var armNames = anyOf.EnumerateArray()
            .Where(a => a.TryGetProperty("$ref", out _))
            .Select(a => Path.GetFileNameWithoutExtension(a.GetProperty("$ref").GetString()))
            .ToList();
        await Assert.That(armNames.Count).IsEqualTo(5)
            .Because("there must be exactly five step kinds.");

        // Each arm pins its kind const and carries the reserved runtime slot.
        var seenKinds = new List<string>();
        foreach (var armName in armNames)
        {
            var arm = await EventSchemas.LoadAsync(armName!);
            var props = arm.GetProperty("properties");

            var kindConst = props.GetProperty("kind").GetProperty("const").GetString();
            seenKinds.Add(kindConst!);

            // runtime is reserved (present, optional, enum of three).
            await Assert.That(props.TryGetProperty("runtime", out var runtime)).IsTrue()
                .Because($"arm {armName} must reserve the runtime federation slot.");
            var runtimeValues = EventSchemas.EnumValues(runtime);
            await Assert.That(runtimeValues).Contains("exarchos");
            await Assert.That(runtimeValues).Contains("strategos");
            await Assert.That(runtimeValues).Contains("remote");

            var required = arm.TryGetProperty("required", out var reqEl)
                ? reqEl.EnumerateArray().Select(e => e.GetString()).ToHashSet()
                : new HashSet<string?>();
            await Assert.That(required.Contains("runtime")).IsFalse()
                .Because("runtime must be optional (default exarchos), not required.");
        }

        foreach (var kind in ExpectedKinds)
        {
            await Assert.That(seenKinds.Contains(kind)).IsTrue()
                .Because($"the {kind} step kind must be present.");
        }
    }

    /// <summary>
    /// Every step arm exposes the same optional occurrence action reference, whose
    /// three required identity names are non-empty strings.
    /// </summary>
    [Test]
    public async Task StepDefinition_ActionReference_IsOptionalAndNonEmptyBySchema()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        var action = await EventSchemas.LoadAsync("ActionReferenceV1");
        var actionProperties = action.GetProperty("properties");
        var actionRequired = action.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in new[] { "domainName", "objectTypeName", "actionName" })
        {
            await Assert.That(actionRequired.Contains(name)).IsTrue();
            await Assert.That(actionProperties.GetProperty(name).GetProperty("type").GetString())
                .IsEqualTo("string");
            await Assert.That(actionProperties.GetProperty(name).GetProperty("minLength").GetInt32())
                .IsEqualTo(1);
            await Assert.That(actionProperties.GetProperty(name).GetProperty("pattern").GetString())
                .IsEqualTo(".*\\S.*");
        }

        foreach (var armName in new[]
                 {
                     "SkillStep",
                     "HandlerStep",
                     "GateStep",
                     "DelegateStep",
                     "ApprovalStep",
                 })
        {
            var arm = await EventSchemas.LoadAsync(armName);
            var actionProperty = arm.GetProperty("properties").GetProperty("action");
            await Assert.That(Path.GetFileNameWithoutExtension(
                    actionProperty.GetProperty("$ref").GetString()))
                .IsEqualTo("ActionReferenceV1");

            var required = arm.GetProperty("required")
                .EnumerateArray()
                .Select(element => element.GetString())
                .ToHashSet(StringComparer.Ordinal);
            await Assert.That(required.Contains("action")).IsFalse()
                .Because($"{armName}.action must remain optional for legacy JSON compatibility.");
        }
    }

    /// <summary>The generated union retains occurrence action identity through JSON.</summary>
    [Test]
    public async Task StepDefinition_ActionReference_RoundTripsGeneratedModel()
    {
        const string json = """
            {
              "kind": "skill",
              "stepId": "capture",
              "stepName": "CapturePaymentStep",
              "isTerminal": false,
              "stepType": "CapturePaymentStep",
              "action": {
                "domainName": "Orders",
                "objectTypeName": "Order",
                "actionName": "CapturePayment"
              }
            }
            """;

        var step = JsonSerializer.Deserialize<StepDefinition>(json, ContractsJson.Options);

        await Assert.That(step).IsTypeOf<SkillStep>();
        var skill = (SkillStep)step!;
        await Assert.That(skill.Action).IsNotNull();
        await Assert.That(skill.Action!.DomainName).IsEqualTo("Orders");
        await Assert.That(skill.Action.ObjectTypeName).IsEqualTo("Order");
        await Assert.That(skill.Action.ActionName).IsEqualTo("CapturePayment");

        var roundTrip = JsonSerializer.Serialize<StepDefinition>(skill, ContractsJson.Options);
        await Assert.That(roundTrip).Contains("\"actionName\": \"CapturePayment\"");
    }

    /// <summary>An action reference cannot omit one of its schema-required identity names.</summary>
    [Test]
    public async Task StepDefinition_ActionReference_RejectsMissingRequiredIdentity()
    {
        const string json = """
            {
              "kind": "skill",
              "stepId": "capture",
              "stepName": "CapturePaymentStep",
              "isTerminal": false,
              "stepType": "CapturePaymentStep",
              "action": {
                "domainName": "Orders",
                "objectTypeName": "Order"
              }
            }
            """;

        var act = () => JsonSerializer.Deserialize<StepDefinition>(json, ContractsJson.Options);

        await Assert.That(act).Throws<JsonException>();
    }

    /// <summary>The generated C# contract enforces TypeSpec's non-empty identity constraint.</summary>
    [Test]
    public async Task ActionReference_GeneratedContract_RejectsEmptyIdentity()
    {
        const string json = """
            {
              "domainName": "",
              "objectTypeName": "Order",
              "actionName": "CapturePayment"
            }
            """;
        var read = () => JsonSerializer.Deserialize<ActionReferenceV1>(json, ContractsJson.Options);
        var write = () => JsonSerializer.Serialize(
            new ActionReferenceV1
            {
                DomainName = string.Empty,
                ObjectTypeName = "Order",
                ActionName = "CapturePayment",
            },
            ContractsJson.Options);

        await Assert.That(read).Throws<JsonException>();
        await Assert.That(write).Throws<JsonException>();
    }

    /// <summary>The generated C# contract enforces TypeSpec's non-blank identity constraint.</summary>
    /// <param name="propertyName">Identity component replaced with whitespace.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("domainName")]
    [Arguments("objectTypeName")]
    [Arguments("actionName")]
    public async Task ActionReference_GeneratedContract_RejectsWhitespaceIdentity(string propertyName)
    {
        var domainName = propertyName == "domainName" ? "   " : "Orders";
        var objectTypeName = propertyName == "objectTypeName" ? "   " : "Order";
        var actionName = propertyName == "actionName" ? "   " : "CapturePayment";
        var json = $$"""
            {
              "domainName": "{{domainName}}",
              "objectTypeName": "{{objectTypeName}}",
              "actionName": "{{actionName}}"
            }
            """;
        var value = new ActionReferenceV1
        {
            DomainName = domainName,
            ObjectTypeName = objectTypeName,
            ActionName = actionName,
        };

        var read = () => JsonSerializer.Deserialize<ActionReferenceV1>(json, ContractsJson.Options);
        var write = () => JsonSerializer.Serialize(value, ContractsJson.Options);

        await Assert.That(read).Throws<JsonException>();
        await Assert.That(write).Throws<JsonException>();
    }
}
