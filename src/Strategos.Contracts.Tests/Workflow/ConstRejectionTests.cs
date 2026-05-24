// =============================================================================
// <copyright file="ConstRejectionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;
using NJsonSchema;
using Strategos.Contracts;
using Strategos.Contracts.Generated;

namespace Strategos.Contracts.Tests.Workflow;

/// <summary>
/// Negative gate for the workflow-IR discriminator / version literals. Makes the
/// <b>enforcement boundary</b> explicit and tested rather than merely claimed.
/// </summary>
/// <remarks>
/// <para>
/// The bundled <c>workflow-definition-v1.schema.json</c> pins
/// <c>schemaVersion: { const: "1.0" }</c> and each step arm pins its
/// <c>kind</c> const. Two enforcers exist, and they do NOT cover the same ground:
/// </para>
/// <list type="bullet">
///   <item>
///     <b>NJsonSchema</b> (the C#-side <see cref="JsonSchema.Validate(string)"/>
///     path used by the cross-product gate) rejects a <b>bogus step kind</b> — no
///     <c>anyOf</c> arm matches, so the array item fails. It does <b>NOT</b>
///     enforce the top-level <c>const</c> on <c>schemaVersion</c>: a wrong
///     version validates clean. This is a known NJsonSchema gap (it treats a
///     string <c>const</c> on a scalar leniently in the
///     draft-2020-12 validator path).
///   </item>
///   <item>
///     <b>STJ polymorphic binding</b> (the canonical
///     <see cref="ContractsJson.Options"/> deserialize path that every consumer
///     routes through) is the authoritative discriminator enforcer: a bogus
///     <c>kind</c> throws a <see cref="JsonException"/> ("unrecognized type
///     discriminator"). <c>const</c> enforcement for <c>schemaVersion</c> is
///     therefore <b>delegated to STJ binding / consumer-side checks</b>, not to
///     NJsonSchema.
///   </item>
/// </list>
/// <para>
/// This test asserts each enforcer against the input it actually catches, so the
/// boundary is mechanical and regression-guarded.
/// </para>
/// </remarks>
[Property("Category", "WorkflowIr")]
public class ConstRejectionTests
{
    private const string WrongSchemaVersionIr = """
        {
          "schemaVersion": "9.9",
          "name": "negative-fixture",
          "steps": [],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": []
        }
        """;

    private const string BogusKindIr = """
        {
          "schemaVersion": "1.0",
          "name": "negative-fixture",
          "steps": [
            { "kind": "definitely-not-a-step-kind", "stepId": "s1", "stepName": "X", "isTerminal": false }
          ],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": []
        }
        """;

    /// <summary>
    /// NJsonSchema rejects an IR whose step carries a <c>kind</c> outside the
    /// discriminated union: no <c>anyOf</c> arm matches, so the step-array item
    /// fails validation against the bundled schema.
    /// </summary>
    [Test]
    public async Task BundledSchema_RejectsBogusStepKind()
    {
        var schema = await JsonSchema.FromJsonAsync(
            await File.ReadAllTextAsync(RepoLayout.WorkflowSchemaPath));

        var errors = schema.Validate(BogusKindIr);

        await Assert.That(errors.Count).IsGreaterThan(0)
            .Because("a step with a kind outside the union must fail the bundled schema "
                + "(no anyOf arm matches).");
        await Assert.That(errors.Any(e => e.Path is not null && e.Path.Contains("steps"))).IsTrue()
            .Because("the validation error must point at the offending step in #/steps.");
    }

    /// <summary>
    /// Documents the NJsonSchema enforcement gap: a wrong <c>schemaVersion</c> does
    /// NOT fail the bundled schema (NJsonSchema does not enforce the scalar
    /// <c>const</c> here). Pins the gap so a future NJsonSchema upgrade that
    /// starts enforcing it is a visible, intentional change — and so the const
    /// boundary below is not silently assumed.
    /// </summary>
    [Test]
    public async Task BundledSchema_DoesNotEnforceSchemaVersionConst_BoundaryIsStj()
    {
        var schema = await JsonSchema.FromJsonAsync(
            await File.ReadAllTextAsync(RepoLayout.WorkflowSchemaPath));

        var errors = schema.Validate(WrongSchemaVersionIr);

        await Assert.That(errors.Count).IsEqualTo(0)
            .Because("NJsonSchema does not enforce the scalar schemaVersion const; "
                + "const enforcement is delegated to STJ binding / consumer checks. "
                + "If this assertion ever fails, NJsonSchema has started enforcing const "
                + "and the boundary doc on this class should be updated.");
    }

    /// <summary>
    /// The authoritative discriminator enforcer: the canonical
    /// <see cref="ContractsJson.Options"/> deserialize path rejects a bogus step
    /// <c>kind</c> with a <see cref="JsonException"/>. This is where const/
    /// discriminator enforcement actually lives.
    /// </summary>
    [Test]
    public async Task StjBinding_RejectsBogusStepKind()
    {
        var act = () => JsonSerializer.Deserialize<WorkflowDefinitionV1>(
            BogusKindIr, ContractsJson.Options);

        await Assert.That(act).Throws<JsonException>()
            .Because("STJ polymorphic binding is the authoritative discriminator enforcer; "
                + "a kind outside the union must throw, not bind to a default arm.");

        // The failure must be a discriminator rejection, not an unrelated parse error.
        JsonException? captured = null;
        try
        {
            act();
        }
        catch (JsonException ex)
        {
            captured = ex;
        }

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Message).Contains("discriminator")
            .Because("the failure must be a discriminator rejection, not an unrelated parse error.");
    }
}
