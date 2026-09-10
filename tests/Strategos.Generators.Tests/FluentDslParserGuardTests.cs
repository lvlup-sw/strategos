// -----------------------------------------------------------------------
// <copyright file="FluentDslParserGuardTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Generators.Tests;

/// <summary>
/// Tests guard clauses for <see cref="FluentDslParser"/> public methods.
/// </summary>
[Property("Category", "Unit")]
public class FluentDslParserGuardTests
{
    // =============================================================================
    // A. ExtractStepNames Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractStepNames throws for null typeDeclaration.
    /// </summary>
    [Test]
    public async Task ExtractStepNames_NullTypeDeclaration_ThrowsArgumentNullException()
    {
        // Arrange
        var semanticModel = CreateSemanticModel("class Test {}");

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractStepNames(null!, semanticModel, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ExtractStepNames throws for null semanticModel.
    /// </summary>
    [Test]
    public async Task ExtractStepNames_NullSemanticModel_ThrowsArgumentNullException()
    {
        // Arrange
        var syntaxTree = CSharpSyntaxTree.ParseText("class Test {}");
        var root = syntaxTree.GetRoot();

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractStepNames(root, null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. ExtractStateTypeName Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractStateTypeName throws for null typeDeclaration.
    /// </summary>
    [Test]
    public async Task ExtractStateTypeName_NullTypeDeclaration_ThrowsArgumentNullException()
    {
        // Arrange
        var semanticModel = CreateSemanticModel("class Test {}");

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractStateTypeName(null!, semanticModel, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ExtractStateTypeName throws for null semanticModel.
    /// </summary>
    [Test]
    public async Task ExtractStateTypeName_NullSemanticModel_ThrowsArgumentNullException()
    {
        // Arrange
        var syntaxTree = CSharpSyntaxTree.ParseText("class Test {}");
        var root = syntaxTree.GetRoot();

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractStateTypeName(root, null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // C. ExtractStepInfos Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractStepInfos throws for null typeDeclaration.
    /// </summary>
    [Test]
    public async Task ExtractStepInfos_NullTypeDeclaration_ThrowsArgumentNullException()
    {
        // Arrange
        var semanticModel = CreateSemanticModel("class Test {}");

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractStepInfos(null!, semanticModel, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ExtractStepInfos throws for null semanticModel.
    /// </summary>
    [Test]
    public async Task ExtractStepInfos_NullSemanticModel_ThrowsArgumentNullException()
    {
        // Arrange
        var syntaxTree = CSharpSyntaxTree.ParseText("class Test {}");
        var root = syntaxTree.GetRoot();

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractStepInfos(root, null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // D. ExtractLoopModels Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractLoopModels throws for null typeDeclaration.
    /// </summary>
    [Test]
    public async Task ExtractLoopModels_NullTypeDeclaration_ThrowsArgumentNullException()
    {
        // Arrange
        var semanticModel = CreateSemanticModel("class Test {}");

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractLoopModels(null!, semanticModel, "TestWorkflow", CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ExtractLoopModels throws for null semanticModel.
    /// </summary>
    [Test]
    public async Task ExtractLoopModels_NullSemanticModel_ThrowsArgumentNullException()
    {
        // Arrange
        var syntaxTree = CSharpSyntaxTree.ParseText("class Test {}");
        var root = syntaxTree.GetRoot();

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractLoopModels(root, null!, "TestWorkflow", CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ExtractLoopModels throws for null workflowName.
    /// </summary>
    [Test]
    public async Task ExtractLoopModels_NullWorkflowName_ThrowsArgumentException()
    {
        // Arrange
        var (root, semanticModel) = CreateSyntaxAndSemanticModel("class Test {}");

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractLoopModels(root, semanticModel, null!, CancellationToken.None))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that ExtractLoopModels throws for empty workflowName.
    /// </summary>
    [Test]
    public async Task ExtractLoopModels_EmptyWorkflowName_ThrowsArgumentException()
    {
        // Arrange
        var (root, semanticModel) = CreateSyntaxAndSemanticModel("class Test {}");

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractLoopModels(root, semanticModel, "", CancellationToken.None))
            .Throws<ArgumentException>();
    }

    // =============================================================================
    // E. ExtractStepModels Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractStepModels throws for null typeDeclaration.
    /// </summary>
    [Test]
    public async Task ExtractStepModels_NullTypeDeclaration_ThrowsArgumentNullException()
    {
        // Arrange
        var semanticModel = CreateSemanticModel("class Test {}");

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractStepModels(null!, semanticModel, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ExtractStepModels throws for null semanticModel.
    /// </summary>
    [Test]
    public async Task ExtractStepModels_NullSemanticModel_ThrowsArgumentNullException()
    {
        // Arrange
        var syntaxTree = CSharpSyntaxTree.ParseText("class Test {}");
        var root = syntaxTree.GetRoot();

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractStepModels(root, null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // F. ExtractBranchModels Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractBranchModels throws for null typeDeclaration.
    /// </summary>
    [Test]
    public async Task ExtractBranchModels_NullTypeDeclaration_ThrowsArgumentNullException()
    {
        // Arrange
        var semanticModel = CreateSemanticModel("class Test {}");

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractBranchModels(null!, semanticModel, "TestWorkflow", CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ExtractBranchModels throws for null semanticModel.
    /// </summary>
    [Test]
    public async Task ExtractBranchModels_NullSemanticModel_ThrowsArgumentNullException()
    {
        // Arrange
        var syntaxTree = CSharpSyntaxTree.ParseText("class Test {}");
        var root = syntaxTree.GetRoot();

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractBranchModels(root, null!, "TestWorkflow", CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ExtractBranchModels throws for null workflowName.
    /// </summary>
    [Test]
    public async Task ExtractBranchModels_NullWorkflowName_ThrowsArgumentException()
    {
        // Arrange
        var (root, semanticModel) = CreateSyntaxAndSemanticModel("class Test {}");

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractBranchModels(root, semanticModel, null!, CancellationToken.None))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that ExtractBranchModels throws for whitespace workflowName.
    /// </summary>
    [Test]
    public async Task ExtractBranchModels_WhitespaceWorkflowName_ThrowsArgumentException()
    {
        // Arrange
        var (root, semanticModel) = CreateSyntaxAndSemanticModel("class Test {}");

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractBranchModels(root, semanticModel, "   ", CancellationToken.None))
            .Throws<ArgumentException>();
    }

    // =============================================================================
    // G. ExtractForkModels Guard Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractForkModels throws for null typeDeclaration.
    /// </summary>
    [Test]
    public async Task ExtractForkModels_NullTypeDeclaration_ThrowsArgumentNullException()
    {
        // Arrange
        var semanticModel = CreateSemanticModel("class Test {}");

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractForkModels(null!, semanticModel, "TestWorkflow", CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ExtractForkModels throws for null semanticModel.
    /// </summary>
    [Test]
    public async Task ExtractForkModels_NullSemanticModel_ThrowsArgumentNullException()
    {
        // Arrange
        var syntaxTree = CSharpSyntaxTree.ParseText("class Test {}");
        var root = syntaxTree.GetRoot();

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractForkModels(root, null!, "TestWorkflow", CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ExtractForkModels throws for null workflowName.
    /// </summary>
    [Test]
    public async Task ExtractForkModels_NullWorkflowName_ThrowsArgumentException()
    {
        // Arrange
        var (root, semanticModel) = CreateSyntaxAndSemanticModel("class Test {}");

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractForkModels(root, semanticModel, null!, CancellationToken.None))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that ExtractForkModels throws for whitespace workflowName.
    /// </summary>
    [Test]
    public async Task ExtractForkModels_WhitespaceWorkflowName_ThrowsArgumentException()
    {
        // Arrange
        var (root, semanticModel) = CreateSyntaxAndSemanticModel("class Test {}");

        // Act & Assert
        await Assert.That(() => FluentDslParser.ExtractForkModels(root, semanticModel, "   ", CancellationToken.None))
            .Throws<ArgumentException>();
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static SemanticModel CreateSemanticModel(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetSemanticModel(syntaxTree);
    }

    private static (SyntaxNode Root, SemanticModel SemanticModel) CreateSyntaxAndSemanticModel(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return (syntaxTree.GetRoot(), compilation.GetSemanticModel(syntaxTree));
    }
}
