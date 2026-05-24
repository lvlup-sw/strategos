// =============================================================================
// <copyright file="EnforcementTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests.Diagnostics;

/// <summary>
/// T27 — the <c>Enforcement</c> discriminated union (#98). Reuses the P2
/// discriminated-union emitter path: a union on <c>mode</c> with two arms —
/// <c>check</c> (carrying a <c>CheckNode</c>) and <c>audit</c> (carrying an
/// <c>audit-prompt</c> string, kebab-case wire name).
/// </summary>
[Property("Category", "Diagnostics")]
[NotInParallel("tsp-compile")]
public class EnforcementTests
{
    /// <summary>
    /// Asserts <c>Enforcement</c> is an <c>anyOf</c> over the two mode arms, that
    /// the <c>check</c> arm carries a <c>CheckNode</c>, and that the <c>audit</c>
    /// arm carries an <c>audit-prompt</c> string.
    /// </summary>
    [Test]
    public async Task Enforcement_DiscriminatesOnMode_CheckOrAudit()
    {
        var compile = await TspToolchain.CompileAsync();
        await Assert.That(compile.ExitCode).IsEqualTo(0).Because(compile.Output);

        var root = await EventSchemas.LoadAsync("Enforcement");
        await Assert.That(root.TryGetProperty("anyOf", out var anyOf)).IsTrue()
            .Because("Enforcement must be a discriminated union (anyOf of arms).");

        var armsByMode = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
        foreach (var armEl in anyOf.EnumerateArray())
        {
            var armName = Path.GetFileNameWithoutExtension(armEl.GetProperty("$ref").GetString());
            var arm = await EventSchemas.LoadAsync(armName!);
            var mode = arm.GetProperty("properties").GetProperty("mode").GetProperty("const").GetString();
            armsByMode[mode!] = arm;
        }

        await Assert.That(armsByMode.ContainsKey("check")).IsTrue()
            .Because("Enforcement must carry a `check` arm.");
        await Assert.That(armsByMode.ContainsKey("audit")).IsTrue()
            .Because("Enforcement must carry an `audit` arm.");

        // check arm carries a CheckNode.
        var checkProps = armsByMode["check"].GetProperty("properties");
        await Assert.That(checkProps.TryGetProperty("check", out var checkRef)).IsTrue()
            .Because("the check arm must carry a `check` member.");
        var checkRefName = Path.GetFileNameWithoutExtension(checkRef.GetProperty("$ref").GetString());
        await Assert.That(checkRefName).IsEqualTo("CheckNode")
            .Because("the check arm's `check` member must reference CheckNode.");

        // audit arm carries an audit-prompt string (kebab wire name).
        var auditProps = armsByMode["audit"].GetProperty("properties");
        await Assert.That(auditProps.TryGetProperty("audit-prompt", out var prompt)).IsTrue()
            .Because("the audit arm must carry a kebab-case `audit-prompt`.");
        await Assert.That(prompt.GetProperty("type").GetString()).IsEqualTo("string")
            .Because("audit-prompt must be a string.");
    }
}
