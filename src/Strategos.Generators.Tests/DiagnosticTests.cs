// -----------------------------------------------------------------------
// <copyright file="DiagnosticTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Diagnostics;
using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Tests for diagnostic reporting in the workflow generator.
/// </summary>
[Property("Category", "Integration")]
public class DiagnosticTests
{
    // =============================================================================
    // A. Empty Workflow Name Tests
    // =============================================================================

    /// <summary>
    /// Verifies that an error diagnostic is reported for empty workflow name.
    /// </summary>
    [Test]
    public async Task Diagnostic_EmptyWorkflowName_ReportsAGWF001()
    {
        // Arrange
        var source = """
            using Strategos.Attributes;

            namespace TestNamespace;

            [Workflow("")]
            public static partial class EmptyNameWorkflow
            {
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should report AGWF001 error
        var agwf001 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF001");
        await Assert.That(agwf001).IsNotNull();
        await Assert.That(agwf001!.Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Verifies that an error diagnostic is reported for whitespace-only workflow name.
    /// </summary>
    [Test]
    public async Task Diagnostic_WhitespaceWorkflowName_ReportsAGWF001()
    {
        // Arrange
        var source = """
            using Strategos.Attributes;

            namespace TestNamespace;

            [Workflow("   ")]
            public static partial class WhitespaceNameWorkflow
            {
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should report AGWF001 error
        var agwf001 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF001");
        await Assert.That(agwf001).IsNotNull();
    }

    // =============================================================================
    // B. No Steps Found Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a warning diagnostic is reported when no steps are found.
    /// </summary>
    [Test]
    public async Task Diagnostic_NoStepsFound_ReportsAGWF002Warning()
    {
        // Arrange - Workflow with attribute but no DSL definition
        var source = """
            using Strategos.Attributes;

            namespace TestNamespace;

            [Workflow("no-steps")]
            public static partial class NoStepsWorkflow
            {
                // No workflow definition
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should report AGWF002 warning
        var agwf002 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF002");
        await Assert.That(agwf002).IsNotNull();
        await Assert.That(agwf002!.Severity).IsEqualTo(DiagnosticSeverity.Warning);
    }

    // =============================================================================
    // C. Diagnostic Location Tests
    // =============================================================================

    /// <summary>
    /// Verifies that diagnostics have correct source location.
    /// </summary>
    [Test]
    public async Task Diagnostic_HasCorrectSourceLocation()
    {
        // Arrange
        var source = """
            using Strategos.Attributes;

            namespace TestNamespace;

            [Workflow("")]
            public static partial class EmptyNameWorkflow
            {
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Diagnostic should point to the attribute location
        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF001");
        await Assert.That(diagnostic).IsNotNull();
        await Assert.That(diagnostic!.Location.IsInSource).IsTrue();
    }

    // =============================================================================
    // D. Valid Workflow Tests
    // =============================================================================

    /// <summary>
    /// Verifies that no error diagnostics are reported for valid workflows.
    /// </summary>
    [Test]
    public async Task Diagnostic_ValidWorkflow_NoErrors()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);

        // Assert - Should have no error diagnostics from the generator
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Where(d => d.Id.StartsWith("AGWF", StringComparison.Ordinal))
            .ToList();

        await Assert.That(errors).IsEmpty();
    }

    /// <summary>
    /// Verifies that valid workflow with steps does not report AGWF002.
    /// </summary>
    [Test]
    public async Task Diagnostic_ValidWorkflowWithSteps_NoAGWF002()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);

        // Assert - Should not have AGWF002 warning
        var agwf002 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF002");
        await Assert.That(agwf002).IsNull();
    }

    // =============================================================================
    // E. Duplicate Step Name Tests (AGWF003)
    // =============================================================================
    // AGWF003 is implemented using EffectiveName (= InstanceName ?? StepName) in every context:
    // - Linear flow duplicates: ERROR (same step twice in main flow)
    // - Fork path duplicates: ERROR (same step in parallel paths)
    // - Branch path duplicates: ERROR (exclusive paths still key routing maps by name)

    /// <summary>
    /// Verifies that an error diagnostic is reported when the same step
    /// appears twice in a linear workflow flow.
    /// </summary>
    [Test]
    public async Task Diagnostic_DuplicateInLinearFlow_ReportsAGWF003()
    {
        // Arrange - Same step (ValidateStep) appears twice in linear flow
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record LinearDuplicateState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class ValidateStep : IWorkflowStep<LinearDuplicateState>
            {
                public Task<StepResult<LinearDuplicateState>> ExecuteAsync(
                    LinearDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<LinearDuplicateState>.FromState(state));
            }

            public class ProcessStep : IWorkflowStep<LinearDuplicateState>
            {
                public Task<StepResult<LinearDuplicateState>> ExecuteAsync(
                    LinearDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<LinearDuplicateState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<LinearDuplicateState>
            {
                public Task<StepResult<LinearDuplicateState>> ExecuteAsync(
                    LinearDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<LinearDuplicateState>.FromState(state));
            }

            [Workflow("linear-duplicate")]
            public static partial class LinearDuplicateWorkflow
            {
                public static WorkflowDefinition<LinearDuplicateState> Definition => Workflow<LinearDuplicateState>
                    .Create("linear-duplicate")
                    .StartWith<ValidateStep>()
                    .Then<ProcessStep>()
                    .Then<ValidateStep>()  // DUPLICATE - should error
                    .Finally<CompleteStep>();
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should report AGWF003 error for ValidateStep
        var agwf003 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF003");
        await Assert.That(agwf003).IsNotNull();
        await Assert.That(agwf003!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(agwf003.GetMessage()).Contains("ValidateStep");
    }

    /// <summary>
    /// Verifies that workflows with unique step names do not report AGWF003.
    /// </summary>
    [Test]
    public async Task Diagnostic_UniqueStepNames_NoAGWF003()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);

        // Debug: List all diagnostics from our generator
        var generatorDiagnostics = result.Results
            .SelectMany(r => r.Diagnostics)
            .Where(d => d.Id.StartsWith("AGWF", StringComparison.Ordinal))
            .ToList();

        // Assert - Should not have AGWF003 or AGWF036
        var agwf003 = generatorDiagnostics.FirstOrDefault(d => d.Id == "AGWF003");
        await Assert.That(agwf003).IsNull();
        var agwf036 = generatorDiagnostics.FirstOrDefault(d => d.Id == "AGWF036");
        await Assert.That(agwf036).IsNull();
    }

    /// <summary>
    /// Verifies that an error diagnostic is reported when the same step
    /// appears in multiple fork paths (parallel execution).
    /// </summary>
    [Test]
    public async Task Diagnostic_DuplicateInForkPaths_ReportsAGWF003()
    {
        // Arrange - Same step (AnalyzeStep) appears in both fork paths
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record ForkDuplicateState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class PrepareStep : IWorkflowStep<ForkDuplicateState>
            {
                public Task<StepResult<ForkDuplicateState>> ExecuteAsync(
                    ForkDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<ForkDuplicateState>.FromState(state));
            }

            public class AnalyzeStep : IWorkflowStep<ForkDuplicateState>
            {
                public Task<StepResult<ForkDuplicateState>> ExecuteAsync(
                    ForkDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<ForkDuplicateState>.FromState(state));
            }

            public class SynthesizeStep : IWorkflowStep<ForkDuplicateState>
            {
                public Task<StepResult<ForkDuplicateState>> ExecuteAsync(
                    ForkDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<ForkDuplicateState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<ForkDuplicateState>
            {
                public Task<StepResult<ForkDuplicateState>> ExecuteAsync(
                    ForkDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<ForkDuplicateState>.FromState(state));
            }

            [Workflow("fork-duplicate")]
            public static partial class ForkDuplicateWorkflow
            {
                public static WorkflowDefinition<ForkDuplicateState> Definition => Workflow<ForkDuplicateState>
                    .Create("fork-duplicate")
                    .StartWith<PrepareStep>()
                    .Fork(
                        path => path.Then<AnalyzeStep>(),
                        path => path.Then<AnalyzeStep>())  // DUPLICATE across fork paths
                    .Join<SynthesizeStep>()
                    .Finally<CompleteStep>();
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should report AGWF003 error for AnalyzeStep
        var agwf003 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF003");
        await Assert.That(agwf003).IsNotNull();
        await Assert.That(agwf003!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(agwf003.GetMessage()).Contains("AnalyzeStep");
    }

    /// <summary>
    /// Verifies that an error diagnostic is reported when the same step
    /// appears in both linear flow and fork path.
    /// </summary>
    [Test]
    public async Task Diagnostic_DuplicateAcrossLinearAndFork_ReportsAGWF003()
    {
        // Arrange - AnalyzeStep appears in linear flow AND in a fork path
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record CrossDuplicateState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class AnalyzeStep : IWorkflowStep<CrossDuplicateState>
            {
                public Task<StepResult<CrossDuplicateState>> ExecuteAsync(
                    CrossDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<CrossDuplicateState>.FromState(state));
            }

            public class OtherStep : IWorkflowStep<CrossDuplicateState>
            {
                public Task<StepResult<CrossDuplicateState>> ExecuteAsync(
                    CrossDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<CrossDuplicateState>.FromState(state));
            }

            public class SynthesizeStep : IWorkflowStep<CrossDuplicateState>
            {
                public Task<StepResult<CrossDuplicateState>> ExecuteAsync(
                    CrossDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<CrossDuplicateState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<CrossDuplicateState>
            {
                public Task<StepResult<CrossDuplicateState>> ExecuteAsync(
                    CrossDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<CrossDuplicateState>.FromState(state));
            }

            [Workflow("cross-duplicate")]
            public static partial class CrossDuplicateWorkflow
            {
                public static WorkflowDefinition<CrossDuplicateState> Definition => Workflow<CrossDuplicateState>
                    .Create("cross-duplicate")
                    .StartWith<AnalyzeStep>()  // First occurrence in linear
                    .Fork(
                        path => path.Then<AnalyzeStep>(),  // DUPLICATE in fork
                        path => path.Then<OtherStep>())
                    .Join<SynthesizeStep>()
                    .Finally<CompleteStep>();
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should report AGWF003 error for AnalyzeStep
        var agwf003 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF003");
        await Assert.That(agwf003).IsNotNull();
        await Assert.That(agwf003!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(agwf003.GetMessage()).Contains("AnalyzeStep");
    }

    /// <summary>
    /// Verifies that the same EffectiveName on two exclusive branch cases emits one
    /// completed Handle that routes both cases (Option B).
    /// </summary>
    [Test]
    public async Task Diagnostic_DuplicateInBranchPaths_EmitsLiveCaseHandle()
    {
        // Arrange - Same step (ValidateStep) appears in both branch paths
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public enum Priority { High, Low }

            public record BranchDuplicateState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
                public Priority Priority { get; init; }
            }

            public class StartStep : IWorkflowStep<BranchDuplicateState>
            {
                public Task<StepResult<BranchDuplicateState>> ExecuteAsync(
                    BranchDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<BranchDuplicateState>.FromState(state));
            }

            public class ValidateStep : IWorkflowStep<BranchDuplicateState>
            {
                public Task<StepResult<BranchDuplicateState>> ExecuteAsync(
                    BranchDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<BranchDuplicateState>.FromState(state));
            }

            public class FinalizeStep : IWorkflowStep<BranchDuplicateState>
            {
                public Task<StepResult<BranchDuplicateState>> ExecuteAsync(
                    BranchDuplicateState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<BranchDuplicateState>.FromState(state));
            }

            [Workflow("branch-duplicate")]
            public static partial class BranchDuplicateWorkflow
            {
                public static WorkflowDefinition<BranchDuplicateState> Definition => Workflow<BranchDuplicateState>
                    .Create("branch-duplicate")
                    .StartWith<StartStep>()
                    .Branch(s => s.Priority,
                        BranchCase<BranchDuplicateState, Priority>.When(Priority.High, high => high.Then<ValidateStep>()),
                        BranchCase<BranchDuplicateState, Priority>.When(Priority.Low, low => low.Then<ValidateStep>()))
                    .Finally<FinalizeStep>();
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF003")).IsNull();
        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF036")).IsNull();
        await Assert.That(HasSaga(result)).IsTrue();
        var saga = GetSaga(result);
        await Assert.That(CountHandlerParameterLines(saga, "ValidateStepCompleted evt,")).IsEqualTo(1);
        await Assert.That(saga).Contains("StartFinalizeStepCommand");
    }

    /// <summary>
    /// #189 — a shared non-last name in two branch cases with different successors emits
    /// one Handle that routes each case to its own successor.
    /// </summary>
    [Test]
    public async Task Diagnostic_SharedInteriorBranchStep_RoutesByLiveCase()
    {
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public enum Priority { High, Low }

            public record SharedInteriorState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
                public Priority Priority { get; init; }
            }

            public class IntakeStep : IWorkflowStep<SharedInteriorState>
            {
                public Task<StepResult<SharedInteriorState>> ExecuteAsync(
                    SharedInteriorState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<SharedInteriorState>.FromState(state));
            }

            public class NormalizeStep : IWorkflowStep<SharedInteriorState>
            {
                public Task<StepResult<SharedInteriorState>> ExecuteAsync(
                    SharedInteriorState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<SharedInteriorState>.FromState(state));
            }

            public class FastTrackStep : IWorkflowStep<SharedInteriorState>
            {
                public Task<StepResult<SharedInteriorState>> ExecuteAsync(
                    SharedInteriorState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<SharedInteriorState>.FromState(state));
            }

            public class SlowTrackStep : IWorkflowStep<SharedInteriorState>
            {
                public Task<StepResult<SharedInteriorState>> ExecuteAsync(
                    SharedInteriorState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<SharedInteriorState>.FromState(state));
            }

            public class FinalizeStep : IWorkflowStep<SharedInteriorState>
            {
                public Task<StepResult<SharedInteriorState>> ExecuteAsync(
                    SharedInteriorState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<SharedInteriorState>.FromState(state));
            }

            [Workflow("shared-interior")]
            public static partial class SharedInteriorWorkflow
            {
                public static WorkflowDefinition<SharedInteriorState> Definition => Workflow<SharedInteriorState>
                    .Create("shared-interior")
                    .StartWith<IntakeStep>()
                    .Branch(s => s.Priority,
                        BranchCase<SharedInteriorState, Priority>.When(Priority.High, high => high.Then<NormalizeStep>().Then<FastTrackStep>()),
                        BranchCase<SharedInteriorState, Priority>.When(Priority.Low, low => low.Then<NormalizeStep>().Then<SlowTrackStep>()))
                    .Finally<FinalizeStep>();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF003")).IsNull();
        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF036")).IsNull();
        await Assert.That(HasSaga(result)).IsTrue();
        var saga = GetSaga(result);
        await Assert.That(CountHandlerParameterLines(saga, "NormalizeStepCompleted evt,")).IsEqualTo(1);
        await Assert.That(saga).Contains("StartFastTrackStepCommand");
        await Assert.That(saga).Contains("StartSlowTrackStepCommand");
        await Assert.That(saga).Contains("Priority.High");
        await Assert.That(saga).Contains("Priority.Low");
    }

    /// <summary>
    /// #191 — a shared last-step name across branch cases of mixed terminality emits
    /// one Handle that completes one case and rejoins the other.
    /// </summary>
    [Test]
    public async Task Diagnostic_SharedLastStepNameMixedTerminality_RoutesByLiveCase()
    {
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public enum Priority { High, Low }

            public record SharedLastState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
                public Priority Priority { get; init; }
            }

            public class IntakeStep : IWorkflowStep<SharedLastState>
            {
                public Task<StepResult<SharedLastState>> ExecuteAsync(
                    SharedLastState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<SharedLastState>.FromState(state));
            }

            public class FastTrackStep : IWorkflowStep<SharedLastState>
            {
                public Task<StepResult<SharedLastState>> ExecuteAsync(
                    SharedLastState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<SharedLastState>.FromState(state));
            }

            public class SlowTrackStep : IWorkflowStep<SharedLastState>
            {
                public Task<StepResult<SharedLastState>> ExecuteAsync(
                    SharedLastState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<SharedLastState>.FromState(state));
            }

            public class NormalizeStep : IWorkflowStep<SharedLastState>
            {
                public Task<StepResult<SharedLastState>> ExecuteAsync(
                    SharedLastState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<SharedLastState>.FromState(state));
            }

            public class FinalizeStep : IWorkflowStep<SharedLastState>
            {
                public Task<StepResult<SharedLastState>> ExecuteAsync(
                    SharedLastState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<SharedLastState>.FromState(state));
            }

            [Workflow("shared-last")]
            public static partial class SharedLastWorkflow
            {
                public static WorkflowDefinition<SharedLastState> Definition => Workflow<SharedLastState>
                    .Create("shared-last")
                    .StartWith<IntakeStep>()
                    .Branch(s => s.Priority,
                        BranchCase<SharedLastState, Priority>.When(Priority.High, high => high.Then<FastTrackStep>().Then<NormalizeStep>().Complete()),
                        BranchCase<SharedLastState, Priority>.When(Priority.Low, low => low.Then<SlowTrackStep>().Then<NormalizeStep>()))
                    .Finally<FinalizeStep>();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF003")).IsNull();
        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF036")).IsNull();
        await Assert.That(HasSaga(result)).IsTrue();
        var saga = GetSaga(result);
        await Assert.That(CountHandlerParameterLines(saga, "NormalizeStepCompleted evt,")).IsEqualTo(1);
        await Assert.That(saga).Contains("MarkCompleted()");
        await Assert.That(saga).Contains("StartFinalizeStepCommand");
        await Assert.That(saga).Contains("Priority.High");
        await Assert.That(saga).Contains("Priority.Low");
    }

    // =============================================================================
    // E2. Instance Name Tests (AGWF003 with InstanceName; Option B Handle routing)
    // =============================================================================
    // EffectiveName = InstanceName ?? StepName
    // - Different instance names → no AGWF003
    // - Same instance names → still report AGWF003
    // - Same type, distinct instance names at fork path-ends → path-qualified Handle

    /// <summary>
    /// #190 — two instance-named fork paths ending in the same step type emit distinct
    /// Handle({PhaseName}Completed) overloads and do not produce CS0111.
    /// </summary>
    [Test]
    public async Task Diagnostic_InstanceNamedForkPathEnds_EmitsQualifiedHandles()
    {
        // Arrange - Same step type (AnalyzeStep) but different instance names at path end
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record InstanceState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class PrepareStep : IWorkflowStep<InstanceState>
            {
                public Task<StepResult<InstanceState>> ExecuteAsync(
                    InstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<InstanceState>.FromState(state));
            }

            public class AnalyzeStep : IWorkflowStep<InstanceState>
            {
                public Task<StepResult<InstanceState>> ExecuteAsync(
                    InstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<InstanceState>.FromState(state));
            }

            public class SynthesizeStep : IWorkflowStep<InstanceState>
            {
                public Task<StepResult<InstanceState>> ExecuteAsync(
                    InstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<InstanceState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<InstanceState>
            {
                public Task<StepResult<InstanceState>> ExecuteAsync(
                    InstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<InstanceState>.FromState(state));
            }

            [Workflow("instance-test")]
            public static partial class InstanceTestWorkflow
            {
                public static WorkflowDefinition<InstanceState> Definition => Workflow<InstanceState>
                    .Create("instance-test")
                    .StartWith<PrepareStep>()
                    .Fork(
                        path => path.Then<AnalyzeStep>("Technical"),
                        path => path.Then<AnalyzeStep>("Fundamental"))
                    .Join<SynthesizeStep>()
                    .Finally<CompleteStep>();
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);
        var compilationDiagnostics = GeneratorTestHelper.GetCompilationDiagnostics(source);

        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF003")).IsNull();
        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF036")).IsNull();
        await Assert.That(HasSaga(result)).IsTrue();
        var saga = GetSaga(result);
        await Assert.That(CountHandlerParameterLines(saga, "TechnicalCompleted evt,")).IsEqualTo(1);
        await Assert.That(CountHandlerParameterLines(saga, "FundamentalCompleted evt,")).IsEqualTo(1);
        await Assert.That(CountHandlerParameterLines(saga, "AnalyzeStepCompleted evt,")).IsEqualTo(0);
        await Assert.That(saga).Contains("ExecuteTechnicalWorkerCommand");
        await Assert.That(saga).Contains("ExecuteFundamentalWorkerCommand");
        await Assert.That(saga).Contains("Path0Status");
        await Assert.That(saga).Contains("Path1Status");
        await Assert.That(compilationDiagnostics.Any(d => d.Id == "CS0111")).IsFalse();
    }

    /// <summary>
    /// #190 interiors — two instance-named fork-path steps of the same type that are
    /// not path-ends emit distinct Handle({PhaseName}Completed) overloads that chain
    /// to each path's own successor.
    /// </summary>
    [Test]
    public async Task Diagnostic_InstanceNamedForkInteriors_EmitsQualifiedHandles()
    {
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record InstanceState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class PrepareStep : IWorkflowStep<InstanceState>
            {
                public Task<StepResult<InstanceState>> ExecuteAsync(
                    InstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<InstanceState>.FromState(state));
            }

            public class AnalyzeStep : IWorkflowStep<InstanceState>
            {
                public Task<StepResult<InstanceState>> ExecuteAsync(
                    InstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<InstanceState>.FromState(state));
            }

            public class ScoreStep : IWorkflowStep<InstanceState>
            {
                public Task<StepResult<InstanceState>> ExecuteAsync(
                    InstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<InstanceState>.FromState(state));
            }

            public class RiskStep : IWorkflowStep<InstanceState>
            {
                public Task<StepResult<InstanceState>> ExecuteAsync(
                    InstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<InstanceState>.FromState(state));
            }

            public class SynthesizeStep : IWorkflowStep<InstanceState>
            {
                public Task<StepResult<InstanceState>> ExecuteAsync(
                    InstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<InstanceState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<InstanceState>
            {
                public Task<StepResult<InstanceState>> ExecuteAsync(
                    InstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<InstanceState>.FromState(state));
            }

            [Workflow("instance-interior-test")]
            public static partial class InstanceInteriorTestWorkflow
            {
                public static WorkflowDefinition<InstanceState> Definition => Workflow<InstanceState>
                    .Create("instance-interior-test")
                    .StartWith<PrepareStep>()
                    .Fork(
                        path => path.Then<AnalyzeStep>("Technical").Then<ScoreStep>(),
                        path => path.Then<AnalyzeStep>("Fundamental").Then<RiskStep>())
                    .Join<SynthesizeStep>()
                    .Finally<CompleteStep>();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var compilationDiagnostics = GeneratorTestHelper.GetCompilationDiagnostics(source);

        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF003")).IsNull();
        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF036")).IsNull();
        await Assert.That(HasSaga(result)).IsTrue();
        var saga = GetSaga(result);
        await Assert.That(CountHandlerParameterLines(saga, "TechnicalCompleted evt,")).IsEqualTo(1);
        await Assert.That(CountHandlerParameterLines(saga, "FundamentalCompleted evt,")).IsEqualTo(1);
        await Assert.That(saga).Contains("StartScoreStepCommand");
        await Assert.That(saga).Contains("StartRiskStepCommand");
        await Assert.That(compilationDiagnostics.Any(d => d.Id == "CS0111")).IsFalse();
    }

    /// <summary>
    /// Two sequential forks that reuse a step type under distinct instance names
    /// emit path-qualified Handles. Completed handlers live on one saga, so
    /// qualification is cross-fork, not per-fork.
    /// </summary>
    [Test]
    public async Task Diagnostic_InstanceNamedAnalyzeStepAcrossForks_EmitsQualifiedHandles()
    {
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record CrossForkState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class PrepareStep : IWorkflowStep<CrossForkState>
            {
                public Task<StepResult<CrossForkState>> ExecuteAsync(
                    CrossForkState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<CrossForkState>.FromState(state));
            }

            public class AnalyzeStep : IWorkflowStep<CrossForkState>
            {
                public Task<StepResult<CrossForkState>> ExecuteAsync(
                    CrossForkState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<CrossForkState>.FromState(state));
            }

            public class ScoreStep : IWorkflowStep<CrossForkState>
            {
                public Task<StepResult<CrossForkState>> ExecuteAsync(
                    CrossForkState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<CrossForkState>.FromState(state));
            }

            public class RiskStep : IWorkflowStep<CrossForkState>
            {
                public Task<StepResult<CrossForkState>> ExecuteAsync(
                    CrossForkState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<CrossForkState>.FromState(state));
            }

            public class SynthesizeStep : IWorkflowStep<CrossForkState>
            {
                public Task<StepResult<CrossForkState>> ExecuteAsync(
                    CrossForkState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<CrossForkState>.FromState(state));
            }

            public class FinalizeStep : IWorkflowStep<CrossForkState>
            {
                public Task<StepResult<CrossForkState>> ExecuteAsync(
                    CrossForkState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<CrossForkState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<CrossForkState>
            {
                public Task<StepResult<CrossForkState>> ExecuteAsync(
                    CrossForkState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<CrossForkState>.FromState(state));
            }

            [Workflow("cross-fork-instance")]
            public static partial class CrossForkInstanceWorkflow
            {
                public static WorkflowDefinition<CrossForkState> Definition => Workflow<CrossForkState>
                    .Create("cross-fork-instance")
                    .StartWith<PrepareStep>()
                    .Fork(
                        path => path.Then<AnalyzeStep>("Technical"),
                        path => path.Then<ScoreStep>())
                    .Join<SynthesizeStep>()
                    .Fork(
                        path => path.Then<AnalyzeStep>("Fundamental"),
                        path => path.Then<RiskStep>())
                    .Join<FinalizeStep>()
                    .Finally<CompleteStep>();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var compilationDiagnostics = GeneratorTestHelper.GetCompilationDiagnostics(source);

        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF003")).IsNull();
        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF036")).IsNull();
        await Assert.That(HasSaga(result)).IsTrue();
        var saga = GetSaga(result);
        await Assert.That(CountHandlerParameterLines(saga, "TechnicalCompleted evt,")).IsEqualTo(1);
        await Assert.That(CountHandlerParameterLines(saga, "FundamentalCompleted evt,")).IsEqualTo(1);
        await Assert.That(compilationDiagnostics.Any(d => d.Id == "CS0111")).IsFalse();
    }

    /// <summary>
    /// Branch cases that share a step type under distinct instance names emit one
    /// Handle({StepType}Completed) that routes by the live case's phase.
    /// </summary>
    [Test]
    public async Task Diagnostic_InstanceNamedBranchCases_RoutesByLivePhase()
    {
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public enum Priority { High, Low }

            public record NamedBranchState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
                public Priority Priority { get; init; }
            }

            public class IntakeStep : IWorkflowStep<NamedBranchState>
            {
                public Task<StepResult<NamedBranchState>> ExecuteAsync(
                    NamedBranchState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<NamedBranchState>.FromState(state));
            }

            public class NormalizeStep : IWorkflowStep<NamedBranchState>
            {
                public Task<StepResult<NamedBranchState>> ExecuteAsync(
                    NamedBranchState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<NamedBranchState>.FromState(state));
            }

            public class FinalizeStep : IWorkflowStep<NamedBranchState>
            {
                public Task<StepResult<NamedBranchState>> ExecuteAsync(
                    NamedBranchState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<NamedBranchState>.FromState(state));
            }

            [Workflow("named-branch")]
            public static partial class NamedBranchWorkflow
            {
                public static WorkflowDefinition<NamedBranchState> Definition => Workflow<NamedBranchState>
                    .Create("named-branch")
                    .StartWith<IntakeStep>()
                    .Branch(s => s.Priority,
                        BranchCase<NamedBranchState, Priority>.When(Priority.High, high => high.Then<NormalizeStep>("HighNorm")),
                        BranchCase<NamedBranchState, Priority>.When(Priority.Low, low => low.Then<NormalizeStep>("LowNorm")))
                    .Finally<FinalizeStep>();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF003")).IsNull();
        await Assert.That(result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF036")).IsNull();
        await Assert.That(HasSaga(result)).IsTrue();
        var saga = GetSaga(result);
        await Assert.That(CountHandlerParameterLines(saga, "NormalizeStepCompleted evt,")).IsEqualTo(1);
        await Assert.That(saga).Contains("HighNorm");
        await Assert.That(saga).Contains("LowNorm");
        await Assert.That(saga).Contains("StartFinalizeStepCommand");
    }

    /// <summary>
    /// Verifies that same instance name in fork paths still reports AGWF003.
    /// Same instance name = same EffectiveName = duplicate.
    /// </summary>
    [Test]
    public async Task Diagnostic_SameInstanceName_StillReportsAGWF003()
    {
        // Arrange - Same step type AND same instance name
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record DupInstanceState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class PrepareStep : IWorkflowStep<DupInstanceState>
            {
                public Task<StepResult<DupInstanceState>> ExecuteAsync(
                    DupInstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<DupInstanceState>.FromState(state));
            }

            public class AnalyzeStep : IWorkflowStep<DupInstanceState>
            {
                public Task<StepResult<DupInstanceState>> ExecuteAsync(
                    DupInstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<DupInstanceState>.FromState(state));
            }

            public class SynthesizeStep : IWorkflowStep<DupInstanceState>
            {
                public Task<StepResult<DupInstanceState>> ExecuteAsync(
                    DupInstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<DupInstanceState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<DupInstanceState>
            {
                public Task<StepResult<DupInstanceState>> ExecuteAsync(
                    DupInstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<DupInstanceState>.FromState(state));
            }

            [Workflow("dup-instance-test")]
            public static partial class DupInstanceTestWorkflow
            {
                public static WorkflowDefinition<DupInstanceState> Definition => Workflow<DupInstanceState>
                    .Create("dup-instance-test")
                    .StartWith<PrepareStep>()
                    .Fork(
                        path => path.Then<AnalyzeStep>("SameName"),
                        path => path.Then<AnalyzeStep>("SameName"))  // DUPLICATE - same instance name
                    .Join<SynthesizeStep>()
                    .Finally<CompleteStep>();
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should report AGWF003 - same instance name = duplicate
        var agwf003 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF003");
        await Assert.That(agwf003).IsNotNull();
        await Assert.That(agwf003!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(agwf003.GetMessage()).Contains("SameName");
    }

    /// <summary>
    /// Verifies that mixing instance-named and unnamed steps with same base type reports AGWF003.
    /// AnalyzeStep (unnamed, EffectiveName="AnalyzeStep") and AnalyzeStep("AnalyzeStep")
    /// would have the same EffectiveName.
    /// </summary>
    [Test]
    public async Task Diagnostic_InstanceNameMatchesStepName_ReportsAGWF003()
    {
        // Arrange - Instance name is same as step name, plus an unnamed occurrence
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record MixedInstanceState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class PrepareStep : IWorkflowStep<MixedInstanceState>
            {
                public Task<StepResult<MixedInstanceState>> ExecuteAsync(
                    MixedInstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<MixedInstanceState>.FromState(state));
            }

            public class AnalyzeStep : IWorkflowStep<MixedInstanceState>
            {
                public Task<StepResult<MixedInstanceState>> ExecuteAsync(
                    MixedInstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<MixedInstanceState>.FromState(state));
            }

            public class SynthesizeStep : IWorkflowStep<MixedInstanceState>
            {
                public Task<StepResult<MixedInstanceState>> ExecuteAsync(
                    MixedInstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<MixedInstanceState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<MixedInstanceState>
            {
                public Task<StepResult<MixedInstanceState>> ExecuteAsync(
                    MixedInstanceState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<MixedInstanceState>.FromState(state));
            }

            [Workflow("mixed-instance-test")]
            public static partial class MixedInstanceTestWorkflow
            {
                public static WorkflowDefinition<MixedInstanceState> Definition => Workflow<MixedInstanceState>
                    .Create("mixed-instance-test")
                    .StartWith<PrepareStep>()
                    .Fork(
                        path => path.Then<AnalyzeStep>(),               // EffectiveName = "AnalyzeStep"
                        path => path.Then<AnalyzeStep>("AnalyzeStep"))  // EffectiveName = "AnalyzeStep" - DUPLICATE
                    .Join<SynthesizeStep>()
                    .Finally<CompleteStep>();
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should report AGWF003 - EffectiveNames both = "AnalyzeStep"
        var agwf003 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF003");
        await Assert.That(agwf003).IsNotNull();
        await Assert.That(agwf003!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(agwf003.GetMessage()).Contains("AnalyzeStep");
    }

    // =============================================================================
    // F. Invalid Namespace Tests (AGWF004)
    // =============================================================================

    /// <summary>
    /// Verifies that an error diagnostic is reported when workflow is in global namespace.
    /// </summary>
    [Test]
    public async Task Diagnostic_GlobalNamespace_ReportsAGWF004()
    {
        // Arrange - Workflow without a namespace
        var source = """
            using Strategos.Attributes;

            [Workflow("global-workflow")]
            public static partial class GlobalWorkflow
            {
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should report AGWF004 error
        var agwf004 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF004");
        await Assert.That(agwf004).IsNotNull();
        await Assert.That(agwf004!.Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Verifies that AGWF004 message includes the workflow name.
    /// </summary>
    [Test]
    public async Task Diagnostic_GlobalNamespace_IncludesWorkflowNameInMessage()
    {
        // Arrange - Workflow without a namespace
        var source = """
            using Strategos.Attributes;

            [Workflow("global-workflow")]
            public static partial class GlobalWorkflow
            {
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Message should contain the workflow name
        var agwf004 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF004");
        await Assert.That(agwf004).IsNotNull();
        await Assert.That(agwf004!.GetMessage()).Contains("global-workflow");
    }

    /// <summary>
    /// Verifies that workflows in a named namespace do not report AGWF004.
    /// </summary>
    [Test]
    public async Task Diagnostic_NamedNamespace_NoAGWF004()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);

        // Assert - Should not have AGWF004 error
        var agwf004 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF004");
        await Assert.That(agwf004).IsNull();
    }

    // =============================================================================
    // G. Missing StartWith Tests (AGWF009)
    // =============================================================================

    /// <summary>
    /// Verifies that an error diagnostic is reported when workflow starts with Then
    /// instead of StartWith.
    /// </summary>
    [Test]
    public async Task Diagnostic_MissingStartWith_ReportsAGWF009()
    {
        // Arrange - Workflow starts with Then instead of StartWith
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record MissingStartState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class StepA : IWorkflowStep<MissingStartState>
            {
                public Task<StepResult<MissingStartState>> ExecuteAsync(
                    MissingStartState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<MissingStartState>.FromState(state));
            }

            public class StepB : IWorkflowStep<MissingStartState>
            {
                public Task<StepResult<MissingStartState>> ExecuteAsync(
                    MissingStartState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<MissingStartState>.FromState(state));
            }

            [Workflow("missing-start")]
            public static partial class MissingStartWorkflow
            {
                public static WorkflowDefinition<MissingStartState> Definition => Workflow<MissingStartState>
                    .Create("missing-start")
                    .Then<StepA>()  // ERROR: Should use StartWith
                    .Finally<StepB>();
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should report AGWF009 error
        var agwf009 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF009");
        await Assert.That(agwf009).IsNotNull();
        await Assert.That(agwf009!.Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Verifies that workflows with proper StartWith do not report AGWF009.
    /// </summary>
    [Test]
    public async Task Diagnostic_ValidStartWith_NoAGWF009()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);

        // Assert - Should not have AGWF009 error
        var agwf009 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF009");
        await Assert.That(agwf009).IsNull();
    }

    /// <summary>
    /// Verifies that AGWF009 message includes the workflow name.
    /// </summary>
    [Test]
    public async Task Diagnostic_MissingStartWith_IncludesWorkflowNameInMessage()
    {
        // Arrange - Workflow starts with Then instead of StartWith
        var source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record NoStartState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class OnlyStep : IWorkflowStep<NoStartState>
            {
                public Task<StepResult<NoStartState>> ExecuteAsync(
                    NoStartState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<NoStartState>.FromState(state));
            }

            [Workflow("no-start-workflow")]
            public static partial class NoStartWorkflow
            {
                public static WorkflowDefinition<NoStartState> Definition => Workflow<NoStartState>
                    .Create("no-start-workflow")
                    .Then<OnlyStep>()  // ERROR: Should use StartWith
                    .Finally<OnlyStep>();
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Message should contain workflow name
        var agwf009 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF009");
        await Assert.That(agwf009).IsNotNull();
        await Assert.That(agwf009!.GetMessage()).Contains("no-start-workflow");
    }

    // =========================================================================
    // Section H: Fork without Join (AGWF012)
    // =========================================================================

    /// <summary>
    /// Verifies that AGWF012 is reported when Fork is not followed by Join.
    /// </summary>
    [Test]
    public async Task Diagnostic_ForkWithoutJoin_ReportsAGWF012()
    {
        // Arrange - Fork followed by Then instead of Join
        var source = SourceTexts.WorkflowForkWithoutJoin;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should report AGWF012
        var agwf012 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF012");
        await Assert.That(agwf012).IsNotNull();
        await Assert.That(agwf012!.Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Verifies that AGWF012 is not reported when Fork is properly followed by Join.
    /// </summary>
    [Test]
    public async Task Diagnostic_ForkWithJoin_NoAGWF012()
    {
        // Arrange - Valid Fork/Join pattern
        var source = SourceTexts.WorkflowWithFork;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should NOT report AGWF012
        var agwf012 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF012");
        await Assert.That(agwf012).IsNull();
    }

    /// <summary>
    /// Verifies that AGWF012 message includes the workflow name.
    /// </summary>
    [Test]
    public async Task Diagnostic_ForkWithoutJoin_IncludesWorkflowNameInMessage()
    {
        // Arrange - Fork without Join
        var source = SourceTexts.WorkflowForkWithoutJoin;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Message should contain workflow name
        var agwf012 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF012");
        await Assert.That(agwf012).IsNotNull();
        await Assert.That(agwf012!.GetMessage()).Contains("fork-no-join");
    }

    // =========================================================================
    // Section I: Missing Finally (AGWF010) - Warning
    // =========================================================================

    /// <summary>
    /// Verifies that AGWF010 is reported as a warning when workflow is missing Finally.
    /// </summary>
    [Test]
    public async Task Diagnostic_MissingFinally_ReportsAGWF010Warning()
    {
        // Arrange - Workflow without Finally
        var source = SourceTexts.WorkflowMissingFinally;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should report AGWF010 as Warning (not Error)
        var agwf010 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF010");
        await Assert.That(agwf010).IsNotNull();
        await Assert.That(agwf010!.Severity).IsEqualTo(DiagnosticSeverity.Warning);
    }

    /// <summary>
    /// Verifies that AGWF010 is not reported when workflow has Finally.
    /// </summary>
    [Test]
    public async Task Diagnostic_ValidFinally_NoAGWF010()
    {
        // Arrange - Valid workflow with Finally
        var source = SourceTexts.WorkflowWithFork;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should NOT report AGWF010
        var agwf010 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF010");
        await Assert.That(agwf010).IsNull();
    }

    /// <summary>
    /// Verifies that AGWF010 message includes the workflow name.
    /// </summary>
    [Test]
    public async Task Diagnostic_MissingFinally_IncludesWorkflowNameInMessage()
    {
        // Arrange - Workflow without Finally
        var source = SourceTexts.WorkflowMissingFinally;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Message should contain workflow name
        var agwf010 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF010");
        await Assert.That(agwf010).IsNotNull();
        await Assert.That(agwf010!.GetMessage()).Contains("no-finally");
    }

    /// <summary>
    /// Verifies that code is still generated despite AGWF010 warning (warning-only).
    /// </summary>
    [Test]
    public async Task Diagnostic_MissingFinally_StillGeneratesCode()
    {
        // Arrange - Workflow without Finally
        var source = SourceTexts.WorkflowMissingFinally;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should still generate code (warning doesn't block generation)
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
    }

    // =========================================================================
    // Section J: Loop Without Body (AGWF014)
    // =========================================================================

    /// <summary>
    /// Verifies that AGWF014 is reported when loop has an empty body.
    /// </summary>
    [Test]
    public async Task Diagnostic_LoopWithoutBody_ReportsAGWF014()
    {
        // Arrange - Loop with empty body
        var source = SourceTexts.WorkflowEmptyLoopBody;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should report AGWF014
        var agwf014 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF014");
        await Assert.That(agwf014).IsNotNull();
        await Assert.That(agwf014!.Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Verifies that AGWF014 is not reported when loop has steps in its body.
    /// </summary>
    [Test]
    public async Task Diagnostic_LoopWithBody_NoAGWF014()
    {
        // Arrange - Valid loop with body
        var source = SourceTexts.WorkflowWithLoop;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should NOT report AGWF014
        var agwf014 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF014");
        await Assert.That(agwf014).IsNull();
    }

    /// <summary>
    /// Verifies that AGWF014 message includes the loop name.
    /// </summary>
    [Test]
    public async Task Diagnostic_LoopWithoutBody_IncludesLoopNameInMessage()
    {
        // Arrange - Loop with empty body
        var source = SourceTexts.WorkflowEmptyLoopBody;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - Message should contain loop name
        var agwf014 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF014");
        await Assert.That(agwf014).IsNotNull();
        await Assert.That(agwf014!.GetMessage()).Contains("process");
    }

    private static bool HasSaga(GeneratorDriverRunResult result)
        => result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal));

    private static string GetSaga(GeneratorDriverRunResult result)
        => result.GeneratedTrees
            .First(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal))
            .GetText()
            .ToString();

    private static int CountHandlerParameterLines(string generatedSource, string parameterDeclaration) =>
        generatedSource
            .Split('\n')
            .Count(line => string.Equals(line.Trim(), parameterDeclaration, StringComparison.Ordinal));
}
