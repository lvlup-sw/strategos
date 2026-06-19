// -----------------------------------------------------------------------
// <copyright file="InvariantGuardTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Standing structural regression guards for the generator's load-bearing
/// invariants. These execute by reflecting over the shipped
/// <c>Strategos.Generators</c> assembly, so they fail the build the moment a
/// future change erodes an invariant rather than at some distant integration
/// point.
/// </summary>
public class InvariantGuardTests
{
    private static Assembly GeneratorsAssembly => typeof(WorkflowIncrementalGenerator).Assembly;

    /// <summary>
    /// INV-6 (sealed-by-default), DR-10 step-resilience surface: the resilience
    /// IR models, the saga resilience-component emitters, and the resilience
    /// parse helpers introduced by the step-resilience epic (#135) must all be
    /// sealed. The IR records carry structural equality that the incremental
    /// generator pipeline relies on for cache-keying, and the emitters/helpers
    /// are leaf collaborators with no intended subclassing; a future edit that
    /// unseals one fails the build mechanically.
    /// </summary>
    /// <remarks>
    /// Resolved by name (not <c>typeof</c>) so the <c>internal</c> emitters and
    /// helpers are covered uniformly — the assembly exposes its internals to this
    /// test project, but name-resolution keeps the guard list flat and explicit.
    /// A C# <c>static class</c> compiles to <c>abstract sealed</c>, so the
    /// <see cref="Type.IsSealed"/> check covers the static helpers too.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InvariantGuard_ResilienceIrAndEmitterTypes_AreSealed()
    {
        var names = new[]
        {
            // Resilience IR models (Models/ResilienceModels.cs) — sealed records.
            "Strategos.Generators.Models.RetryModel",
            "Strategos.Generators.Models.TimeoutModel",
            "Strategos.Generators.Models.CompensationModel",
            "Strategos.Generators.Models.ConfidenceModel",

            // Multi-step / rejoining OnLowConfidence handler chain IR (G-4 / #139) — sealed record.
            "Strategos.Generators.Models.LowConfidenceHandlerChainModel",

            // New saga resilience-component emitters — sealed classes.
            "Strategos.Generators.Emitters.Saga.SagaTimeoutComponentEmitter",
            "Strategos.Generators.Emitters.Saga.SagaCompensationComponentEmitter",

            // New resilience parse helpers — sealed (static) classes.
            "Strategos.Generators.Helpers.ResilienceParser",
            "Strategos.Generators.Helpers.FailureHandlerExtractor",
        };

        var offenders = new List<string>();
        foreach (var name in names)
        {
            var type = GeneratorsAssembly.GetType(name);
            if (type is null)
            {
                offenders.Add($"{name} (type not found)");
                continue;
            }

            if (!type.IsInterface && !type.IsSealed)
            {
                offenders.Add($"{type.FullName} is not sealed");
            }
        }

        await Assert.That(offenders).IsEmpty();
    }

    /// <summary>
    /// INV-6 (immutable IR half), DR-10: the resilience IR models must expose no
    /// externally-settable instance property — every writable member must be
    /// <c>init</c>-only (positional-record params lower to init-only setters).
    /// A mutable IR member would let a downstream pipeline stage rewrite the IR
    /// after parse, breaking the deterministic parse → emit contract the
    /// incremental generator depends on.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InvariantGuard_ResilienceIrModels_HaveOnlyInitOnlyMembers()
    {
        var names = new[]
        {
            "Strategos.Generators.Models.RetryModel",
            "Strategos.Generators.Models.TimeoutModel",
            "Strategos.Generators.Models.CompensationModel",
            "Strategos.Generators.Models.ConfidenceModel",
            "Strategos.Generators.Models.LowConfidenceHandlerChainModel",
        };

        var offenders = new List<string>();
        foreach (var name in names)
        {
            var type = GeneratorsAssembly.GetType(name);
            if (type is null)
            {
                offenders.Add($"{name} (type not found)");
                continue;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var prop in type.GetProperties(flags).Where(p => p.DeclaringType == type))
            {
                var setter = prop.SetMethod;
                if (setter is null)
                {
                    continue;
                }

                // An init-only setter carries the IsExternalInit modreq on its
                // return parameter; a plain mutable setter does not.
                var isInitOnly = setter.ReturnParameter
                    .GetRequiredCustomModifiers()
                    .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");

                if (!isInitOnly)
                {
                    offenders.Add($"{type.FullName}.{prop.Name} has a mutable (non-init) setter");
                }
            }
        }

        await Assert.That(offenders).IsEmpty();
    }

    /// <summary>
    /// An EventSourced workflow that declares a failing step with a workflow-level
    /// <c>OnFailure</c> chain (drives the <c>StepFailed</c> audit event) and a
    /// confidence-gated step with an <c>OnLowConfidence</c> handler (drives the
    /// <c>LowConfidenceRouted</c> audit event).
    /// </summary>
    private const string EventSourcedAuditWorkflow = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Agents.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record AuditState : IEventSourcedState<AuditState>
        {
            public System.Guid WorkflowId { get; init; }
            public AuditState ApplyEvent(IProgressEvent evt) => this;
        }

        public class PrepareStep : IWorkflowStep<AuditState>
        {
            public Task<StepResult<AuditState>> ExecuteAsync(AuditState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<AuditState>.FromState(s));
        }

        public class GatedStep : IWorkflowStep<AuditState>
        {
            public Task<StepResult<AuditState>> ExecuteAsync(AuditState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<AuditState>.WithConfidence(s, 0.5));
        }

        public class ReviewStep : IWorkflowStep<AuditState>
        {
            public Task<StepResult<AuditState>> ExecuteAsync(AuditState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<AuditState>.FromState(s));
        }

        public class FailingStep : IWorkflowStep<AuditState>
        {
            public Task<StepResult<AuditState>> ExecuteAsync(AuditState s, StepContext c, CancellationToken ct)
                => throw new System.InvalidOperationException("always fails");
        }

        public class NotifyStep : IWorkflowStep<AuditState>
        {
            public Task<StepResult<AuditState>> ExecuteAsync(AuditState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<AuditState>.FromState(s));
        }

        public class FinishStep : IWorkflowStep<AuditState>
        {
            public Task<StepResult<AuditState>> ExecuteAsync(AuditState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<AuditState>.FromState(s));
        }

        [Workflow("audit-events", Persistence = PersistenceMode.EventSourced)]
        public static partial class AuditEventsWorkflow
        {
            public static WorkflowDefinition<AuditState> Definition => Workflow<AuditState>
                .Create("audit-events")
                .StartWith<PrepareStep>()
                .Then<GatedStep>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt.Then<ReviewStep>()))
                .Then<FailingStep>()
                .OnFailure(flow => flow.Then<NotifyStep>().Complete())
                .Finally<FinishStep>();
        }
        """;

    /// <summary>
    /// The SagaDocument (default-mode) counterpart of <see cref="EventSourcedAuditWorkflow"/>.
    /// The audit stream events apply only in EventSourced mode, so document mode must
    /// NOT emit them (byte-unchanged document-mode output).
    /// </summary>
    private const string DocumentModeAuditWorkflow = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        [WorkflowState]
        public record AuditState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public class PrepareStep : IWorkflowStep<AuditState>
        {
            public Task<StepResult<AuditState>> ExecuteAsync(AuditState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<AuditState>.FromState(s));
        }

        public class GatedStep : IWorkflowStep<AuditState>
        {
            public Task<StepResult<AuditState>> ExecuteAsync(AuditState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<AuditState>.WithConfidence(s, 0.5));
        }

        public class ReviewStep : IWorkflowStep<AuditState>
        {
            public Task<StepResult<AuditState>> ExecuteAsync(AuditState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<AuditState>.FromState(s));
        }

        public class FailingStep : IWorkflowStep<AuditState>
        {
            public Task<StepResult<AuditState>> ExecuteAsync(AuditState s, StepContext c, CancellationToken ct)
                => throw new System.InvalidOperationException("always fails");
        }

        public class NotifyStep : IWorkflowStep<AuditState>
        {
            public Task<StepResult<AuditState>> ExecuteAsync(AuditState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<AuditState>.FromState(s));
        }

        public class FinishStep : IWorkflowStep<AuditState>
        {
            public Task<StepResult<AuditState>> ExecuteAsync(AuditState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<AuditState>.FromState(s));
        }

        [Workflow("audit-events")]
        public static partial class AuditEventsWorkflow
        {
            public static WorkflowDefinition<AuditState> Definition => Workflow<AuditState>
                .Create("audit-events")
                .StartWith<PrepareStep>()
                .Then<GatedStep>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt.Then<ReviewStep>()))
                .Then<FailingStep>()
                .OnFailure(flow => flow.Then<NotifyStep>().Complete())
                .Finally<FinishStep>();
        }
        """;

    /// <summary>
    /// INV-6 (sealed-by-default) + init-only, #138 audit-event taxonomy (OQ#1): the
    /// generated <c>StepFailed</c> and <c>LowConfidenceRouted</c> audit STREAM events
    /// are emitted as <c>sealed partial record</c>s with positional (init-only)
    /// members. Positional record parameters lower to init-only setters, so asserting
    /// the <c>sealed partial record</c> declaration with the expected positional
    /// parameter list pins both the sealing and the init-only contract mechanically.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InvariantGuard_AuditStreamEvents_AreSealedInitOnlyRecords()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedAuditWorkflow);
        var eventsSource = GeneratorTestHelper.GetGeneratedSource(result, "AuditEventsEvents.g.cs");

        // StepFailed: sealed init-only record carrying the failed step + exception type/message.
        await Assert.That(eventsSource).Contains("public sealed partial record AuditEventsStepFailed(");
        await Assert.That(eventsSource).Contains("string FailedStepName,");
        await Assert.That(eventsSource).Contains("string? ExceptionType,");

        // LowConfidenceRouted: sealed init-only record carrying the step + score + threshold.
        await Assert.That(eventsSource).Contains("public sealed partial record AuditEventsLowConfidenceRouted(");
        await Assert.That(eventsSource).Contains("string StepName,");
        await Assert.That(eventsSource).Contains("double Confidence,");
        await Assert.That(eventsSource).Contains("double Threshold,");

        // Both implement the workflow's event marker interface (so the saga's
        // ApplyEvent(IProgressEvent) and the Marten stream recognize them).
        await Assert.That(eventsSource).Contains(") : IAuditEventsEvent;");
    }

    /// <summary>
    /// #138 byte-unchanged guard: the audit stream events apply only in EventSourced
    /// mode, so a SagaDocument-mode workflow must NOT emit them. This pins that the
    /// document-mode generated output is unchanged by the audit-event taxonomy.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InvariantGuard_DocumentMode_DoesNotEmitAuditStreamEvents()
    {
        var result = GeneratorTestHelper.RunGenerator(DocumentModeAuditWorkflow);
        var eventsSource = GeneratorTestHelper.GetGeneratedSource(result, "AuditEventsEvents.g.cs");

        await Assert.That(eventsSource).DoesNotContain("StepFailed");
        await Assert.That(eventsSource).DoesNotContain("LowConfidenceRouted");
    }
}
