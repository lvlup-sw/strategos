// -----------------------------------------------------------------------
// <copyright file="ContextAssemblerEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Unit tests for the <see cref="ContextAssemblerEmitter"/> class.
/// </summary>
[Property("Category", "Unit")]
public class ContextAssemblerEmitterTests
{
    // =============================================================================
    // A. Basic Generation Tests (Task B4)
    // =============================================================================

    /// <summary>
    /// Verifies that Emit generates an assembler class for a step with context.
    /// </summary>
    [Test]
    public async Task Emit_StepWithContext_GeneratesAssemblerClass()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public sealed partial class ValidateStepContextAssembler");
    }

    /// <summary>
    /// Verifies that the generated assembler implements IContextAssembler.
    /// </summary>
    [Test]
    public async Task Emit_StepWithContext_ImplementsIContextAssembler()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains(": IContextAssembler<TestState>");
    }

    // =============================================================================
    // C. Interface Signature Compliance Tests
    // =============================================================================

    /// <summary>
    /// Verifies that AssembleAsync returns Task of AssembledContext (not Task of string).
    /// </summary>
    [Test]
    public async Task Emit_AssembleAsyncMethod_ReturnsTaskOfAssembledContext()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Task<AssembledContext> AssembleAsync");
        await Assert.That(source).DoesNotContain("Task<string> AssembleAsync");
    }

    /// <summary>
    /// Verifies that AssembleAsync has StepContext parameter between state and cancellationToken.
    /// </summary>
    [Test]
    public async Task Emit_AssembleAsyncMethod_HasStepContextParameter()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("TestState state, StepContext stepContext, CancellationToken cancellationToken");
    }

    /// <summary>
    /// Verifies that generated code uses AssembledContextBuilder instead of StringBuilder.
    /// </summary>
    [Test]
    public async Task Emit_AssembleAsyncMethod_UsesAssembledContextBuilder()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("new AssembledContextBuilder()");
        await Assert.That(source).DoesNotContain("new StringBuilder()");
    }

    /// <summary>
    /// Verifies that generated code returns contextBuilder.Build() not ToString().
    /// </summary>
    [Test]
    public async Task Emit_AssembleAsyncMethod_ReturnsBuildNotToString()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("return contextBuilder.Build()");
        await Assert.That(source).DoesNotContain("return contextBuilder.ToString()");
    }

    /// <summary>
    /// Verifies that required using directives are included.
    /// </summary>
    [Test]
    public async Task Emit_StepWithContext_IncludesRequiredUsings()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("using Strategos.Agents.Models;");
        await Assert.That(source).Contains("using Strategos.Ontology.ObjectSets;");
        await Assert.That(source).Contains("using Strategos.Steps;");
    }

    /// <summary>
    /// Verifies that the generated assembler has the correct namespace.
    /// </summary>
    [Test]
    public async Task Emit_StepWithContext_HasCorrectNamespace()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("namespace TestNamespace;");
    }

    /// <summary>
    /// Verifies that the emitter generates the auto-generated header.
    /// </summary>
    [Test]
    public async Task Emit_StepWithContext_HasAutoGeneratedHeader()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("// <auto-generated/>");
        await Assert.That(source).Contains("#nullable enable");
    }

    /// <summary>
    /// Verifies that null model throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Emit_WithNullModel_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.That(() => ContextAssemblerEmitter.Emit(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Source-Specific Generation Tests (Task B5)
    // =============================================================================

    /// <summary>
    /// Verifies that state context generates AddStateContext call.
    /// </summary>
    [Test]
    public async Task Emit_WithStateContext_GeneratesAddStateContextCall()
    {
        // Arrange
        var stateSource = new StateContextSourceModel("CustomerName", "string", "state.CustomerName");
        var context = new ContextModel([stateSource]);
        var step = StepModel.Create("ProcessStep", "TestNamespace.ProcessStep", context: context);
        var model = CreateWorkflowModel(step);

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("contextBuilder.AddStateContext(\"CustomerName\", state.CustomerName)");
    }

    /// <summary>
    /// Verifies that retrieval context generates ExecuteSimilarityAsync call instead of SearchAsync.
    /// </summary>
    [Test]
    public async Task Emit_WithRetrievalContext_GeneratesExecuteSimilarityAsync()
    {
        // Arrange
        var retrievalSource = new RetrievalContextSourceModel(
            "ProductCatalog",
            QueryExpression: null,
            LiteralQuery: "product info",
            TopK: 5,
            MinRelevance: 0.7m,
            Filters: []);
        var context = new ContextModel([retrievalSource]);
        var step = StepModel.Create("ProcessStep", "TestNamespace.ProcessStep", context: context);
        var model = CreateWorkflowModel(step);

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ExecuteSimilarityAsync");
        await Assert.That(source).DoesNotContain("SearchAsync");
        await Assert.That(source).Contains("contextBuilder.AddRetrievalContext(\"ProductCatalog\"");
    }

    /// <summary>
    /// Verifies that retrieval context uses IObjectSetProvider interface (not IVectorSearchAdapter).
    /// </summary>
    [Test]
    public async Task Emit_WithRetrievalContext_UsesIObjectSetProvider()
    {
        // Arrange
        var retrievalSource = new RetrievalContextSourceModel(
            "ProductCatalog",
            QueryExpression: null,
            LiteralQuery: "product info",
            TopK: 5,
            MinRelevance: 0.7m,
            Filters: []);
        var context = new ContextModel([retrievalSource]);
        var step = StepModel.Create("ProcessStep", "TestNamespace.ProcessStep", context: context);
        var model = CreateWorkflowModel(step);

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert - Verify correct interface is used
        await Assert.That(source).Contains("IObjectSetProvider");
        await Assert.That(source).DoesNotContain("IVectorSearchAdapter");
        await Assert.That(source).DoesNotContain("IVectorCollection");
    }

    /// <summary>
    /// Verifies that literal context generates AddLiteralContext call.
    /// </summary>
    [Test]
    public async Task Emit_WithLiteralContext_GeneratesAddLiteralContextCall()
    {
        // Arrange
        var literalSource = new LiteralContextSourceModel("You are a helpful assistant.");
        var context = new ContextModel([literalSource]);
        var step = StepModel.Create("ProcessStep", "TestNamespace.ProcessStep", context: context);
        var model = CreateWorkflowModel(step);

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("contextBuilder.AddLiteralContext(\"You are a helpful assistant.\")");
    }

    /// <summary>
    /// Verifies that multiple sources generate all in order.
    /// </summary>
    [Test]
    public async Task Emit_WithMultipleSources_GeneratesAllInOrder()
    {
        // Arrange
        var sources = new ContextSourceModel[]
        {
            new LiteralContextSourceModel("System prompt here"),
            new StateContextSourceModel("CustomerName", "string", "state.CustomerName"),
            new RetrievalContextSourceModel(
                "KnowledgeBase",
                QueryExpression: null,
                LiteralQuery: "relevant docs",
                TopK: 3,
                MinRelevance: 0.8m,
                Filters: []),
        };
        var context = new ContextModel(sources);
        var step = StepModel.Create("ProcessStep", "TestNamespace.ProcessStep", context: context);
        var model = CreateWorkflowModel(step);

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("System prompt here");
        await Assert.That(source).Contains("state.CustomerName");
        await Assert.That(source).Contains("KnowledgeBase");
    }

    /// <summary>
    /// Verifies that steps without context are skipped.
    /// </summary>
    [Test]
    public async Task Emit_StepsWithoutContext_AreSkipped()
    {
        // Arrange
        var stepWithContext = StepModel.Create(
            "ProcessStep",
            "TestNamespace.ProcessStep",
            context: new ContextModel([new LiteralContextSourceModel("Hello")]));
        var stepWithoutContext = StepModel.Create(
            "ValidateStep",
            "TestNamespace.ValidateStep");
        var model = new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ProcessStep", "ValidateStep"],
            StateTypeName: "TestState",
            Steps: [stepWithContext, stepWithoutContext]);

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ProcessStepContextAssembler");
        await Assert.That(source).DoesNotContain("ValidateStepContextAssembler");
    }

    // =============================================================================
    // D. IObjectSetProvider Migration Tests (Phase 4)
    // =============================================================================

    /// <summary>
    /// Verifies that emitted code contains SimilarityExpression construction.
    /// </summary>
    [Test]
    public async Task Emit_WithRetrievalContext_EmitsSimilarityExpression()
    {
        // Arrange
        var retrievalSource = new RetrievalContextSourceModel(
            "ProductCatalog",
            QueryExpression: null,
            LiteralQuery: "product info",
            TopK: 5,
            MinRelevance: 0.7m,
            Filters: []);
        var context = new ContextModel([retrievalSource]);
        var step = StepModel.Create("ProcessStep", "TestNamespace.ProcessStep", context: context);
        var model = CreateWorkflowModel(step);

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("new SimilarityExpression(");
        await Assert.That(source).Contains("new RootExpression(typeof(ProductCatalog))");
    }

    /// <summary>
    /// Verifies that emitted code maps ScoredObjectSetResult items to RetrievalResult.
    /// </summary>
    [Test]
    public async Task Emit_WithRetrievalContext_MapsToRetrievalResult()
    {
        // Arrange
        var retrievalSource = new RetrievalContextSourceModel(
            "ProductCatalog",
            QueryExpression: null,
            LiteralQuery: "product info",
            TopK: 5,
            MinRelevance: 0.7m,
            Filters: []);
        var context = new ContextModel([retrievalSource]);
        var step = StepModel.Create("ProcessStep", "TestNamespace.ProcessStep", context: context);
        var model = CreateWorkflowModel(step);

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("new RetrievalResult");
        await Assert.That(source).Contains("Content = item.ToString()");
        await Assert.That(source).Contains(".Scores[i]");
    }

    /// <summary>
    /// Verifies that a single IObjectSetProvider is injected, not one per collection.
    /// </summary>
    [Test]
    public async Task Emit_WithRetrievalContext_SingleObjectSetProviderInjection()
    {
        // Arrange - two retrieval sources
        var retrieval1 = new RetrievalContextSourceModel(
            "ProductCatalog",
            QueryExpression: null,
            LiteralQuery: "product info",
            TopK: 5,
            MinRelevance: 0.7m,
            Filters: []);
        var retrieval2 = new RetrievalContextSourceModel(
            "KnowledgeBase",
            QueryExpression: null,
            LiteralQuery: "knowledge",
            TopK: 3,
            MinRelevance: 0.8m,
            Filters: []);
        var context = new ContextModel([retrieval1, retrieval2]);
        var step = StepModel.Create("ProcessStep", "TestNamespace.ProcessStep", context: context);
        var model = CreateWorkflowModel(step);

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert - Single field, not per-collection
        await Assert.That(source).Contains("private readonly IObjectSetProvider _objectSetProvider;");
        await Assert.That(source).DoesNotContain("_productCatalogCollection");
        await Assert.That(source).DoesNotContain("_knowledgeBaseCollection");
    }

    /// <summary>
    /// Verifies that filters are emitted as a dictionary in the SimilarityExpression.
    /// </summary>
    [Test]
    public async Task Emit_WithRetrievalContext_WithFilters_EmitsFilterDictionary()
    {
        // Arrange
        var retrievalSource = new RetrievalContextSourceModel(
            "ProductCatalog",
            QueryExpression: null,
            LiteralQuery: "product info",
            TopK: 5,
            MinRelevance: 0.7m,
            Filters:
            [
                new RetrievalFilterModel("category", "electronics", null),
                new RetrievalFilterModel("status", null, "s => s.CurrentStatus"),
            ]);
        var context = new ContextModel([retrievalSource]);
        var step = StepModel.Create("ProcessStep", "TestNamespace.ProcessStep", context: context);
        var model = CreateWorkflowModel(step);

        // Act
        var source = ContextAssemblerEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("var productCatalogResultsFilters = new Dictionary<string, object>");
        await Assert.That(source).Contains("\"category\"");
        await Assert.That(source).Contains("\"electronics\"");
        await Assert.That(source).Contains("\"status\"");
        await Assert.That(source).Contains("filters: productCatalogResultsFilters");
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static WorkflowModel CreateTestModel()
    {
        var literalSource = new LiteralContextSourceModel("Test context");
        var context = new ContextModel([literalSource]);
        var step = StepModel.Create("ValidateStep", "TestNamespace.ValidateStep", context: context);

        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ValidateStep"],
            StateTypeName: "TestState",
            Steps: [step]);
    }

    private static WorkflowModel CreateWorkflowModel(StepModel step)
    {
        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: [step.StepName],
            StateTypeName: "TestState",
            Steps: [step]);
    }
}
