// -----------------------------------------------------------------------
// <copyright file="ForkExtractorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="ForkExtractor"/>.
/// </summary>
[Property("Category", "Unit")]
public class ForkExtractorTests
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
            ForkExtractor.Extract(null!));
    }

    // =============================================================================
    // B. Fork Extraction Tests - No Fork Cases
    // =============================================================================

    /// <summary>
    /// Verifies that Extract returns empty list when no Fork calls exist.
    /// </summary>
    [Test]
    public async Task Extract_NoForkCalls_ReturnsEmptyList()
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
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result).IsEmpty();
    }

    // =============================================================================
    // C. Fork Extraction Tests - Basic Fork
    // =============================================================================

    /// <summary>
    /// Verifies that Extract extracts fork ID from a Fork call.
    /// </summary>
    [Test]
    public async Task Extract_WithFork_ExtractsForkId()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Fork(
                            path => path.Then<ProcessPayment>(),
                            path => path.Then<ReserveInventory>())
                        .Join<Synthesize>()
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "OrderWorkflow");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].ForkId).IsNotNull();
    }

    /// <summary>
    /// Verifies that Extract extracts correct path count.
    /// </summary>
    [Test]
    public async Task Extract_WithFork_ExtractsPathCount()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Fork(
                            path => path.Then<ProcessPayment>(),
                            path => path.Then<ReserveInventory>(),
                            path => path.Then<NotifyCustomer>())
                        .Join<Synthesize>()
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "OrderWorkflow");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].PathCount).IsEqualTo(3);
    }

    // =============================================================================
    // D. Path Step Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract extracts step names from path.
    /// </summary>
    [Test]
    public async Task Extract_PathWithSteps_ExtractsStepNames()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Fork(
                            path => path.Then<ProcessPayment>().Then<ChargeCard>(),
                            path => path.Then<ReserveInventory>())
                        .Join<Synthesize>()
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "OrderWorkflow");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].Paths[0].StepNames.Count).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that Extract extracts path index correctly.
    /// </summary>
    [Test]
    public async Task Extract_MultiplePaths_ExtractsPathIndices()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Fork(
                            path => path.Then<ProcessPayment>(),
                            path => path.Then<ReserveInventory>())
                        .Join<Synthesize>()
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "OrderWorkflow");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].Paths[0].PathIndex).IsEqualTo(0);
        await Assert.That(result[0].Paths[1].PathIndex).IsEqualTo(1);
    }

    // =============================================================================
    // E. Failure Handler Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract detects failure handler on path.
    /// </summary>
    [Test]
    public async Task Extract_PathWithFailureHandler_SetsHasFailureHandler()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Fork(
                            path => path.Then<ProcessPayment>().OnFailure(f => f.Then<RefundPayment>()),
                            path => path.Then<ReserveInventory>())
                        .Join<Synthesize>()
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "OrderWorkflow");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].Paths[0].HasFailureHandler).IsTrue();
        await Assert.That(result[0].Paths[1].HasFailureHandler).IsFalse();
    }

    /// <summary>
    /// Verifies that Extract detects terminal failure handler with Complete().
    /// </summary>
    [Test]
    public async Task Extract_TerminalFailureHandler_SetsIsTerminalOnFailure()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Fork(
                            path => path.Then<ProcessPayment>().OnFailure(f => f.Then<RefundPayment>().Complete()),
                            path => path.Then<ReserveInventory>())
                        .Join<Synthesize>()
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "OrderWorkflow");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].Paths[0].IsTerminalOnFailure).IsTrue();
        await Assert.That(result[0].Paths[1].IsTerminalOnFailure).IsFalse();
    }

    // =============================================================================
    // F. Join Step Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract extracts join step name.
    /// </summary>
    [Test]
    public async Task Extract_WithJoin_SetsJoinStepName()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Fork(
                            path => path.Then<ProcessPayment>(),
                            path => path.Then<ReserveInventory>())
                        .Join<SynthesizeResults>()
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "OrderWorkflow");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].JoinStepName).IsEqualTo("SynthesizeResults");
    }

    // =============================================================================
    // G. Previous Step Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract extracts previous step name.
    /// </summary>
    [Test]
    public async Task Extract_WithFork_ExtractsPreviousStepName()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Then<Validate>()
                        .Fork(
                            path => path.Then<ProcessPayment>(),
                            path => path.Then<ReserveInventory>())
                        .Join<Synthesize>()
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "OrderWorkflow");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].PreviousStepName).IsEqualTo("Validate");
    }

    // =============================================================================
    // H. Model Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that HasAnyFailureHandler returns true when any path has handler.
    /// </summary>
    [Test]
    public async Task Extract_AnyPathWithHandler_HasAnyFailureHandlerIsTrue()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Fork(
                            path => path.Then<ProcessPayment>().OnFailure(f => f.Then<Refund>()),
                            path => path.Then<ReserveInventory>())
                        .Join<Synthesize>()
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "OrderWorkflow");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].HasAnyFailureHandler).IsTrue();
    }

    // =============================================================================
    // I. Loop Context Tests - Fork Inside RepeatUntil
    // =============================================================================

    /// <summary>
    /// Verifies that Fork inside a loop applies loop prefix to path step names.
    /// </summary>
    [Test]
    public async Task Extract_ForkInsideLoop_AppliesLoopPrefixToPathSteps()
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
                            ""TargetLoop"",
                            loop => loop
                                .Then<SelectTarget>()
                                .Fork(
                                    path => path.Then<AnalyzeNews>(),
                                    path => path.Then<AnalyzeTechnical>())
                                .Join<AggregateVotes>())
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "PortfolioManager");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Paths[0].StepNames[0]).IsEqualTo("TargetLoop_AnalyzeNews");
        await Assert.That(result[0].Paths[1].StepNames[0]).IsEqualTo("TargetLoop_AnalyzeTechnical");
    }

    /// <summary>
    /// Verifies that Fork inside a loop applies loop prefix to join step.
    /// </summary>
    [Test]
    public async Task Extract_ForkInsideLoop_AppliesLoopPrefixToJoinStep()
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
                            ""TargetLoop"",
                            loop => loop
                                .Then<SelectTarget>()
                                .Fork(
                                    path => path.Then<AnalyzeNews>(),
                                    path => path.Then<AnalyzeTechnical>())
                                .Join<AggregateVotes>())
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "PortfolioManager");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].JoinStepName).IsEqualTo("TargetLoop_AggregateVotes");
    }

    /// <summary>
    /// Verifies that Fork inside a loop applies loop prefix to previous step.
    /// </summary>
    [Test]
    public async Task Extract_ForkInsideLoop_AppliesLoopPrefixToPreviousStep()
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
                            ""TargetLoop"",
                            loop => loop
                                .Then<SelectTarget>()
                                .Fork(
                                    path => path.Then<AnalyzeNews>(),
                                    path => path.Then<AnalyzeTechnical>())
                                .Join<AggregateVotes>())
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "PortfolioManager");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].PreviousStepName).IsEqualTo("TargetLoop_SelectTarget");
    }

    /// <summary>
    /// Verifies that Fork outside a loop does not apply any prefix.
    /// </summary>
    [Test]
    public async Task Extract_ForkOutsideLoop_NoLoopPrefix()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Then<SelectTarget>()
                        .Fork(
                            path => path.Then<AnalyzeNews>(),
                            path => path.Then<AnalyzeTechnical>())
                        .Join<AggregateVotes>()
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "PortfolioManager");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].Paths[0].StepNames[0]).IsEqualTo("AnalyzeNews");
        await Assert.That(result[0].Paths[1].StepNames[0]).IsEqualTo("AnalyzeTechnical");
        await Assert.That(result[0].JoinStepName).IsEqualTo("AggregateVotes");
        await Assert.That(result[0].PreviousStepName).IsEqualTo("SelectTarget");
    }

    /// <summary>
    /// Verifies that Fork inside nested loops applies combined prefix.
    /// </summary>
    [Test]
    public async Task Extract_ForkInsideNestedLoops_AppliesCombinedPrefix()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .RepeatUntil(
                            state => state.OuterDone,
                            ""OuterLoop"",
                            outer => outer
                                .Then<PrepareOuter>()
                                .RepeatUntil(
                                    state => state.InnerDone,
                                    ""InnerLoop"",
                                    inner => inner
                                        .Then<SelectTarget>()
                                        .Fork(
                                            path => path.Then<AnalyzeNews>(),
                                            path => path.Then<AnalyzeTechnical>())
                                        .Join<AggregateVotes>()))
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "PortfolioManager");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Paths[0].StepNames[0]).IsEqualTo("OuterLoop_InnerLoop_AnalyzeNews");
        await Assert.That(result[0].Paths[1].StepNames[0]).IsEqualTo("OuterLoop_InnerLoop_AnalyzeTechnical");
        await Assert.That(result[0].JoinStepName).IsEqualTo("OuterLoop_InnerLoop_AggregateVotes");
    }

    /// <summary>
    /// Verifies that Fork applies loop prefix to multi-step paths correctly.
    /// </summary>
    [Test]
    public async Task Extract_ForkInsideLoopWithMultiStepPath_AppliesPrefixToAllSteps()
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
                            ""TargetLoop"",
                            loop => loop
                                .Then<SelectTarget>()
                                .Fork(
                                    path => path.Then<AnalyzeNews>().Then<ValidateNews>(),
                                    path => path.Then<AnalyzeTechnical>())
                                .Join<AggregateVotes>())
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "PortfolioManager");

        // Act
        var result = ForkExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].Paths[0].StepNames.Count).IsEqualTo(2);
        await Assert.That(result[0].Paths[0].StepNames[0]).IsEqualTo("TargetLoop_AnalyzeNews");
        await Assert.That(result[0].Paths[0].StepNames[1]).IsEqualTo("TargetLoop_ValidateNews");
    }

    // =============================================================================
    // J. Instance Name Tests
    // =============================================================================

    /// <summary>
    /// Verifies that fork path steps keep instance names so phase names differ for the
    /// same type on two paths.
    /// </summary>
    [Test]
    public async Task Extract_ForkPathsWithInstanceNames_StoresEffectivePhaseNames()
    {
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Fork(
                            path => path.Then<AnalyzeStep>(""TechnicalAnalysis""),
                            path => path.Then<AnalyzeStep>(""FundamentalAnalysis""))
                        .Join<Synthesize>()
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "OrderWorkflow");

        var result = ForkExtractor.Extract(context);

        await Assert.That(result[0].Paths[0].StepNames[0]).IsEqualTo("TechnicalAnalysis");
        await Assert.That(result[0].Paths[0].LastStepName).IsEqualTo("TechnicalAnalysis");
        await Assert.That(result[0].Paths[1].StepNames[0]).IsEqualTo("FundamentalAnalysis");
        await Assert.That(result[0].Paths[1].LastStepName).IsEqualTo("FundamentalAnalysis");
    }

    /// <summary>
    /// Verifies that a fork following an instance-named step keys PreviousStepName by
    /// effective name, not bare type.
    /// </summary>
    [Test]
    public async Task Extract_ForkAfterInstanceNamedStep_PreviousStepNameIsEffectiveName()
    {
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Then<Validate>(""Intake"")
                        .Fork(
                            path => path.Then<ProcessPayment>(),
                            path => path.Then<ReserveInventory>())
                        .Join<Synthesize>()
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "OrderWorkflow");

        var result = ForkExtractor.Extract(context);

        await Assert.That(result[0].PreviousStepName).IsEqualTo("Intake");
    }

    /// <summary>
    /// Verifies that instance names inside a loop are loop-prefixed in the phase name.
    /// </summary>
    [Test]
    public async Task Extract_ForkInsideLoopWithInstanceNames_PrefixesEffectiveNames()
    {
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .RepeatUntil(
                            state => state.Done,
                            ""TargetLoop"",
                            loop => loop
                                .Then<SelectTarget>()
                                .Fork(
                                    path => path.Then<AnalyzeStep>(""NewsPath""),
                                    path => path.Then<AnalyzeStep>(""TechPath""))
                                .Join<AggregateVotes>())
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "PortfolioManager");

        var result = ForkExtractor.Extract(context);

        await Assert.That(result[0].Paths[0].StepNames[0]).IsEqualTo("TargetLoop_NewsPath");
        await Assert.That(result[0].Paths[1].StepNames[0]).IsEqualTo("TargetLoop_TechPath");
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
