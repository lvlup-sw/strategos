// =============================================================================
// <copyright file="SandboxGuaranteeTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;

namespace Strategos.Contracts.Tests.Diagnostics;

/// <summary>
/// T26 — the no-executable-leaf structural guarantee (INV-4 / LB-1: "the contract
/// is declarative-only; it never serializes executable code"). The sandbox
/// guarantee must be a property of the <c>CheckNode</c> <em>shape</em>, not of
/// downstream validation: the schema must be structurally incapable of expressing
/// an embedded executable.
///
/// Two prongs:
/// <list type="number">
///   <item>A shape assertion that no <c>CheckNode</c> arm declares a member whose
///   wire name admits arbitrary code / a command / an exec string, and that every
///   arm member is either an inert scalar match field or a recursive
///   <c>CheckNode</c> reference — the closed, inert member set.</item>
///   <item>A negative test: a fixture carrying an executable-ish discriminator
///   (<c>kind: "exec"</c>) fails to bind to any arm of the generated
///   polymorphic <c>CheckNode</c> — there is no arm for it to land on.</item>
/// </list>
/// </summary>
[Property("Category", "Diagnostics")]
[NotInParallel("tsp-compile")]
public class SandboxGuaranteeTests
{
    /// <summary>
    /// Wire-name tokens that would betray a code-carrying member. The schema must
    /// contain none of these on any CheckNode arm.
    /// </summary>
    private static readonly string[] ForbiddenMemberTokens =
        ["command", "exec", "script", "code", "shell", "eval", "run", "invoke", "spawn", "lambda", "function"];

    /// <summary>
    /// The closed set of inert scalar arm members CheckNode may carry. Anything
    /// not in this set must be a recursive $ref to CheckNode — never a free-form
    /// member that could hold a payload.
    /// </summary>
    private static readonly string[] AllowedScalarMembers =
        ["kind", "pattern", "file-glob", "threshold"];

    /// <summary>
    /// Asserts the CheckNode schema is structurally incapable of expressing an
    /// embedded executable, and that an executable-ish fixture cannot bind.
    /// </summary>
    [Test]
    public async Task CheckNode_Schema_CannotExpressExecutableLeaf()
    {
        var compile = await TspToolchain.CompileAsync();
        await Assert.That(compile.ExitCode).IsEqualTo(0).Because(compile.Output);

        var root = await EventSchemas.LoadAsync("CheckNode");
        var armNames = root.GetProperty("anyOf").EnumerateArray()
            .Where(a => a.TryGetProperty("$ref", out _))
            .Select(a => Path.GetFileNameWithoutExtension(a.GetProperty("$ref").GetString()))
            .ToList();
        await Assert.That(armNames.Count).IsGreaterThan(0);

        // --- Prong 1: structural shape assertion over every arm member. ---
        foreach (var armName in armNames)
        {
            var arm = await EventSchemas.LoadAsync(armName!);
            var props = arm.GetProperty("properties");

            foreach (var member in props.EnumerateObject())
            {
                var name = member.Name;

                // No member name may match a code-carrying token.
                foreach (var token in ForbiddenMemberTokens)
                {
                    await Assert.That(name.Contains(token, StringComparison.OrdinalIgnoreCase)).IsFalse()
                        .Because($"CheckNode arm '{armName}' member '{name}' must not admit code (token '{token}').");
                }

                // Every member must be either an inert scalar match field from the
                // closed allow-list, or a recursive CheckNode reference (child/
                // children). There is no free-form member that could hold a payload.
                var isAllowedScalar = AllowedScalarMembers.Contains(name);
                var refsCheckNode = RefsCheckNode(member.Value);
                await Assert.That(isAllowedScalar || refsCheckNode).IsTrue()
                    .Because($"CheckNode arm '{armName}' member '{name}' must be an inert match field "
                             + "or a recursive CheckNode reference — nothing free-form.");

                // An inert scalar must be a primitive (string/number/const), never
                // an open object that could smuggle arbitrary structure.
                if (isAllowedScalar && !refsCheckNode)
                {
                    await Assert.That(IsInertPrimitive(member.Value)).IsTrue()
                        .Because($"CheckNode arm '{armName}' member '{name}' must be an inert primitive.");
                }
            }
        }

        // --- Prong 2: an executable-ish fixture cannot bind to any arm. ---
        // System.Text.Json polymorphic deserialization rejects an unknown
        // discriminator: there is no `exec` arm for it to land on.
        var checkNodeType = typeof(ContractsMarker).Assembly
            .GetTypes().First(t => t.Name == "CheckNode");

        const string executableFixture = """
            { "kind": "exec", "command": "rm -rf /" }
            """;

        Exception? thrown = null;
        try
        {
            JsonSerializer.Deserialize(executableFixture, checkNodeType);
        }
        catch (JsonException ex)
        {
            thrown = ex;
        }

        await Assert.That(thrown).IsNotNull()
            .Because("an executable-ish fixture (kind: exec, command: ...) must fail to bind — "
                     + "there is no CheckNode arm that admits a command.");
    }

    private static bool RefsCheckNode(JsonElement member)
    {
        if (member.TryGetProperty("$ref", out var r)
            && Path.GetFileNameWithoutExtension(r.GetString()) == "CheckNode")
        {
            return true;
        }

        // Array of CheckNode (the all-of / any-of children).
        if (member.TryGetProperty("type", out var t) && t.GetString() == "array"
            && member.TryGetProperty("items", out var items)
            && items.TryGetProperty("$ref", out var ir)
            && Path.GetFileNameWithoutExtension(ir.GetString()) == "CheckNode")
        {
            return true;
        }

        return false;
    }

    private static bool IsInertPrimitive(JsonElement member)
    {
        // A scalar (string/number) or a const string discriminator.
        if (member.TryGetProperty("const", out _))
        {
            return true;
        }

        if (member.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String)
        {
            var type = t.GetString();
            return type is "string" or "number" or "integer" or "boolean";
        }

        return false;
    }
}
