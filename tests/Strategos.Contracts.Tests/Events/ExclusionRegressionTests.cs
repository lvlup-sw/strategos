// =============================================================================
// <copyright file="ExclusionRegressionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Contracts.Tests.Events;

/// <summary>
/// T10 — the exclusion regression (ADR §2.3.4). <c>NotificationEnvelope</c> and
/// <c>FormattedNotification</c> are deliberately NOT part of the cross-product
/// contract; they are an Exarchos-internal presentation concern. This is
/// enforced mechanically — if anyone adds either model to TypeSpec, the emitted
/// schemas or generated types would carry it and this test fails (rather than
/// relying on a comment that says "don't add this").
/// </summary>
[Property("Category", "Events")]
[NotInParallel("tsp-compile")]
public class ExclusionRegressionTests
{
    private static readonly string[] Excluded =
    [
        "NotificationEnvelope",
        "FormattedNotification",
    ];

    /// <summary>
    /// Compiles the contracts <c>.tsp</c> and asserts neither excluded model is
    /// present in the emitted JSON Schema set or in the generated assembly types.
    /// </summary>
    [Test]
    public async Task Contracts_DoNotDefine_NotificationEnvelope()
    {
        var result = await TspToolchain.CompileAsync();
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);

        var emitted = EventSchemas.AllModelNames();
        var generatedTypes = typeof(ContractsMarker).Assembly
            .GetTypes()
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in Excluded)
        {
            await Assert.That(emitted.Contains(name)).IsFalse()
                .Because($"{name} must NOT be emitted as a JSON Schema (ADR §2.3.4 exclusion).");
            await Assert.That(generatedTypes.Contains(name)).IsFalse()
                .Because($"{name} must NOT be a generated contract type (ADR §2.3.4 exclusion).");
        }
    }
}
