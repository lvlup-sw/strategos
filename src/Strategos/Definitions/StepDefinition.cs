// =============================================================================
// <copyright file="StepDefinition.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Definitions;

/// <summary>
/// Immutable definition of a single step within a workflow.
/// </summary>
/// <remarks>
/// <para>
/// Step definitions capture metadata about workflow steps for:
/// <list type="bullet">
///   <item><description>Source generation of phase enums and saga handlers</description></item>
///   <item><description>Runtime step execution and routing</description></item>
///   <item><description>Workflow validation and visualization</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed record StepDefinition
{
    /// <summary>
    /// Gets the unique identifier for this step.
    /// </summary>
    public required string StepId { get; init; }

    /// <summary>
    /// Gets the step name (derived from type or explicit).
    /// </summary>
    public required string StepName { get; init; }

    /// <summary>
    /// Gets the step implementation type.
    /// </summary>
    public required Type StepType { get; init; }

    /// <summary>
    /// Gets the step type name.
    /// </summary>
    public string StepTypeName => StepType.Name;

    /// <summary>
    /// Gets the optional instance name for this step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Instance names allow the same step type to be reused in different
    /// contexts with distinct identities. This is useful in fork/branch paths
    /// where the same step type serves different purposes.
    /// </para>
    /// <para>
    /// When specified, the instance name is used for:
    /// <list type="bullet">
    ///   <item><description>Phase enum generation (e.g., "Technical" instead of "AnalyzeStep")</description></item>
    ///   <item><description>Duplicate step detection bypass (different instance names are unique)</description></item>
    ///   <item><description>Saga state machine phase tracking</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public string? InstanceName { get; init; }

    /// <summary>
    /// Gets a value indicating whether this step terminates the workflow.
    /// </summary>
    public bool IsTerminal { get; init; }

    /// <summary>
    /// Gets the step configuration (confidence, retry, compensation, etc.).
    /// </summary>
    public StepConfigurationDefinition? Configuration { get; init; }

    /// <summary>
    /// Gets the language-neutral ontology action performed by this step occurrence.
    /// </summary>
    /// <remarks>
    /// This identity belongs to the occurrence rather than the CLR step type, allowing
    /// the same implementation type to be reused for different ontology actions.
    /// </remarks>
    public WorkflowActionReference? Action { get; init; }

    /// <summary>
    /// Gets a value indicating whether this step is part of a loop body.
    /// </summary>
    public bool IsLoopBodyStep { get; init; }

    /// <summary>
    /// Gets the parent loop ID if this step is part of a loop body.
    /// </summary>
    public string? ParentLoopId { get; init; }

    /// <summary>
    /// Gets a value indicating whether this step is a lambda step (inline implementation).
    /// </summary>
    public bool IsLambdaStep { get; init; }

    /// <summary>
    /// Gets the lambda step delegate if this is a lambda step.
    /// </summary>
    /// <remarks>
    /// This is a boxed delegate that must be cast to the appropriate StepDelegate{TState} type at runtime.
    /// </remarks>
    public Delegate? LambdaDelegate { get; init; }

    /// <summary>
    /// Creates a new step definition from a step type.
    /// </summary>
    /// <param name="stepType">The step implementation type.</param>
    /// <param name="customName">Optional custom name override for the step name.</param>
    /// <param name="instanceName">Optional instance name for distinguishing same step type in different contexts.</param>
    /// <returns>A new step definition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stepType"/> is null.</exception>
    public static StepDefinition Create(Type stepType, string? customName = null, string? instanceName = null)
    {
        ArgumentNullException.ThrowIfNull(stepType, nameof(stepType));

        var stepName = customName ?? DeriveStepName(stepType);

        return new StepDefinition
        {
            StepId = Guid.NewGuid().ToString("N"),
            StepName = stepName,
            StepType = stepType,
            IsTerminal = false,
            InstanceName = instanceName,
        };
    }

    /// <summary>
    /// Creates a new step definition from a lambda delegate.
    /// </summary>
    /// <param name="stepName">The step name.</param>
    /// <param name="lambdaDelegate">The lambda delegate implementing the step logic.</param>
    /// <returns>A new step definition for the lambda step.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stepName"/> or <paramref name="lambdaDelegate"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="stepName"/> is empty or whitespace.
    /// </exception>
    public static StepDefinition CreateFromLambda(string stepName, Delegate lambdaDelegate)
    {
        ArgumentNullException.ThrowIfNull(stepName, nameof(stepName));
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName, nameof(stepName));
        ArgumentNullException.ThrowIfNull(lambdaDelegate, nameof(lambdaDelegate));

        return new StepDefinition
        {
            StepId = Guid.NewGuid().ToString("N"),
            StepName = stepName,
            StepType = typeof(LambdaStepMarker),
            IsTerminal = false,
            IsLambdaStep = true,
            LambdaDelegate = lambdaDelegate,
        };
    }

    /// <summary>
    /// Creates a new step definition with IsTerminal set to true.
    /// </summary>
    /// <returns>A new step definition marked as terminal.</returns>
    public StepDefinition AsTerminal() => this with { IsTerminal = true };

    /// <summary>
    /// Creates a new step definition with the specified configuration.
    /// </summary>
    /// <param name="configuration">The step configuration.</param>
    /// <returns>A new step definition with the configuration set.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configuration"/> is null.
    /// </exception>
    public StepDefinition WithConfiguration(StepConfigurationDefinition configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        return this with { Configuration = configuration };
    }

    /// <summary>
    /// Creates a new step definition marked as a loop body step.
    /// </summary>
    /// <param name="loopId">The parent loop ID.</param>
    /// <returns>A new step definition marked as a loop body step.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="loopId"/> is null.
    /// </exception>
    public StepDefinition AsLoopBodyStep(string loopId)
    {
        ArgumentNullException.ThrowIfNull(loopId, nameof(loopId));

        return this with { IsLoopBodyStep = true, ParentLoopId = loopId };
    }

    /// <summary>
    /// Derives the step name from the type name by stripping common suffixes.
    /// </summary>
    /// <param name="stepType">The step type.</param>
    /// <returns>The derived step name.</returns>
    private static string DeriveStepName(Type stepType)
    {
        var typeName = stepType.Name;

        // Strip common suffixes
        if (typeName.EndsWith("Step", StringComparison.Ordinal))
        {
            return typeName[..^4];
        }

        return typeName;
    }
}

/// <summary>
/// Marker type used for lambda step definitions.
/// </summary>
/// <remarks>
/// This type is never instantiated - it serves as a placeholder type
/// for <see cref="StepDefinition.StepType"/> when the step is defined via lambda.
/// </remarks>
internal sealed class LambdaStepMarker
{
    private LambdaStepMarker()
    {
    }
}
