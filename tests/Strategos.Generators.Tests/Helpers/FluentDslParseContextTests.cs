// -----------------------------------------------------------------------
// <copyright file="FluentDslParseContextTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;
using Strategos.Generators.Tests.Fixtures;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="FluentDslParseContext"/>.
/// </summary>
[Property("Category", "Unit")]
public class FluentDslParseContextTests
{
    // =============================================================================
    // A. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create throws ArgumentNullException when typeDeclaration is null.
    /// </summary>
    [Test]
    public void Create_NullTypeDeclaration_ThrowsArgumentNullException()
    {
        // Arrange
        var (_, semanticModel) = CreateCompilation("class Empty { }");

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            FluentDslParseContext.Create(null!, semanticModel, null, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that Create throws ArgumentNullException when semanticModel is null.
    /// </summary>
    [Test]
    public void Create_NullSemanticModel_ThrowsArgumentNullException()
    {
        // Arrange
        var (compilation, _) = CreateCompilation("class Empty { }");
        var typeDeclaration = compilation.SyntaxTrees.First().GetRoot();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            FluentDslParseContext.Create(typeDeclaration, null!, null, CancellationToken.None));
    }

    // =============================================================================
    // B. Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that TypeDeclaration property returns the provided syntax node.
    /// </summary>
    [Test]
    public async Task Create_ValidInputs_TypeDeclarationIsSet()
    {
        // Arrange
        var (compilation, semanticModel) = CreateCompilation("class Empty { }");
        var typeDeclaration = compilation.SyntaxTrees.First().GetRoot();

        // Act
        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, CancellationToken.None);

        // Assert
        await Assert.That(context.TypeDeclaration).IsEqualTo(typeDeclaration);
    }

    /// <summary>
    /// Verifies that SemanticModel property returns the provided model.
    /// </summary>
    [Test]
    public async Task Create_ValidInputs_SemanticModelIsSet()
    {
        // Arrange
        var (compilation, semanticModel) = CreateCompilation("class Empty { }");
        var typeDeclaration = compilation.SyntaxTrees.First().GetRoot();

        // Act
        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, CancellationToken.None);

        // Assert
        await Assert.That(context.SemanticModel).IsEqualTo(semanticModel);
    }

    /// <summary>
    /// Verifies that WorkflowName property returns the provided name.
    /// </summary>
    [Test]
    public async Task Create_WithWorkflowName_WorkflowNameIsSet()
    {
        // Arrange
        var (compilation, semanticModel) = CreateCompilation("class Empty { }");
        var typeDeclaration = compilation.SyntaxTrees.First().GetRoot();

        // Act
        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, "MyWorkflow", CancellationToken.None);

        // Assert
        await Assert.That(context.WorkflowName).IsEqualTo("MyWorkflow");
    }

    /// <summary>
    /// Verifies that WorkflowName property returns null when not provided.
    /// </summary>
    [Test]
    public async Task Create_WithoutWorkflowName_WorkflowNameIsNull()
    {
        // Arrange
        var (compilation, semanticModel) = CreateCompilation("class Empty { }");
        var typeDeclaration = compilation.SyntaxTrees.First().GetRoot();

        // Act
        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, CancellationToken.None);

        // Assert
        await Assert.That(context.WorkflowName).IsNull();
    }

    // =============================================================================
    // C. Pre-Computed Lookups Tests
    // =============================================================================

    /// <summary>
    /// Verifies that AllInvocations contains all invocations in the type.
    /// </summary>
    [Test]
    public async Task Create_WithInvocations_AllInvocationsIsPopulated()
    {
        // Arrange
        const string code = @"
            class Test
            {
                void M()
                {
                    A().B().C();
                }
            }";
        var (compilation, semanticModel) = CreateCompilation(code);
        var typeDeclaration = compilation.SyntaxTrees.First().GetRoot();

        // Act
        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, CancellationToken.None);

        // Assert - A(), B(), C() = 3 invocations
        await Assert.That(context.AllInvocations.Count).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that AllInvocations is empty when type has no invocations.
    /// </summary>
    [Test]
    public async Task Create_NoInvocations_AllInvocationsIsEmpty()
    {
        // Arrange
        const string code = "class Test { int x = 5; }";
        var (compilation, semanticModel) = CreateCompilation(code);
        var typeDeclaration = compilation.SyntaxTrees.First().GetRoot();

        // Act
        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, CancellationToken.None);

        // Assert
        await Assert.That(context.AllInvocations).IsEmpty();
    }

    /// <summary>
    /// Verifies that FinallyInvocation is populated when Finally() call exists.
    /// </summary>
    [Test]
    public async Task Create_WithFinallyCall_FinallyInvocationIsPopulated()
    {
        // Arrange
        const string code = @"
            class Test
            {
                void M()
                {
                    builder.StartWith<A>().Then<B>().Finally<C>();
                }
            }";
        var (compilation, semanticModel) = CreateCompilation(code);
        var typeDeclaration = compilation.SyntaxTrees.First().GetRoot();

        // Act
        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, CancellationToken.None);

        // Assert
        await Assert.That(context.FinallyInvocation).IsNotNull();
    }

    /// <summary>
    /// Verifies that FinallyInvocation is null when no Finally() call exists.
    /// </summary>
    [Test]
    public async Task Create_WithoutFinallyCall_FinallyInvocationIsNull()
    {
        // Arrange
        const string code = @"
            class Test
            {
                void M()
                {
                    builder.StartWith<A>().Then<B>();
                }
            }";
        var (compilation, semanticModel) = CreateCompilation(code);
        var typeDeclaration = compilation.SyntaxTrees.First().GetRoot();

        // Act
        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, CancellationToken.None);

        // Assert
        await Assert.That(context.FinallyInvocation).IsNull();
    }

    // =============================================================================
    // Private Helpers
    // =============================================================================

    private static (CSharpCompilation Compilation, SemanticModel SemanticModel) CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        return (compilation, semanticModel);
    }
}
