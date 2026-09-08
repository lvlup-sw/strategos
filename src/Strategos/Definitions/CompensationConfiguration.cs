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
///   <item><description>Timeout: Optional timeout for compensation execution</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed record CompensationConfiguration
{
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
    /// Gets the timeout for compensation execution.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

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
    /// Creates a new compensation configuration with the specified timeout.
    /// </summary>
    /// <param name="timeout">The timeout for compensation execution.</param>
    /// <returns>A new compensation configuration with the timeout set.</returns>
    public CompensationConfiguration WithTimeout(TimeSpan timeout)
    {
        return this with { Timeout = timeout };
    }
}
