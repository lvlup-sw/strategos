// -----------------------------------------------------------------------
// <copyright file="LoopCompletedHandlerEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Text;

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits handler methods for loop completion events in a Wolverine saga.
/// </summary>
/// <remarks>
/// <para>
/// This emitter generates handlers that process the completed event for the last
/// step in a loop body. The handler evaluates:
/// <list type="bullet">
///   <item><description>Max iteration guard - exits if max reached</description></item>
///   <item><description>Exit condition - calls ShouldExitLoop() method</description></item>
///   <item><description>Continue loop - increments counter and returns to first step</description></item>
/// </list>
/// </para>
/// <para>
/// Nested loops are handled from innermost to outermost, with each exit potentially
/// triggering evaluation of the next outer loop.
/// </para>
/// </remarks>
internal sealed class LoopCompletedHandlerEmitter
{
    /// <summary>
    /// Emits a handler method for a loop completion event.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="stepName">The name of the last step in the loop body.</param>
    /// <param name="context">The handler context with loop information.</param>
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

        // Use unprefixed step type name for completed event (workers return per-type events)
        // When stepModel is available, use its StepName directly.
        // When stepModel is null (semantic resolution failed), extract base step name from phase name.
        var stepModel = context.StepModel;
        var baseStepName = stepModel?.StepName ?? ExtractBaseStepName(stepName);
        var eventName = $"{baseStepName}Completed";
        var reducerTypeName = model.ReducerTypeName;
        var loops = context.LoopsAtStep!;
        var sagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);
        var innermostLoop = loops[0];

        // XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Handles the {eventName} event - evaluates loop condition(s).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"evt\">The {stepName} completed event.</param>");
        sb.AppendLine("    /// <param name=\"logger\">The logger for diagnostic output.</param>");
        sb.AppendLine("    /// <returns>The command for the next step based on loop conditions.</returns>");

        // Return type is object since we can return different command types
        // Uses method injection for ILogger to work with Wolverine's saga rehydration pattern
        sb.AppendLine($"    public object Handle(");
        sb.AppendLine($"        {eventName} evt,");
        StateApplicationHelper.EmitSessionParameter(sb, model);
        sb.AppendLine($"        ILogger<{sagaClassName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(evt, nameof(evt));");
        StateApplicationHelper.EmitSessionGuard(sb, model);
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(logger, nameof(logger));");
        sb.AppendLine();

        // Apply state change
        if (!string.IsNullOrEmpty(model.StateTypeName))
        {
            StateApplicationHelper.EmitStateApplication(sb, model);
            sb.AppendLine();
        }

        // Log loop iteration evaluation
        sb.AppendLine($"        logger.LogDebug(");
        sb.AppendLine($"            \"Loop {{LoopName}} evaluating iteration {{Iteration}} for workflow {{WorkflowId}}\",");
        sb.AppendLine($"            \"{innermostLoop.LoopName}\",");
        sb.AppendLine($"            {innermostLoop.IterationPropertyName},");
        sb.AppendLine("            WorkflowId);");
        sb.AppendLine();

        // For nested loops, we need to check innermost first, then outer loops
        // Each loop has: max iteration guard, condition check, then continue/exit logic
        EmitNestedLoopChecks(sb, model, loops, 0);

        sb.AppendLine("    }");
    }

    private static void EmitNestedLoopChecks(
        StringBuilder sb,
        WorkflowModel model,
        IReadOnlyList<LoopModel> loops,
        int loopIndex)
    {
        var loop = loops[loopIndex];
        var conditionMethod = loop.ConditionMethodName;
        var iterationCountProp = loop.IterationPropertyName;
        var firstLoopStepCommand = $"Start{loop.FirstBodyStepName}Command";

        // Determine what happens when this loop exits
        var isOutermostLoop = loopIndex == loops.Count - 1;
        var hasOuterLoop = loopIndex < loops.Count - 1;

        // Check max iterations guard
        sb.AppendLine($"        // {loop.LoopName} loop - max iteration guard");
        sb.AppendLine($"        if ({iterationCountProp} >= {loop.MaxIterations})");
        sb.AppendLine("        {");

        if (hasOuterLoop)
        {
            // Exit to outer loop check
            var outerLoop = loops[loopIndex + 1];
            sb.AppendLine($"            // Exit {loop.LoopName} loop, check {outerLoop.LoopName} loop");
            EmitOuterLoopCheckInline(sb, model, loops, loopIndex + 1);
        }
        else
        {
            EmitLoopExitLogic(sb, model, loop, "            ");
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        // Check loop exit condition
        sb.AppendLine($"        // {loop.LoopName} loop - exit condition check");
        sb.AppendLine($"        if ({conditionMethod}())");
        sb.AppendLine("        {");

        if (hasOuterLoop)
        {
            // Exit to outer loop check
            var outerLoop = loops[loopIndex + 1];
            sb.AppendLine($"            // Exit {loop.LoopName} loop, check {outerLoop.LoopName} loop");
            EmitOuterLoopCheckInline(sb, model, loops, loopIndex + 1);
        }
        else
        {
            EmitLoopExitLogic(sb, model, loop, "            ");
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        // Continue loop - increment and return first loop step
        sb.AppendLine($"        // Continue {loop.LoopName} loop");
        sb.AppendLine($"        {iterationCountProp}++;");
        sb.AppendLine($"        return new {firstLoopStepCommand}(WorkflowId);");
    }

    /// <summary>
    /// Emits the loop exit logic, which may route through a branch or directly to a continuation step.
    /// </summary>
    private static void EmitLoopExitLogic(StringBuilder sb, WorkflowModel model, LoopModel loop, string indent)
    {
        if (loop.HasBranchOnExit && loop.BranchOnExit is not null)
        {
            // Use the branch model stored directly on the loop
            EmitBranchRouting(sb, model, loop.BranchOnExit, indent);
            return;
        }

        // Default: route to continuation step or complete
        if (loop.ContinuationStepName is not null)
        {
            sb.AppendLine($"{indent}return new Start{loop.ContinuationStepName}Command(WorkflowId);");
        }
        else
        {
            sb.AppendLine($"{indent}Phase = {model.PhaseEnumName}.Completed;");
            sb.AppendLine($"{indent}MarkCompleted();");
            sb.AppendLine($"{indent}return null!;");
        }
    }

    /// <summary>
    /// Emits branch routing logic as a switch expression.
    /// </summary>
    private static void EmitBranchRouting(StringBuilder sb, WorkflowModel model, BranchModel branch, string indent)
    {
        // Method discriminators need qualified class name since they're defined in the workflow definition class
        // Property discriminators are accessed on the State property
        var discriminatorAccess = branch.IsMethodDiscriminator
            ? $"{model.PascalName}WorkflowDefinition.{branch.DiscriminatorPropertyPath}(State)"
            : $"State.{branch.DiscriminatorPropertyPath}";

        sb.AppendLine($"{indent}// Branch routing based on {branch.DiscriminatorPropertyPath}");
        sb.AppendLine($"{indent}return {discriminatorAccess} switch");
        sb.AppendLine($"{indent}{{");

        // Emit case for each branch path
        foreach (var branchCase in branch.Cases)
        {
            var firstStepCommand = $"Start{branchCase.FirstStepName}Command";

            if (branchCase.CaseValueLiteral == "_" || branchCase.CaseValueLiteral == "default")
            {
                // Otherwise case (default)
                sb.AppendLine($"{indent}    _ => new {firstStepCommand}(WorkflowId),");
            }
            else
            {
                sb.AppendLine($"{indent}    {branchCase.CaseValueLiteral} => new {firstStepCommand}(WorkflowId),");
            }
        }

        // Add default if no otherwise case
        var hasOtherwise = branch.Cases.Any(c => c.CaseValueLiteral == "_" || c.CaseValueLiteral == "default");
        if (!hasOtherwise)
        {
            sb.AppendLine($"{indent}    _ => throw new InvalidOperationException($\"Unhandled branch value: {{{discriminatorAccess}}}\"),");
        }

        sb.AppendLine($"{indent}}};");
    }

    private static void EmitOuterLoopCheckInline(
        StringBuilder sb,
        WorkflowModel model,
        IReadOnlyList<LoopModel> loops,
        int loopIndex)
    {
        var loop = loops[loopIndex];
        var conditionMethod = loop.ConditionMethodName;
        var iterationCountProp = loop.IterationPropertyName;
        var firstLoopStepCommand = $"Start{loop.FirstBodyStepName}Command";
        var hasOuterLoop = loopIndex < loops.Count - 1;

        // Check max iterations
        sb.AppendLine($"            if ({iterationCountProp} >= {loop.MaxIterations})");
        sb.AppendLine("            {");

        if (hasOuterLoop)
        {
            var outerLoop = loops[loopIndex + 1];
            sb.AppendLine($"                // Exit {loop.LoopName} loop, check {outerLoop.LoopName} loop");
            EmitOuterLoopCheckDoubleInline(sb, model, loops, loopIndex + 1);
        }
        else
        {
            EmitLoopExitLogic(sb, model, loop, "                ");
        }

        sb.AppendLine("            }");
        sb.AppendLine();

        // Check condition
        sb.AppendLine($"            if ({conditionMethod}())");
        sb.AppendLine("            {");

        if (hasOuterLoop)
        {
            var outerLoop = loops[loopIndex + 1];
            sb.AppendLine($"                // Exit {loop.LoopName} loop, check {outerLoop.LoopName} loop");
            EmitOuterLoopCheckDoubleInline(sb, model, loops, loopIndex + 1);
        }
        else
        {
            EmitLoopExitLogic(sb, model, loop, "                ");
        }

        sb.AppendLine("            }");
        sb.AppendLine();

        // Continue loop
        sb.AppendLine($"            {iterationCountProp}++;");
        sb.AppendLine($"            return new {firstLoopStepCommand}(WorkflowId);");
    }

    private static void EmitOuterLoopCheckDoubleInline(
        StringBuilder sb,
        WorkflowModel model,
        IReadOnlyList<LoopModel> loops,
        int loopIndex)
    {
        var loop = loops[loopIndex];
        var conditionMethod = loop.ConditionMethodName;
        var iterationCountProp = loop.IterationPropertyName;
        var firstLoopStepCommand = $"Start{loop.FirstBodyStepName}Command";

        // For deeply nested loops, we simplify and just check this loop
        // In practice, 3+ levels of nesting is rare

        sb.AppendLine($"                if ({iterationCountProp} >= {loop.MaxIterations} || {conditionMethod}())");
        sb.AppendLine("                {");

        EmitLoopExitLogic(sb, model, loop, "                    ");

        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine($"                {iterationCountProp}++;");
        sb.AppendLine($"                return new {firstLoopStepCommand}(WorkflowId);");
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
