// -----------------------------------------------------------------------
// <copyright file="InvocationChainWalkerTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using TUnit.Assertions.Enums;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="InvocationChainWalker"/>.
/// </summary>
[Property("Category", "Unit")]
public class InvocationChainWalkerTests
{
    // =============================================================================
    // A. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WalkChain throws ArgumentNullException when context is null.
    /// </summary>
    [Test]
    public void WalkChain_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            InvocationChainWalker.WalkChain(null!).ToList());
    }

    // =============================================================================
    // B. Empty/No Finally Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WalkChain returns empty enumerable when no Finally call exists.
    /// </summary>
    [Test]
    public async Task WalkChain_NoFinallyCall_ReturnsEmpty()
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
        var result = InvocationChainWalker.WalkChain(context).ToList();

        // Assert
        await Assert.That(result).IsEmpty();
    }

    // =============================================================================
    // C. Linear Workflow Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WalkChain returns all invocations in a linear workflow.
    /// </summary>
    [Test]
    public async Task WalkChain_LinearWorkflow_ReturnsAllInvocations()
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
        var result = InvocationChainWalker.WalkChain(context).ToList();

        // Assert - Should find StartWith, Then, Finally
        await Assert.That(result.Count).IsGreaterThanOrEqualTo(3);

        // Verify step invocations have IsStepMethod = true
        var stepNodes = result.Where(n => n.IsStepMethod).ToList();
        await Assert.That(stepNodes.Count).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that transparent syntax around a fluent receiver does not truncate the walk.
    /// </summary>
    [Test]
    public async Task WalkChain_ParenthesizedReceiver_ReturnsEarlierInvocations()
    {
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    (builder.StartWith<ValidateOrder>())
                        .Then<ProcessPayment>()
                        .Finally<SendConfirmation>();
                }
            }";
        var context = CreateContext(code);

        var stepNodes = InvocationChainWalker.WalkChain(context)
            .Where(node => node.IsStepMethod)
            .ToArray();

        await Assert.That(stepNodes.Length).IsEqualTo(3);
    }

    // =============================================================================
    // D. Loop Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WalkChain applies correct prefix to steps inside a single loop.
    /// </summary>
    [Test]
    public async Task WalkChain_SingleLoop_AppliesCorrectPrefix()
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
        var result = InvocationChainWalker.WalkChain(context).ToList();

        // Assert - Steps inside loop should have LoopPrefix set
        var loopSteps = result.Where(n => n.LoopPrefix == "Retry").ToList();
        await Assert.That(loopSteps.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// Verifies that WalkChain applies hierarchical prefix to nested loops.
    /// </summary>
    [Test]
    public async Task WalkChain_NestedLoops_AppliesHierarchicalPrefix()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Start>()
                        .RepeatUntil(
                            state => state.OuterDone,
                            ""Outer"",
                            outer => outer
                                .Then<OuterStep>()
                                .RepeatUntil(
                                    state => state.InnerDone,
                                    ""Inner"",
                                    inner => inner.Then<InnerStep>(),
                                    maxIterations: 3),
                            maxIterations: 5)
                        .Finally<Done>();
                }
            }";
        var context = CreateContext(code);

        // Act
        var result = InvocationChainWalker.WalkChain(context).ToList();

        // Assert - Outer loop steps have "Outer" prefix, inner have "Outer_Inner" prefix
        var outerSteps = result.Where(n => n.LoopPrefix == "Outer").ToList();
        var innerSteps = result.Where(n => n.LoopPrefix == "Outer_Inner").ToList();

        await Assert.That(outerSteps.Count).IsGreaterThan(0);
        await Assert.That(innerSteps.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// Verifies that separate statements and a nested fluent receiver both retain declaration
    /// order. These two shapes have opposite Roslyn descendant order.
    /// </summary>
    [Test]
    public async Task CollectInvocationsInLambda_MixedChains_PreservesDeclarationOrder()
    {
        const string code = """
            class Test
            {
                void M()
                {
                    Use(path =>
                    {
                        path.Then<A>().Then<B>();
                        path.Then<C>();
                    });
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var lambda = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.LambdaExpressionSyntax>()
            .Single();

        var invocations = InvocationChainWalker.CollectInvocationsInLambda(lambda)
            .Select(invocation => invocation.Expression.ToString())
            .ToArray();

        await Assert.That(invocations).IsEquivalentTo(new[]
        {
            "path.Then<A>",
            "path.Then<A>().Then<B>",
            "path.Then<C>",
        }, CollectionOrdering.Matching);
    }

    // =============================================================================
    // E. Cancellation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WalkChain respects cancellation token.
    /// </summary>
    [Test]
    public async Task WalkChain_CancellationRequested_ThrowsOperationCanceledException()
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

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var context = CreateContext(code, cts.Token);

        // Act & Assert
        await Assert.That(() => InvocationChainWalker.WalkChain(context).ToList())
            .Throws<OperationCanceledException>();
    }

    // =============================================================================
    // Private Helpers
    // =============================================================================

    private static FluentDslParseContext CreateContext(string source, CancellationToken cancellationToken = default)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var typeDeclaration = syntaxTree.GetRoot();

        return FluentDslParseContext.Create(typeDeclaration, semanticModel, null, cancellationToken);
    }
}
