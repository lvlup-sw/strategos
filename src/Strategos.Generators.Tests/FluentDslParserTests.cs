// -----------------------------------------------------------------------
// <copyright file="FluentDslParserTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Unit tests for the Fluent DSL parser that extracts step names from workflow definitions.
/// </summary>
[Property("Category", "Unit")]
public class FluentDslParserTests
{
    // =============================================================================
    // A. Step Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that step names from a linear workflow are extracted and included in the generated enum.
    /// </summary>
    [Test]
    public async Task Parse_LinearWorkflow_ExtractsStepNames()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderPhase.g.cs");

        // Assert - All step types should appear as enum values
        await Assert.That(generatedSource).Contains("ValidateOrder");
        await Assert.That(generatedSource).Contains("ProcessPayment");
        await Assert.That(generatedSource).Contains("SendConfirmation");
    }

    /// <summary>
    /// Verifies that step order is preserved from the DSL definition.
    /// </summary>
    [Test]
    public async Task Parse_LinearWorkflow_PreservesStepOrder()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderPhase.g.cs");

        // Assert - Steps should appear in order: NotStarted, ValidateOrder, ProcessPayment, SendConfirmation, Completed, Failed
        var notStartedIndex = generatedSource.IndexOf("NotStarted", StringComparison.Ordinal);
        var validateOrderIndex = generatedSource.IndexOf("ValidateOrder", StringComparison.Ordinal);
        var processPaymentIndex = generatedSource.IndexOf("ProcessPayment", StringComparison.Ordinal);
        var sendConfirmationIndex = generatedSource.IndexOf("SendConfirmation", StringComparison.Ordinal);
        var completedIndex = generatedSource.IndexOf("Completed", StringComparison.Ordinal);

        await Assert.That(notStartedIndex).IsLessThan(validateOrderIndex);
        await Assert.That(validateOrderIndex).IsLessThan(processPaymentIndex);
        await Assert.That(processPaymentIndex).IsLessThan(sendConfirmationIndex);
        await Assert.That(sendConfirmationIndex).IsLessThan(completedIndex);
    }

    /// <summary>
    /// Verifies that the generated enum values match the DSL step types exactly.
    /// </summary>
    [Test]
    public async Task Parse_LinearWorkflow_HasCorrectEnumValueCount()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderPhase.g.cs");

        // Assert - Should have: NotStarted + 3 steps + Completed + Failed = 6 enum values
        // Count enum value lines (lines that don't start with /// and contain a comma or the last value)
        var enumContentStart = generatedSource.IndexOf("{", generatedSource.IndexOf("public enum", StringComparison.Ordinal), StringComparison.Ordinal);
        var enumContentEnd = generatedSource.IndexOf("}", enumContentStart, StringComparison.Ordinal);
        var enumContent = generatedSource.Substring(enumContentStart, enumContentEnd - enumContentStart);

        // Count actual enum values (excluding comments and empty lines)
        var enumValueLines = enumContent
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("///") && !string.IsNullOrWhiteSpace(l) && l.Trim() != "{")
            .Where(l => l.Contains(",") || l.Contains("Failed"))
            .Count();

        await Assert.That(enumValueLines).IsEqualTo(6); // NotStarted, ValidateOrder, ProcessPayment, SendConfirmation, Completed, Failed
    }

    // =============================================================================
    // B. Workflow Without Steps Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a workflow without steps still generates the standard phases.
    /// </summary>
    [Test]
    public async Task Parse_WorkflowWithoutSteps_HasOnlyStandardPhases()
    {
        // Arrange - Use the simple attribute-only workflow
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderPhase.g.cs");

        // Assert - Should have: NotStarted, Completed, Failed
        await Assert.That(generatedSource).Contains("NotStarted");
        await Assert.That(generatedSource).Contains("Completed");
        await Assert.That(generatedSource).Contains("Failed");

        // Should NOT have any step names
        await Assert.That(generatedSource).DoesNotContain("ValidateOrder");
    }

    // =============================================================================
    // C. Method Recognition Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StartWith method is recognized.
    /// </summary>
    [Test]
    public async Task Parse_RecognizesStartWith()
    {
        // Arrange
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record TestState : IWorkflowState { public Guid WorkflowId { get; init; } }
            public class FirstStep : IWorkflowStep<TestState>
            {
                public Task<StepResult<TestState>> ExecuteAsync(TestState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<TestState>.FromState(state));
            }
            public class LastStep : IWorkflowStep<TestState>
            {
                public Task<StepResult<TestState>> ExecuteAsync(TestState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<TestState>.FromState(state));
            }

            [Workflow("two-step")]
            public static partial class TwoStepWorkflow
            {
                public static WorkflowDefinition<TestState> Definition => Workflow<TestState>
                    .Create("two-step")
                    .StartWith<FirstStep>()
                    .Finally<LastStep>();
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "TwoStepPhase.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("FirstStep");
    }

    /// <summary>
    /// Verifies that Then method is recognized.
    /// </summary>
    [Test]
    public async Task Parse_RecognizesThen()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderPhase.g.cs");

        // Assert - ProcessPayment comes from Then<ProcessPayment>()
        await Assert.That(generatedSource).Contains("ProcessPayment");
    }

    /// <summary>
    /// Verifies that Finally method is recognized.
    /// </summary>
    [Test]
    public async Task Parse_RecognizesFinally()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderPhase.g.cs");

        // Assert - SendConfirmation comes from Finally<SendConfirmation>()
        await Assert.That(generatedSource).Contains("SendConfirmation");
    }

    // =============================================================================
    // D. Loop Phase Detection Tests (Iteration 14)
    // =============================================================================

    /// <summary>
    /// Verifies that RepeatUntil loop body steps are detected in workflow definition.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithLoop_DetectsLoopBodySteps()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementPhase.g.cs");

        // Assert - Loop body steps should appear in enum (with or without prefix)
        await Assert.That(generatedSource).Contains("CritiqueStep");
        await Assert.That(generatedSource).Contains("RefineStep");
    }

    /// <summary>
    /// Verifies that loop body steps extract the parent loop name for prefixing.
    /// </summary>
    [Test]
    public async Task Generator_LoopBodyStep_ExtractsParentLoopName()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementPhase.g.cs");

        // Assert - Loop body steps should be prefixed with loop name
        await Assert.That(generatedSource).Contains("Refinement_");
    }

    /// <summary>
    /// Verifies that multiple loops in a workflow are tracked separately.
    /// </summary>
    [Test]
    public async Task Generator_MultipleLoops_TracksEachLoopSeparately()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithMultipleLoops);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "MultiLoopPhase.g.cs");

        // Assert - Each loop's steps should have their own prefix
        await Assert.That(generatedSource).Contains("Refinement_CritiqueStep");
        await Assert.That(generatedSource).Contains("Validation_CheckStep");
    }

    // =============================================================================
    // E. Prefixed Phase Emission Tests (Iteration 15)
    // =============================================================================

    /// <summary>
    /// Verifies that loop body steps emit prefixed phase names with underscore separator.
    /// </summary>
    [Test]
    public async Task Generator_LoopBodyStep_EmitsPrefixedPhase()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementPhase.g.cs");

        // Assert - Prefixed phase should appear exactly as expected
        await Assert.That(generatedSource).Contains("Refinement_CritiqueStep,");
        await Assert.That(generatedSource).Contains("Refinement_RefineStep,");
    }

    /// <summary>
    /// Verifies the exact enum value format for Refinement_CritiqueStep step.
    /// </summary>
    [Test]
    public async Task Generator_Refinement_Critique_EmitsCorrectEnum()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementPhase.g.cs");

        // Assert - Full pattern match including XML doc comment
        await Assert.That(generatedSource).Contains("/// <summary>Executing Refinement_CritiqueStep step.</summary>");
        await Assert.That(generatedSource).Contains("Refinement_CritiqueStep,");
    }

    /// <summary>
    /// Verifies that workflow with loop has correct total phase count.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithLoop_HasCorrectPhaseCount()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementPhase.g.cs");

        // Assert - Should have: NotStarted + ValidateInput + Refinement_CritiqueStep + Refinement_RefineStep + PublishResult + Completed + Failed = 7 phases
        var enumContentStart = generatedSource.IndexOf("{", generatedSource.IndexOf("public enum", StringComparison.Ordinal), StringComparison.Ordinal);
        var enumContentEnd = generatedSource.IndexOf("}", enumContentStart, StringComparison.Ordinal);
        var enumContent = generatedSource.Substring(enumContentStart, enumContentEnd - enumContentStart);

        var enumValueLines = enumContent
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("///") && !string.IsNullOrWhiteSpace(l) && l.Trim() != "{")
            .Where(l => l.Contains(",") || l.Contains("Failed"))
            .Count();

        await Assert.That(enumValueLines).IsEqualTo(7);
    }

    // =============================================================================
    // F. Nested Loop Tests (Iteration 15 - Extended)
    // =============================================================================

    /// <summary>
    /// Verifies that truly nested loops produce hierarchical phase names.
    /// Outer loop step should be prefixed with "Outer_".
    /// Inner loop step should be prefixed with "Outer_Inner_".
    /// </summary>
    [Test]
    public async Task Generator_NestedLoops_PreservesHierarchy()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithNestedLoops);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "NestedLoopsPhase.g.cs");

        // Assert - Outer loop step has single prefix
        await Assert.That(generatedSource).Contains("Outer_OuterStep,");

        // Assert - Inner loop step has nested prefix (Outer_Inner_)
        await Assert.That(generatedSource).Contains("Outer_Inner_InnerStep,");
    }

    /// <summary>
    /// Verifies nested loop body steps do NOT appear with only inner prefix.
    /// The inner step should have the full "Outer_Inner_" prefix, not just "Inner_".
    /// </summary>
    [Test]
    public async Task Generator_NestedLoop_DoesNotEmitPartialPrefix()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithNestedLoops);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "NestedLoopsPhase.g.cs");

        // Assert - Should NOT have "Inner_InnerStep" without Outer prefix
        await Assert.That(generatedSource).DoesNotContain("    Inner_InnerStep,");
    }

    /// <summary>
    /// Verifies loop body steps do not appear unprefixed.
    /// </summary>
    [Test]
    public async Task Generator_LoopBodyStep_DoesNotEmitUnprefixedVersion()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementPhase.g.cs");

        // Assert - Should NOT contain unprefixed loop body steps
        await Assert.That(generatedSource).DoesNotContain("    CritiqueStep,");
        await Assert.That(generatedSource).DoesNotContain("    RefineStep,");
    }

    // =============================================================================
    // G. Step Type Name Extraction Tests (Milestone 8b - Full Saga Integration)
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractStepModels returns StepModel records with step names.
    /// </summary>
    [Test]
    public async Task Parse_LinearWorkflow_ExtractsStepModels_WithStepNames()
    {
        // Arrange
        var steps = ParserTestHelper.ExtractStepModels(SourceTexts.LinearWorkflow);

        // Assert - Should extract all step names
        var stepNames = steps.Select(s => s.StepName).ToList();
        await Assert.That(stepNames).Contains("ValidateOrder");
        await Assert.That(stepNames).Contains("ProcessPayment");
        await Assert.That(stepNames).Contains("SendConfirmation");
    }

    /// <summary>
    /// Verifies that step type names include the namespace for DI registration.
    /// </summary>
    [Test]
    public async Task Parse_StepTypeName_ExtractsFullyQualifiedName()
    {
        // Arrange
        var steps = ParserTestHelper.ExtractStepModels(SourceTexts.LinearWorkflow);

        // Assert - Should include fully qualified type name with namespace
        var validateOrderStep = steps.FirstOrDefault(s => s.StepName == "ValidateOrder");
        await Assert.That(validateOrderStep).IsNotNull();
        await Assert.That(validateOrderStep!.StepTypeName).IsEqualTo("TestNamespace.ValidateOrder");
    }

    /// <summary>
    /// Verifies that step models preserve loop context information.
    /// </summary>
    [Test]
    public async Task Parse_LoopSteps_ExtractsStepModelsWithLoopContext()
    {
        // Arrange
        var steps = ParserTestHelper.ExtractStepModels(SourceTexts.WorkflowWithLoop);

        // Assert - Loop body steps should have loop name set
        var critiqueStep = steps.FirstOrDefault(s => s.StepName == "CritiqueStep");
        await Assert.That(critiqueStep).IsNotNull();
        await Assert.That(critiqueStep!.LoopName).IsEqualTo("Refinement");
        await Assert.That(critiqueStep.PhaseName).IsEqualTo("Refinement_CritiqueStep");
    }

    // =============================================================================
    // H. Loop Model Extraction Tests (Milestone 8c - Branch/Loop Saga Support)
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractLoopModels extracts loop information from a workflow with a single loop.
    /// </summary>
    [Test]
    public async Task Parse_WorkflowWithLoop_ExtractsLoopModel()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.WorkflowWithLoop, "ProcessClaim");

        // Assert - Should extract one loop
        await Assert.That(loops.Count).IsEqualTo(1);
        await Assert.That(loops[0].LoopName).IsEqualTo("Refinement");
    }

    /// <summary>
    /// Verifies that loop model has correct condition ID.
    /// </summary>
    [Test]
    public async Task Parse_LoopModel_HasCorrectConditionId()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.WorkflowWithLoop, "ProcessClaim");

        // Assert
        await Assert.That(loops[0].ConditionId).IsEqualTo("ProcessClaim-Refinement");
    }

    /// <summary>
    /// Verifies that loop model extracts first body step name with prefix.
    /// </summary>
    [Test]
    public async Task Parse_LoopModel_ExtractsFirstBodyStepName()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.WorkflowWithLoop, "ProcessClaim");

        // Assert - First body step should be prefixed
        await Assert.That(loops[0].FirstBodyStepName).IsEqualTo("Refinement_CritiqueStep");
    }

    /// <summary>
    /// Verifies that loop model extracts last body step name with prefix.
    /// </summary>
    [Test]
    public async Task Parse_LoopModel_ExtractsLastBodyStepName()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.WorkflowWithLoop, "ProcessClaim");

        // Assert - Last body step should be prefixed
        await Assert.That(loops[0].LastBodyStepName).IsEqualTo("Refinement_RefineStep");
    }

    /// <summary>
    /// Verifies that loop model extracts continuation step name.
    /// </summary>
    [Test]
    public async Task Parse_LoopModel_ExtractsContinuationStepName()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.WorkflowWithLoop, "ProcessClaim");

        // Assert - Continuation step is the step after the loop
        await Assert.That(loops[0].ContinuationStepName).IsEqualTo("PublishResult");
    }

    /// <summary>
    /// DR-5 (IR half): the loop body is carried as configured <c>StepModel</c> records, and a
    /// loop-body step's confidence gate (declared via the
    /// <c>Then&lt;TStep&gt;(step =&gt; step.RequireConfidence(...).OnLowConfidence(...))</c>
    /// configure-lambda overload) reaches <c>StepModel.Confidence</c> in the <c>LoopModel</c> IR —
    /// the config is PARSED, not just the step name. <c>FirstBodyStepName</c>/<c>LastBodyStepName</c>
    /// stay computed projections over the body steps, so the emitted contract is unchanged.
    /// </summary>
    [Test]
    public async Task Parse_LoopBodyConfidence_ReachesBodyStepModelConfidence()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.WorkflowWithLoopBodyConfidence, "iterative-refinement");

        // Assert - single loop carrying its body as configured StepModel records (not bare names)
        await Assert.That(loops.Count).IsEqualTo(1);
        var body = loops[0].BodySteps;
        await Assert.That(body.Count).IsEqualTo(2);

        // The gated loop-body step's confidence config reached the IR (the configure lambda was
        // parsed, not dropped): threshold + the OnLowConfidence handler chain are both present.
        var gated = body.Single(s => s.StepName == "CritiqueStep");
        await Assert.That(gated.Confidence).IsNotNull();
        await Assert.That(gated.Confidence!.Threshold).IsEqualTo(0.85);
        await Assert.That(gated.Confidence!.OnLowConfidenceHandlerId).IsEqualTo("ReviewStep");
        await Assert.That(gated.Confidence!.OnLowConfidenceHandlerChain).IsNotNull();
        await Assert.That(gated.Confidence!.OnLowConfidenceHandlerChain!.Steps[0].StepName).IsEqualTo("ReviewStep");

        // The non-gated body step carries no confidence.
        var plain = body.Single(s => s.StepName == "RefineStep");
        await Assert.That(plain.Confidence).IsNull();

        // First/Last body-step-name projections are byte-identical to the pre-DR-5 bare-name shape.
        await Assert.That(loops[0].FirstBodyStepName).IsEqualTo("Refinement_CritiqueStep");
        await Assert.That(loops[0].LastBodyStepName).IsEqualTo("Refinement_RefineStep");
    }

    /// <summary>
    /// Verifies that top-level loop has null parent loop name.
    /// </summary>
    [Test]
    public async Task Parse_TopLevelLoop_HasNullParentLoopName()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.WorkflowWithLoop, "ProcessClaim");

        // Assert
        await Assert.That(loops[0].ParentLoopName).IsNull();
    }

    /// <summary>
    /// Verifies that nested loop has parent loop name set.
    /// </summary>
    [Test]
    public async Task Parse_NestedLoop_HasParentLoopName()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.WorkflowWithNestedLoops, "TestWorkflow");

        // Assert - Should have 2 loops: Outer and Inner
        await Assert.That(loops.Count).IsEqualTo(2);

        var innerLoop = loops.FirstOrDefault(l => l.LoopName == "Inner");
        await Assert.That(innerLoop).IsNotNull();
        await Assert.That(innerLoop!.ParentLoopName).IsEqualTo("Outer");
    }

    /// <summary>
    /// Verifies that nested loop has correct full prefix in step names.
    /// </summary>
    [Test]
    public async Task Parse_NestedLoop_HasCorrectFullPrefix()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.WorkflowWithNestedLoops, "TestWorkflow");

        // Assert
        var innerLoop = loops.FirstOrDefault(l => l.LoopName == "Inner");
        await Assert.That(innerLoop!.FullPrefix).IsEqualTo("Outer_Inner");
        await Assert.That(innerLoop.FirstBodyStepName).IsEqualTo("Outer_Inner_InnerStep");
    }

    /// <summary>
    /// Verifies that ExtractLoopModels returns empty list for workflow without loops.
    /// </summary>
    [Test]
    public async Task Parse_LinearWorkflow_ReturnsEmptyLoopList()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.LinearWorkflow, "ProcessOrder");

        // Assert
        await Assert.That(loops.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that multiple sibling loops are extracted correctly.
    /// </summary>
    [Test]
    public async Task Parse_WorkflowWithMultipleLoops_ExtractsAllLoops()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.WorkflowWithMultipleLoops, "TestWorkflow");

        // Assert - Should have 2 sibling loops
        await Assert.That(loops.Count).IsEqualTo(2);

        var refinementLoop = loops.FirstOrDefault(l => l.LoopName == "Refinement");
        var validationLoop = loops.FirstOrDefault(l => l.LoopName == "Validation");

        await Assert.That(refinementLoop).IsNotNull();
        await Assert.That(validationLoop).IsNotNull();

        // Both should be top-level (no parent)
        await Assert.That(refinementLoop!.ParentLoopName).IsNull();
        await Assert.That(validationLoop!.ParentLoopName).IsNull();
    }

    /// <summary>
    /// Verifies that loop model extracts max iterations from DSL.
    /// </summary>
    [Test]
    public async Task Parse_LoopModel_ExtractsMaxIterations()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.WorkflowWithLoop, "ProcessClaim");

        // Assert - MaxIterations comes from the DSL (default or explicit)
        await Assert.That(loops[0].MaxIterations).IsGreaterThan(0);
    }

    /// <summary>
    /// Verifies that loop model detects when Branch immediately follows RepeatUntil.
    /// </summary>
    [Test]
    public async Task Parse_LoopWithBranchAfter_SetsBranchOnExitId()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.WorkflowWithLoopThenBranch, "loop-then-branch");

        // Assert - Should have exactly one loop
        await Assert.That(loops.Count).IsEqualTo(1);

        // Loop should have branch on exit set
        await Assert.That(loops[0].HasBranchOnExit).IsTrue();
        await Assert.That(loops[0].BranchOnExitId).IsNotNull();

        // Branch ID should contain the workflow name and property path
        await Assert.That(loops[0].BranchOnExitId).Contains("loop-then-branch");
    }

    /// <summary>
    /// Verifies that loop model without following Branch has null BranchOnExitId.
    /// </summary>
    [Test]
    public async Task Parse_LoopWithoutBranch_HasNullBranchOnExitId()
    {
        // Arrange
        var loops = ParserTestHelper.ExtractLoopModels(SourceTexts.WorkflowWithLoop, "ProcessClaim");

        // Assert
        await Assert.That(loops[0].HasBranchOnExit).IsFalse();
        await Assert.That(loops[0].BranchOnExitId).IsNull();
    }

    // =============================================================================
    // I. Branch Model Extraction Tests (Milestone 8c - Branch/Loop Saga Support)
    // =============================================================================

    /// <summary>
    /// Verifies that a single branch is extracted from an enum-based branch workflow.
    /// </summary>
    [Test]
    public async Task Parse_EnumBranch_ExtractsSingleBranchModel()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.WorkflowWithEnumBranch, "ProcessClaim");

        // Assert - Should have exactly one branch
        await Assert.That(branches.Count).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that branch model extracts discriminator property path.
    /// </summary>
    [Test]
    public async Task Parse_BranchModel_ExtractsDiscriminatorPropertyPath()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.WorkflowWithEnumBranch, "ProcessClaim");

        // Assert - Property path from state => state.Type
        await Assert.That(branches[0].DiscriminatorPropertyPath).IsEqualTo("Type");
    }

    /// <summary>
    /// Verifies that branch model extracts enum discriminator type name.
    /// </summary>
    [Test]
    public async Task Parse_BranchModel_ExtractsEnumDiscriminatorTypeName()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.WorkflowWithEnumBranch, "ProcessClaim");

        // Assert - ClaimType is the enum type
        await Assert.That(branches[0].DiscriminatorTypeName).IsEqualTo("ClaimType");
        await Assert.That(branches[0].IsEnumDiscriminator).IsTrue();
    }

    /// <summary>
    /// Verifies that branch model extracts all cases.
    /// </summary>
    [Test]
    public async Task Parse_BranchModel_ExtractsAllCases()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.WorkflowWithEnumBranch, "ProcessClaim");

        // Assert - 3 cases: Auto, Home, Otherwise (Life)
        await Assert.That(branches[0].Cases.Count).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that branch case extracts case value literal.
    /// </summary>
    [Test]
    public async Task Parse_BranchCase_ExtractsCaseValueLiteral()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.WorkflowWithEnumBranch, "ProcessClaim");

        // Assert - First case should be Auto
        var autoCase = branches[0].Cases.FirstOrDefault(c => c.CaseValueLiteral == "ClaimType.Auto");
        await Assert.That(autoCase).IsNotNull();
    }

    /// <summary>
    /// Verifies that branch case extracts step names.
    /// </summary>
    [Test]
    public async Task Parse_BranchCase_ExtractsStepNames()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.WorkflowWithEnumBranch, "ProcessClaim");

        // Assert - Auto case should have ProcessAutoClaim step
        var autoCase = branches[0].Cases.FirstOrDefault(c => c.CaseValueLiteral == "ClaimType.Auto");
        await Assert.That(autoCase!.StepNames.Count).IsEqualTo(1);
        await Assert.That(autoCase.StepNames[0]).IsEqualTo("ProcessAutoClaim");
    }

    /// <summary>
    /// Verifies that branch model extracts previous step name.
    /// </summary>
    [Test]
    public async Task Parse_BranchModel_ExtractsPreviousStepName()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.WorkflowWithEnumBranch, "ProcessClaim");

        // Assert - Previous step before branch is ValidateClaim
        await Assert.That(branches[0].PreviousStepName).IsEqualTo("ValidateClaim");
    }

    /// <summary>
    /// Verifies that branch model extracts rejoin step name.
    /// </summary>
    [Test]
    public async Task Parse_BranchModel_ExtractsRejoinStepName()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.WorkflowWithEnumBranch, "ProcessClaim");

        // Assert - Rejoin step after branch is CompleteClaim
        await Assert.That(branches[0].RejoinStepName).IsEqualTo("CompleteClaim");
    }

    /// <summary>
    /// Verifies that string discriminator branch extracts correct type.
    /// </summary>
    [Test]
    public async Task Parse_StringBranch_ExtractsStringDiscriminatorType()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.WorkflowWithStringBranch, "ProcessDocument");

        // Assert
        await Assert.That(branches[0].DiscriminatorTypeName).IsEqualTo("String");
        await Assert.That(branches[0].IsEnumDiscriminator).IsFalse();
    }

    /// <summary>
    /// Verifies that terminal branch case is marked correctly.
    /// </summary>
    [Test]
    public async Task Parse_TerminalBranch_MarksTerminalCaseCorrectly()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.WorkflowWithTerminalBranch, "ValidateOrder");

        // Assert - The false case calls .Complete() making it terminal
        var falseCase = branches[0].Cases.FirstOrDefault(c => c.CaseValueLiteral == "false");
        await Assert.That(falseCase!.IsTerminal).IsTrue();

        // The true case does NOT call .Complete() so it's not terminal
        var trueCase = branches[0].Cases.FirstOrDefault(c => c.CaseValueLiteral == "true");
        await Assert.That(trueCase!.IsTerminal).IsFalse();
    }

    /// <summary>
    /// Verifies that multi-step branch extracts all steps in order.
    /// </summary>
    [Test]
    public async Task Parse_MultiStepBranch_ExtractsAllStepsInOrder()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.WorkflowWithMultiStepBranch, "ProcessTicket");

        // Assert - High priority path has 3 steps
        var highCase = branches[0].Cases.FirstOrDefault(c => c.CaseValueLiteral == "Priority.High");
        await Assert.That(highCase!.StepNames.Count).IsEqualTo(3);
        await Assert.That(highCase.StepNames[0]).IsEqualTo("AssignAgent");
        await Assert.That(highCase.StepNames[1]).IsEqualTo("EscalateToManager");
        await Assert.That(highCase.StepNames[2]).IsEqualTo("NotifyCustomer");
    }

    /// <summary>
    /// Verifies that ExtractBranchModels returns empty list for workflow without branches.
    /// </summary>
    [Test]
    public async Task Parse_LinearWorkflow_ReturnsEmptyBranchList()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.LinearWorkflow, "ProcessOrder");

        // Assert
        await Assert.That(branches.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that branch handler method name is computed correctly.
    /// </summary>
    [Test]
    public async Task Parse_BranchModel_ComputesBranchHandlerMethodName()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.WorkflowWithEnumBranch, "ProcessClaim");

        // Assert - Method name from property path "Type" -> "RouteByType"
        await Assert.That(branches[0].BranchHandlerMethodName).IsEqualTo("RouteByType");
    }

    /// <summary>
    /// Verifies that branch ID is set correctly.
    /// </summary>
    [Test]
    public async Task Parse_BranchModel_SetsBranchId()
    {
        // Arrange
        var branches = ParserTestHelper.ExtractBranchModels(SourceTexts.WorkflowWithEnumBranch, "ProcessClaim");

        // Assert - Branch ID should contain workflow name
        await Assert.That(branches[0].BranchId).Contains("ProcessClaim");
    }

    // =============================================================================
    // J. Validation Guard Extraction Tests (Milestone 9 - Guard Logic Injection)
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractStepModels extracts validation predicate from steps with .ValidateState().
    /// </summary>
    [Test]
    public async Task Parse_StepWithValidation_ExtractsValidationPredicate()
    {
        // Arrange
        var steps = ParserTestHelper.ExtractStepModels(SourceTexts.WorkflowWithValidation);

        // Assert - ProcessPayment step should have validation predicate
        var processPaymentStep = steps.FirstOrDefault(s => s.StepName == "ProcessPayment");
        await Assert.That(processPaymentStep).IsNotNull();
        await Assert.That(processPaymentStep!.HasValidation).IsTrue();
        await Assert.That(processPaymentStep.ValidationPredicate).Contains("Total > 0");
    }

    /// <summary>
    /// Verifies that ExtractStepModels extracts validation error message from steps with .ValidateState().
    /// </summary>
    [Test]
    public async Task Parse_StepWithValidation_ExtractsValidationErrorMessage()
    {
        // Arrange
        var steps = ParserTestHelper.ExtractStepModels(SourceTexts.WorkflowWithValidation);

        // Assert - ProcessPayment step should have validation error message
        var processPaymentStep = steps.FirstOrDefault(s => s.StepName == "ProcessPayment");
        await Assert.That(processPaymentStep).IsNotNull();
        await Assert.That(processPaymentStep!.ValidationErrorMessage).IsEqualTo("Total must be positive");
    }

    /// <summary>
    /// Verifies that steps without validation have null validation properties.
    /// </summary>
    [Test]
    public async Task Parse_StepWithoutValidation_HasNullValidationProperties()
    {
        // Arrange
        var steps = ParserTestHelper.ExtractStepModels(SourceTexts.WorkflowWithValidation);

        // Assert - ValidatePayment step should NOT have validation
        var validatePaymentStep = steps.FirstOrDefault(s => s.StepName == "ValidatePayment");
        await Assert.That(validatePaymentStep).IsNotNull();
        await Assert.That(validatePaymentStep!.HasValidation).IsFalse();
        await Assert.That(validatePaymentStep.ValidationPredicate).IsNull();
        await Assert.That(validatePaymentStep.ValidationErrorMessage).IsNull();
    }

    /// <summary>
    /// Verifies that multiple validation guards are extracted correctly.
    /// </summary>
    [Test]
    public async Task Parse_WorkflowWithMultipleValidations_ExtractsAllValidations()
    {
        // Arrange
        var steps = ParserTestHelper.ExtractStepModels(SourceTexts.WorkflowWithMultipleValidations);

        // Assert - ProcessItems step should have item count validation
        var processItemsStep = steps.FirstOrDefault(s => s.StepName == "ProcessItems");
        await Assert.That(processItemsStep).IsNotNull();
        await Assert.That(processItemsStep!.HasValidation).IsTrue();
        await Assert.That(processItemsStep.ValidationPredicate).Contains("ItemCount > 0");
        await Assert.That(processItemsStep.ValidationErrorMessage).IsEqualTo("Order must have items");

        // Assert - ChargePayment step should have total/verified validation
        var chargePaymentStep = steps.FirstOrDefault(s => s.StepName == "ChargePayment");
        await Assert.That(chargePaymentStep).IsNotNull();
        await Assert.That(chargePaymentStep!.HasValidation).IsTrue();
        await Assert.That(chargePaymentStep.ValidationPredicate).Contains("Total > 0");
        await Assert.That(chargePaymentStep.ValidationPredicate).Contains("IsVerified");
        await Assert.That(chargePaymentStep.ValidationErrorMessage).IsEqualTo("Total must be positive and verified");
    }

    /// <summary>
    /// Verifies that linear workflow without validation guards returns steps with null validation.
    /// </summary>
    [Test]
    public async Task Parse_LinearWorkflow_ReturnsStepsWithoutValidation()
    {
        // Arrange
        var steps = ParserTestHelper.ExtractStepModels(SourceTexts.LinearWorkflow);

        // Assert - No steps should have validation
        await Assert.That(steps.All(s => !s.HasValidation)).IsTrue();
    }
}
