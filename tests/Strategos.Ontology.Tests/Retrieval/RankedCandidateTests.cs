// =============================================================================
// <copyright file="RankedCandidateTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

/// <summary>
/// PR-B Task 10: <see cref="RankedCandidate"/> record contract.
/// </summary>
public sealed class RankedCandidateTests
{
    [Test]
    public async Task Ctor_Constructs_FieldsRoundTrip()
    {
        var candidate = new RankedCandidate("doc-1", 3);

        await Assert.That(candidate.DocumentId).IsEqualTo("doc-1");
        await Assert.That(candidate.Rank).IsEqualTo(3);
    }
}
