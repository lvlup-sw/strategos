// -----------------------------------------------------------------------
// <copyright file="StepExtractorContextTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;
using Strategos.Generators.Tests.Fixtures;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// TDD Cycle 1: Tests for StepContext tracking in StepExtractor.
/// These tests verify that steps are properly marked with their context
/// (Linear, ForkPath, BranchPath) to enable context-aware duplicate detection.
/// </summary>
[Property("Category", "Unit")]
public class StepExtractorContextTests
{
    // =============================================================================
    // A. Linear Flow Context Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractRawStepInfos returns all steps with Linear context
    /// for a simple linear workflow.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_LinearFlow_AllStepsHaveLinearContext()
    {
        // Arrange
        var context = CreateContext(SourceTexts.LinearWorkflow);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert
        await Assert.That(rawSteps.Count).IsEqualTo(3);
        await Assert.That(rawSteps.All(s => s.Context == StepContext.Linear)).IsTrue();
    }

    // =============================================================================
    // B. Fork Path Context Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractRawStepInfos marks fork path steps with ForkPath context.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_ForkPaths_StepsHaveForkPathContext()
    {
        // Arrange - WorkflowWithFork has fork paths with ProcessPayment and ReserveInventory
        var context = CreateContext(SourceTexts.WorkflowWithFork);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert - fork path steps should have ForkPath context
        var forkSteps = rawSteps.Where(s => s.StepName is "ProcessPayment" or "ReserveInventory").ToList();
        await Assert.That(forkSteps.Count).IsEqualTo(2);
        await Assert.That(forkSteps.All(s => s.Context == StepContext.ForkPath)).IsTrue();
    }

    /// <summary>
    /// Verifies that non-fork steps in a fork workflow still have Linear context.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_ForkWorkflow_NonForkStepsHaveLinearContext()
    {
        // Arrange
        var context = CreateContext(SourceTexts.WorkflowWithFork);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert - ValidateOrder and SendConfirmation should be Linear
        var linearSteps = rawSteps.Where(s => s.StepName is "ValidateOrder" or "SendConfirmation").ToList();
        await Assert.That(linearSteps.Count).IsEqualTo(2);
        await Assert.That(linearSteps.All(s => s.Context == StepContext.Linear)).IsTrue();
    }

    // =============================================================================
    // C. Branch Path Context Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractRawStepInfos marks branch path steps with BranchPath context.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_BranchPaths_StepsHaveBranchPathContext()
    {
        // Arrange - WorkflowWithEnumBranch has branch paths with ProcessAutoClaim, ProcessHomeClaim, ProcessLifeClaim
        var context = CreateContext(SourceTexts.WorkflowWithEnumBranch);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert - branch path steps should have BranchPath context
        var branchSteps = rawSteps.Where(s =>
            s.StepName is "ProcessAutoClaim" or "ProcessHomeClaim" or "ProcessLifeClaim").ToList();

        await Assert.That(branchSteps.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(branchSteps.All(s => s.Context == StepContext.BranchPath)).IsTrue();
    }

    // =============================================================================
    // D. No Deduplication Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractRawStepInfos does NOT deduplicate steps.
    /// This is the key difference from ExtractStepInfos - duplicates are preserved
    /// so that duplicate detection can work correctly.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_DuplicateStepsInForkPaths_PreservesDuplicates()
    {
        // Arrange - Create a workflow with duplicate steps in fork paths
        const string duplicateForkWorkflow = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record AnalysisState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class PrepareStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class AnalyzeStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class SynthesizeStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            [Workflow("duplicate-fork")]
            public static partial class DuplicateForkWorkflow
            {
                public static WorkflowDefinition<AnalysisState> Definition => Workflow<AnalysisState>
                    .Create("duplicate-fork")
                    .StartWith<PrepareStep>()
                    .Fork(
                        path => path.Then<AnalyzeStep>(),
                        path => path.Then<AnalyzeStep>())
                    .Join<SynthesizeStep>()
                    .Finally<CompleteStep>();
            }
            """;

        var context = CreateContext(duplicateForkWorkflow);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert - should have TWO AnalyzeStep entries (no deduplication)
        var analyzeStepCount = rawSteps.Count(s => s.StepName == "AnalyzeStep");
        await Assert.That(analyzeStepCount).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that the existing ExtractStepInfos still deduplicates (unchanged behavior).
    /// </summary>
    [Test]
    public async Task ExtractStepInfos_DuplicateStepsInForkPaths_Deduplicates()
    {
        // Arrange - Same workflow as above
        const string duplicateForkWorkflow = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record AnalysisState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class PrepareStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class AnalyzeStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class SynthesizeStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            [Workflow("duplicate-fork")]
            public static partial class DuplicateForkWorkflow
            {
                public static WorkflowDefinition<AnalysisState> Definition => Workflow<AnalysisState>
                    .Create("duplicate-fork")
                    .StartWith<PrepareStep>()
                    .Fork(
                        path => path.Then<AnalyzeStep>(),
                        path => path.Then<AnalyzeStep>())
                    .Join<SynthesizeStep>()
                    .Finally<CompleteStep>();
            }
            """;

        var context = CreateContext(duplicateForkWorkflow);

        // Act
        var dedupedSteps = StepExtractor.ExtractStepInfos(context);

        // Assert - should have only ONE AnalyzeStep (deduplication preserved)
        var analyzeStepCount = dedupedSteps.Count(s => s.StepName == "AnalyzeStep");
        await Assert.That(analyzeStepCount).IsEqualTo(1);
    }

    // =============================================================================
    // E. Step-List Ordering Oracles
    // =============================================================================
    //
    // A workflow's steps have two in-generator representations, and nothing anywhere
    // asserts they agree as an ORDERED sequence:
    //
    //   * the phase-name list the saga emitter walks to pick each step's successor
    //     (ExtractStepInfos -> PhaseName, which is what FluentDslParser.ExtractStepNames
    //     returns and what the workflow model carries as StepNames), and
    //   * the step-model list every other consumer reads (ExtractStepModels, which the
    //     workflow model carries as Steps).
    //
    // Both are produced by walking the same fluent chain backwards. The step-model walker
    // splices each construct's path steps in as a block, so its output is document-ordered.
    // The phase-name walker instead delegates fork and branch paths to helpers that append
    // to the tail of a caller-owned list, so a top-level fork's or branch's path steps land
    // AFTER the terminal step. The forward (in-loop) walkers append in source order, so a
    // fork nested inside a repeat-until loop already agrees.
    //
    // These fixtures use only shapes with no failure handlers, approval points or
    // confidence handlers, so no post-parse append block contributes and the parse-tier
    // lists are exactly the two model lists.

    /// <summary>
    /// A workflow whose repeat-until loop body contains a fork and its join, exercising the
    /// FORWARD path-collection walkers rather than the backward top-level one.
    /// </summary>
    private const string ForkInsideRepeatUntilWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record FulfilmentState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public decimal AllocationScore { get; init; }
        }

        public class ValidateOrder : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class AllocateStock : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class ChargePayment : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class ReserveInventory : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class ConfirmAllocation : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class ShipOrder : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        [Workflow("fulfil-order")]
        public static partial class FulfilOrderWorkflow
        {
            public static WorkflowDefinition<FulfilmentState> Definition => Workflow<FulfilmentState>
                .Create("fulfil-order")
                .StartWith<ValidateOrder>()
                .RepeatUntil(
                    state => state.AllocationScore >= 0.9m,
                    "Fulfilment",
                    loop => loop
                        .Then<AllocateStock>()
                        .Fork(
                            path => path.Then<ChargePayment>(),
                            path => path.Then<ReserveInventory>())
                        .Join<ConfirmAllocation>(),
                    maxIterations: 5)
                .Finally<ShipOrder>();
        }
        """;

    /// <summary>
    /// For a top-level <c>Fork</c>, the phase-name list must equal the step-model list as an
    /// ORDERED sequence — both in document order, terminal last.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Skip("Red until the splice direction moves to the caller. A top-level fork's path steps "
        + "are appended after the terminal, so the phase-name list is observed as "
        + "[ValidateOrder, SynthesizeResults, SendConfirmation, ProcessPayment, ReserveInventory] "
        + "against a document order of "
        + "[ValidateOrder, ProcessPayment, ReserveInventory, SynthesizeResults, SendConfirmation]. "
        + "Do NOT satisfy this by rewriting the expectation to match today's output.")]
    public async Task StepNames_ForkWorkflow_MatchesDocumentOrder()
    {
        var context = CreateContext(SourceTexts.WorkflowWithFork);

        var phaseNames = PhaseNameList(context);
        var stepModelNames = StepModelPhaseNameList(context);

        await Assert.That(Describe(phaseNames))
            .IsEqualTo(Describe([
                "ValidateOrder", "ProcessPayment", "ReserveInventory", "SynthesizeResults", "SendConfirmation",
            ]))
            .Because("a top-level fork's path steps sit between the pre-fork step and the join in the source");

        await Assert.That(Describe(phaseNames))
            .IsEqualTo(Describe(stepModelNames))
            .Because(
                "the phase-name list the saga emitter walks must agree with the step-model list "
                + "as an ORDERED sequence, not merely as a set");
    }

    /// <summary>
    /// For a top-level <c>Branch</c>, the phase-name list must equal the step-model list as
    /// an ORDERED sequence — both in document order, terminal last.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Skip("Red until the splice direction moves to the caller. A top-level branch's case steps "
        + "are appended after the terminal, so the phase-name list is observed as "
        + "[ValidateClaim, CompleteClaim, ProcessAutoClaim, ProcessHomeClaim, ProcessLifeClaim] "
        + "against a document order of "
        + "[ValidateClaim, ProcessAutoClaim, ProcessHomeClaim, ProcessLifeClaim, CompleteClaim]. "
        + "Do NOT satisfy this by rewriting the expectation to match today's output.")]
    public async Task StepNames_BranchWorkflow_MatchesDocumentOrder()
    {
        var context = CreateContext(SourceTexts.WorkflowWithEnumBranch);

        var phaseNames = PhaseNameList(context);
        var stepModelNames = StepModelPhaseNameList(context);

        await Assert.That(Describe(phaseNames))
            .IsEqualTo(Describe([
                "ValidateClaim", "ProcessAutoClaim", "ProcessHomeClaim", "ProcessLifeClaim", "CompleteClaim",
            ]))
            .Because("a top-level branch's case steps sit between the discriminator step and the terminal in the source");

        await Assert.That(Describe(phaseNames))
            .IsEqualTo(Describe(stepModelNames))
            .Because(
                "the phase-name list the saga emitter walks must agree with the step-model list "
                + "as an ORDERED sequence, not merely as a set");
    }

    /// <summary>
    /// For a <c>Fork</c> nested inside a <c>RepeatUntil</c> the two representations ALREADY
    /// agree, because the in-loop walkers append path steps in source order.
    /// </summary>
    /// <remarks>
    /// This case is the stay-green discriminator for the splice-direction repair. Moving the
    /// splice decision to each caller — backward callers splicing at the front, forward
    /// callers appending — leaves this green. Flipping the shared path helpers to insert at
    /// the front in place would make the top-level cases pass while turning this one RED,
    /// because the in-loop caller would then get its path steps reversed into the wrong
    /// position. It is expected to pass both before and after that work; a run in which it
    /// changes state is a signal the repair was made in the wrong place.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StepNames_ForkInsideRepeatUntil_MatchesDocumentOrder()
    {
        var context = CreateContext(ForkInsideRepeatUntilWorkflow);

        var phaseNames = PhaseNameList(context);
        var stepModelNames = StepModelPhaseNameList(context);

        await Assert.That(Describe(phaseNames))
            .IsEqualTo(Describe([
                "ValidateOrder",
                "Fulfilment_AllocateStock",
                "Fulfilment_ChargePayment",
                "Fulfilment_ReserveInventory",
                "Fulfilment_ConfirmAllocation",
                "ShipOrder",
            ]))
            .Because("the in-loop walkers append path steps in source order, so this shape is already document-ordered");

        await Assert.That(Describe(phaseNames))
            .IsEqualTo(Describe(stepModelNames))
            .Because(
                "the two representations must agree as an ORDERED sequence for a fork nested in a loop");
    }

    // =============================================================================
    // Private Helpers
    // =============================================================================

    /// <summary>
    /// The ordered phase-name list the saga emitter walks — what the workflow model carries
    /// as its step names, taken at the parse tier.
    /// </summary>
    /// <param name="context">The parse context for the workflow under test.</param>
    /// <returns>The ordered phase names.</returns>
    private static IReadOnlyList<string> PhaseNameList(FluentDslParseContext context) =>
        [.. StepExtractor.ExtractStepInfos(context).Select(s => s.PhaseName)];

    /// <summary>
    /// The ordered phase-name list of the step models — what the workflow model carries as
    /// its steps, taken at the parse tier.
    /// </summary>
    /// <param name="context">The parse context for the workflow under test.</param>
    /// <returns>The ordered phase names of the step models.</returns>
    private static IReadOnlyList<string> StepModelPhaseNameList(FluentDslParseContext context) =>
        [.. StepExtractor.ExtractStepModels(context).Select(s => s.PhaseName)];

    /// <summary>
    /// Renders a step list for an assertion message.
    /// </summary>
    /// <param name="steps">The step names to render.</param>
    /// <returns>A bracketed, comma-separated rendering.</returns>
    private static string Describe(IEnumerable<string> steps) => "[" + string.Join(", ", steps) + "]";

    private static FluentDslParseContext CreateContext(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = syntaxTree.GetRoot();

        return FluentDslParseContext.Create(root, semanticModel, null, CancellationToken.None);
    }

    private static List<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>();

        // Add core runtime references
        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var coreAssemblies = new[]
        {
            "System.Runtime.dll",
            "System.Private.CoreLib.dll",
            "netstandard.dll",
        };

        foreach (var assembly in coreAssemblies)
        {
            var path = Path.Combine(runtimePath, assembly);
            if (File.Exists(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        // Add loaded assemblies (filtering out dynamic ones)
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
                catch
                {
                    // Ignore assemblies that can't be loaded as references
                }
            }
        }

        // Add the Workflow library reference
        var workflowAssembly = typeof(Strategos.Abstractions.IWorkflowState).Assembly;
        if (!string.IsNullOrEmpty(workflowAssembly.Location))
        {
            references.Add(MetadataReference.CreateFromFile(workflowAssembly.Location));
        }

        return references;
    }
}
