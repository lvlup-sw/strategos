// -----------------------------------------------------------------------
// <copyright file="SagaIdentityEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Verifies the additive G1-Strategos identity emit changes: the saga base list
/// includes <c>IPhaseAwareSaga</c>, a computed <c>CurrentPhaseName</c> property
/// exists, and the abstractions namespace is in the usings list.
/// </summary>
/// <remarks>
/// Anchors DR-6 (generator portion). DR-7 negation tests live in
/// <see cref="SagaIdentityNegationTests"/>.
/// </remarks>
[Property("Category", "Integration")]
public class SagaIdentityEmitterTests
{
    /// <summary>
    /// The generated saga base list must include <c>IPhaseAwareSaga</c> so
    /// the basileus middleware can bind to the marker interface.
    /// </summary>
    [Test]
    public async Task SagaEmitter_GeneratesPartialClass_WithIPhaseAwareSagaInBaseList()
    {
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).Contains(": Saga, IPhaseAwareSaga");
    }

    /// <summary>
    /// The generator emits a computed read-only property over <c>Phase.ToString()</c>;
    /// this is the only new instance member added per DR-6.
    /// </summary>
    [Test]
    public async Task SagaEmitter_GeneratesCurrentPhaseNameProperty_AsComputedReadOnlyOverPhaseToString()
    {
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).Contains("public string CurrentPhaseName => Phase.ToString();");
    }

    /// <summary>
    /// The usings list must contain the abstractions namespace so the generated
    /// source resolves the <c>IPhaseAwareSaga</c> base-list reference.
    /// </summary>
    [Test]
    public async Task SagaEmitter_GeneratesUsingForStrategosIdentityAbstractions()
    {
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).Contains("using Strategos.Identity.Abstractions;");
    }
}
