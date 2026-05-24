// =============================================================================
// <copyright file="WorkflowCorpus.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Steps;
using Strategos.Tests.Fixtures;

namespace Strategos.Tests.FixtureExport;

/// <summary>
/// The #53 fixture corpus: real builder invocations spanning every combinator,
/// each produced via the fluent DSL (never hand-written JSON). Every case is
/// projected through <c>ToContract()</c> and serialized by the contracts
/// canonical serializer during fixture export.
/// </summary>
/// <remarks>
/// The 16 builder test files exercise each combinator; the corpus parameterizes
/// over step counts, instance names, and combinator arity to reach the ≥100
/// distinct cases the equivalence gate (T23) requires, tagged by the eight
/// combinator families: <c>startWith</c>, <c>then</c>, <c>branch</c>,
/// <c>repeatUntil</c>, <c>fork-join</c>, <c>awaitApproval</c>, <c>onFailure</c>,
/// and <c>config</c>.
/// </remarks>
internal static class WorkflowCorpus
{
    /// <summary>The eight combinator-coverage tags the corpus must span.</summary>
    public static readonly string[] Tags =
    [
        "startWith", "then", "branch", "repeatUntil",
        "fork-join", "awaitApproval", "onFailure", "config",
    ];

    private sealed class ManagerApprover;

    /// <summary>A single corpus case: a named, tagged, built workflow.</summary>
    public sealed record Case(string Tag, string Name, WorkflowDefinition<TestWorkflowState> Workflow);

    /// <summary>Builds every corpus case (≥100 across all eight tags).</summary>
    public static IReadOnlyList<Case> All()
    {
        var cases = new List<Case>();
        cases.AddRange(StartWithCases());
        cases.AddRange(ThenCases());
        cases.AddRange(BranchCases());
        cases.AddRange(RepeatUntilCases());
        cases.AddRange(ForkJoinCases());
        cases.AddRange(AwaitApprovalCases());
        cases.AddRange(OnFailureCases());
        cases.AddRange(ConfigCases());
        return cases;
    }

    // startWith — entry-step shapes, with and without instance names.
    private static IEnumerable<Case> StartWithCases()
    {
        for (var i = 0; i < 14; i++)
        {
            var n = i;
            yield return new Case("startWith", $"startWith-plain-{n:D2}",
                Workflow<TestWorkflowState>.Create($"sw-plain-{n}")
                    .StartWith<ValidateStep>()
                    .Finally<CompleteStep>());

            yield return new Case("startWith", $"startWith-named-{n:D2}",
                Workflow<TestWorkflowState>.Create($"sw-named-{n}")
                    .StartWith<ValidateStep>($"Entry{n}")
                    .Finally<CompleteStep>());
        }
    }

    // then — linear chains of increasing length.
    private static IEnumerable<Case> ThenCases()
    {
        for (var len = 1; len <= 13; len++)
        {
            var length = len;
            yield return new Case("then", $"then-chain-{length:D2}", BuildChain($"then-{length}", length));

            yield return new Case("then", $"then-named-{length:D2}",
                Workflow<TestWorkflowState>.Create($"then-named-{length}")
                    .StartWith<ValidateStep>()
                    .Then<ProcessStep>($"Process{length}")
                    .Then<NotifyStep>($"Notify{length}")
                    .Finally<CompleteStep>());
        }
    }

    // branch — conditional fan-out, varying path count.
    private static IEnumerable<Case> BranchCases()
    {
        for (var i = 0; i < 13; i++)
        {
            var n = i;
            yield return new Case("branch", $"branch-two-{n:D2}",
                Workflow<TestWorkflowState>.Create($"branch-two-{n}")
                    .StartWith<ValidateStep>()
                    .Branch(
                        s => s.ProcessingMode,
                        BranchCase<TestWorkflowState, ProcessingMode>.When(
                            ProcessingMode.Auto, p => p.Then<AutoProcessStep>()),
                        BranchCase<TestWorkflowState, ProcessingMode>.Otherwise(
                            p => p.Then<ManualProcessStep>()))
                    .Finally<CompleteStep>());

            yield return new Case("branch", $"branch-single-{n:D2}",
                Workflow<TestWorkflowState>.Create($"branch-single-{n}")
                    .StartWith<ValidateStep>()
                    .Branch(
                        s => s.ProcessingMode,
                        BranchCase<TestWorkflowState, ProcessingMode>.When(
                            ProcessingMode.Auto, p => p.Then<AutoProcessStep>().Then<NotifyStep>()))
                    .Finally<CompleteStep>());
        }
    }

    // repeatUntil — loop bodies of varying length / iteration caps.
    private static IEnumerable<Case> RepeatUntilCases()
    {
        for (var i = 0; i < 13; i++)
        {
            var n = i;
            var maxIter = (n % 9) + 2;
            yield return new Case("repeatUntil", $"loop-single-{n:D2}",
                Workflow<TestWorkflowState>.Create($"loop-single-{n}")
                    .StartWith<ValidateStep>()
                    .RepeatUntil(
                        s => s.QualityScore >= 0.9m,
                        $"Refine{n}",
                        loop => loop.Then<CritiqueStep>(),
                        maxIterations: maxIter)
                    .Finally<CompleteStep>());

            yield return new Case("repeatUntil", $"loop-multi-{n:D2}",
                Workflow<TestWorkflowState>.Create($"loop-multi-{n}")
                    .StartWith<ValidateStep>()
                    .RepeatUntil(
                        s => s.QualityScore >= 0.95m,
                        $"RefineMulti{n}",
                        loop => loop.Then<CritiqueStep>().Then<RefineStep>(),
                        maxIterations: maxIter)
                    .Finally<CompleteStep>());
        }
    }

    // fork-join — concurrent fan-out into a join, varying path count.
    private static IEnumerable<Case> ForkJoinCases()
    {
        for (var i = 0; i < 13; i++)
        {
            var n = i;
            yield return new Case("fork-join", $"fork-two-{n:D2}",
                Workflow<TestWorkflowState>.Create($"fork-two-{n}")
                    .StartWith<ValidateStep>()
                    .Fork(
                        p => p.Then<AutoProcessStep>(),
                        p => p.Then<ManualProcessStep>())
                    .Join<NotifyStep>()
                    .Finally<CompleteStep>());

            yield return new Case("fork-join", $"fork-three-{n:D2}",
                Workflow<TestWorkflowState>.Create($"fork-three-{n}")
                    .StartWith<ValidateStep>()
                    .Fork(
                        p => p.Then<AutoProcessStep>(),
                        p => p.Then<ManualProcessStep>(),
                        p => p.Then<NotifyStep>())
                    .Join<RefineStep>()
                    .Finally<CompleteStep>());
        }
    }

    // awaitApproval — human-approval pauses with a CLR approver type.
    private static IEnumerable<Case> AwaitApprovalCases()
    {
        for (var i = 0; i < 13; i++)
        {
            var n = i;
            yield return new Case("awaitApproval", $"approval-context-{n:D2}",
                Workflow<TestWorkflowState>.Create($"approval-context-{n}")
                    .StartWith<ValidateStep>()
                    .AwaitApproval<ManagerApprover>(a => a.WithContext($"Approve request {n}"))
                    .Finally<CompleteStep>());

            yield return new Case("awaitApproval", $"approval-plain-{n:D2}",
                Workflow<TestWorkflowState>.Create($"approval-plain-{n}")
                    .StartWith<ValidateStep>()
                    .Then<ProcessStep>()
                    .AwaitApproval<ManagerApprover>(a => a.WithContext($"Sign off {n}"))
                    .Finally<CompleteStep>());
        }
    }

    // onFailure — workflow-scoped failure handlers of varying length.
    private static IEnumerable<Case> OnFailureCases()
    {
        for (var i = 0; i < 13; i++)
        {
            var n = i;
            yield return new Case("onFailure", $"failure-single-{n:D2}",
                Workflow<TestWorkflowState>.Create($"failure-single-{n}")
                    .StartWith<ValidateStep>()
                    .Then<ProcessStep>()
                    .OnFailure(f => f.Then<LogFailureStep>())
                    .Finally<CompleteStep>());

            yield return new Case("onFailure", $"failure-multi-{n:D2}",
                Workflow<TestWorkflowState>.Create($"failure-multi-{n}")
                    .StartWith<ValidateStep>()
                    .Then<ProcessStep>()
                    .OnFailure(f => f.Then<LogFailureStep>().Then<NotifyAdminStep>())
                    .Finally<CompleteStep>());
        }
    }

    // config — step configuration variants (confidence / retry / timeout / validation).
    private static IEnumerable<Case> ConfigCases()
    {
        for (var i = 0; i < 13; i++)
        {
            var n = i;
            yield return new Case("config", $"config-retry-{n:D2}",
                Workflow<TestWorkflowState>.Create($"config-retry-{n}")
                    .StartWith<ValidateStep>()
                    .Then<ProcessStep>(step => step.WithRetry((n % 5) + 1))
                    .Finally<CompleteStep>());

            yield return new Case("config", $"config-confidence-{n:D2}",
                Workflow<TestWorkflowState>.Create($"config-confidence-{n}")
                    .StartWith<ValidateStep>()
                    .Then<ProcessStep>(step => step
                        .RequireConfidence(0.5 + (n * 0.03))
                        .WithTimeout(TimeSpan.FromSeconds(30 + n)))
                    .Finally<CompleteStep>());
        }
    }

    private static WorkflowDefinition<TestWorkflowState> BuildChain(string name, int length)
    {
        var builder = Workflow<TestWorkflowState>.Create(name).StartWith<ValidateStep>();
        for (var i = 0; i < length; i++)
        {
            builder = builder.Then<ProcessStep>($"Step{i}");
        }

        return builder.Finally<CompleteStep>();
    }
}
