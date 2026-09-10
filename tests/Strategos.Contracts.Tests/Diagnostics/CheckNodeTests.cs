// =============================================================================
// <copyright file="CheckNodeTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests.Diagnostics;

/// <summary>
/// T25 — the recursive <c>CheckNode</c> combinator tree (#98). Compiles the
/// canonical <c>.tsp</c> and asserts <c>CheckNode</c> is a discriminated union
/// over the three leaf kinds (<c>grep</c> / <c>structural</c> / <c>heuristic</c>,
/// carrying <c>pattern</c> / <c>file-glob</c> / <c>threshold</c>) and the four
/// combinator arms (<c>all-of</c> / <c>any-of</c> / <c>not</c> / <c>scope</c>)
/// whose children recurse back into <c>CheckNode</c> via a self-referential
/// <c>$ref</c>.
/// </summary>
[Property("Category", "Diagnostics")]
[NotInParallel("tsp-compile")]
public class CheckNodeTests
{
    private static readonly string[] ExpectedKinds =
        ["grep", "structural", "heuristic", "all-of", "any-of", "not", "scope"];

    /// <summary>
    /// Asserts <c>CheckNode</c> emits an <c>anyOf</c> union over the seven kind
    /// arms, that the leaf arms carry their declarative match fields, and that
    /// the combinator arms recurse into <c>CheckNode</c> (a self <c>$ref</c>).
    /// </summary>
    [Test]
    public async Task CheckNode_Schema_SupportsRecursiveAllOfAnyOfNotScope()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        var root = await EventSchemas.LoadAsync("CheckNode");

        await Assert.That(root.TryGetProperty("anyOf", out var anyOf)).IsTrue()
            .Because("CheckNode must be a discriminated union (anyOf of arms).");

        var armNames = anyOf.EnumerateArray()
            .Where(a => a.TryGetProperty("$ref", out _))
            .Select(a => Path.GetFileNameWithoutExtension(a.GetProperty("$ref").GetString()))
            .ToList();
        await Assert.That(armNames.Count).IsEqualTo(7)
            .Because("there must be exactly seven CheckNode arms (3 leaves + 4 combinators).");

        // Map each arm's kind const to its emitted document.
        var armsByKind = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
        foreach (var armName in armNames)
        {
            var arm = await EventSchemas.LoadAsync(armName!);
            var kind = arm.GetProperty("properties").GetProperty("kind").GetProperty("const").GetString();
            armsByKind[kind!] = arm;
        }

        foreach (var kind in ExpectedKinds)
        {
            await Assert.That(armsByKind.ContainsKey(kind)).IsTrue()
                .Because($"the {kind} CheckNode arm must be present.");
        }

        // Leaf arms carry declarative match fields.
        var grepProps = armsByKind["grep"].GetProperty("properties");
        await Assert.That(grepProps.TryGetProperty("pattern", out _)).IsTrue()
            .Because("the grep leaf must carry a `pattern`.");
        await Assert.That(grepProps.TryGetProperty("file-glob", out _)).IsTrue()
            .Because("the grep leaf must carry a kebab-case `file-glob`.");

        var heuristicProps = armsByKind["heuristic"].GetProperty("properties");
        await Assert.That(heuristicProps.TryGetProperty("threshold", out _)).IsTrue()
            .Because("the heuristic leaf must carry a `threshold`.");

        // Combinator arms recurse: all-of / any-of carry a `children` array of
        // CheckNode; not / scope carry a single child CheckNode.
        await AssertRefsCheckNodeArray(armsByKind["all-of"], "all-of");
        await AssertRefsCheckNodeArray(armsByKind["any-of"], "any-of");
        await AssertRefsCheckNodeChild(armsByKind["not"], "not");
        await AssertRefsCheckNodeChild(armsByKind["scope"], "scope");
    }

    private static async Task AssertRefsCheckNodeArray(System.Text.Json.JsonElement arm, string kind)
    {
        var children = arm.GetProperty("properties").GetProperty("children");
        await Assert.That(children.GetProperty("type").GetString()).IsEqualTo("array")
            .Because($"the {kind} arm's children must be an array.");
        var itemRef = Path.GetFileNameWithoutExtension(
            children.GetProperty("items").GetProperty("$ref").GetString());
        await Assert.That(itemRef).IsEqualTo("CheckNode")
            .Because($"the {kind} arm's children must recurse into CheckNode.");
    }

    private static async Task AssertRefsCheckNodeChild(System.Text.Json.JsonElement arm, string kind)
    {
        var child = arm.GetProperty("properties").GetProperty("child");
        var childRef = Path.GetFileNameWithoutExtension(child.GetProperty("$ref").GetString());
        await Assert.That(childRef).IsEqualTo("CheckNode")
            .Because($"the {kind} arm's child must recurse into CheckNode.");
    }
}
