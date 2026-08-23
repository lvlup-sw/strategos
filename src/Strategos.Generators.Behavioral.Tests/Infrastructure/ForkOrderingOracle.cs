// -----------------------------------------------------------------------
// <copyright file="ForkOrderingOracle.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// The five step names of a fork-join workflow, in the roles the ordering contract is stated over.
/// </summary>
/// <param name="PreFork">The step that runs before the fork splits.</param>
/// <param name="LeftPath">The step on the first parallel path.</param>
/// <param name="RightPath">The step on the second parallel path.</param>
/// <param name="Join">The step that gates on both parallel paths completing.</param>
/// <param name="Terminal">The declared terminal step that completes the workflow.</param>
public sealed record ForkStepNames(
    string PreFork,
    string LeftPath,
    string RightPath,
    string Join,
    string Terminal);

/// <summary>
/// Decides whether a recorded step-invocation sequence obeys the fork-join ordering contract.
/// </summary>
/// <remarks>
/// <para>
/// Per-step invocation counts cannot express this contract. A fork saga that ran every step exactly
/// once is indistinguishable, by count, from one that dispatched its paths sequentially, fired the
/// join before both paths finished, or ran the terminal before the join. That last shape is the
/// dangerous one: whichever step happens to run last calls <c>MarkCompleted()</c>, so the workflow
/// still terminates and the saga document still disappears. A count-only oracle therefore accepts a
/// wrong fix. The order the steps were recorded in is the only evidence that separates them.
/// </para>
/// <para>
/// Absence is checked before order for the same reason. Index comparison alone can be satisfied by a
/// step that never ran at all — a missing name yields <c>-1</c>, which compares "before" everything,
/// so a vanished pre-fork step would silently pass a "pre-fork precedes both paths" test. Every role
/// must be present before any relative claim about it is meaningful.
/// </para>
/// </remarks>
public static class ForkOrderingOracle
{
    /// <summary>
    /// Finds the first way the recorded sequence violates the fork-join ordering contract.
    /// </summary>
    /// <param name="invocations">The ordered step names recorded while the workflow ran.</param>
    /// <param name="steps">The step names playing each role in the fork shape.</param>
    /// <returns>
    /// A description of the first violation found, or <see langword="null"/> when the sequence
    /// satisfies the contract.
    /// </returns>
    /// <remarks>
    /// The sequence may contain steps from other workflows — only the five named steps are
    /// considered — but each of the five must be unique to the fork under test for the positions to
    /// mean anything.
    /// </remarks>
    public static string? FindViolation(IReadOnlyList<string> invocations, ForkStepNames steps)
    {
        ArgumentNullException.ThrowIfNull(invocations, nameof(invocations));
        ArgumentNullException.ThrowIfNull(steps, nameof(steps));

        var missing = DescribeMissingStep(invocations, steps);
        if (missing is not null)
        {
            return missing;
        }

        var preFork = PositionOf(invocations, steps.PreFork);
        var left = PositionOf(invocations, steps.LeftPath);
        var right = PositionOf(invocations, steps.RightPath);
        var join = PositionOf(invocations, steps.Join);
        var terminal = PositionOf(invocations, steps.Terminal);

        if (preFork > left)
        {
            return Explain(steps.PreFork, preFork, "must run before", steps.LeftPath, left, invocations);
        }

        if (preFork > right)
        {
            return Explain(steps.PreFork, preFork, "must run before", steps.RightPath, right, invocations);
        }

        if (join < left)
        {
            return Explain(steps.Join, join, "must run after", steps.LeftPath, left, invocations);
        }

        if (join < right)
        {
            return Explain(steps.Join, join, "must run after", steps.RightPath, right, invocations);
        }

        if (terminal < join)
        {
            return Explain(steps.Terminal, terminal, "must run after", steps.Join, join, invocations);
        }

        return null;
    }

    /// <summary>
    /// Reports the first of the five roles that never appears in the recorded sequence.
    /// </summary>
    /// <param name="invocations">The ordered step names recorded while the workflow ran.</param>
    /// <param name="steps">The step names playing each role in the fork shape.</param>
    /// <returns>A description of the missing step, or <see langword="null"/> when all five ran.</returns>
    private static string? DescribeMissingStep(IReadOnlyList<string> invocations, ForkStepNames steps)
    {
        var roles = new (string Role, string Step)[]
        {
            ("pre-fork", steps.PreFork),
            ("left path", steps.LeftPath),
            ("right path", steps.RightPath),
            ("join", steps.Join),
            ("terminal", steps.Terminal),
        };

        foreach (var (role, step) in roles)
        {
            if (PositionOf(invocations, step) < 0)
            {
                return $"the {role} step '{step}' never ran: recorded sequence was "
                    + $"[{string.Join(", ", invocations)}]. Ordering cannot be judged against a step "
                    + "that is absent, because a missing step compares before every other one.";
            }
        }

        return null;
    }

    /// <summary>Finds the first position at which the named step was recorded.</summary>
    /// <param name="invocations">The ordered step names recorded while the workflow ran.</param>
    /// <param name="step">The step name to locate.</param>
    /// <returns>The zero-based position, or <c>-1</c> when the step never ran.</returns>
    private static int PositionOf(IReadOnlyList<string> invocations, string step)
    {
        for (var index = 0; index < invocations.Count; index++)
        {
            if (string.Equals(invocations[index], step, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Renders a violation with both positions and the whole sequence for diagnosis.</summary>
    /// <param name="subject">The step whose position is wrong.</param>
    /// <param name="subjectPosition">Where the subject ran.</param>
    /// <param name="relation">The relation the subject was required to satisfy.</param>
    /// <param name="other">The step the subject is compared against.</param>
    /// <param name="otherPosition">Where the other step ran.</param>
    /// <param name="invocations">The full recorded sequence.</param>
    /// <returns>The violation description.</returns>
    private static string Explain(
        string subject,
        int subjectPosition,
        string relation,
        string other,
        int otherPosition,
        IReadOnlyList<string> invocations) =>
        $"'{subject}' (position {subjectPosition}) {relation} '{other}' (position {otherPosition}): "
            + $"recorded sequence was [{string.Join(", ", invocations)}].";
}
