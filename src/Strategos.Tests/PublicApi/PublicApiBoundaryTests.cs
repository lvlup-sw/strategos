// =============================================================================
// <copyright file="PublicApiBoundaryTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Tests.FixtureExport;

namespace Strategos.Tests.PublicApi;

/// <summary>
/// #106 api-mirror scope. The public-api-drift gate watches
/// <c>PublicAPI.Shipped.txt</c> and notifies the downstream exarchos
/// <c>strategos-api-mirror</c>. That mirror parses only the SEVEN historical
/// <c>Strategos.Builders</c> entrypoint signatures, not the transitive type
/// graph. The local analyzer allowlist is deliberately broader for the public
/// API changed by #167. This suite asserts the workflow header documents both
/// boundaries on a stable, greppable marker.
/// </summary>
public sealed class PublicApiBoundaryTests
{
    private static string WorkflowPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, ".github", "workflows", "public-api-drift.yml");

    [Test]
    public async Task PublicApiGate_BoundaryDocumented()
    {
        var text = await File.ReadAllTextAsync(WorkflowPath);

        // Stable greppable marker pinning the documented boundary to #106.
        await Assert.That(text).Contains("Gated surface boundary (#106)");

        // Entrypoint-only parsing rationale (seven signatures, not the graph).
        await Assert.That(text).Contains("seven");
        await Assert.That(text).Contains("not the transitive type graph");

        // #167's local analyzer expansion does not silently redefine the
        // historical downstream parser boundary.
        await Assert.That(text).Contains("local analyzer allowlist");
        await Assert.That(text).Contains("WorkflowActionReference");

        // Continuation interfaces named from each end of the list.
        await Assert.That(text).Contains("IForkPathBuilder");
        await Assert.That(text).Contains("IContextBuilder");
    }
}
