// -----------------------------------------------------------------------
// <copyright file="RollbackRuleDisclosureTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IO;

namespace Strategos.Generators.Tests.Docs;

/// <summary>
/// Prose gate binding the published rollback-order rule to the emitted plan predicate.
/// </summary>
/// <remarks>
/// <para>
/// The derived rollback has two failure ingresses and they select different prefixes. An
/// in-flight failure at <c>C</c> after <c>A ; B</c> completed runs <c>B^-1 ; A^-1</c> and
/// <c>C</c> is absent, because <c>C</c> never journaled a completed entry. A failure reported
/// after <c>C</c> completed — the reducer flags <c>Failed</c> on an occurrence whose journal
/// entry already reads <c>Completed</c> — runs <c>C^-1 ; B^-1 ; A^-1</c>, because the plan
/// predicate selects purely by journal status and the post-completion failure claim requires
/// and preserves that entry.
/// </para>
/// <para>
/// A consumer who reads only the exclusive rule authors an inverse that performs an external
/// effect unconditionally, and it runs on a path the documentation said cannot happen. Every
/// consumer-facing document that states the rollback order must therefore state both rules,
/// and the sentence is bound here to the mechanism it describes: if the plan predicate moves
/// out of <c>BeginCompensationScope</c>, this test names the documents to re-check.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class RollbackRuleDisclosureTests
{
    private const string PostCompletionMarker = "post-completion failure";

    private const string PlanPredicate =
        "string.Equals(entry.Status, \"Completed\", StringComparison.Ordinal)";

    private const string EmitterPath =
        "src/Strategos.Generators/Emitters/Saga/SagaCompensationComponentEmitter.cs";

    /// <summary>The consumer-facing documents that state the derived rollback order.</summary>
    /// <returns>Repository-relative paths.</returns>
    public static IEnumerable<string> DisclosureDocuments()
    {
        yield return "CHANGELOG.md";
        yield return "docs/src/content/docs/reference/action-calculus.md";
        yield return "docs/src/content/docs/guide/ontology/migration-v2-13.md";
        yield return "docs/src/content/docs/reference/diagnostics/agwf-agsr.md";
    }

    /// <summary>Every document stating the rollback order names the post-completion ingress.</summary>
    /// <param name="relativePath">The repository-relative document path.</param>
    /// <returns>A task representing the assertion.</returns>
    [Test]
    [MethodDataSource(nameof(DisclosureDocuments))]
    public async Task RollbackDisclosure_NamesThePostCompletionIngress(string relativePath)
    {
        var content = await ReadDocumentAsync(relativePath);

        await Assert.That(content.Contains(PostCompletionMarker, StringComparison.Ordinal))
            .IsTrue()
            .Because(
                $"{relativePath} describes the derived rollback order, so it must state the "
                + $"post-completion ingress using the marker phrase '{PostCompletionMarker}'");
    }

    /// <summary>
    /// The exclusive rule may only be stated in a paragraph that also states the inclusive one.
    /// </summary>
    /// <param name="relativePath">The repository-relative document path.</param>
    /// <returns>A task representing the assertion.</returns>
    [Test]
    [MethodDataSource(nameof(DisclosureDocuments))]
    public async Task ExclusiveRollbackClaim_IsQualifiedInItsOwnParagraph(string relativePath)
    {
        var content = await ReadDocumentAsync(relativePath);

        var unqualified = SplitParagraphs(content)
            .Where(static paragraph =>
                paragraph.Contains("is never included", StringComparison.OrdinalIgnoreCase)
                || paragraph.Contains(
                    "the action that failed is excluded",
                    StringComparison.OrdinalIgnoreCase))
            .Where(static paragraph =>
                !paragraph.Contains("post-completion", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        await Assert.That(unqualified).IsEmpty()
            .Because(
                $"{relativePath} states that the failing action is excluded from its rollback, "
                + "which is true only of the in-flight ingress; the same paragraph must qualify "
                + "it with the post-completion case");
    }

    /// <summary>
    /// The documented sentence is bound to the emitted predicate that produces the behaviour.
    /// </summary>
    /// <returns>A task representing the assertion.</returns>
    [Test]
    public async Task PlanPredicate_StillSelectsCompletedEntriesInsideBeginCompensationScope()
    {
        var emitter = await ReadDocumentAsync(EmitterPath);

        // The emitter writes C# as string literals, so the predicate appears escaped in the
        // emitter source. Normalise the escaping before matching.
        var emitted = Unescape(emitter);
        var planner = CarveMethod(emitted, "EmitRollbackPlanner", "EmitDispatchMethod");

        await Assert.That(planner.Contains("BeginCompensationScope(", StringComparison.Ordinal))
            .IsTrue()
            .Because("EmitRollbackPlanner is the emitter that writes BeginCompensationScope");
        await Assert.That(planner.Contains(PlanPredicate, StringComparison.Ordinal))
            .IsTrue()
            .Because(
                "the rollback scope is selected purely by journal status, which is why a "
                + "post-completion failure includes the failing occurrence's own inverse. If "
                + "this predicate moved or narrowed, re-check the rollback-order sentence in "
                + string.Join(", ", DisclosureDocuments()));
    }

    private static string Unescape(string source) =>
        source.Replace("\\\"", "\"", StringComparison.Ordinal);

    private static string CarveMethod(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException(
                $"'{startMarker}' was not found in {EmitterPath}. The rollback planner was "
                + "renamed or removed; re-check the documented rollback order.");
        }

        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        return end < 0 ? source[start..] : source[start..end];
    }

    private static IEnumerable<string> SplitParagraphs(string content) =>
        content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

    private static Task<string> ReadDocumentAsync(string relativePath) =>
        File.ReadAllTextAsync(
            Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string RepoRoot()
    {
        // Walk up from the test binary's location until global.json is found.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                $"Could not locate repository root (global.json) from {AppContext.BaseDirectory}.");
        }

        return directory.FullName;
    }
}
