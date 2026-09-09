// =============================================================================
// <copyright file="CompensationConfiguration.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Definitions;

/// <summary>
/// Immutable configuration for step compensation (rollback) behavior.
/// </summary>
/// <remarks>
/// <para>
/// Compensation configuration captures rollback step settings:
/// <list type="bullet">
///   <item><description>CompensationStepType: The step type to execute for rollback</description></item>
///   <item><description>InverseAction: The ontology action implemented by that rollback step</description></item>
///   <item><description>RequiredOnFailure: Whether legacy compensation is required when the step fails; typed rollback programs require true</description></item>
///   <item><description>Timeout: Optional deadline for one inverse execution (must be positive when set)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed record CompensationConfiguration
{
    private readonly TimeSpan? _timeout;

    /// <summary>
    /// Gets the compensation step type.
    /// </summary>
    public required Type CompensationStepType { get; init; }

    /// <summary>
    /// Gets the language-neutral identity of the ontology action implemented by the
    /// compensation step.
    /// </summary>
    /// <remarks>
    /// A null value is the legacy, runtime-only form. Static rollback proof requires a
    /// closed inverse identity supplied through <c>Compensate&lt;T&gt;(inverseAction)</c>.
    /// </remarks>
    public WorkflowActionReference? InverseAction { get; init; }

    /// <summary>
    /// Gets a value indicating whether compensation is required on failure.
    /// </summary>
    /// <remarks>
    /// Typed inverse programs derive mandatory completed-prefix rollback and therefore
    /// require this value to remain <see langword="true"/>. The optional value is retained
    /// for the legacy runtime-only compensation shape.
    /// </remarks>
    public bool RequiredOnFailure { get; init; } = true;

    /// <summary>
    /// Gets the deadline for one inverse (rollback) execution, or <see langword="null"/>
    /// when the generated default deadline applies.
    /// </summary>
    /// <remarks>
    /// This is the INVERSE step's deadline, not the forward step's. The forward deadline is
    /// authored with <c>step.WithTimeout(...)</c>; this one is authored with a
    /// <c>step.Compensate&lt;T&gt;(timeout)</c> overload. A non-positive value is rejected:
    /// a zero or negative rollback deadline can never elapse into a meaningful timeout, and
    /// silently accepting one produced a saga whose journal entry was rejected at runtime.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is non-null and less than or equal to <see cref="TimeSpan.Zero"/>.
    /// </exception>
    public TimeSpan? Timeout
    {
        get => _timeout;
        init => _timeout = ValidateTimeout(value, nameof(value));
    }

    /// <summary>
    /// Creates a compensation configuration for the specified step type.
    /// </summary>
    /// <param name="stepType">The compensation step type.</param>
    /// <returns>A new compensation configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stepType"/> is null.</exception>
    public static CompensationConfiguration Create(Type stepType)
    {
        ArgumentNullException.ThrowIfNull(stepType, nameof(stepType));

        return new CompensationConfiguration
        {
            CompensationStepType = stepType,
        };
    }

    /// <summary>
    /// Creates a typed compensation configuration for the specified step and inverse action.
    /// </summary>
    /// <param name="stepType">The compensation step type.</param>
    /// <param name="inverseAction">The ontology action implemented by the compensation step.</param>
    /// <returns>A new typed compensation configuration.</returns>
    public static CompensationConfiguration Create(
        Type stepType,
        WorkflowActionReference inverseAction)
    {
        ArgumentNullException.ThrowIfNull(stepType, nameof(stepType));
        ArgumentNullException.ThrowIfNull(inverseAction, nameof(inverseAction));

        return new CompensationConfiguration
        {
            CompensationStepType = stepType,
            InverseAction = inverseAction,
        };
    }

    /// <summary>
    /// Creates a compensation configuration for the specified step type.
    /// </summary>
    /// <typeparam name="TStep">The compensation step type.</typeparam>
    /// <returns>A new compensation configuration.</returns>
    public static CompensationConfiguration Create<TStep>()
        where TStep : class
    {
        return new CompensationConfiguration
        {
            CompensationStepType = typeof(TStep),
        };
    }

    /// <summary>
    /// Creates a typed compensation configuration for the specified step and inverse action.
    /// </summary>
    /// <typeparam name="TStep">The compensation step type.</typeparam>
    /// <param name="inverseAction">The ontology action implemented by the compensation step.</param>
    /// <returns>A new typed compensation configuration.</returns>
    public static CompensationConfiguration Create<TStep>(WorkflowActionReference inverseAction)
        where TStep : class
    {
        ArgumentNullException.ThrowIfNull(inverseAction, nameof(inverseAction));

        return new CompensationConfiguration
        {
            CompensationStepType = typeof(TStep),
            InverseAction = inverseAction,
        };
    }

    /// <summary>
    /// Creates a new compensation configuration with the specified inverse deadline.
    /// </summary>
    /// <param name="timeout">The deadline for one inverse (rollback) execution.</param>
    /// <returns>A new compensation configuration with the deadline set.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="timeout"/> is less than or equal to <see cref="TimeSpan.Zero"/>.
    /// </exception>
    public CompensationConfiguration WithTimeout(TimeSpan timeout)
    {
        return this with { Timeout = ValidateTimeout(timeout, nameof(timeout)) };
    }

    /// <summary>
    /// Rejects a non-positive inverse deadline while allowing the null (unset) value.
    /// </summary>
    /// <param name="value">The candidate deadline.</param>
    /// <param name="parameterName">The parameter name reported on rejection.</param>
    /// <returns>The validated deadline.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="value"/> is non-null and non-positive.
    /// </exception>
    private static TimeSpan? ValidateTimeout(TimeSpan? value, string parameterName)
    {
        if (value is { } candidate && candidate <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                candidate,
                "A compensation deadline must be greater than zero.");
        }

        return value;
    }
}
