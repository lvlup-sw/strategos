// =============================================================================
// <copyright file="FusionMethodTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

/// <summary>
/// PR-C Task 23: <see cref="FusionMethod"/> enum surface.
/// </summary>
public sealed class FusionMethodTests
{
    [Test]
    public async Task FusionMethod_HasReciprocalAndDistributionBased_NumericValuesStable()
    {
        // Numeric stability matters because the values can be persisted in
        // configuration / logs / _meta projections; design §6.1 pins them.
        await Assert.That((int)FusionMethod.Reciprocal).IsEqualTo(0);
        await Assert.That((int)FusionMethod.DistributionBased).IsEqualTo(1);
    }
}
