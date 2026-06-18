// -----------------------------------------------------------------------
// <copyright file="StepStartHandlerEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits handler methods for step start commands in a Wolverine saga.
/// </summary>
/// <remarks>
/// <para>
/// This emitter generates handlers that process StartStepCommand messages.
/// Depending on whether the step has validation configured, it generates either:
/// <list type="bullet">
///   <item><description>
///     Standard handler returning a worker command directly
///   </description></item>
///   <item><description>
///     Yield-based handler that checks validation guard first, then dispatches
///   </description></item>
/// </list>
/// </para>
/// <para>
/// The Guard-Then-Dispatch pattern ensures validation failures are caught
/// before worker execution, providing fast-fail semantics.
/// </para>
/// </remarks>
internal sealed class StepStartHandlerEmitter
{
    /// <summary>
    /// Emits a handler method for a step start command.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="stepName">The name of the step.</param>
    /// <param name="context">The handler context with step information.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    public void EmitHandler(
        StringBuilder sb,
        WorkflowModel model,
        string stepName,
        HandlerContext context)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNull(stepName, nameof(stepName));
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var commandName = $"Start{stepName}Command";
        var stepModel = context.StepModel;

        // Determine worker command name:
        // Worker handlers are generated per step TYPE, not per phase, so we always use the unprefixed
        // step type name. This ensures fork path steps (e.g., TargetLoop_ValidateThesisStep) route to
        // the same handler as regular steps (ValidateThesisStep) since the step implementation is shared.
        // When stepModel is available, use its StepName directly.
        // When stepModel is null (semantic resolution failed), extract base step name from phase name.
        var baseStepName = stepModel?.StepName ?? ExtractBaseStepName(stepName);
        var workerCommandName = $"Execute{baseStepName}WorkerCommand";

        // XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the {commandName} - transitions phase and dispatches to worker.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"command\">The start {stepName} command.</param>");

        // When the step declares a timeout, the start handler also cascades a
        // {Phase}Timeout (a Wolverine TimeoutMessage) alongside the worker command,
        // so the deadline race begins when the step begins. The timeout type is
        // named per PHASE so reused step types in distinct phases don't collide.
        var hasTimeout = stepModel?.Timeout is not null;

        if (stepModel?.HasValidation == true)
        {
            EmitValidationHandler(sb, model, stepName, stepModel, commandName, workerCommandName, hasTimeout);
        }
        else
        {
            EmitStandardHandler(sb, model, stepName, commandName, workerCommandName, hasTimeout);
        }
    }

    private static void EmitValidationHandler(
        StringBuilder sb,
        WorkflowModel model,
        string stepName,
        StepModel stepModel,
        string commandName,
        string workerCommandName,
        bool hasTimeout)
    {
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

        // Yield-based handler for steps with validation guards
        // Uses method injection for ILogger to work with Wolverine's saga rehydration pattern
        sb.AppendLine($"    /// <returns>The events/commands for the {stepName} step.</returns>");
        sb.AppendLine($"    public IEnumerable<object> Handle(");
        sb.AppendLine($"        {commandName} command,");
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(command, nameof(command));");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();

        // Emit validation guard (Guard-Then-Dispatch pattern)
        // Replace lambda parameter (typically "state") with saga's State property
        var guardPredicate = ReplaceStateParameter(stepModel.ValidationPredicate!);
        sb.AppendLine("        // Validation guard");
        sb.AppendLine($"        if (!({guardPredicate}))");
        sb.AppendLine("        {");
        sb.AppendLine($"            Phase = {model.PhaseEnumName}.ValidationFailed;");
        sb.AppendLine();
        sb.AppendLine($"            logger.LogWarning(");
        sb.AppendLine($"                \"Validation failed for step {{StepName}} in workflow {{WorkflowId}}: {{ValidationMessage}}\",");
        sb.AppendLine($"                \"{stepName}\",");
        sb.AppendLine("                WorkflowId,");
        sb.AppendLine($"                \"{stepModel.ValidationErrorMessage}\");");
        sb.AppendLine();
        sb.AppendLine($"            yield return new {model.PascalName}ValidationFailed(");
        sb.AppendLine("                WorkflowId,");
        sb.AppendLine($"                \"{stepName}\",");
        sb.AppendLine($"                \"{stepModel.ValidationErrorMessage}\",");
        sb.AppendLine("                DateTimeOffset.UtcNow);");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Validation passed - dispatch to worker
        sb.AppendLine($"        Phase = {model.PhaseEnumName}.{stepName};");
        sb.AppendLine();
        sb.AppendLine($"        logger.LogDebug(");
        sb.AppendLine($"            \"Dispatching {{CommandType}} for workflow {{WorkflowId}}\",");
        sb.AppendLine($"            nameof({workerCommandName}),");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();
        sb.AppendLine($"        yield return new {workerCommandName}(WorkflowId, Guid.NewGuid(), State);");

        if (hasTimeout)
        {
            // Cascade the timeout message: Wolverine auto-schedules its delayed
            // delivery from the TimeoutMessage base and re-enters the saga later.
            sb.AppendLine();
            sb.AppendLine("        // Start the timeout deadline race for this step.");
            sb.AppendLine($"        yield return new {stepName}Timeout(WorkflowId);");
        }

        sb.AppendLine("    }");
    }

    private static void EmitStandardHandler(
        StringBuilder sb,
        WorkflowModel model,
        string stepName,
        string commandName,
        string workerCommandName,
        bool hasTimeout)
    {
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);

        if (hasTimeout)
        {
            EmitTimeoutCascadingStandardHandler(sb, model, stepName, commandName, workerCommandName, sagaClassName);
            return;
        }

        // Return the worker command directly - this is the standard saga cascading pattern
        // Uses method injection for ILogger to work with Wolverine's saga rehydration pattern
        sb.AppendLine($"    /// <returns>The worker command to execute the step.</returns>");
        sb.AppendLine($"    public {workerCommandName} Handle(");
        sb.AppendLine($"        {commandName} command,");
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(command, nameof(command));");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();
        sb.AppendLine($"        Phase = {model.PhaseEnumName}.{stepName};");
        sb.AppendLine();
        sb.AppendLine($"        logger.LogDebug(");
        sb.AppendLine($"            \"Dispatching {{CommandType}} for workflow {{WorkflowId}}\",");
        sb.AppendLine($"            nameof({workerCommandName}),");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();
        sb.AppendLine($"        return new {workerCommandName}(WorkflowId, Guid.NewGuid(), State);");
        sb.AppendLine("    }");
    }

    private static void EmitTimeoutCascadingStandardHandler(
        StringBuilder sb,
        WorkflowModel model,
        string stepName,
        string commandName,
        string workerCommandName,
        string sagaClassName)
    {
        // Yield-based handler so the start handler can cascade BOTH the worker
        // command and the {Phase}Timeout (Wolverine TimeoutMessage) that begins the
        // deadline race. Uses method injection for ILogger to work with Wolverine's
        // saga rehydration pattern.
        sb.AppendLine($"    /// <returns>The worker command and the timeout deadline message.</returns>");
        sb.AppendLine($"    public IEnumerable<object> Handle(");
        sb.AppendLine($"        {commandName} command,");
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(command, nameof(command));");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();
        sb.AppendLine($"        Phase = {model.PhaseEnumName}.{stepName};");
        sb.AppendLine();
        sb.AppendLine($"        logger.LogDebug(");
        sb.AppendLine($"            \"Dispatching {{CommandType}} for workflow {{WorkflowId}}\",");
        sb.AppendLine($"            nameof({workerCommandName}),");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();
        sb.AppendLine($"        yield return new {workerCommandName}(WorkflowId, Guid.NewGuid(), State);");
        sb.AppendLine();
        sb.AppendLine("        // Start the timeout deadline race for this step.");
        sb.AppendLine($"        yield return new {stepName}Timeout(WorkflowId);");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Replaces the lambda parameter name (typically "state") with the saga's State property.
    /// </summary>
    /// <param name="predicate">The predicate expression from the lambda.</param>
    /// <returns>The predicate with lambda parameter replaced by State.</returns>
    private static string ReplaceStateParameter(string predicate)
    {
        // Common lambda parameter names in order of specificity
        // Match "state." at word boundary to avoid partial matches
        var result = Regex.Replace(
            predicate,
            @"\bstate\.",
            "State.",
            RegexOptions.None);

        return result;
    }

    /// <summary>
    /// Extracts the base step name from a phase name.
    /// </summary>
    /// <param name="phaseName">The phase name (e.g., "LoopName_StepName" or "StepName").</param>
    /// <returns>The base step name (the part after the last underscore, or the whole string if no underscore).</returns>
    /// <remarks>
    /// Phase names for loop steps follow the pattern "{LoopName}_{StepName}" (e.g., "SpecialistExecution_SelectSpecialistStep").
    /// For nested loops, the pattern is "{OuterLoop}_{InnerLoop}_{StepName}".
    /// This method extracts the step name by taking the part after the last underscore.
    /// </remarks>
    private static string ExtractBaseStepName(string phaseName)
    {
        var lastUnderscoreIndex = phaseName.LastIndexOf('_');
        return lastUnderscoreIndex >= 0
            ? phaseName.Substring(lastUnderscoreIndex + 1)
            : phaseName;
    }
}
