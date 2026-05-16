// =============================================================================
// <copyright file="HybridQueryOptionsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

/// <summary>
/// PR-C Tasks 24/25: <see cref="HybridQueryOptions"/> record — defaults & validation.
/// </summary>
public sealed class HybridQueryOptionsTests
{
    // ---- Task 24: defaults & immutability ----

    [Test]
    public async Task Defaults_EnableKeywordTrue_FusionMethodReciprocal_K60_SparseAndDenseTopK50()
    {
        var options = new HybridQueryOptions();

        await Assert.That(options.EnableKeyword).IsTrue();
        await Assert.That(options.FusionMethod).IsEqualTo(FusionMethod.Reciprocal);
        await Assert.That(options.RrfK).IsEqualTo(60);
        await Assert.That(options.SparseTopK).IsEqualTo(50);
        await Assert.That(options.DenseTopK).IsEqualTo(50);
    }

    [Test]
    public async Task Defaults_SourceWeightsNull_BmSaturationThreshold18()
    {
        var options = new HybridQueryOptions();

        await Assert.That(options.SourceWeights).IsNull();
        await Assert.That(options.BmSaturationThreshold).IsEqualTo(18.0);
    }

    [Test]
    public async Task With_OverrideFusionMethod_PreservesOthers()
    {
        var options = new HybridQueryOptions { FusionMethod = FusionMethod.DistributionBased };

        await Assert.That(options.FusionMethod).IsEqualTo(FusionMethod.DistributionBased);
        // every other property keeps its default
        await Assert.That(options.EnableKeyword).IsTrue();
        await Assert.That(options.RrfK).IsEqualTo(60);
        await Assert.That(options.SparseTopK).IsEqualTo(50);
        await Assert.That(options.DenseTopK).IsEqualTo(50);
        await Assert.That(options.SourceWeights).IsNull();
        await Assert.That(options.BmSaturationThreshold).IsEqualTo(18.0);
    }

    // ---- Task 25: argument validation ----

    [Test]
    public async Task Validate_SparseTopKNegative_ThrowsArgumentOutOfRangeException()
    {
        var options = new HybridQueryOptions { SparseTopK = -1 };

        await Assert.That(() => options.Validate())
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validate_DenseTopKNegative_ThrowsArgumentOutOfRangeException()
    {
        var options = new HybridQueryOptions { DenseTopK = -1 };

        await Assert.That(() => options.Validate())
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validate_RrfKZero_ThrowsArgumentOutOfRangeException()
    {
        var options = new HybridQueryOptions { RrfK = 0 };

        await Assert.That(() => options.Validate())
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validate_RrfKNegative_ThrowsArgumentOutOfRangeException()
    {
        var options = new HybridQueryOptions { RrfK = -3 };

        await Assert.That(() => options.Validate())
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Validate_SourceWeightsLengthThree_ThrowsArgumentException()
    {
        var options = new HybridQueryOptions { SourceWeights = new[] { 1.0, 0.5, 0.25 } };

        await Assert.That(() => options.Validate())
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Validate_SourceWeightsNegativeElement_ThrowsArgumentException()
    {
        var options = new HybridQueryOptions { SourceWeights = new[] { 1.0, -0.5 } };

        await Assert.That(() => options.Validate())
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Validate_HappyPath_DoesNotThrow()
    {
        // Sanity: every default must validate.
        var options = new HybridQueryOptions();

        await Assert.That(() => options.Validate()).ThrowsNothing();
    }
}
