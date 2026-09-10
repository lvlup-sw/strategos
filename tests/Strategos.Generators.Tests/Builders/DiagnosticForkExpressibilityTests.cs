// -----------------------------------------------------------------------
// <copyright file="DiagnosticForkExpressibilityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Builders;

/// <summary>
/// Compilation-refusal tests for the <c>AllowDiagnosticFork</c> staged builder surface
/// (DR-7, #151). The staged (make-illegal-states-unrepresentable) design makes a
/// diagnostic-fork edge <b>inexpressible</b> without declaring at least one anchor and
/// at least one permitted trigger: the compiler refuses any chain that skips a stage,
/// because each stage exposes only the next required step.
/// </summary>
/// <remarks>
/// <para>
/// These drive real workflow snippets through the shared Roslyn harness
/// (<see cref="GeneratorTestHelper.GetCompilationDiagnostics"/>), which compiles the
/// source together with the generator output and returns the compiler diagnostics. A
/// chain that skips the required trigger (or anchor) surfaces a <c>CS1061</c>
/// "no such member" error at the skipped stage — a genuine compile-time refusal that
/// needs no analyzer. The conformant baseline compiles the same fork chain WITHOUT that
/// refusal, proving the error is caused by the omission, not by an unrelated defect.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public sealed class DiagnosticForkExpressibilityTests
{
    private const string MissingMemberId = "CS1061";

    /// <summary>
    /// A conformant, fully-specified diagnostic fork compiles without any missing-member
    /// error on the <c>PermitTrigger</c> / <c>WithCompensationSeed</c> stage members.
    /// This is the differential baseline: the refusal cases below differ ONLY in the
    /// omitted stage.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConformantDiagnosticFork_HasNoStageMissingMemberError()
    {
        var source = WorkflowWithForkChain("""
            .AllowDiagnosticFork(fork => fork
                .Anchor("Assess")
                .PermitTrigger(Strategos.Contracts.Generated.ForkTrigger.RatificationFailure, "provisionalStampEventId")
                .WithCompensationSeed("RollbackAssessment")
                .MaxForks(3))
            """);

        var diagnostics = GeneratorTestHelper.GetCompilationDiagnostics(source);

        var stageMissingMember = StageMissingMemberErrors(diagnostics);

        await Assert.That(stageMissingMember).IsEmpty()
            .Because("a fully-specified diagnostic fork must compile without a missing-member error on its stage members");
    }

    /// <summary>
    /// Omitting <c>PermitTrigger</c> — reaching for the compensation seed straight off
    /// the anchor's trigger stage — is a compile error: the trigger stage exposes only
    /// <c>PermitTrigger</c>, so <c>WithCompensationSeed</c> is a missing member (CS1061).
    /// The construct is inexpressible without a permitted trigger (DR-7).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkWithoutTrigger_RefusesToCompile()
    {
        var source = WorkflowWithForkChain("""
            .AllowDiagnosticFork(fork => fork
                .Anchor("Assess")
                .WithCompensationSeed("RollbackAssessment")
                .MaxForks(3))
            """);

        var diagnostics = GeneratorTestHelper.GetCompilationDiagnostics(source);

        var refusal = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Where(d => string.Equals(d.Id, MissingMemberId, StringComparison.Ordinal))
            .Where(d => d.GetMessage().Contains("WithCompensationSeed", StringComparison.Ordinal))
            .ToList();

        await Assert.That(refusal).IsNotEmpty()
            .Because("skipping PermitTrigger must fail to compile — the construct is inexpressible without a permitted trigger");
    }

    /// <summary>
    /// Omitting <c>Anchor</c> — reaching for a permitted trigger before naming where the
    /// workflow may fork — is a compile error: the entry stage exposes only
    /// <c>Anchor</c>, so <c>PermitTrigger</c> is a missing member (CS1061).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkWithoutAnchor_RefusesToCompile()
    {
        var source = WorkflowWithForkChain("""
            .AllowDiagnosticFork(fork => fork
                .PermitTrigger(Strategos.Contracts.Generated.ForkTrigger.RatificationFailure, "provisionalStampEventId")
                .WithCompensationSeed("RollbackAssessment")
                .MaxForks(3))
            """);

        var diagnostics = GeneratorTestHelper.GetCompilationDiagnostics(source);

        var refusal = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Where(d => string.Equals(d.Id, MissingMemberId, StringComparison.Ordinal))
            .Where(d => d.GetMessage().Contains("PermitTrigger", StringComparison.Ordinal))
            .ToList();

        await Assert.That(refusal).IsNotEmpty()
            .Because("skipping Anchor must fail to compile — a fork edge has nowhere to fork without an anchor");
    }

    /// <summary>
    /// The CS1061 missing-member errors that reference a diagnostic-fork stage member —
    /// used to prove the conformant baseline is clean of the very refusal the omission
    /// cases trigger.
    /// </summary>
    private static List<Diagnostic> StageMissingMemberErrors(IEnumerable<Diagnostic> diagnostics) =>
        diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Where(d => string.Equals(d.Id, MissingMemberId, StringComparison.Ordinal))
            .Where(d =>
            {
                var message = d.GetMessage();
                return message.Contains("PermitTrigger", StringComparison.Ordinal)
                    || message.Contains("WithCompensationSeed", StringComparison.Ordinal)
                    || message.Contains("Anchor", StringComparison.Ordinal)
                    || message.Contains("MaxForks", StringComparison.Ordinal);
            })
            .ToList();

    /// <summary>
    /// Builds a compilable single-step workflow whose <c>Definition</c> carries the
    /// supplied <c>AllowDiagnosticFork</c> chain fragment between <c>StartWith</c> and
    /// <c>Finally</c>.
    /// </summary>
    private static string WorkflowWithForkChain(string forkChain) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class IntakeClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class SettleClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("diagnostic-fork-claim")]
        public static partial class DiagnosticForkClaimWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("diagnostic-fork-claim")
                .StartWith<IntakeClaim>()
                {{forkChain}}
                .Finally<SettleClaim>();
        }
        """;
}
