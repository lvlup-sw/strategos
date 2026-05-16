// =============================================================================
// <copyright file="FusedResultTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

/// <summary>
/// PR-B Task 12: <see cref="FusedResult"/> record contract.
/// </summary>
public sealed class FusedResultTests
{
    [Test]
    public async Task Ctor_Constructs_FieldsRoundTrip()
    {
        var result = new FusedResult("doc-1", 0.0328, 2);

        await Assert.That(result.DocumentId).IsEqualTo("doc-1");
        await Assert.That(result.FusedScore).IsEqualTo(0.0328);
        await Assert.That(result.Rank).IsEqualTo(2);
    }
}
