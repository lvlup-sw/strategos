// -----------------------------------------------------------------------
// <copyright file="SagaEmitterIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Integration tests for the SagaEmitter functionality.
/// </summary>
[Property("Category", "Integration")]
public class SagaEmitterIntegrationTests
{
    // =============================================================================
    // A. Saga File Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces a Saga file for linear workflows.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_GeneratesSagaFile()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert
        await Assert.That(sagaSource).IsNotNull().And.IsNotEmpty();
    }

    // =============================================================================
    // B. Saga Structure Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the saga extends Wolverine's Saga base class.
    /// </summary>
    [Test]
    public async Task Generator_Saga_ExtendsSagaBaseClass()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains(": Saga");
    }

    /// <summary>
    /// Verifies that the saga has the SagaIdentity attribute on WorkflowId.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HasSagaIdentityAttribute()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("[SagaIdentity]");
    }

    /// <summary>
    /// Verifies that the saga has the Identity attribute for Marten.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HasMartenIdentityAttribute()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert
        // Fully qualified [JasperFx.Identity] (the Marten document-identity
        // attribute) to avoid CS0616: the short [Identity] form collides with
        // the Strategos.Identity namespace in consumers that reference
        // Strategos.Identity.Abstractions.
        await Assert.That(sagaSource).Contains("[JasperFx.Identity]");
    }

    /// <summary>
    /// Verifies that the saga has the Version attribute for optimistic concurrency.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HasVersionAttribute()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("[Version]");
    }

    /// <summary>
    /// Verifies that the saga has a Phase property using the generated enum.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HasPhaseProperty()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("ProcessOrderPhase Phase");
    }

    // =============================================================================
    // C. Start Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the saga has a static Start method.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HasStaticStartMethod()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("public static");
        await Assert.That(sagaSource).Contains("Start(");
    }

    /// <summary>
    /// Verifies that the Start method accepts the StartCommand.
    /// </summary>
    [Test]
    public async Task Generator_Saga_StartMethodAcceptsStartCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("StartProcessOrderCommand command");
    }

    /// <summary>
    /// Verifies that the Start method returns a tuple with saga and cascade command.
    /// </summary>
    [Test]
    public async Task Generator_Saga_StartMethodReturnsTuple()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - Should return tuple with saga and first step's start command (two-phase pattern)
        await Assert.That(sagaSource).Contains("(ProcessOrderSaga Saga, StartValidateOrderCommand");
    }

    // =============================================================================
    // D. Step Handler Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the saga has Handle methods for step completed events.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HasHandleMethodForFirstStep()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - Handle method exists for first step's completed event (with ILogger method injection)
        await Assert.That(sagaSource).Contains("ValidateOrderCompleted evt,");
        await Assert.That(sagaSource).Contains("ILogger<ProcessOrderSaga> logger)");
    }

    /// <summary>
    /// Verifies that the middle step handler returns the next step command.
    /// </summary>
    [Test]
    public async Task Generator_Saga_MiddleStepHandlerReturnsNextCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - ValidateOrder completed handler should return StartProcessPayment command (two-phase)
        await Assert.That(sagaSource).Contains("StartProcessPaymentCommand");
    }

    /// <summary>
    /// Verifies that the final step handler calls MarkCompleted.
    /// </summary>
    [Test]
    public async Task Generator_Saga_FinalStepHandlerCallsMarkCompleted()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("MarkCompleted()");
    }

    // =============================================================================
    // E. NotFound Handler Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the saga has NotFound handlers for commands.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HasNotFoundHandlers()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("public static void NotFound(");
    }

    // =============================================================================
    // F. Versioning Tests
    // =============================================================================

    /// <summary>
    /// Verifies that versioned workflows generate versioned saga class names.
    /// </summary>
    [Test]
    public async Task Generator_VersionedWorkflow_GeneratesVersionedSagaName()
    {
        // Arrange
        const string versionedWorkflow = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record OrderStateV2 : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class ValidateOrderV2 : IWorkflowStep<OrderStateV2>
            {
                public Task<StepResult<OrderStateV2>> ExecuteAsync(
                    OrderStateV2 state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<OrderStateV2>.FromState(state));
            }

            public class ProcessOrderV2 : IWorkflowStep<OrderStateV2>
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
                    .Finally<ProcessOrderV2>();
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(versionedWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSagaV2.g.cs");

        // Assert - Version 2 should have V2 suffix
        await Assert.That(sagaSource).Contains("ProcessOrderSagaV2");
    }

    // =============================================================================
    // G. State Property Tests (Milestone 8b - Full Saga Integration)
    // =============================================================================

    /// <summary>
    /// Verifies that the saga has a State property with the correct state type.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HasStateProperty_WithCorrectType()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - Should have State property with correct type
        await Assert.That(sagaSource).Contains("OrderState State { get; set; }");
    }

    /// <summary>
    /// Verifies that the State property has a default initialization.
    /// </summary>
    [Test]
    public async Task Generator_Saga_StateProperty_HasDefaultInitialization()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - Should have default initialization
        await Assert.That(sagaSource).Contains("State { get; set; } = default!");
    }

    /// <summary>
    /// Verifies that the Start method initializes State from the command's InitialState.
    /// </summary>
    [Test]
    public async Task Generator_Saga_Start_InitializesStateFromCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - Start method should assign State from command
        await Assert.That(sagaSource).Contains("State = command.InitialState");
    }

    // =============================================================================
    // H. Two-Phase Handler Tests (Milestone 8b - Message Tripling)
    // =============================================================================

    /// <summary>
    /// Verifies that the Start method returns StartStepCommand instead of ExecuteCommand.
    /// </summary>
    [Test]
    public async Task Generator_Saga_StartMethod_ReturnsStartStepCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - Start should return StartValidateOrderCommand (not Execute)
        await Assert.That(sagaSource).Contains("StartValidateOrderCommand Command)");
    }

    /// <summary>
    /// Verifies that the saga has handlers for StartStep commands.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HasHandleStartStepMethod()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - Should have Handle method for StartStepCommand (with ILogger method injection)
        await Assert.That(sagaSource).Contains("Handle(");
        await Assert.That(sagaSource).Contains("StartValidateOrderCommand command,");
        await Assert.That(sagaSource).Contains("ILogger<ProcessOrderSaga> logger)");
    }

    /// <summary>
    /// Verifies that StartStep handler transitions the phase.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HandleStartStep_TransitionsPhase()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - Handler should set Phase
        await Assert.That(sagaSource).Contains("Phase = ProcessOrderPhase.ValidateOrder");
    }

    /// <summary>
    /// Verifies that StartStep handler returns ExecuteWorkerCommand with state.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HandleStartStep_ReturnsWorkerCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - Should return ExecuteWorkerCommand
        await Assert.That(sagaSource).Contains("ExecuteValidateOrderWorkerCommand");
    }

    /// <summary>
    /// Verifies that StartStep handler passes current state to worker command.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HandleStartStep_PassesCurrentState()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - Worker command should receive State
        await Assert.That(sagaSource).Contains("State)");
    }

    // =============================================================================
    // I. Reducer Integration Tests (Milestone 8b)
    // =============================================================================

    /// <summary>
    /// Verifies that step completion handler applies the reducer.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HandleCompleted_AppliesReducer()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - Should call reducer
        await Assert.That(sagaSource).Contains("OrderStateReducer.Reduce(");
    }

    /// <summary>
    /// Verifies that step completion handler assigns reduced state.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HandleCompleted_AssignsReducedState()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - Should assign result of reducer to State
        await Assert.That(sagaSource).Contains("State = OrderStateReducer.Reduce(State, evt.UpdatedState)");
    }

    /// <summary>
    /// Verifies that step completion handler returns StartNextStepCommand.
    /// </summary>
    [Test]
    public async Task Generator_Saga_HandleCompleted_ReturnsStartNextCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert - ValidateOrderCompleted should return StartProcessPaymentCommand
        await Assert.That(sagaSource).Contains("StartProcessPaymentCommand");
    }

    // =============================================================================
    // J. Loop Support Tests (Milestone 8c - Branch/Loop Saga Support)
    // =============================================================================

    /// <summary>
    /// Verifies that workflow with loop generates saga file.
    /// </summary>
    [Test]
    public async Task Generator_LoopWorkflow_GeneratesSagaFile()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementSaga.g.cs");

        // Assert
        await Assert.That(sagaSource).IsNotNull().And.IsNotEmpty();
    }

    /// <summary>
    /// Verifies that saga has iteration count property for each loop.
    /// </summary>
    [Test]
    public async Task Generator_LoopWorkflow_Saga_HasIterationCountProperty()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementSaga.g.cs");

        // Assert - Should have iteration count property for "Refinement" loop
        await Assert.That(sagaSource).Contains("RefinementIterationCount");
    }

    /// <summary>
    /// Verifies that iteration count property has correct type.
    /// </summary>
    [Test]
    public async Task Generator_LoopWorkflow_Saga_IterationCountPropertyIsInt()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementSaga.g.cs");

        // Assert - Should be int type
        await Assert.That(sagaSource).Contains("int RefinementIterationCount");
    }

    /// <summary>
    /// Verifies that last loop step handler has condition evaluation.
    /// </summary>
    [Test]
    public async Task Generator_LoopWorkflow_Saga_LastLoopStepHandler_HasConditionCheck()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementSaga.g.cs");

        // Assert - RefineStepCompleted handler should check loop condition
        // The handler should reference the condition evaluation (via registry or generated method)
        await Assert.That(sagaSource).Contains("ShouldExitRefinementLoop");
    }

    /// <summary>
    /// Verifies that loop continuation increments iteration count.
    /// </summary>
    [Test]
    public async Task Generator_LoopWorkflow_Saga_LoopContinuation_IncrementsIterationCount()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementSaga.g.cs");

        // Assert - Should increment the iteration count
        await Assert.That(sagaSource).Contains("RefinementIterationCount++");
    }

    /// <summary>
    /// Verifies that loop continuation returns start command for first loop body step.
    /// </summary>
    [Test]
    public async Task Generator_LoopWorkflow_Saga_LoopContinuation_ReturnsFirstLoopStepCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementSaga.g.cs");

        // Assert - Continuation should return StartCritiqueStepCommand (first loop body step)
        await Assert.That(sagaSource).Contains("StartRefinement_CritiqueStepCommand");
    }

    /// <summary>
    /// Verifies that loop exit returns continuation step command.
    /// </summary>
    [Test]
    public async Task Generator_LoopWorkflow_Saga_LoopExit_ReturnsContinuationStepCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementSaga.g.cs");

        // Assert - Loop exit should return StartPublishResultCommand (continuation step)
        await Assert.That(sagaSource).Contains("StartPublishResultCommand");
    }

    /// <summary>
    /// Verifies that max iteration guard is present.
    /// </summary>
    [Test]
    public async Task Generator_LoopWorkflow_Saga_HasMaxIterationGuard()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementSaga.g.cs");

        // Assert - Should check against max iterations (5 in the test source)
        await Assert.That(sagaSource).Contains("RefinementIterationCount >= 5");
    }

    // =============================================================================
    // K. Branch Support Tests (Milestone 8c - Branch/Loop Saga Support)
    // =============================================================================

    /// <summary>
    /// Verifies that workflow with branch generates saga file.
    /// </summary>
    [Test]
    public async Task Generator_BranchWorkflow_GeneratesSagaFile()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithEnumBranch);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessClaimSaga.g.cs");

        // Assert
        await Assert.That(sagaSource).IsNotNull().And.IsNotEmpty();
    }

    /// <summary>
    /// Verifies that saga has branch routing handler for the step before branch.
    /// </summary>
    [Test]
    public async Task Generator_BranchWorkflow_Saga_HasBranchRoutingHandler()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithEnumBranch);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessClaimSaga.g.cs");

        // Assert - After ValidateClaim completes, should have branch routing logic
        // The handler should evaluate the discriminator (State.Type) and route to appropriate path
        await Assert.That(sagaSource).Contains("ValidateClaimCompleted");
    }

    /// <summary>
    /// Verifies that branch routing evaluates discriminator.
    /// </summary>
    [Test]
    public async Task Generator_BranchWorkflow_Saga_BranchRoutingEvaluatesDiscriminator()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithEnumBranch);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessClaimSaga.g.cs");

        // Assert - Should evaluate the discriminator (State.Type in this case)
        await Assert.That(sagaSource).Contains("State.Type");
    }

    /// <summary>
    /// Verifies that branch routing has case for each branch path.
    /// </summary>
    [Test]
    public async Task Generator_BranchWorkflow_Saga_HasCasesForEachPath()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithEnumBranch);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessClaimSaga.g.cs");

        // Assert - Should have cases for Auto and Home claim types
        await Assert.That(sagaSource).Contains("ClaimType.Auto");
        await Assert.That(sagaSource).Contains("ClaimType.Home");
    }

    /// <summary>
    /// Verifies that branch routing returns correct start command for each case.
    /// </summary>
    [Test]
    public async Task Generator_BranchWorkflow_Saga_CasesReturnCorrectStartCommands()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithEnumBranch);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessClaimSaga.g.cs");

        // Assert - Should return start commands for branch path steps
        await Assert.That(sagaSource).Contains("StartProcessAutoClaimCommand");
        await Assert.That(sagaSource).Contains("StartProcessHomeClaimCommand");
    }

    /// <summary>
    /// Verifies that branch paths complete to rejoin step.
    /// </summary>
    [Test]
    public async Task Generator_BranchWorkflow_Saga_BranchPathsLeadToRejoin()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithEnumBranch);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessClaimSaga.g.cs");

        // Assert - Branch path completions should lead to rejoin step (CompleteClaim)
        await Assert.That(sagaSource).Contains("StartCompleteClaimCommand");
    }

    /// <summary>
    /// Verifies that otherwise case is handled.
    /// </summary>
    [Test]
    public async Task Generator_BranchWorkflow_Saga_HasOtherwiseCase()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithEnumBranch);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessClaimSaga.g.cs");

        // Assert - Should have default/otherwise handling (ProcessLifeClaim in this workflow)
        await Assert.That(sagaSource).Contains("StartProcessLifeClaimCommand");
    }

    // =============================================================================
    // L. Validation Guard Tests (Milestone 9 - Guard Logic Injection)
    // =============================================================================

    /// <summary>
    /// Verifies that workflow with validation generates saga file.
    /// </summary>
    [Test]
    public async Task Generator_ValidationWorkflow_GeneratesSagaFile()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithValidation);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessPaymentSaga.g.cs");

        // Assert
        await Assert.That(sagaSource).IsNotNull().And.IsNotEmpty();
    }

    /// <summary>
    /// Verifies that step with validation has handler returning IEnumerable for yield support.
    /// </summary>
    [Test]
    public async Task Generator_ValidationWorkflow_Saga_HandleStartStep_ReturnsEnumerable()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithValidation);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessPaymentSaga.g.cs");

        // Assert - ProcessPayment has validation, so its StartStep handler should return IEnumerable<object>
        // and include ILogger method injection
        await Assert.That(sagaSource).Contains("IEnumerable<object> Handle(");
        await Assert.That(sagaSource).Contains("StartProcessPaymentCommand command,");
        await Assert.That(sagaSource).Contains("ILogger<ProcessPaymentSaga> logger)");
    }

    /// <summary>
    /// Verifies that step without validation has normal handler (no IEnumerable).
    /// </summary>
    [Test]
    public async Task Generator_ValidationWorkflow_Saga_HandleStartStep_WithoutValidation_ReturnsWorkerCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithValidation);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessPaymentSaga.g.cs");

        // Assert - ValidatePayment has no validation, should return normal worker command type
        // and include ILogger method injection
        await Assert.That(sagaSource).Contains("ExecuteValidatePaymentWorkerCommand Handle(");
        await Assert.That(sagaSource).Contains("StartValidatePaymentCommand command,");
        await Assert.That(sagaSource).Contains("ILogger<ProcessPaymentSaga> logger)");
    }

    /// <summary>
    /// Verifies that validation guard checks the predicate before dispatching.
    /// </summary>
    [Test]
    public async Task Generator_ValidationWorkflow_Saga_HasValidationGuard()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithValidation);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessPaymentSaga.g.cs");

        // Assert - Should have validation predicate check (Total > 0)
        await Assert.That(sagaSource).Contains("State.Total > 0");
    }

    /// <summary>
    /// Verifies that validation failure yields ValidationFailed event.
    /// </summary>
    [Test]
    public async Task Generator_ValidationWorkflow_Saga_ValidationFailure_YieldsValidationFailedEvent()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithValidation);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessPaymentSaga.g.cs");

        // Assert - Should yield ValidationFailed event on failure
        await Assert.That(sagaSource).Contains("yield return new ProcessPaymentValidationFailed");
    }

    /// <summary>
    /// Verifies that validation failure transitions to ValidationFailed phase.
    /// </summary>
    [Test]
    public async Task Generator_ValidationWorkflow_Saga_ValidationFailure_TransitionsPhase()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithValidation);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessPaymentSaga.g.cs");

        // Assert - Should transition to ValidationFailed phase
        await Assert.That(sagaSource).Contains("Phase = ProcessPaymentPhase.ValidationFailed");
    }

    /// <summary>
    /// Verifies that validation failure uses yield break (not exception).
    /// </summary>
    [Test]
    public async Task Generator_ValidationWorkflow_Saga_ValidationFailure_UsesYieldBreak()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithValidation);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessPaymentSaga.g.cs");

        // Assert - Should use yield break instead of throwing
        await Assert.That(sagaSource).Contains("yield break");
    }

    /// <summary>
    /// Verifies that validation success yields the worker command.
    /// </summary>
    [Test]
    public async Task Generator_ValidationWorkflow_Saga_ValidationSuccess_YieldsWorkerCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithValidation);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessPaymentSaga.g.cs");

        // Assert - On validation success, should yield the worker command
        await Assert.That(sagaSource).Contains("yield return new ExecuteProcessPaymentWorkerCommand");
    }

    /// <summary>
    /// Verifies that ValidationFailed event includes step name and error message.
    /// </summary>
    [Test]
    public async Task Generator_ValidationWorkflow_Saga_ValidationFailedEvent_IncludesStepNameAndMessage()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithValidation);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessPaymentSaga.g.cs");

        // Assert - ValidationFailed event should include step name and error message
        await Assert.That(sagaSource).Contains("\"ProcessPayment\"");
        await Assert.That(sagaSource).Contains("\"Total must be positive\"");
    }
}
