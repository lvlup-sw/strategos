// =============================================================================
// <copyright file="ScoredCandidateTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

/// <summary>
/// PR-B Task 11: <see cref="ScoredCandidate"/> record contract.
/// </summary>
public sealed class ScoredCandidateTests
{
    [Test]
    public async Task Ctor_Constructs_FieldsRoundTrip()
    {
        var candidate = new ScoredCandidate("doc-1", 0.92);

        await Assert.That(candidate.DocumentId).IsEqualTo("doc-1");
        await Assert.That(candidate.Score).IsEqualTo(0.92);
    }
}
