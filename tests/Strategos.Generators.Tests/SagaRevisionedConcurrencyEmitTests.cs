// -----------------------------------------------------------------------
// <copyright file="SagaRevisionedConcurrencyEmitTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Structural guard on the one property that decides whether a generated saga's
/// persistence call is concurrency-guarded: the saga type must satisfy the
/// <c>CanBeCastTo&lt;JasperFx.IRevisioned&gt;</c> test performed by
/// <c>Wolverine.Marten.Persistence.Sagas.MartenPersistenceFrameProvider.DetermineUpdateFrame</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>DetermineUpdateFrame</c> emits <c>UpdateSagaRevisionFrame</c> — i.e.
/// <c>documentSession.UpdateRevision(saga, expectedSagaRevision)</c> — only when the
/// saga type can be cast to <c>JasperFx.IRevisioned</c>. Otherwise it emits a plain
/// <c>documentSession.Update(saga)</c>, which leaves Marten's numeric revision guard
/// (<c>? = 0 OR mt_version &lt; ?</c>) at zero and therefore always passes: two
/// concurrent deliveries for one saga both commit and the loser's whole transition is
/// silently overwritten, with no exception raised and so no retry.
/// </para>
/// <para>
/// The prior emit satisfied Marten's document mapping with
/// <c>[Version] public new long Version</c>, a shadow over the base
/// <c>Wolverine.Saga.Version</c> (int). Marten's mapping was Numeric; Wolverine's
/// interface test still failed, because a shadow property does not implement an
/// interface. These assertions pin the interface, the absence of the shadow, and the
/// retry policy that keeps the now-throwing race live instead of dead-lettering it on
/// the first loss.
/// </para>
/// <para>
/// The fixtures are authored here rather than reused from <c>SourceTexts</c> so they
/// can go through <c>RunGeneratorWithValidInput</c>, which compiles both the fixture
/// and the generated output. That is what makes these assertions more than substring
/// checks: the emitted base list, the emitted <c>Configure</c> body and the emitted
/// usings all have to bind against the real Wolverine, Marten and JasperFx assemblies.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public class SagaRevisionedConcurrencyEmitTests
{
    /// <summary>A linear workflow: start, then, finally.</summary>
    private const string LinearSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace RevisionProbe.Linear;

        [WorkflowState]
        public record LinearState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class LinearStart : IWorkflowStep<LinearState>
        {
            public Task<StepResult<LinearState>> ExecuteAsync(
                LinearState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LinearState>.FromState(state));
        }

        public class LinearMiddle : IWorkflowStep<LinearState>
        {
            public Task<StepResult<LinearState>> ExecuteAsync(
                LinearState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LinearState>.FromState(state));
        }

        public class LinearEnd : IWorkflowStep<LinearState>
        {
            public Task<StepResult<LinearState>> ExecuteAsync(
                LinearState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LinearState>.FromState(state));
        }

        [Workflow("revision-linear")]
        public static partial class RevisionLinearWorkflowDefinition
        {
            public static WorkflowDefinition<LinearState> Definition => Workflow<LinearState>
                .Create("revision-linear")
                .StartWith<LinearStart>()
                .Then<LinearMiddle>()
                .Finally<LinearEnd>();
        }
        """;

    /// <summary>An enum-branched workflow: the saga carries branch handlers too.</summary>
    private const string BranchSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace RevisionProbe.Branch;

        public enum BranchKind { Left, Right }

        [WorkflowState]
        public record BranchState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public BranchKind Kind { get; init; }
        }

        public class BranchStart : IWorkflowStep<BranchState>
        {
            public Task<StepResult<BranchState>> ExecuteAsync(
                BranchState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<BranchState>.FromState(state));
        }

        public class BranchLeft : IWorkflowStep<BranchState>
        {
            public Task<StepResult<BranchState>> ExecuteAsync(
                BranchState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<BranchState>.FromState(state));
        }

        public class BranchRight : IWorkflowStep<BranchState>
        {
            public Task<StepResult<BranchState>> ExecuteAsync(
                BranchState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<BranchState>.FromState(state));
        }

        public class BranchEnd : IWorkflowStep<BranchState>
        {
            public Task<StepResult<BranchState>> ExecuteAsync(
                BranchState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<BranchState>.FromState(state));
        }

        [Workflow("revision-branch")]
        public static partial class RevisionBranchWorkflowDefinition
        {
            public static WorkflowDefinition<BranchState> Definition => Workflow<BranchState>
                .Create("revision-branch")
                .StartWith<BranchStart>()
                .Branch(
                    state => state.Kind,
                    BranchCase<BranchState, BranchKind>.When(BranchKind.Left, path => path.Then<BranchLeft>()),
                    BranchCase<BranchState, BranchKind>.Otherwise(path => path.Then<BranchRight>()))
                .Finally<BranchEnd>();
        }
        """;

    /// <summary>A forked workflow: the shape whose lanes actually race in the host.</summary>
    private const string ForkSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace RevisionProbe.Fork;

        [WorkflowState]
        public record ForkState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ForkStart : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(
                ForkState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(state));
        }

        public class ForkLaneA : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(
                ForkState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(state));
        }

        public class ForkLaneB : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(
                ForkState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(state));
        }

        public class ForkJoin : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(
                ForkState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(state));
        }

        public class ForkEnd : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(
                ForkState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(state));
        }

        [Workflow("revision-fork")]
        public static partial class RevisionForkWorkflowDefinition
        {
            public static WorkflowDefinition<ForkState> Definition => Workflow<ForkState>
                .Create("revision-fork")
                .StartWith<ForkStart>()
                .Fork(
                    path => path.Then<ForkLaneA>(),
                    path => path.Then<ForkLaneB>())
                .Join<ForkJoin>()
                .Finally<ForkEnd>();
        }
        """;

    /// <summary>
    /// The emitted <c>Configure(HandlerChain)</c> signature, matched exactly.
    /// </summary>
    private static readonly Regex ConfigureMethod = new(
        @"public static void Configure\(HandlerChain chain\)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The saga base list declares <c>JasperFx.IRevisioned</c>, fully qualified so no
    /// <c>using JasperFx;</c> is dragged into consumer compilations.
    /// </summary>
    /// <param name="source">The workflow fixture to lower.</param>
    /// <param name="sagaHintName">The generated saga's hint name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(LinearSource, "RevisionLinearSaga.g.cs")]
    [Arguments(BranchSource, "RevisionBranchSaga.g.cs")]
    [Arguments(ForkSource, "RevisionForkSaga.g.cs")]
    public async Task SagaEmitter_DeclaresIRevisioned_SoWolverineEmitsUpdateRevision(
        string source,
        string sagaHintName)
    {
        var sagaSource = EmitSaga(source, sagaHintName);

        await Assert.That(sagaSource)
            .Contains(": Saga, IPhaseAwareSaga, JasperFx.IRevisioned")
            .Because(
                "MartenPersistenceFrameProvider.DetermineUpdateFrame emits UpdateRevision only "
                + "for a saga type that can be cast to JasperFx.IRevisioned; without it the "
                + "emitted call is a plain Update and concurrent transitions are last-write-wins");

        await Assert.That(sagaSource)
            .DoesNotContain("using JasperFx;")
            .Because("the interface is written fully qualified so no JasperFx root namespace "
                + "is imported into consumer compilations");
    }

    /// <summary>
    /// No <c>Version</c> shadow survives: the interface is implemented by the
    /// inherited <c>Saga.Version</c> (int), which Marten's default VersionedPolicy
    /// maps to numeric revisions without any attribute.
    /// </summary>
    /// <param name="source">The workflow fixture to lower.</param>
    /// <param name="sagaHintName">The generated saga's hint name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(LinearSource, "RevisionLinearSaga.g.cs")]
    [Arguments(BranchSource, "RevisionBranchSaga.g.cs")]
    [Arguments(ForkSource, "RevisionForkSaga.g.cs")]
    public async Task SagaEmitter_EmitsNoVersionShadow_ThatWouldHideTheInterfaceMember(
        string source,
        string sagaHintName)
    {
        var sagaSource = EmitSaga(source, sagaHintName);

        await Assert.That(sagaSource)
            .DoesNotContain("new long Version")
            .Because("a `new` shadow hides the base property and does NOT implement the interface");

        await Assert.That(sagaSource)
            .DoesNotContain("[Version]")
            .Because("Marten's VersionedPolicy maps an IRevisioned document to numeric revisions "
                + "with no attribute, and Marten 9 throws on a [Version] int");
    }

    /// <summary>
    /// Every saga carries exactly one saga-level <c>Configure(HandlerChain)</c> that
    /// retries a lost concurrency race. Wolverine has no default rule for
    /// <c>JasperFx.ConcurrencyException</c>, so without this the first lost race
    /// dead-letters immediately.
    /// </summary>
    /// <param name="source">The workflow fixture to lower.</param>
    /// <param name="sagaHintName">The generated saga's hint name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(LinearSource, "RevisionLinearSaga.g.cs")]
    [Arguments(BranchSource, "RevisionBranchSaga.g.cs")]
    [Arguments(ForkSource, "RevisionForkSaga.g.cs")]
    public async Task SagaEmitter_EmitsExactlyOneConcurrencyRetryPolicy(
        string source,
        string sagaHintName)
    {
        var sagaSource = EmitSaga(source, sagaHintName);

        await Assert.That(sagaSource)
            .Contains("chain.OnException<JasperFx.ConcurrencyException>().RetryTimes(3);")
            .Because("an IRevisioned saga now THROWS on a lost race, and Wolverine's unmatched "
                + "exception path is MoveToErrorQueue — a dead letter on the first loss");

        // Wolverine binds one static Configure per handler type and calls it once per
        // message chain. Exactly one is both necessary (a missing one is a silent
        // dead-letter policy) and sufficient (no per-command dispatch is needed).
        await Assert.That(ConfigureMethod.Matches(sagaSource).Count)
            .IsEqualTo(1)
            .Because("one unconditional Configure covers every message chain of the saga");
    }

    /// <summary>
    /// The saga file imports the namespaces the emitted error policy needs, and no
    /// longer imports the namespace that existed only for the deleted attribute.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SagaEmitter_ImportsTheNamespacesTheConcurrencyPolicyNeeds()
    {
        var sagaSource = EmitSaga(LinearSource, "RevisionLinearSaga.g.cs");

        await Assert.That(sagaSource).Contains("using Wolverine.Runtime.Handlers;");
        await Assert.That(sagaSource).Contains("using Wolverine.ErrorHandling;");

        await Assert.That(sagaSource)
            .DoesNotContain("using Marten.Schema;")
            .Because("it was imported solely for the [Version] attribute that is now gone; "
                + "[SagaIdentity] is Wolverine.Persistence.Sagas and [JasperFx.Identity] is "
                + "written fully qualified");
    }

    /// <summary>
    /// Lowers a fixture through the validating harness — which compiles both the
    /// authored source and the generated output — and returns the saga emit.
    /// </summary>
    /// <param name="source">The workflow fixture to lower.</param>
    /// <param name="sagaHintName">The generated saga's hint name.</param>
    /// <returns>The generated saga source.</returns>
    private static string EmitSaga(string source, string sagaHintName)
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(source);
        return GeneratorTestHelper.GetGeneratedSource(result, sagaHintName);
    }
}
