// -----------------------------------------------------------------------
// <copyright file="NamingHelper.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Provides helper methods for generating consistent naming conventions in source generators.
/// </summary>
internal static class NamingHelper
{
    /// <summary>
    /// Gets the Start command name for a workflow.
    /// </summary>
    /// <param name="workflowPascalName">The Pascal-cased workflow name.</param>
    /// <returns>The Start command name (e.g., "StartProcessOrderCommand").</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="workflowPascalName"/> is null.</exception>
    public static string GetStartCommandName(string workflowPascalName)
    {
        ThrowHelper.ThrowIfNull(workflowPascalName, nameof(workflowPascalName));
        return $"Start{workflowPascalName}Command";
    }

    /// <summary>
    /// Gets the StartStep command name for a step.
    /// </summary>
    /// <param name="stepName">The step name.</param>
    /// <returns>The StartStep command name (e.g., "StartValidateOrderCommand").</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stepName"/> is null.</exception>
    public static string GetStartStepCommandName(string stepName)
    {
        ThrowHelper.ThrowIfNull(stepName, nameof(stepName));
        return $"Start{stepName}Command";
    }

    /// <summary>
    /// Gets the Execute command name for a step.
    /// </summary>
    /// <param name="stepName">The step name.</param>
    /// <returns>The Execute command name (e.g., "ExecuteValidateOrderCommand").</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stepName"/> is null.</exception>
    public static string GetExecuteCommandName(string stepName)
    {
        ThrowHelper.ThrowIfNull(stepName, nameof(stepName));
        return $"Execute{stepName}Command";
    }

    /// <summary>
    /// Gets the Worker command name for a step.
    /// </summary>
    /// <param name="stepName">The step name.</param>
    /// <returns>The Worker command name (e.g., "ExecuteValidateOrderWorkerCommand").</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stepName"/> is null.</exception>
    public static string GetWorkerCommandName(string stepName)
    {
        ThrowHelper.ThrowIfNull(stepName, nameof(stepName));
        return $"Execute{stepName}WorkerCommand";
    }

    /// <summary>
    /// Gets the Completed event name for a step.
    /// </summary>
    /// <param name="stepName">The step name.</param>
    /// <returns>The Completed event name (e.g., "ValidateOrderCompleted").</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stepName"/> is null.</exception>
    public static string GetCompletedEventName(string stepName)
    {
        ThrowHelper.ThrowIfNull(stepName, nameof(stepName));
        return $"{stepName}Completed";
    }

    /// <summary>
    /// Gets the Saga class name for a workflow.
    /// </summary>
    /// <param name="pascalName">The Pascal-cased workflow name.</param>
    /// <param name="version">The workflow version.</param>
    /// <returns>The Saga class name (e.g., "ProcessOrderSaga" for v1, "ProcessOrderSagaV2" for v2+).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pascalName"/> is null.</exception>
    public static string GetSagaClassName(string pascalName, int version)
    {
        ThrowHelper.ThrowIfNull(pascalName, nameof(pascalName));
        return version == 1 ? $"{pascalName}Saga" : $"{pascalName}SagaV{version}";
    }

    /// <summary>
    /// Gets the Reducer type name for a state type.
    /// </summary>
    /// <param name="stateTypeName">The state type name.</param>
    /// <returns>The Reducer type name (e.g., "OrderStateReducer").</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stateTypeName"/> is null.</exception>
    public static string GetReducerTypeName(string stateTypeName)
    {
        ThrowHelper.ThrowIfNull(stateTypeName, nameof(stateTypeName));
        return $"{stateTypeName}Reducer";
    }

    /// <summary>
    /// Gets the simple (unqualified) type name from a possibly fully qualified
    /// type name.
    /// </summary>
    /// <param name="typeName">The type name (e.g., "MyApp.Steps.RollbackStep").</param>
    /// <returns>The simple type name (e.g., "RollbackStep").</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="typeName"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// Used by the compensation lowering path to derive worker-handler, command,
    /// and event names from a <c>CompensationModel</c>'s fully qualified
    /// compensation step type name (which is carried as a descriptor string per
    /// INV-8, never a CLR <see cref="System.Type"/>).
    /// </para>
    /// <para>
    /// Any generic-arity suffix is stripped FIRST (everything from the first
    /// <c>&lt;</c>), so a fully qualified generic such as <c>Ns.Foo&lt;Ns.Bar&gt;</c>
    /// returns the valid identifier <c>Foo</c> rather than <c>Bar&gt;</c> — without
    /// the truncation the trailing <c>&gt;</c> (and, for a qualified type argument,
    /// the WRONG inner name) would leak into the derived command/event identifiers.
    /// </para>
    /// </remarks>
    public static string GetSimpleTypeName(string typeName)
    {
        ThrowHelper.ThrowIfNull(typeName, nameof(typeName));

        // Strip any generic-arity suffix first so the last-dot split operates only
        // on the outer type's namespace-qualified name, never on a (possibly
        // qualified) type argument inside the angle brackets.
        var angle = typeName.IndexOf('<');
        if (angle >= 0)
        {
            typeName = typeName.Substring(0, angle);
        }

        var lastDot = typeName.LastIndexOf('.');
        return lastDot >= 0 && lastDot < typeName.Length - 1
            ? typeName.Substring(lastDot + 1)
            : typeName;
    }
}
