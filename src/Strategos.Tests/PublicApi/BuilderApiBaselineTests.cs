// =============================================================================
// <copyright file="BuilderApiBaselineTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Text.RegularExpressions;

using Strategos.Builders;
using Strategos.Tests.FixtureExport;

namespace Strategos.Tests.PublicApi;

/// <summary>
/// #51 builder API-stability gate (PR-B), tasks T9/T10.
/// <para>
/// Microsoft.CodeAnalysis.PublicApiAnalyzers tracks the public surface of
/// <c>src/Strategos</c> against <c>PublicAPI.Shipped.txt</c>. INV-1 requires the
/// baseline be scoped to ONLY the 7 builder interfaces (so no source-generator-
/// internal types leak into the cross-product baseline exarchos's
/// <c>strategos-api-mirror.test.ts</c> consumes).
/// </para>
/// <para>
/// These tests assert the on-disk baseline (a) exists, (b) declares every one of
/// the 7 tracked interface TYPES, and (c) does not leak any non-builder type
/// (the INV-1 scope check). The analyzer itself enforces member-level drift at
/// build time; this suite guards the file-level invariants the analyzer config
/// depends on, in a fast deterministic unit test.
/// </para>
/// <para>
/// Shares the <c>PublicAPI.Shipped.txt-mutation</c> non-parallel key with
/// <see cref="GateFailClosedTests"/>: that suite transiently mutates the
/// on-disk baseline during its fail-closed proof, so these readers must be
/// serialized against it or they can observe the file mid-mutation and flake.
/// </para>
/// </summary>
[NotInParallel("PublicAPI.Shipped.txt-mutation")]
public sealed class BuilderApiBaselineTests
{
    /// <summary>The exactly-7 builder interface types that form the tracked surface.</summary>
    private static readonly Type[] TrackedInterfaces =
    [
        typeof(IWorkflowBuilder<>),
        typeof(IBranchBuilder<>),
        typeof(ILoopBuilder<>),
        typeof(IForkJoinBuilder<>),
        typeof(IApprovalBuilder<,>),
        typeof(IFailureBuilder<>),
        typeof(IStepConfiguration<>),
    ];

    /// <summary>
    /// DIM-5: the INTENDED gate scope, encoded as an explicit allowlist of the
    /// 7 builder-interface file stems (no <c>.cs</c>). This is the source of
    /// truth the <c>.editorconfig</c> re-enable section must match exactly.
    /// <para>
    /// The design scopes the cross-product gate to "7 entrypoints" (the
    /// <c>IWorkflowBuilder</c> family). Five more public builder interfaces
    /// (<c>IForkPathBuilder, ILoopForkJoinBuilder, IApprovalEscalationBuilder,
    /// IApprovalRejectionBuilder, IContextBuilder</c>) exist and appear as
    /// param/return types in the baseline but are INTENTIONALLY ungated — they
    /// are nested-fluent continuations reached only through the 7 entrypoints,
    /// not standalone surface exarchos mirrors. We therefore encode the
    /// intended-7 as an explicit allowlist (rather than "every interface under
    /// Abstractions/") and assert <c>.editorconfig == this set</c>, so renaming,
    /// moving, or adding an entrypoint interface without updating the
    /// <c>.editorconfig</c> glob converts the silent fail-open into a loud test
    /// failure. See docs/designs/2026-05-24-slice-b-convergence-close.md (DIM-5).
    /// </para>
    /// </summary>
    private static readonly string[] IntendedGatedFileStems =
    [
        "IWorkflowBuilder",
        "IBranchBuilder",
        "ILoopBuilder",
        "IForkJoinBuilder",
        "IApprovalBuilder",
        "IFailureBuilder",
        "IStepConfiguration",
    ];

    private static string ShippedBaselinePath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos", "PublicAPI", "PublicAPI.Shipped.txt");

    private static string EditorConfigPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos", ".editorconfig");

    private static string AbstractionsDir { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos", "Abstractions");

    [Test]
    public async Task ShippedBaseline_Exists()
    {
        await Assert.That(File.Exists(ShippedBaselinePath))
            .IsTrue();
    }

    [Test]
    public async Task ShippedBaseline_DeclaresAllSevenBuilderInterfaces()
    {
        var baseline = await File.ReadAllTextAsync(ShippedBaselinePath);

        foreach (var iface in TrackedInterfaces)
        {
            // PublicAPI.Shipped.txt records the unbound generic name without the
            // `1/`2 arity suffix, e.g. "Strategos.Builders.IWorkflowBuilder<TState>".
            var simpleName = iface.Name.Split('`')[0];
            var fqnPrefix = iface.Namespace + "." + simpleName;

            await Assert.That(baseline)
                .Contains(fqnPrefix);
        }
    }

    [Test]
    public async Task ShippedBaseline_DoesNotLeakNonBuilderTypes_Inv1()
    {
        var baseline = await File.ReadAllTextAsync(ShippedBaselinePath);
        var nonDirectiveLines = baseline
            .Split('\n')
            .Select(static l => l.Trim())
            .Where(static l => l.Length > 0 && !l.StartsWith('#'))
            .ToArray();

        // Every non-directive line declares a member OF one of the 7 tracked
        // interfaces, so its owning symbol is always in Strategos.Builders.
        // (Member signatures may *reference* types from other namespaces, e.g.
        // a return type — that is not a tracked type, just a reference.)
        foreach (var line in nonDirectiveLines)
        {
            await Assert.That(line)
                .Contains("Strategos.Builders");
        }

        // The hard INV-1 contract: the analyzer tracks EXACTLY 7 top-level
        // TYPE declarations and nothing else. A PublicAPI type-declaration line
        // is a bare fully-qualified type name with no member ('.' after the
        // type) and no signature arrow ('->'). If a source-generator-internal
        // or orchestration type leaked into scope it would add an 8th such line.
        var typeDeclarationLines = nonDirectiveLines
            .Where(static l => !l.Contains("->", StringComparison.Ordinal))
            .Where(static l =>
            {
                // Strip the generic argument list before counting member dots,
                // so "IApprovalBuilder<TState, TApprover>" is a type decl but
                // "IApprovalBuilder<TState, TApprover>.Build(...)" is not.
                var afterGenerics = l.Contains('>', StringComparison.Ordinal)
                    ? l[(l.LastIndexOf('>') + 1)..]
                    : l;
                return !afterGenerics.Contains('.', StringComparison.Ordinal);
            })
            .ToArray();

        await Assert.That(typeDeclarationLines.Length).IsEqualTo(7);
        foreach (var decl in typeDeclarationLines)
        {
            await Assert.That(decl).StartsWith("Strategos.Builders.I");
        }
    }

    [Test]
    public async Task ShippedBaseline_DeclaresEveryPublicMemberOfTrackedInterfaces()
    {
        var baseline = await File.ReadAllTextAsync(ShippedBaselinePath);

        var baselineLines = baseline.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var iface in TrackedInterfaces)
        {
            // Qualify by the DECLARING interface so a method name shared across
            // builder interfaces (e.g. Build/Complete) can't satisfy the check
            // via a different interface's line — a cross-interface false positive.
            var ownerPrefix = $"{iface.Namespace}.{iface.Name.Split('`')[0]}<";
            foreach (var member in iface.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                // A member missing from its owner's baseline lines is exactly the
                // RS0016 the analyzer raises at build time; assert the
                // interface-qualified member line is present so the baseline can
                // never silently drop a builder member.
                var found = baselineLines.Any(l =>
                    l.StartsWith(ownerPrefix, StringComparison.Ordinal) &&
                    l.Contains($".{member.Name}", StringComparison.Ordinal));
                await Assert.That(found).IsTrue();
            }
        }
    }

    /// <summary>
    /// DIM-5 consistency check: the reflection-driven <see cref="TrackedInterfaces"/>
    /// list and the file-stem <see cref="IntendedGatedFileStems"/> allowlist must
    /// describe the SAME 7 interfaces. Keeps the two intent encodings in lockstep.
    /// </summary>
    [Test]
    public async Task TrackedInterfaces_AndIntendedFileStems_DescribeSameSeven()
    {
        var typeStems = TrackedInterfaces
            .Select(static t => t.Name.Split('`')[0])
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();

        var allowlistStems = IntendedGatedFileStems
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();

        await Assert.That(typeStems).IsEquivalentTo(allowlistStems);
    }

    /// <summary>
    /// DIM-5: close the INV-1 fail-OPEN scoping hole. The <c>.editorconfig</c>
    /// re-enables the build-breaking RS00xx diagnostics for ONLY a hardcoded
    /// brace-list of filenames under <c>Abstractions/</c>. If a builder
    /// entrypoint interface is renamed/moved/added without updating that glob, it
    /// silently drops out of the gate (fail-open). This asserts the brace-list
    /// EXACTLY equals the intended-7 allowlist, so any drift fails this test loudly.
    /// </summary>
    [Test]
    public async Task EditorConfig_ReEnableGlob_ExactlyMatchesIntendedSeven()
    {
        var glob = await ReadEditorConfigBuilderGlobStemsAsync();

        var expected = IntendedGatedFileStems
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();
        var actual = glob
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();

        // Exact set equality: no missing entry (fail-open) and no extra entry
        // (gate scope creep beyond the design's 7 entrypoints).
        await Assert.That(actual).IsEquivalentTo(expected);
    }

    /// <summary>
    /// DIM-5 guard: every file the <c>.editorconfig</c> glob names must actually
    /// exist under <c>Abstractions/</c>. A glob entry that matches no file is a
    /// silent fail-open (the diagnostic is re-enabled for nothing), so a
    /// rename/move that left a stale glob entry behind fails here.
    /// </summary>
    [Test]
    public async Task EditorConfig_ReEnableGlob_NamesOnlyExistingAbstractionsFiles()
    {
        var glob = await ReadEditorConfigBuilderGlobStemsAsync();

        foreach (var stem in glob)
        {
            var path = Path.Combine(AbstractionsDir, stem + ".cs");
            await Assert.That(File.Exists(path))
                .IsTrue();
        }
    }

    /// <summary>
    /// Parses the single <c>[Abstractions/{...}.cs]</c> brace-list section header
    /// from <c>src/Strategos/.editorconfig</c> and returns the file stems it names.
    /// </summary>
    private static async Task<string[]> ReadEditorConfigBuilderGlobStemsAsync()
    {
        var text = await File.ReadAllTextAsync(EditorConfigPath);

        // Match the builder re-enable section header, e.g.
        //   [Abstractions/{IWorkflowBuilder,IBranchBuilder,...}.cs]
        var match = Regex.Match(
            text,
            @"\[Abstractions/\{(?<stems>[^}]+)\}\.cs\]",
            RegexOptions.None,
            TimeSpan.FromSeconds(2));

        await Assert.That(match.Success)
            .IsTrue();

        return match.Groups["stems"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }
}
