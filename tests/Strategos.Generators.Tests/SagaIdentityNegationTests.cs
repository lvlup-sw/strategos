// -----------------------------------------------------------------------
// <copyright file="SagaIdentityNegationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// DR-7 regression net: locks in that the generator emits NO identity-related
/// code beyond the additive <c>CurrentPhaseName</c> + <c>IPhaseAwareSaga</c>
/// pieces verified in <see cref="SagaIdentityEmitterTests"/>.
/// </summary>
/// <remarks>
/// These tests pass immediately on first run after T8; their purpose is to
/// fail loudly if a future change tries to re-introduce the descoped
/// A1/A2/A3 emit (private fields, helpers, additional properties, or
/// <c>InternalsVisibleTo</c>).
/// </remarks>
[Property("Category", "Integration")]
public class SagaIdentityNegationTests
{
    /// <summary>DR-7: no CurrentAgentIdentity property is emitted on the saga.</summary>
    [Test]
    public async Task SagaEmitter_DoesNotEmit_CurrentAgentIdentity_Property()
    {
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).DoesNotContain("CurrentAgentIdentity");
    }

    /// <summary>DR-7: no InitializeIdentity helper method is emitted.</summary>
    [Test]
    public async Task SagaEmitter_DoesNotEmit_InitializeIdentity_Helper()
    {
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).DoesNotContain("InitializeIdentity");
    }

    /// <summary>DR-7: no _workflowIdentity backing field is emitted.</summary>
    [Test]
    public async Task SagaEmitter_DoesNotEmit_WorkflowIdentityField()
    {
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).DoesNotContain("_workflowIdentity");
    }

    /// <summary>DR-7: no _identityProvider backing field is emitted.</summary>
    [Test]
    public async Task SagaEmitter_DoesNotEmit_IdentityProviderField()
    {
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).DoesNotContain("_identityProvider");
    }

    /// <summary>DR-7: the generated saga assembly does not require InternalsVisibleTo.</summary>
    [Test]
    public async Task SagaEmitter_DoesNotEmit_InternalsVisibleTo_Attribute()
    {
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).DoesNotContain("InternalsVisibleTo");
    }
}
