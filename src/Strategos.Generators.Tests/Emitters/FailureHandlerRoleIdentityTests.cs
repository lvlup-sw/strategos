// =============================================================================
// <copyright file="FailureHandlerRoleIdentityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Guards the distinct forward and recovery identities of a CLR step type reused by OnFailure.
/// </summary>
[Property("Category", "Unit")]
public class FailureHandlerRoleIdentityTests
{
    /// <summary>
    /// A type reused by the main flow and OnFailure remains reachable in its forward position.
    /// </summary>
    [Test]
    public async Task SharedForwardAndRecoveryType_MainFlowCompletionStillChainsForward()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(SharedForwardAndRecoverySource);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");
        var completedHandler = ExtractMember(saga, "SharedStepCompleted evt,");

        await Assert.That(completedHandler).Contains("new StartContinueStepCommand(WorkflowId)");
        await Assert.That(completedHandler).DoesNotContain("MarkCompleted();");
    }

    /// <summary>
    /// A type reused by a fork path and OnFailure retains the forward worker failure policy.
    /// </summary>
    [Test]
    public async Task SharedForkAndRecoveryType_ForwardWorkerStillPublishesFailureTrigger()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(SharedForkAndRecoverySource);
        var handlers = GeneratorTestHelper.GetGeneratedSource(result, "Handlers.g.cs");
        var forward = ExtractRegion(
            handlers,
            "class SharedStepHandler",
            "class FailureHandler_SharedForkRecovery_FailureHandler0_SharedStepHandler");

        await Assert.That(forward).Contains(
            "PublishAsync(new TriggerSharedForkRecoveryFailureHandlerCommand");
    }

    /// <summary>
    /// Distinct instance names on repeated recovery uses produce distinct dedicated artifacts
    /// while both workers still inject the shared CLR step type.
    /// </summary>
    [Test]
    public async Task RepeatedRecoveryType_WithInstanceNames_EmitsUniqueCompilingArtifacts()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(RepeatedRecoveryTypeSource);
        var commands = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");
        var events = GeneratorTestHelper.GetGeneratedSource(result, "Events.g.cs");
        var handlers = GeneratorTestHelper.GetGeneratedSource(result, "Handlers.g.cs");
        var phase = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        await Assert.That(commands).Contains("StartFailureHandler_RepeatedRecovery_FailureHandler0_PersistFailureCommand");
        await Assert.That(commands).Contains("StartFailureHandler_RepeatedRecovery_FailureHandler0_NotifyFailureCommand");
        await Assert.That(commands).Contains("ExecuteFailureHandler_RepeatedRecovery_FailureHandler0_PersistFailureWorkerCommand");
        await Assert.That(commands).Contains("ExecuteFailureHandler_RepeatedRecovery_FailureHandler0_NotifyFailureWorkerCommand");
        await Assert.That(events).Contains("FailureHandler_RepeatedRecovery_FailureHandler0_PersistFailureCompleted");
        await Assert.That(events).Contains("FailureHandler_RepeatedRecovery_FailureHandler0_NotifyFailureCompleted");
        await Assert.That(handlers).Contains("class FailureHandler_RepeatedRecovery_FailureHandler0_PersistFailureHandler(");
        await Assert.That(handlers).Contains("class FailureHandler_RepeatedRecovery_FailureHandler0_NotifyFailureHandler(");
        await Assert.That(phase).Contains("PersistFailure,");
        await Assert.That(phase).Contains("NotifyFailure,");
        var firstDedicatedHandler = handlers.IndexOf(
            "class FailureHandler_RepeatedRecovery_FailureHandler0_PersistFailureHandler(",
            StringComparison.Ordinal);
        var dedicatedHandlers = handlers.Substring(firstDedicatedHandler);
        var sharedStepInjections = dedicatedHandlers.Split(
            ["    RecoveryStep step,"],
            StringSplitOptions.None).Length - 1;
        await Assert.That(sharedStepInjections).IsEqualTo(2);
        var recoveryOnlyForwardArtifact = ExtractRegion(
            handlers,
            "class RecoveryStepHandler",
            "class FailureHandler_RepeatedRecovery_FailureHandler0_PersistFailureHandler");
        await Assert.That(recoveryOnlyForwardArtifact).DoesNotContain(
            "PublishAsync(new TriggerRepeatedRecoveryFailureHandlerCommand");
        await Assert.That(saga).Contains("new StartFailureHandler_RepeatedRecovery_FailureHandler0_NotifyFailureCommand(WorkflowId)");
    }

    /// <summary>
    /// Repeating the same unnamed recovery phase is rejected before duplicate CLR artifacts
    /// can escape into the generated compilation.
    /// </summary>
    [Test]
    public async Task RepeatedRecoveryType_WithoutInstanceNames_ReportsDuplicatePhase()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(
            DuplicateRecoveryTypeSource,
            "AGWF003");

        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == "AGWF003")).IsTrue();
    }

    private static string ExtractMember(string source, string signatureFragment)
    {
        var start = source.IndexOf(signatureFragment, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var end = source.IndexOf("    /// <summary>", start + signatureFragment.Length, StringComparison.Ordinal);
        return end < 0 ? source.Substring(start) : source.Substring(start, end - start);
    }

    private static string ExtractRegion(string source, string startFragment, string endFragment)
    {
        var start = source.IndexOf(startFragment, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var end = source.IndexOf(endFragment, start + startFragment.Length, StringComparison.Ordinal);
        return end < 0 ? source.Substring(start) : source.Substring(start, end - start);
    }

    private const string SharedForwardAndRecoverySource = CommonPreamble + """
        [Workflow("shared-forward-recovery")]
        public static partial class SharedForwardRecoveryWorkflowDefinition
        {
            public static WorkflowDefinition<TestState> Definition => Workflow<TestState>
                .Create("shared-forward-recovery")
                .StartWith<SharedStep>()
                .Then<ContinueStep>()
                .OnFailure(failure => failure.Then<SharedStep>().Complete())
                .Finally<CompleteStep>();
        }
        """;

    private const string RepeatedRecoveryTypeSource = CommonPreamble + """
        [Workflow("repeated-recovery")]
        public static partial class RepeatedRecoveryWorkflowDefinition
        {
            public static WorkflowDefinition<TestState> Definition => Workflow<TestState>
                .Create("repeated-recovery")
                .StartWith<StartStep>()
                .OnFailure(failure => failure
                    .Then<RecoveryStep>("PersistFailure")
                    .Then<RecoveryStep>("NotifyFailure")
                    .Complete())
                .Finally<CompleteStep>();
        }
        """;

    private const string DuplicateRecoveryTypeSource = CommonPreamble + """
        [Workflow("duplicate-recovery")]
        public static partial class DuplicateRecoveryWorkflowDefinition
        {
            public static WorkflowDefinition<TestState> Definition => Workflow<TestState>
                .Create("duplicate-recovery")
                .StartWith<StartStep>()
                .OnFailure(failure => failure
                    .Then<RecoveryStep>()
                    .Then<RecoveryStep>()
                    .Complete())
                .Finally<CompleteStep>();
        }
        """;

    private const string SharedForkAndRecoverySource = CommonPreamble + """
        [Workflow("shared-fork-recovery")]
        public static partial class SharedForkRecoveryWorkflowDefinition
        {
            public static WorkflowDefinition<TestState> Definition => Workflow<TestState>
                .Create("shared-fork-recovery")
                .StartWith<StartStep>()
                .Fork(
                    path => path.Then<SharedStep>(),
                    path => path.Then<OtherStep>())
                .Join<JoinStep>()
                .OnFailure(failure => failure.Then<SharedStep>().Complete())
                .Finally<CompleteStep>();
        }
        """;

    private const string CommonPreamble = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        [WorkflowState]
        public sealed partial record TestState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class StartStep : TestStep;
        public class SharedStep : TestStep;
        public class ContinueStep : TestStep;
        public class RecoveryStep : TestStep;
        public class OtherStep : TestStep;
        public class JoinStep : TestStep;
        public class CompleteStep : TestStep;

        public abstract class TestStep : IWorkflowStep<TestState>
        {
            public Task<StepResult<TestState>> ExecuteAsync(
                TestState state,
                StepContext context,
                CancellationToken ct) => Task.FromResult(StepResult<TestState>.FromState(state));
        }

        """;
}
