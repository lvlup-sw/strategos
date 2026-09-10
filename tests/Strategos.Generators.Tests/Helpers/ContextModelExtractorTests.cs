// -----------------------------------------------------------------------
// <copyright file="ContextModelExtractorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for the <see cref="ContextModelExtractor"/> class.
/// </summary>
[Property("Category", "Unit")]
public class ContextModelExtractorTests
{
    // =============================================================================
    // A. Basic Extraction Tests (Task B1)
    // =============================================================================

    /// <summary>
    /// Verifies that Extract returns null when the step has no context configuration.
    /// </summary>
    [Test]
    public async Task Extract_StepWithNoContext_ReturnsNull()
    {
        // Arrange
        var context = CreateParseContext(@"
            public class TestWorkflow : WorkflowDefinition<TestState>
            {
                public override IWorkflowBuilder<TestState> Configure(IWorkflowBuilder<TestState> builder)
                    => builder
                        .StartWith<ValidateStep>()
                        .Finally<CompleteStep>();
            }
            public record TestState;
            public class ValidateStep { }
            public class CompleteStep { }
        ");

        // Act
        var stepContexts = ContextModelExtractor.Extract(context);

        // Assert
        await Assert.That(stepContexts).HasCount(0);
    }

    /// <summary>
    /// Verifies that Extract returns a LiteralSource when the step has literal context.
    /// </summary>
    [Test]
    public async Task Extract_StepWithLiteralContext_ReturnsLiteralSource()
    {
        // Arrange
        var context = CreateParseContext(@"
            public class TestWorkflow : WorkflowDefinition<TestState>
            {
                public override IWorkflowBuilder<TestState> Configure(IWorkflowBuilder<TestState> builder)
                    => builder
                        .StartWith<ValidateStep>()
                        .WithContext(c => c.FromLiteral(""You are a helpful assistant.""))
                        .Finally<CompleteStep>();
            }
            public record TestState;
            public class ValidateStep { }
            public class CompleteStep { }
        ");

        // Act
        var stepContexts = ContextModelExtractor.Extract(context);

        // Assert
        await Assert.That(stepContexts).HasCount(1);
        var (stepName, contextModel) = stepContexts.First();
        await Assert.That(stepName).IsEqualTo("ValidateStep");
        await Assert.That(contextModel.Sources).HasCount(1);
        await Assert.That(contextModel.Sources[0]).IsTypeOf<LiteralContextSourceModel>();
        var literal = (LiteralContextSourceModel)contextModel.Sources[0];
        await Assert.That(literal.Value).IsEqualTo("You are a helpful assistant.");
    }

    // =============================================================================
    // B. State Context Extraction Tests (Task B2)
    // =============================================================================

    /// <summary>
    /// Verifies that Extract returns a StateSource when the step has state context.
    /// </summary>
    [Test]
    public async Task Extract_StepWithStateContext_ReturnsStateSource()
    {
        // Arrange
        var context = CreateParseContext(@"
            public class TestWorkflow : WorkflowDefinition<TestState>
            {
                public override IWorkflowBuilder<TestState> Configure(IWorkflowBuilder<TestState> builder)
                    => builder
                        .StartWith<ValidateStep>()
                        .WithContext(c => c.FromState(s => s.CustomerName))
                        .Finally<CompleteStep>();
            }
            public record TestState { public string CustomerName { get; init; } }
            public class ValidateStep { }
            public class CompleteStep { }
        ");

        // Act
        var stepContexts = ContextModelExtractor.Extract(context);

        // Assert
        await Assert.That(stepContexts).HasCount(1);
        var (stepName, contextModel) = stepContexts.First();
        await Assert.That(contextModel.Sources).HasCount(1);
        await Assert.That(contextModel.Sources[0]).IsTypeOf<StateContextSourceModel>();
        var stateSource = (StateContextSourceModel)contextModel.Sources[0];
        await Assert.That(stateSource.PropertyPath).IsEqualTo("CustomerName");
    }

    /// <summary>
    /// Verifies that Extract handles nested property paths.
    /// </summary>
    [Test]
    public async Task Extract_StateContextWithNestedProperty_ExtractsFullPath()
    {
        // Arrange
        var context = CreateParseContext(@"
            public class TestWorkflow : WorkflowDefinition<TestState>
            {
                public override IWorkflowBuilder<TestState> Configure(IWorkflowBuilder<TestState> builder)
                    => builder
                        .StartWith<ValidateStep>()
                        .WithContext(c => c.FromState(s => s.Order.Summary))
                        .Finally<CompleteStep>();
            }
            public record Order { public string Summary { get; init; } }
            public record TestState { public Order Order { get; init; } }
            public class ValidateStep { }
            public class CompleteStep { }
        ");

        // Act
        var stepContexts = ContextModelExtractor.Extract(context);

        // Assert
        await Assert.That(stepContexts).HasCount(1);
        var (_, contextModel) = stepContexts.First();
        var stateSource = (StateContextSourceModel)contextModel.Sources[0];
        await Assert.That(stateSource.PropertyPath).IsEqualTo("Order.Summary");
    }

    // =============================================================================
    // C. Retrieval Context Extraction Tests (Task B3)
    // =============================================================================

    /// <summary>
    /// Verifies that Extract returns a RetrievalSource when the step has retrieval context.
    /// </summary>
    [Test]
    public async Task Extract_StepWithRetrievalContext_ReturnsRetrievalSource()
    {
        // Arrange
        var context = CreateParseContext(@"
            public class TestWorkflow : WorkflowDefinition<TestState>
            {
                public override IWorkflowBuilder<TestState> Configure(IWorkflowBuilder<TestState> builder)
                    => builder
                        .StartWith<ValidateStep>()
                        .WithContext(c => c.FromRetrieval<ProductCatalog>(r => r.Query(""product info"").TopK(5)))
                        .Finally<CompleteStep>();
            }
            public record TestState;
            public class ValidateStep { }
            public class CompleteStep { }
            public class ProductCatalog { }
        ");

        // Act
        var stepContexts = ContextModelExtractor.Extract(context);

        // Assert
        await Assert.That(stepContexts).HasCount(1);
        var (_, contextModel) = stepContexts.First();
        await Assert.That(contextModel.Sources).HasCount(1);
        await Assert.That(contextModel.Sources[0]).IsTypeOf<RetrievalContextSourceModel>();
        var retrieval = (RetrievalContextSourceModel)contextModel.Sources[0];
        await Assert.That(retrieval.CollectionTypeName).IsEqualTo("ProductCatalog");
        await Assert.That(retrieval.TopK).IsEqualTo(5);
    }

    /// <summary>
    /// Verifies that Extract extracts all filters from retrieval context.
    /// </summary>
    [Test]
    public async Task Extract_RetrievalWithFilters_ExtractsAllFilters()
    {
        // Arrange
        var context = CreateParseContext(@"
            public class TestWorkflow : WorkflowDefinition<TestState>
            {
                public override IWorkflowBuilder<TestState> Configure(IWorkflowBuilder<TestState> builder)
                    => builder
                        .StartWith<ValidateStep>()
                        .WithContext(c => c.FromRetrieval<ProductCatalog>(r => r
                            .Query(""products"")
                            .Filter(""category"", ""electronics"")))
                        .Finally<CompleteStep>();
            }
            public record TestState;
            public class ValidateStep { }
            public class CompleteStep { }
            public class ProductCatalog { }
        ");

        // Act
        var stepContexts = ContextModelExtractor.Extract(context);

        // Assert
        await Assert.That(stepContexts).HasCount(1);
        var (_, contextModel) = stepContexts.First();
        var retrieval = (RetrievalContextSourceModel)contextModel.Sources[0];
        await Assert.That(retrieval.Filters).HasCount(1);
        await Assert.That(retrieval.Filters[0].Key).IsEqualTo("category");
        await Assert.That(retrieval.Filters[0].IsStatic).IsTrue();
    }

    /// <summary>
    /// Verifies that Extract extracts dynamic query expressions.
    /// </summary>
    [Test]
    public async Task Extract_RetrievalWithDynamicQuery_ExtractsQueryExpression()
    {
        // Arrange
        var context = CreateParseContext(@"
            public class TestWorkflow : WorkflowDefinition<TestState>
            {
                public override IWorkflowBuilder<TestState> Configure(IWorkflowBuilder<TestState> builder)
                    => builder
                        .StartWith<ValidateStep>()
                        .WithContext(c => c.FromRetrieval<ProductCatalog>(r => r.Query(s => s.SearchQuery)))
                        .Finally<CompleteStep>();
            }
            public record TestState { public string SearchQuery { get; init; } }
            public class ValidateStep { }
            public class CompleteStep { }
            public class ProductCatalog { }
        ");

        // Act
        var stepContexts = ContextModelExtractor.Extract(context);

        // Assert
        await Assert.That(stepContexts).HasCount(1);
        var (_, contextModel) = stepContexts.First();
        var retrieval = (RetrievalContextSourceModel)contextModel.Sources[0];
        await Assert.That(retrieval.QueryExpression).IsNotNull();
        await Assert.That(retrieval.LiteralQuery).IsNull();
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static FluentDslParseContext CreateParseContext(string source)
    {
        // Note: This is a simplified helper that creates a parse context for testing.
        // A full implementation would use the actual Roslyn compilation setup.
        return Fixtures.ParserTestHelper.CreateParseContext(source);
    }
}
