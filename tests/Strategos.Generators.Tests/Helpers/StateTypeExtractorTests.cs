// -----------------------------------------------------------------------
// <copyright file="StateTypeExtractorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="StateTypeExtractor"/>.
/// </summary>
[Property("Category", "Unit")]
public class StateTypeExtractorTests
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
            StateTypeExtractor.Extract(null!));
    }

    // =============================================================================
    // B. State Type Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract returns the state type name from Workflow&lt;TState&gt;.Create().
    /// </summary>
    [Test]
    public async Task Extract_WorkflowWithStateType_ReturnsStateTypeName()
    {
        // Arrange
        const string code = @"
            public class OrderState { }
            public class OrderWorkflow
            {
                public void Define()
                {
                    Workflow<OrderState>.Create(""order"")
                        .StartWith<ValidateOrder>()
                        .Finally<CompleteOrder>();
                }
            }";
        var context = CreateContext(code);

        // Act
        var result = StateTypeExtractor.Extract(context);

        // Assert
        await Assert.That(result).IsEqualTo("OrderState");
    }

    /// <summary>
    /// Verifies that Extract returns null when no Create call exists.
    /// </summary>
    [Test]
    public async Task Extract_NoCreateCall_ReturnsNull()
    {
        // Arrange
        const string code = @"
            public class SomeWorkflow
            {
                public void Define()
                {
                    builder.StartWith<ValidateOrder>();
                }
            }";
        var context = CreateContext(code);

        // Act
        var result = StateTypeExtractor.Extract(context);

        // Assert
        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Verifies that Extract returns null when Create is not on a generic type.
    /// </summary>
    [Test]
    public async Task Extract_CreateOnNonGenericType_ReturnsNull()
    {
        // Arrange
        const string code = @"
            public class SomeWorkflow
            {
                public void Define()
                {
                    Builder.Create(""order"")
                        .StartWith<ValidateOrder>();
                }
            }";
        var context = CreateContext(code);

        // Act
        var result = StateTypeExtractor.Extract(context);

        // Assert
        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Verifies that Extract handles qualified state type names.
    /// </summary>
    [Test]
    public async Task Extract_QualifiedTypeName_ReturnsSimpleName()
    {
        // Arrange
        const string code = @"
            namespace MyNamespace
            {
                public class OrderState { }
            }
            public class OrderWorkflow
            {
                public void Define()
                {
                    Workflow<MyNamespace.OrderState>.Create(""order"")
                        .StartWith<ValidateOrder>()
                        .Finally<CompleteOrder>();
                }
            }";
        var context = CreateContext(code);

        // Act
        var result = StateTypeExtractor.Extract(context);

        // Assert
        await Assert.That(result).IsEqualTo("OrderState");
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
