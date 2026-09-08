// -----------------------------------------------------------------------
// <copyright file="BranchModel.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;
using Strategos.Generators.Utilities;

namespace Strategos.Generators.Models;

/// <summary>
/// Represents a single case/path within a branch construct.
/// </summary>
/// <remarks>
/// <para>
/// Each case maps a discriminator value (e.g., enum member, string literal)
/// to a sequence of steps that execute when that value matches.
/// </para>
/// </remarks>
/// <param name="CaseValueLiteral">The literal value in switch expression (e.g., "OrderStatus.Approved", "\"premium\"", "1").</param>
/// <param name="BranchPathPrefix">The prefix for steps in this branch (e.g., "Approved").</param>
/// <param name="StepNames">The ordered list of step names in this branch path.</param>
/// <param name="IsTerminal">Whether this branch path terminates the workflow (no rejoin).</param>
internal sealed record BranchCaseModel(
    string CaseValueLiteral,
    string BranchPathPrefix,
    IReadOnlyList<string> StepNames,
    bool IsTerminal)
{
    /// <summary>
    /// Gets the first step name in the branch path.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="StepNames"/> is empty.</exception>
    public string FirstStepName => StepNames.Count > 0
        ? StepNames[0]
        : throw new InvalidOperationException("Cannot access FirstStepName: StepNames is empty.");

    /// <summary>
    /// Gets the last step name in the branch path.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="StepNames"/> is empty.</exception>
    public string LastStepName => StepNames.Count > 0
        ? StepNames[StepNames.Count - 1]
        : throw new InvalidOperationException("Cannot access LastStepName: StepNames is empty.");

    /// <summary>
    /// Creates a new <see cref="BranchCaseModel"/> with validation.
    /// </summary>
    /// <param name="caseValueLiteral">The literal value in switch expression. Cannot be null or whitespace.</param>
    /// <param name="branchPathPrefix">The prefix for steps in this branch. Cannot be null or whitespace.</param>
    /// <param name="stepNames">The ordered list of step names. Must have at least one step.</param>
    /// <param name="isTerminal">Whether this branch path terminates the workflow.</param>
    /// <returns>A validated <see cref="BranchCaseModel"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static BranchCaseModel Create(
        string caseValueLiteral,
        string branchPathPrefix,
        IReadOnlyList<string> stepNames,
        bool isTerminal)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(caseValueLiteral, nameof(caseValueLiteral));
        ThrowHelper.ThrowIfNullOrWhiteSpace(branchPathPrefix, nameof(branchPathPrefix));
        ThrowHelper.ThrowIfNull(stepNames, nameof(stepNames));

        if (stepNames.Count == 0)
        {
            throw new ArgumentException("Branch case must have at least one step.", nameof(stepNames));
        }

        return new BranchCaseModel(
            CaseValueLiteral: caseValueLiteral,
            BranchPathPrefix: branchPathPrefix,
            StepNames: stepNames,
            IsTerminal: isTerminal);
    }
}

/// <summary>
/// Represents a branch construct (conditional routing) within a workflow for code generation.
/// </summary>
/// <remarks>
/// <para>
/// Branch models capture the structure of Case/When constructs in the workflow DSL.
/// The source generator uses this model to emit:
/// - Switch expression handlers for routing based on discriminator
/// - Per-branch step phase names with prefixes
/// - Convergence point transitions for rejoining branches.
/// </para>
/// <para>
/// Supports discriminator types: enum, string, int, bool.
/// Enum discriminators generate type-safe switch arms with enum members.
/// Other types generate literal pattern matching.
/// </para>
/// </remarks>
/// <param name="BranchId">The unique identifier for the branch point (e.g., "ProcessOrder-OrderStatus").</param>
/// <param name="PreviousStepName">The step that precedes this branch (e.g., "ValidateOrder").</param>
/// <param name="DiscriminatorPropertyPath">The state property path for routing (e.g., "Status" or "Order.Status"), or method name for method references.</param>
/// <param name="DiscriminatorTypeName">The type name of the discriminator (e.g., "OrderStatus", "string").</param>
/// <param name="IsEnumDiscriminator">Whether the discriminator is an enum type.</param>
/// <param name="IsMethodDiscriminator">Whether the discriminator is a method reference (e.g., DetermineOutcome vs state => state.Status).</param>
/// <param name="Cases">The ordered list of branch cases.</param>
/// <param name="RejoinStepName">The step where branches converge, or null if all branches are terminal.</param>
/// <param name="LoopPrefix">The loop prefix if this branch is inside a loop (e.g., "TargetLoop"), or null if not in a loop.</param>
/// <param name="NextConsecutiveBranch">The next branch in a consecutive chain (e.g., when multiple .Branch() calls follow each other), or null if this is the last branch before the rejoin step.</param>
internal sealed record BranchModel(
    string BranchId,
    string PreviousStepName,
    string DiscriminatorPropertyPath,
    string DiscriminatorTypeName,
    bool IsEnumDiscriminator,
    bool IsMethodDiscriminator,
    IReadOnlyList<BranchCaseModel> Cases,
    string? RejoinStepName,
    string? LoopPrefix = null,
    BranchModel? NextConsecutiveBranch = null)
{
    /// <summary>
    /// Gets a value indicating whether one or more declared branch cases could not be lowered
    /// into the generator's closed branch grammar.
    /// </summary>
    /// <remarks>
    /// Runtime emission keeps its historical fall-through behavior for such syntax, but a
    /// workflow-bound action must not prove a contract over a silently smaller topology.
    /// </remarks>
    public bool HasUnresolvedCases { get; init; }

    /// <summary>
    /// Gets the method name for the branch routing handler.
    /// </summary>
    /// <remarks>
    /// Derived from the property path with dots removed.
    /// E.g., "Status" becomes "RouteByStatus", "Order.ShippingMethod" becomes "RouteByOrderShippingMethod".
    /// </remarks>
    public string BranchHandlerMethodName => $"RouteBy{DiscriminatorPropertyPath.Replace(".", string.Empty)}";

    /// <summary>
    /// Gets whether this branch has a convergence point.
    /// </summary>
    public bool HasRejoinPoint => RejoinStepName is not null;

    /// <summary>
    /// Gets whether all cases in this branch are terminal.
    /// </summary>
    public bool AllCasesTerminal => Cases.All(c => c.IsTerminal);

    /// <summary>
    /// Gets whether this branch has a consecutive branch following it.
    /// </summary>
    /// <remarks>
    /// When multiple <c>.Branch()</c> calls are chained without intervening steps,
    /// this indicates there are more conditions to evaluate before reaching the rejoin step.
    /// </remarks>
    public bool HasNextConsecutiveBranch => NextConsecutiveBranch is not null;

    /// <summary>
    /// Gets whether this branch is inside a loop.
    /// </summary>
    public bool IsInsideLoop => LoopPrefix is not null;

    /// <summary>
    /// Creates a new <see cref="BranchModel"/> with validation.
    /// </summary>
    /// <param name="branchId">The unique identifier for the branch point. Cannot be null or whitespace.</param>
    /// <param name="previousStepName">The step that precedes this branch. Cannot be null or whitespace.</param>
    /// <param name="discriminatorPropertyPath">The state property path for routing. Must be a valid property path.</param>
    /// <param name="discriminatorTypeName">The type name of the discriminator. Cannot be null or whitespace.</param>
    /// <param name="isEnumDiscriminator">Whether the discriminator is an enum type.</param>
    /// <param name="isMethodDiscriminator">Whether the discriminator is a method reference.</param>
    /// <param name="cases">The ordered list of branch cases. Must have at least one case.</param>
    /// <param name="rejoinStepName">The optional step where branches converge.</param>
    /// <param name="loopPrefix">The loop prefix if this branch is inside a loop.</param>
    /// <returns>A validated <see cref="BranchModel"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static BranchModel Create(
        string branchId,
        string previousStepName,
        string discriminatorPropertyPath,
        string discriminatorTypeName,
        bool isEnumDiscriminator,
        bool isMethodDiscriminator,
        IReadOnlyList<BranchCaseModel> cases,
        string? rejoinStepName = null,
        string? loopPrefix = null)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(branchId, nameof(branchId));
        ThrowHelper.ThrowIfNullOrWhiteSpace(previousStepName, nameof(previousStepName));

        // Method discriminators use method names which are valid identifiers, not property paths
        if (!isMethodDiscriminator)
        {
            IdentifierValidator.ValidatePropertyPath(discriminatorPropertyPath, nameof(discriminatorPropertyPath));
        }
        else
        {
            IdentifierValidator.ValidateIdentifier(discriminatorPropertyPath, nameof(discriminatorPropertyPath));
        }

        ThrowHelper.ThrowIfNullOrWhiteSpace(discriminatorTypeName, nameof(discriminatorTypeName));
        ThrowHelper.ThrowIfNull(cases, nameof(cases));

        if (cases.Count == 0)
        {
            throw new ArgumentException("Branch must have at least one case.", nameof(cases));
        }

        return new BranchModel(
            BranchId: branchId,
            PreviousStepName: previousStepName,
            DiscriminatorPropertyPath: discriminatorPropertyPath,
            DiscriminatorTypeName: discriminatorTypeName,
            IsEnumDiscriminator: isEnumDiscriminator,
            IsMethodDiscriminator: isMethodDiscriminator,
            Cases: cases,
            RejoinStepName: rejoinStepName,
            LoopPrefix: loopPrefix);
    }
}
