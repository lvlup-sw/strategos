// -----------------------------------------------------------------------
// <copyright file="SagaStartMethodEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits the static Start method for a Wolverine saga class.
/// </summary>
/// <remarks>
/// <para>
/// This emitter generates the static factory method that creates a new saga
/// instance and returns it along with the first step command. The generated
/// method follows Wolverine's saga patterns for workflow initialization.
/// </para>
/// <para>
/// The Start method:
/// <list type="bullet">
///   <item><description>Takes a StartWorkflowCommand parameter</description></item>
///   <item><description>Validates the command with a guard clause</description></item>
///   <item><description>Creates and initializes a new saga instance</description></item>
///   <item><description>Returns a tuple of (Saga, FirstStepCommand)</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class SagaStartMethodEmitter : ISagaComponentEmitter
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sb"/> or <paramref name="model"/> is null.
    /// </exception>
    public void Emit(StringBuilder sb, WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));

        var sagaClassName = $"{model.PascalName}Saga";
        var firstStepName = model.StepNames.Count > 0 ? model.StepNames[0] : "Unknown";
        var phaseEnumName = model.PhaseEnumName;

        // XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Starts a new {model.WorkflowName} workflow.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"command\">The start workflow command.</param>");
        sb.AppendLine("    /// <returns>A tuple containing the saga instance and the first step command.</returns>");

        // Method signature
        sb.AppendLine($"    public static ({sagaClassName} Saga, Start{firstStepName}Command Command) Start(");
        sb.AppendLine($"        Start{model.PascalName}Command command)");
        sb.AppendLine("    {");

        // Guard clause
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(command, nameof(command));");
        sb.AppendLine();

        // Saga instantiation
        sb.AppendLine($"        var saga = new {sagaClassName}");
        sb.AppendLine("        {");
        sb.AppendLine("            WorkflowId = command.WorkflowId,");
        sb.AppendLine($"            Phase = {phaseEnumName}.NotStarted,");

        // Initialize State from command if state type is specified
        if (!string.IsNullOrEmpty(model.StateTypeName))
        {
            sb.AppendLine("            State = command.InitialState,");
        }

        if (CompensationTopology.UsesDerivedRuntime(model))
        {
            sb.AppendLine("            CompensationJournalSchemaVersion = 1,");
        }

        sb.AppendLine("            StartedAt = DateTimeOffset.UtcNow");
        sb.AppendLine("        };");
        sb.AppendLine();

        // Create and return first step command
        sb.AppendLine($"        var stepCommand = new Start{firstStepName}Command(command.WorkflowId);");
        sb.AppendLine();
        sb.AppendLine("        return (saga, stepCommand);");
        sb.AppendLine("    }");
    }
}
