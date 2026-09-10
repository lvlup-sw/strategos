// -----------------------------------------------------------------------
// <copyright file="LoopExtractorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="LoopExtractor"/>.
/// </summary>
[Property("Category", "Unit")]
public class LoopExtractorTests
{
    // =============================================================================
    // A. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract throws ArgumentNullException when context is null.
    /// </summary>
    [Test]
    public void Extract_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            LoopExtractor.Extract(null!));
    }

    // =============================================================================
    // B. Loop Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract returns empty list when no Finally call exists.
    /// </summary>
    [Test]
    public async Task Extract_NoFinallyCall_ReturnsEmptyList()
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
        var context = CreateContext(code, "TestWorkflow");

        // Act
        var result = LoopExtractor.Extract(context);

        // Assert
        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Verifies that Extract returns empty list when no loops exist.
    /// </summary>
    [Test]
    public async Task Extract_LinearWorkflow_ReturnsEmptyList()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Step1>()
                        .Then<Step2>()
                        .Finally<Step3>();
                }
            }";
        var context = CreateContext(code, "TestWorkflow");

        // Act
        var result = LoopExtractor.Extract(context);

        // Assert
        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Verifies that Extract extracts loop name.
    /// </summary>
    [Test]
    public async Task Extract_WithLoop_ExtractsLoopName()
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
        var context = CreateContext(code, "TestWorkflow");

        // Act
        var result = LoopExtractor.Extract(context);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].LoopName).IsEqualTo("Retry");
    }

    /// <summary>
    /// Verifies that Extract generates correct condition ID.
    /// </summary>
    [Test]
    public async Task Extract_WithLoop_GeneratesConditionId()
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
        var context = CreateContext(code, "OrderWorkflow");

        // Act
        var result = LoopExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].ConditionId).IsEqualTo("OrderWorkflow-Retry");
    }

    /// <summary>
    /// Verifies that Extract extracts max iterations.
    /// </summary>
    [Test]
    public async Task Extract_WithMaxIterations_ExtractsMaxIterations()
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
                            loop => loop.Then<RetryStep>(),
                            5)
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "TestWorkflow");

        // Act
        var result = LoopExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].MaxIterations).IsEqualTo(5);
    }

    /// <summary>
    /// Verifies that Extract defaults max iterations to 10.
    /// </summary>
    [Test]
    public async Task Extract_WithoutMaxIterations_DefaultsTo10()
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
        var context = CreateContext(code, "TestWorkflow");

        // Act
        var result = LoopExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].MaxIterations).IsEqualTo(10);
    }

    // =============================================================================
    // Private Helpers
    // =============================================================================

    private static FluentDslParseContext CreateContext(string source, string workflowName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var typeDeclaration = syntaxTree.GetRoot();

        return FluentDslParseContext.Create(typeDeclaration, semanticModel, workflowName, CancellationToken.None);
    }
}
