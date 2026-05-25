// =============================================================================
// <copyright file="BuilderApiBaselineTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;

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
/// </summary>
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

    private static string ShippedBaselinePath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos", "PublicAPI", "PublicAPI.Shipped.txt");

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
        var typeDeclarationLines = baseline
            .Split('\n')
            .Select(static l => l.Trim())
            .Where(static l => l.Length > 0 && !l.StartsWith('#'))
            .ToArray();

        // Every non-directive line must reference the Strategos.Builders
        // namespace — the only namespace any of the 7 tracked interfaces lives
        // in. A line outside it would mean a non-builder (e.g. SG-internal or
        // orchestration) type leaked into the cross-product baseline (INV-1).
        foreach (var line in typeDeclarationLines)
        {
            await Assert.That(line)
                .Contains("Strategos.Builders");
        }
    }

    [Test]
    public async Task ShippedBaseline_DeclaresEveryPublicMemberOfTrackedInterfaces()
    {
        var baseline = await File.ReadAllTextAsync(ShippedBaselinePath);

        foreach (var iface in TrackedInterfaces)
        {
            foreach (var member in iface.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                // The baseline records each member's simple name on its line; a
                // missing member would be exactly the RS0016 the analyzer raises
                // at build time. We assert the member name appears so the
                // baseline can never silently drop a builder method.
                await Assert.That(baseline)
                    .Contains(member.Name);
            }
        }
    }
}
