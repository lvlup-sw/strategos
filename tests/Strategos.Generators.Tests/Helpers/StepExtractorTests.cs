// -----------------------------------------------------------------------
// <copyright file="StepExtractorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="StepExtractor"/>.
/// </summary>
[Property("Category", "Unit")]
public class StepExtractorTests
{
    // =============================================================================
    // A. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractStepInfos throws ArgumentNullException when context is null.
    /// </summary>
    [Test]
    public void ExtractStepInfos_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            StepExtractor.ExtractStepInfos(null!));
    }

    /// <summary>
    /// Verifies that ExtractStepModels throws ArgumentNullException when context is null.
    /// </summary>
    [Test]
    public void ExtractStepModels_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            StepExtractor.ExtractStepModels(null!));
    }

    // =============================================================================
    // B. ExtractStepInfos Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractStepInfos returns empty list when no Finally call exists.
    /// </summary>
    [Test]
    public async Task ExtractStepInfos_NoFinallyCall_ReturnsEmptyList()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Step1>().Then<Step2>();
                }
            }";
        var context = CreateContext(code);

        // Act
        var result = StepExtractor.ExtractStepInfos(context);

        // Assert
        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Verifies that ExtractStepInfos extracts steps in correct order.
    /// </summary>
    [Test]
    public async Task ExtractStepInfos_LinearWorkflow_ExtractsStepsInOrder()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<ValidateOrder>()
                        .Then<ProcessPayment>()
                        .Finally<SendConfirmation>();
                }
            }";
        var context = CreateContext(code);

        // Act
        var result = StepExtractor.ExtractStepInfos(context);

        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0].StepName).IsEqualTo("ValidateOrder");
        await Assert.That(result[1].StepName).IsEqualTo("ProcessPayment");
        await Assert.That(result[2].StepName).IsEqualTo("SendConfirmation");
    }

    /// <summary>
    /// Verifies that ExtractStepInfos sets LoopName to null for top-level steps.
    /// </summary>
    [Test]
    public async Task ExtractStepInfos_TopLevelStep_HasNullLoopName()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Step1>().Finally<Step2>();
                }
            }";
        var context = CreateContext(code);

        // Act
        var result = StepExtractor.ExtractStepInfos(context);

        // Assert
        await Assert.That(result[0].LoopName).IsNull();
        await Assert.That(result[0].PhaseName).IsEqualTo("Step1");
    }

    /// <summary>
    /// Verifies that ExtractStepInfos handles loop steps with correct prefix.
    /// </summary>
    [Test]
    public async Task ExtractStepInfos_LoopStep_HasLoopPrefix()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .RepeatUntil(
                            state => state.Done,
                            ""Retry"",
                            loop => loop.Then<RetryStep>())
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code);

        // Act
        var result = StepExtractor.ExtractStepInfos(context);

        // Assert
        // Steps should be: Init, Retry_RetryStep, Complete
        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0].PhaseName).IsEqualTo("Init");
        await Assert.That(result[1].PhaseName).IsEqualTo("Retry_RetryStep");
        await Assert.That(result[1].LoopName).IsEqualTo("Retry");
        await Assert.That(result[2].PhaseName).IsEqualTo("Complete");
    }

    // =============================================================================
    // C. ExtractStepModels Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractStepModels returns empty list when no Finally call exists.
    /// </summary>
    [Test]
    public async Task ExtractStepModels_NoFinallyCall_ReturnsEmptyList()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Step1>().Then<Step2>();
                }
            }";
        var context = CreateContext(code);

        // Act
        var result = StepExtractor.ExtractStepModels(context);

        // Assert
        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Verifies that ExtractStepModels extracts step type names.
    /// </summary>
    [Test]
    public async Task ExtractStepModels_LinearWorkflow_ExtractsStepTypeNames()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<ValidateOrder>()
                        .Then<ProcessPayment>()
                        .Finally<SendConfirmation>();
                }
            }";
        var context = CreateContext(code);

        // Act
        var result = StepExtractor.ExtractStepModels(context);

        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0].StepTypeName).IsEqualTo("ValidateOrder");
        await Assert.That(result[1].StepTypeName).IsEqualTo("ProcessPayment");
        await Assert.That(result[2].StepTypeName).IsEqualTo("SendConfirmation");
    }

    // =============================================================================
    // Private Helpers
    // =============================================================================

    private static FluentDslParseContext CreateContext(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var typeDeclaration = syntaxTree.GetRoot();

        return FluentDslParseContext.Create(typeDeclaration, semanticModel, null, CancellationToken.None);
    }
}
