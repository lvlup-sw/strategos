// -----------------------------------------------------------------------
// <copyright file="SourceTexts.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Generators.Tests.Fixtures;

/// <summary>
/// Contains sample source code strings for generator testing.
/// </summary>
public static class SourceTexts
{
    /// <summary>
    /// A class with the [Workflow] attribute applied.
    /// </summary>
    public const string ClassWithWorkflowAttribute = """
        using Strategos.Attributes;

        namespace TestNamespace;

        [Workflow("process-order")]
        public static partial class ProcessOrderWorkflow
        {
        }
        """;

    /// <summary>
    /// A class without the [Workflow] attribute.
    /// </summary>
    public const string ClassWithoutWorkflowAttribute = """
        namespace TestNamespace;

        public static class SomeOtherClass
        {
            public static int Value { get; set; }
        }
        """;

    /// <summary>
    /// A struct with the [Workflow] attribute applied.
    /// </summary>
    public const string StructWithWorkflowAttribute = """
        using Strategos.Attributes;

        namespace TestNamespace;

        [Workflow("data-pipeline")]
        public partial struct DataPipelineWorkflow
        {
        }
        """;

    /// <summary>
    /// A workflow with an empty name.
    /// </summary>
    public const string WorkflowWithEmptyName = """
        using Strategos.Attributes;

        namespace TestNamespace;

        [Workflow("")]
        public static partial class EmptyNameWorkflow
        {
        }
        """;

    /// <summary>
    /// A simple linear workflow definition with steps.
    /// </summary>
    public const string LinearWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record OrderState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidateOrder : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class ProcessPayment : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class SendConfirmation : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        [Workflow("process-order")]
        public static partial class ProcessOrderWorkflow
        {
            public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
                .Create("process-order")
                .StartWith<ValidateOrder>()
                .Then<ProcessPayment>()
                .Finally<SendConfirmation>();
        }
        """;

    /// <summary>
    /// A workflow definition with a RepeatUntil loop containing body steps.
    /// </summary>
    public const string WorkflowWithLoop = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record RefinementState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public decimal QualityScore { get; init; }
        }

        public class ValidateInput : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        public class CritiqueStep : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        public class RefineStep : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        public class PublishResult : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        [Workflow("iterative-refinement")]
        public static partial class IterativeRefinementWorkflow
        {
            public static WorkflowDefinition<RefinementState> Definition => Workflow<RefinementState>
                .Create("iterative-refinement")
                .StartWith<ValidateInput>()
                .RepeatUntil(
                    state => state.QualityScore >= 0.9m,
                    "Refinement",
                    loop => loop
                        .Then<CritiqueStep>()
                        .Then<RefineStep>(),
                    maxIterations: 5)
                .Finally<PublishResult>();
        }
        """;

    /// <summary>
    /// A workflow whose RepeatUntil loop body declares a confidence-gated step via the
    /// <c>Then&lt;TStep&gt;(step =&gt; step.RequireConfidence(...).OnLowConfidence(...))</c> configure-lambda
    /// overload. Exercises the DR-5 IR half: the loop-body configure lambda is parsed so
    /// <see cref="Strategos.Generators.Models.StepModel.Confidence"/> is populated on the loop body.
    /// </summary>
    public const string WorkflowWithLoopBodyConfidence = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record RefinementState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public decimal QualityScore { get; init; }
        }

        public class ValidateInput : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        public class CritiqueStep : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.WithConfidence(state, 0.5));
        }

        public class ReviewStep : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        public class RefineStep : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        public class PublishResult : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        [Workflow("iterative-refinement")]
        public static partial class IterativeRefinementWorkflow
        {
            public static WorkflowDefinition<RefinementState> Definition => Workflow<RefinementState>
                .Create("iterative-refinement")
                .StartWith<ValidateInput>()
                .RepeatUntil(
                    state => state.QualityScore >= 0.9m,
                    "Refinement",
                    loop => loop
                        .Then<CritiqueStep>(step => step
                            .RequireConfidence(0.85)
                            .OnLowConfidence(alt => alt.Then<ReviewStep>()))
                        .Then<RefineStep>(),
                    maxIterations: 5)
                .Finally<PublishResult>();
        }
        """;

    /// <summary>
    /// A workflow definition with multiple separate RepeatUntil loops.
    /// </summary>
    public const string WorkflowWithMultipleLoops = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record MultiLoopState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public decimal QualityScore { get; init; }
            public bool IsValid { get; init; }
        }

        public class StartStep : IWorkflowStep<MultiLoopState>
        {
            public Task<StepResult<MultiLoopState>> ExecuteAsync(
                MultiLoopState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<MultiLoopState>.FromState(state));
        }

        public class CritiqueStep : IWorkflowStep<MultiLoopState>
        {
            public Task<StepResult<MultiLoopState>> ExecuteAsync(
                MultiLoopState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<MultiLoopState>.FromState(state));
        }

        public class CheckStep : IWorkflowStep<MultiLoopState>
        {
            public Task<StepResult<MultiLoopState>> ExecuteAsync(
                MultiLoopState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<MultiLoopState>.FromState(state));
        }

        public class FinalStep : IWorkflowStep<MultiLoopState>
        {
            public Task<StepResult<MultiLoopState>> ExecuteAsync(
                MultiLoopState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<MultiLoopState>.FromState(state));
        }

        [Workflow("multi-loop")]
        public static partial class MultiLoopWorkflow
        {
            public static WorkflowDefinition<MultiLoopState> Definition => Workflow<MultiLoopState>
                .Create("multi-loop")
                .StartWith<StartStep>()
                .RepeatUntil(
                    state => state.QualityScore >= 0.9m,
                    "Refinement",
                    loop => loop.Then<CritiqueStep>(),
                    maxIterations: 5)
                .RepeatUntil(
                    state => state.IsValid,
                    "Validation",
                    loop => loop.Then<CheckStep>(),
                    maxIterations: 3)
                .Finally<FinalStep>();
        }
        """;

    /// <summary>
    /// A workflow definition with truly nested loops for hierarchical phase naming.
    /// Outer loop contains OuterStep and a nested Inner loop containing InnerStep.
    /// Expected phase names: Outer_OuterStep, Outer_Inner_InnerStep.
    /// </summary>
    public const string WorkflowWithNestedLoops = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record NestedLoopState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public bool OuterDone { get; init; }
            public bool InnerDone { get; init; }
        }

        public class StartStep : IWorkflowStep<NestedLoopState>
        {
            public Task<StepResult<NestedLoopState>> ExecuteAsync(
                NestedLoopState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<NestedLoopState>.FromState(state));
        }

        public class OuterStep : IWorkflowStep<NestedLoopState>
        {
            public Task<StepResult<NestedLoopState>> ExecuteAsync(
                NestedLoopState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<NestedLoopState>.FromState(state));
        }

        public class InnerStep : IWorkflowStep<NestedLoopState>
        {
            public Task<StepResult<NestedLoopState>> ExecuteAsync(
                NestedLoopState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<NestedLoopState>.FromState(state));
        }

        public class DoneStep : IWorkflowStep<NestedLoopState>
        {
            public Task<StepResult<NestedLoopState>> ExecuteAsync(
                NestedLoopState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<NestedLoopState>.FromState(state));
        }

        [Workflow("nested-loops")]
        public static partial class NestedLoopsWorkflow
        {
            public static WorkflowDefinition<NestedLoopState> Definition => Workflow<NestedLoopState>
                .Create("nested-loops")
                .StartWith<StartStep>()
                .RepeatUntil(
                    state => state.OuterDone,
                    "Outer",
                    outer => outer
                        .Then<OuterStep>()
                        .RepeatUntil(
                            state => state.InnerDone,
                            "Inner",
                            inner => inner.Then<InnerStep>(),
                            maxIterations: 3),
                    maxIterations: 5)
                .Finally<DoneStep>();
        }
        """;

    // =============================================================================
    // Branch Workflow Test Sources (Milestone 8c - Branch/Loop Saga Support)
    // =============================================================================

    /// <summary>
    /// A workflow definition with an enum-based branch.
    /// </summary>
    public const string WorkflowWithEnumBranch = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum ClaimType { Auto, Home, Life }

        public record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public ClaimType Type { get; init; }
        }

        public class ValidateClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class ProcessAutoClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class ProcessHomeClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class ProcessLifeClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class CompleteClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("process-claim")]
        public static partial class ProcessClaimWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("process-claim")
                .StartWith<ValidateClaim>()
                .Branch(state => state.Type,
                    BranchCase<ClaimState, ClaimType>.When(ClaimType.Auto, path => path.Then<ProcessAutoClaim>()),
                    BranchCase<ClaimState, ClaimType>.When(ClaimType.Home, path => path.Then<ProcessHomeClaim>()),
                    BranchCase<ClaimState, ClaimType>.Otherwise(path => path.Then<ProcessLifeClaim>()))
                .Finally<CompleteClaim>();
        }
        """;

    /// <summary>
    /// A workflow definition with a string-based branch.
    /// </summary>
    public const string WorkflowWithStringBranch = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record DocumentState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public string DocumentType { get; init; } = "";
        }

        public class ValidateDocument : IWorkflowStep<DocumentState>
        {
            public Task<StepResult<DocumentState>> ExecuteAsync(
                DocumentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<DocumentState>.FromState(state));
        }

        public class ProcessPdf : IWorkflowStep<DocumentState>
        {
            public Task<StepResult<DocumentState>> ExecuteAsync(
                DocumentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<DocumentState>.FromState(state));
        }

        public class ProcessWord : IWorkflowStep<DocumentState>
        {
            public Task<StepResult<DocumentState>> ExecuteAsync(
                DocumentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<DocumentState>.FromState(state));
        }

        public class ArchiveDocument : IWorkflowStep<DocumentState>
        {
            public Task<StepResult<DocumentState>> ExecuteAsync(
                DocumentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<DocumentState>.FromState(state));
        }

        [Workflow("process-document")]
        public static partial class ProcessDocumentWorkflow
        {
            public static WorkflowDefinition<DocumentState> Definition => Workflow<DocumentState>
                .Create("process-document")
                .StartWith<ValidateDocument>()
                .Branch(state => state.DocumentType,
                    BranchCase<DocumentState, string>.When("pdf", path => path.Then<ProcessPdf>()),
                    BranchCase<DocumentState, string>.Otherwise(path => path.Then<ProcessWord>()))
                .Finally<ArchiveDocument>();
        }
        """;

    /// <summary>
    /// A workflow definition with a terminal branch (no rejoin).
    /// </summary>
    public const string WorkflowWithTerminalBranch = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record OrderState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public bool IsValid { get; init; }
        }

        public class ValidateOrder : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class ProcessOrder : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class RejectOrder : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class ShipOrder : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        [Workflow("validate-order")]
        public static partial class ValidateOrderWorkflow
        {
            public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
                .Create("validate-order")
                .StartWith<ValidateOrder>()
                .Branch(state => state.IsValid,
                    BranchCase<OrderState, bool>.When(true, path => path.Then<ProcessOrder>()),
                    BranchCase<OrderState, bool>.When(false, path => path.Then<RejectOrder>().Complete()))
                .Finally<ShipOrder>();
        }
        """;

    /// <summary>
    /// A workflow definition with multiple steps in a branch path.
    /// </summary>
    public const string WorkflowWithMultiStepBranch = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum Priority { Low, High }

        public record TicketState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public Priority Priority { get; init; }
        }

        public class TriageTicket : IWorkflowStep<TicketState>
        {
            public Task<StepResult<TicketState>> ExecuteAsync(
                TicketState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<TicketState>.FromState(state));
        }

        public class AssignAgent : IWorkflowStep<TicketState>
        {
            public Task<StepResult<TicketState>> ExecuteAsync(
                TicketState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<TicketState>.FromState(state));
        }

        public class EscalateToManager : IWorkflowStep<TicketState>
        {
            public Task<StepResult<TicketState>> ExecuteAsync(
                TicketState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<TicketState>.FromState(state));
        }

        public class NotifyCustomer : IWorkflowStep<TicketState>
        {
            public Task<StepResult<TicketState>> ExecuteAsync(
                TicketState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<TicketState>.FromState(state));
        }

        public class AddToQueue : IWorkflowStep<TicketState>
        {
            public Task<StepResult<TicketState>> ExecuteAsync(
                TicketState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<TicketState>.FromState(state));
        }

        public class CloseTicket : IWorkflowStep<TicketState>
        {
            public Task<StepResult<TicketState>> ExecuteAsync(
                TicketState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<TicketState>.FromState(state));
        }

        [Workflow("process-ticket")]
        public static partial class ProcessTicketWorkflow
        {
            public static WorkflowDefinition<TicketState> Definition => Workflow<TicketState>
                .Create("process-ticket")
                .StartWith<TriageTicket>()
                .Branch(state => state.Priority,
                    BranchCase<TicketState, Priority>.When(Priority.High, path => path
                        .Then<AssignAgent>()
                        .Then<EscalateToManager>()
                        .Then<NotifyCustomer>()),
                    BranchCase<TicketState, Priority>.Otherwise(path => path
                        .Then<AddToQueue>()))
                .Finally<CloseTicket>();
        }
        """;

    // =============================================================================
    // Versioned Workflow Test Sources
    // =============================================================================

    /// <summary>
    /// A versioned workflow definition (version 2).
    /// </summary>
    public const string VersionedWorkflowV2 = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record OrderStateV2 : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public string CustomerEmail { get; init; } = "";
        }

        public class ValidateOrderV2 : IWorkflowStep<OrderStateV2>
        {
            public Task<StepResult<OrderStateV2>> ExecuteAsync(
                OrderStateV2 state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderStateV2>.FromState(state));
        }

        public class ProcessPaymentV2 : IWorkflowStep<OrderStateV2>
        {
            public Task<StepResult<OrderStateV2>> ExecuteAsync(
                OrderStateV2 state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderStateV2>.FromState(state));
        }

        [Workflow("process-order", version: 2)]
        public static partial class ProcessOrderWorkflowV2
        {
            public static WorkflowDefinition<OrderStateV2> Definition => Workflow<OrderStateV2>
                .Create("process-order")
                .StartWith<ValidateOrderV2>()
                .Finally<ProcessPaymentV2>();
        }
        """;

    // =============================================================================
    // State Reducer Generator Test Sources
    // =============================================================================

    /// <summary>
    /// A record with the [WorkflowState] attribute and standard properties.
    /// </summary>
    public const string StateWithStandardProperties = """
        using Strategos.Attributes;

        namespace TestNamespace;

        [WorkflowState]
        public record OrderState
        {
            public string Status { get; init; } = "";
            public decimal Total { get; init; }
        }
        """;

    /// <summary>
    /// A record without [WorkflowState] attribute - should not generate reducer.
    /// </summary>
    public const string StateWithoutAttribute = """
        namespace TestNamespace;

        public record PlainState
        {
            public string Name { get; init; } = "";
        }
        """;

    /// <summary>
    /// A record with [WorkflowState] attribute and [Append] property.
    /// </summary>
    public const string StateWithAppendProperty = """
        using System.Collections.Generic;
        using Strategos.Attributes;

        namespace TestNamespace;

        [WorkflowState]
        public record OrderState
        {
            public string Status { get; init; } = "";

            [Append]
            public IReadOnlyList<string> Items { get; init; } = [];
        }
        """;

    /// <summary>
    /// A record with [WorkflowState] attribute and [Merge] property.
    /// </summary>
    public const string StateWithMergeProperty = """
        using System.Collections.Generic;
        using Strategos.Attributes;

        namespace TestNamespace;

        [WorkflowState]
        public record OrderState
        {
            public string Status { get; init; } = "";

            [Merge]
            public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
        }
        """;

    /// <summary>
    /// A record with mixed property kinds (Standard, Append, Merge).
    /// </summary>
    public const string StateWithMixedProperties = """
        using System.Collections.Generic;
        using Strategos.Attributes;

        namespace TestNamespace;

        [WorkflowState]
        public record ComplexState
        {
            public string Status { get; init; } = "";
            public int Count { get; init; }

            [Append]
            public IReadOnlyList<string> Items { get; init; } = [];

            [Append]
            public IReadOnlyList<int> Scores { get; init; } = [];

            [Merge]
            public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
        }
        """;

    /// <summary>
    /// A struct with [WorkflowState] attribute.
    /// </summary>
    public const string StructWithWorkflowState = """
        using Strategos.Attributes;

        namespace TestNamespace;

        [WorkflowState]
        public readonly record struct ValueState
        {
            public int Id { get; init; }
            public string Name { get; init; }
        }
        """;

    /// <summary>
    /// A state implementing IWorkflowState but without explicit attribute.
    /// Should generate reducer when interface fallback is enabled.
    /// </summary>
    public const string StateWithInterfaceOnly = """
        using System;
        using Strategos.Abstractions;

        namespace TestNamespace;

        public record WorkflowStateViaInterface : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public string Status { get; init; } = "";
        }
        """;

    /// <summary>
    /// A state with nested namespace.
    /// </summary>
    public const string StateWithNestedNamespace = """
        using Strategos.Attributes;

        namespace Company.Product.Domain;

        [WorkflowState]
        public record DomainState
        {
            public string Name { get; init; } = "";
        }
        """;

    /// <summary>
    /// An empty state with no properties.
    /// </summary>
    public const string EmptyState = """
        using Strategos.Attributes;

        namespace TestNamespace;

        [WorkflowState]
        public record EmptyState;
        """;

    // =============================================================================
    // Validation Guard Test Sources (Milestone 9 - Guard Logic Injection)
    // =============================================================================

    /// <summary>
    /// A workflow definition with validation guards on steps.
    /// </summary>
    public const string WorkflowWithValidation = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record PaymentState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public decimal Total { get; init; }
            public bool IsVerified { get; init; }
        }

        public class ValidatePayment : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        public class ProcessPayment : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        public class SendReceipt : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        [Workflow("process-payment")]
        public static partial class ProcessPaymentWorkflow
        {
            public static WorkflowDefinition<PaymentState> Definition => Workflow<PaymentState>
                .Create("process-payment")
                .StartWith<ValidatePayment>()
                .Then<ProcessPayment>()
                    .ValidateState(state => state.Total > 0, "Total must be positive")
                .Finally<SendReceipt>();
        }
        """;

    /// <summary>
    /// A workflow definition with multiple validation guards.
    /// </summary>
    public const string WorkflowWithMultipleValidations = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record OrderProcessState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public decimal Total { get; init; }
            public bool IsVerified { get; init; }
            public int ItemCount { get; init; }
        }

        public class ValidateOrder : IWorkflowStep<OrderProcessState>
        {
            public Task<StepResult<OrderProcessState>> ExecuteAsync(
                OrderProcessState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderProcessState>.FromState(state));
        }

        public class ProcessItems : IWorkflowStep<OrderProcessState>
        {
            public Task<StepResult<OrderProcessState>> ExecuteAsync(
                OrderProcessState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderProcessState>.FromState(state));
        }

        public class ChargePayment : IWorkflowStep<OrderProcessState>
        {
            public Task<StepResult<OrderProcessState>> ExecuteAsync(
                OrderProcessState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderProcessState>.FromState(state));
        }

        public class ShipOrder : IWorkflowStep<OrderProcessState>
        {
            public Task<StepResult<OrderProcessState>> ExecuteAsync(
                OrderProcessState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderProcessState>.FromState(state));
        }

        [Workflow("process-order-with-guards")]
        public static partial class ProcessOrderWithGuardsWorkflow
        {
            public static WorkflowDefinition<OrderProcessState> Definition => Workflow<OrderProcessState>
                .Create("process-order-with-guards")
                .StartWith<ValidateOrder>()
                .Then<ProcessItems>()
                    .ValidateState(state => state.ItemCount > 0, "Order must have items")
                .Then<ChargePayment>()
                    .ValidateState(state => state.Total > 0 && state.IsVerified, "Total must be positive and verified")
                .Finally<ShipOrder>();
        }
        """;

    // =============================================================================
    // Fork/Join Workflow Test Sources (Milestone 15 - Parallel Execution)
    // =============================================================================

    /// <summary>
    /// A workflow definition with a basic fork/join for parallel execution.
    /// </summary>
    public const string WorkflowWithFork = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record OrderState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public bool PaymentProcessed { get; init; }
            public bool InventoryReserved { get; init; }
        }

        public class ValidateOrder : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class ProcessPayment : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state with { PaymentProcessed = true }));
        }

        public class ReserveInventory : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state with { InventoryReserved = true }));
        }

        public class SynthesizeResults : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class SendConfirmation : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        [Workflow("parallel-order")]
        public static partial class ParallelOrderWorkflow
        {
            public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
                .Create("parallel-order")
                .StartWith<ValidateOrder>()
                .Fork(
                    path => path.Then<ProcessPayment>(),
                    path => path.Then<ReserveInventory>())
                .Join<SynthesizeResults>()
                .Finally<SendConfirmation>();
        }
        """;

    /// <summary>
    /// A workflow definition with a three-path fork for parallel execution.
    /// </summary>
    public const string WorkflowWithThreePathFork = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record NotificationState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public bool EmailSent { get; init; }
            public bool SmsSent { get; init; }
            public bool PushSent { get; init; }
        }

        public class PrepareNotification : IWorkflowStep<NotificationState>
        {
            public Task<StepResult<NotificationState>> ExecuteAsync(
                NotificationState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<NotificationState>.FromState(state));
        }

        public class SendEmail : IWorkflowStep<NotificationState>
        {
            public Task<StepResult<NotificationState>> ExecuteAsync(
                NotificationState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<NotificationState>.FromState(state with { EmailSent = true }));
        }

        public class SendSms : IWorkflowStep<NotificationState>
        {
            public Task<StepResult<NotificationState>> ExecuteAsync(
                NotificationState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<NotificationState>.FromState(state with { SmsSent = true }));
        }

        public class SendPush : IWorkflowStep<NotificationState>
        {
            public Task<StepResult<NotificationState>> ExecuteAsync(
                NotificationState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<NotificationState>.FromState(state with { PushSent = true }));
        }

        public class RecordDelivery : IWorkflowStep<NotificationState>
        {
            public Task<StepResult<NotificationState>> ExecuteAsync(
                NotificationState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<NotificationState>.FromState(state));
        }

        public class FinishNotification : IWorkflowStep<NotificationState>
        {
            public Task<StepResult<NotificationState>> ExecuteAsync(
                NotificationState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<NotificationState>.FromState(state));
        }

        [Workflow("multi-channel-notification")]
        public static partial class MultiChannelNotificationWorkflow
        {
            public static WorkflowDefinition<NotificationState> Definition => Workflow<NotificationState>
                .Create("multi-channel-notification")
                .StartWith<PrepareNotification>()
                .Fork(
                    path => path.Then<SendEmail>(),
                    path => path.Then<SendSms>(),
                    path => path.Then<SendPush>())
                .Join<RecordDelivery>()
                .Finally<FinishNotification>();
        }
        """;

    /// <summary>
    /// A workflow definition with an OnFailure handler (terminal).
    /// </summary>
    public const string WorkflowWithOnFailure = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record FailureTestState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public string ErrorMessage { get; init; } = "";
        }

        public class ValidateInput : IWorkflowStep<FailureTestState>
        {
            public Task<StepResult<FailureTestState>> ExecuteAsync(
                FailureTestState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FailureTestState>.FromState(state));
        }

        public class ProcessData : IWorkflowStep<FailureTestState>
        {
            public Task<StepResult<FailureTestState>> ExecuteAsync(
                FailureTestState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FailureTestState>.FromState(state));
        }

        public class SaveResult : IWorkflowStep<FailureTestState>
        {
            public Task<StepResult<FailureTestState>> ExecuteAsync(
                FailureTestState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FailureTestState>.FromState(state));
        }

        public class LogFailure : IWorkflowStep<FailureTestState>
        {
            public Task<StepResult<FailureTestState>> ExecuteAsync(
                FailureTestState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FailureTestState>.FromState(state));
        }

        public class NotifyAdmin : IWorkflowStep<FailureTestState>
        {
            public Task<StepResult<FailureTestState>> ExecuteAsync(
                FailureTestState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FailureTestState>.FromState(state));
        }

        [Workflow("failure-handling-test")]
        public static partial class FailureHandlingTestWorkflow
        {
            public static WorkflowDefinition<FailureTestState> Definition => Workflow<FailureTestState>
                .Create("failure-handling-test")
                .StartWith<ValidateInput>()
                .Then<ProcessData>()
                .Finally<SaveResult>()
                .OnFailure(f => f
                    .Then<LogFailure>()
                    .Then<NotifyAdmin>()
                    .Complete());
        }
        """;

    /// <summary>
    /// A workflow definition with RepeatUntil followed by Branch - testing the loop-branch interaction.
    /// </summary>
    public const string WorkflowWithLoopThenBranch = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum WorkflowOutcome
        {
            Success,
            NeedsEscalation,
            Failed
        }

        public record LoopBranchState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public bool AllTasksComplete { get; init; }
            public WorkflowOutcome Outcome { get; init; }
        }

        public class InitializeStep : IWorkflowStep<LoopBranchState>
        {
            public Task<StepResult<LoopBranchState>> ExecuteAsync(
                LoopBranchState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoopBranchState>.FromState(state));
        }

        public class SelectTaskStep : IWorkflowStep<LoopBranchState>
        {
            public Task<StepResult<LoopBranchState>> ExecuteAsync(
                LoopBranchState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoopBranchState>.FromState(state));
        }

        public class ExecuteTaskStep : IWorkflowStep<LoopBranchState>
        {
            public Task<StepResult<LoopBranchState>> ExecuteAsync(
                LoopBranchState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoopBranchState>.FromState(state));
        }

        public class CompleteStep : IWorkflowStep<LoopBranchState>
        {
            public Task<StepResult<LoopBranchState>> ExecuteAsync(
                LoopBranchState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoopBranchState>.FromState(state));
        }

        public class EscalateStep : IWorkflowStep<LoopBranchState>
        {
            public Task<StepResult<LoopBranchState>> ExecuteAsync(
                LoopBranchState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoopBranchState>.FromState(state));
        }

        public class FailedStep : IWorkflowStep<LoopBranchState>
        {
            public Task<StepResult<LoopBranchState>> ExecuteAsync(
                LoopBranchState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoopBranchState>.FromState(state));
        }

        [Workflow("loop-then-branch")]
        public static partial class LoopThenBranchWorkflow
        {
            private static WorkflowOutcome DetermineOutcome(LoopBranchState state) => state.Outcome;

            public static WorkflowDefinition<LoopBranchState> Definition => Workflow<LoopBranchState>
                .Create("loop-then-branch")
                .StartWith<InitializeStep>()
                .RepeatUntil(
                    state => state.AllTasksComplete,
                    "TaskLoop",
                    loop => loop
                        .Then<SelectTaskStep>()
                        .Then<ExecuteTaskStep>(),
                    maxIterations: 10)
                .Branch(
                    DetermineOutcome,
                    BranchCase<LoopBranchState, WorkflowOutcome>.When(
                        WorkflowOutcome.Success,
                        path => path.Then<CompleteStep>().Complete()),
                    BranchCase<LoopBranchState, WorkflowOutcome>.When(
                        WorkflowOutcome.NeedsEscalation,
                        path => path.Then<EscalateStep>().Complete()),
                    BranchCase<LoopBranchState, WorkflowOutcome>.Otherwise(
                        path => path.Then<FailedStep>().Complete()))
                .Finally<CompleteStep>();
        }
        """;

    /// <summary>
    /// A workflow with fork paths using the same step type with different instance names.
    /// This tests that the generator uses EffectiveName for phases.
    /// </summary>
    public const string WorkflowWithInstanceNames = """
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

        public class PrepareDataStep : IWorkflowStep<AnalysisState>
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

        [Workflow("multi-analysis")]
        public static partial class MultiAnalysisWorkflow
        {
            public static WorkflowDefinition<AnalysisState> Definition => Workflow<AnalysisState>
                .Create("multi-analysis")
                .StartWith<PrepareDataStep>()
                .Fork(
                    path => path.Then<AnalyzeStep>("Technical"),
                    path => path.Then<AnalyzeStep>("Fundamental"))
                .Join<SynthesizeStep>()
                .Finally<CompleteStep>();
        }
        """;

    // =============================================================================
    // Diagnostic Test Sources - Invalid Workflow Patterns
    // =============================================================================

    /// <summary>
    /// A state with multiple collection properties (for metadata caching performance test).
    /// </summary>
    public const string StateWithManyCollections = """
        using System.Collections.Generic;
        using Strategos.Attributes;

        namespace TestNamespace;

        [WorkflowState]
        public record MultiCollectionState
        {
            public string Name { get; init; } = "";

            [Append]
            public IReadOnlyList<string> Items1 { get; init; } = [];

            [Append]
            public IReadOnlyList<int> Items2 { get; init; } = [];

            [Append]
            public IReadOnlyList<decimal> Items3 { get; init; } = [];

            [Merge]
            public IReadOnlyDictionary<string, string> Dict1 { get; init; } = new Dictionary<string, string>();

            [Merge]
            public IReadOnlyDictionary<string, int> Dict2 { get; init; } = new Dictionary<string, int>();
        }
        """;

    /// <summary>
    /// A workflow with Fork but missing Join - should trigger AGWF012.
    /// </summary>
    public const string WorkflowForkWithoutJoin = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record ForkState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class StartStep : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(
                ForkState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(state));
        }

        public class PathAStep : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(
                ForkState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(state));
        }

        public class PathBStep : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(
                ForkState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(state));
        }

        public class EndStep : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(
                ForkState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(state));
        }

        [Workflow("fork-no-join")]
        public static partial class ForkNoJoinWorkflow
        {
            public static WorkflowDefinition<ForkState> Definition => Workflow<ForkState>
                .Create("fork-no-join")
                .StartWith<StartStep>()
                .Fork(
                    path => path.Then<PathAStep>(),
                    path => path.Then<PathBStep>())
                .Then<EndStep>()  // ERROR: Fork followed by Then instead of Join
                .Finally<EndStep>();
        }
        """;

    /// <summary>
    /// A workflow with an empty loop body - should trigger AGWF014 error.
    /// </summary>
    public const string WorkflowEmptyLoopBody = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record LoopState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public bool Done { get; init; }
        }

        public class StartStep : IWorkflowStep<LoopState>
        {
            public Task<StepResult<LoopState>> ExecuteAsync(
                LoopState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoopState>.FromState(state));
        }

        public class EndStep : IWorkflowStep<LoopState>
        {
            public Task<StepResult<LoopState>> ExecuteAsync(
                LoopState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoopState>.FromState(state));
        }

        [Workflow("empty-loop")]
        public static partial class EmptyLoopWorkflow
        {
            public static WorkflowDefinition<LoopState> Definition => Workflow<LoopState>
                .Create("empty-loop")
                .StartWith<StartStep>()
                .RepeatUntil(s => s.Done, "process", loop => { })  // ERROR: Empty loop body
                .Finally<EndStep>();
        }
        """;

    /// <summary>
    /// A workflow missing the Finally step - should trigger AGWF010 warning.
    /// </summary>
    public const string WorkflowMissingFinally = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record NoFinallyState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class StartStep : IWorkflowStep<NoFinallyState>
        {
            public Task<StepResult<NoFinallyState>> ExecuteAsync(
                NoFinallyState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<NoFinallyState>.FromState(state));
        }

        public class MiddleStep : IWorkflowStep<NoFinallyState>
        {
            public Task<StepResult<NoFinallyState>> ExecuteAsync(
                NoFinallyState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<NoFinallyState>.FromState(state));
        }

        [Workflow("no-finally")]
        public static partial class NoFinallyWorkflow
        {
            public static WorkflowDefinition<NoFinallyState> Definition => Workflow<NoFinallyState>
                .Create("no-finally")
                .StartWith<StartStep>()
                .Then<MiddleStep>();  // WARNING: No Finally step
        }
        """;
}
