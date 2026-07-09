// -----------------------------------------------------------------------
// <copyright file="DiagnosticForkExtractor.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Extracts diagnostic-fork models from a workflow definition (DR-9, #151).
/// </summary>
/// <remarks>
/// <para>
/// This extractor re-parses the staged <c>AllowDiagnosticFork(...)</c> fluent chain from the
/// DSL syntax into a <see cref="DiagnosticForkModel"/>, mirroring how <c>ForkExtractor</c> and
/// the loop/branch/approval extractors turn their fluent surfaces into generator IR. The saga
/// LOWERING that emits fork guards/events from the model is deferred (#151); this extractor
/// only produces the IR.
/// </para>
/// <para>
/// The expected DSL pattern is:
/// <code>
/// .AllowDiagnosticFork(fork => fork
///     .Anchor("RatifyDeployment")
///     .PermitTrigger(ForkTrigger.RatificationFailure, "provisionalStampEventId")
///     .PermitTrigger(ForkTrigger.GateContradiction, "leftGateId", "rightGateId")
///     .WithCompensationSeed("RollbackProvisionalStamp")
///     .MaxForks(3))
/// </code>
/// </para>
/// </remarks>
internal static class DiagnosticForkExtractor
{
    /// <summary>
    /// Extracts diagnostic-fork models from the workflow DSL.
    /// </summary>
    /// <param name="context">The parse context containing pre-computed lookups.</param>
    /// <returns>
    /// A list of diagnostic-fork models in the order they appear in the workflow. Empty when the
    /// workflow declares no <c>AllowDiagnosticFork(...)</c> edge.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static IReadOnlyList<DiagnosticForkModel> Extract(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // AllInvocations is pre-order (outermost fluent call first), so chained
        // AllowDiagnosticFork edges appear in reverse source order; reverse to recover the
        // authored order so the model list mirrors how the edges were declared.
        var forkInvocations = context.AllInvocations
            .Where(inv => SyntaxHelper.IsMethodCall(inv, "AllowDiagnosticFork"))
            .Reverse()
            .ToList();

        if (forkInvocations.Count == 0)
        {
            return [];
        }

        var forks = new List<DiagnosticForkModel>();

        foreach (var invocation in forkInvocations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (TryParseDiagnosticFork(invocation, context.SemanticModel, context.CancellationToken, out var model))
            {
                forks.Add(model);
            }
        }

        return forks;
    }

    private static bool TryParseDiagnosticFork(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out DiagnosticForkModel model)
    {
        model = default!;

        var configureLambda = GetConfigurationLambda(invocation);
        if (configureLambda is null)
        {
            return false;
        }

        // Walk the staged chain in logical (source) order. DescendantNodes yields the fluent
        // chain outermost-first (MaxForks, then WithCompensationSeed, ... , Anchor innermost);
        // reversing recovers the authored order so anchors and triggers are captured as written.
        var chainInvocations = configureLambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Reverse()
            .ToList();

        var anchors = new List<string>();
        var triggers = new List<PermittedForkTriggerModel>();
        string? compensationSeed = null;
        int? maxForks = null;

        foreach (var inv in chainInvocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SyntaxHelper.IsMethodCall(inv, "Anchor"))
            {
                foreach (var arg in inv.ArgumentList.Arguments)
                {
                    if (TryGetStringValue(arg.Expression, semanticModel, out var anchor))
                    {
                        anchors.Add(anchor);
                    }
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "PermitTrigger"))
            {
                if (TryParsePermitTrigger(inv, semanticModel, out var trigger))
                {
                    triggers.Add(trigger);
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "WithCompensationSeed"))
            {
                var seedArg = inv.ArgumentList.Arguments.FirstOrDefault();
                if (seedArg is not null && TryGetStringValue(seedArg.Expression, semanticModel, out var seed))
                {
                    compensationSeed = seed;
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "MaxForks"))
            {
                var boundArg = inv.ArgumentList.Arguments.FirstOrDefault();
                if (boundArg is not null && TryGetIntValue(boundArg.Expression, semanticModel, out var bound))
                {
                    maxForks = bound;
                }
            }
        }

        // The staged builder guarantees these floors at authoring time; a chain missing any of
        // them is malformed and is skipped rather than surfaced as a partial model.
        if (anchors.Count == 0 || triggers.Count == 0 || compensationSeed is null || maxForks is null)
        {
            return false;
        }

        model = DiagnosticForkModel.Create(anchors, triggers, compensationSeed, maxForks.Value);
        return true;
    }

    private static bool TryParsePermitTrigger(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out PermittedForkTriggerModel trigger)
    {
        trigger = default!;

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            // PermitTrigger requires the trigger plus at least one evidence field.
            return false;
        }

        if (!TryGetForkTriggerName(arguments[0].Expression, out var triggerName))
        {
            return false;
        }

        var evidenceFields = new List<string>();
        for (var i = 1; i < arguments.Count; i++)
        {
            if (TryGetStringValue(arguments[i].Expression, semanticModel, out var field))
            {
                evidenceFields.Add(field);
            }
        }

        if (evidenceFields.Count == 0)
        {
            return false;
        }

        trigger = PermittedForkTriggerModel.Create(triggerName, evidenceFields);
        return true;
    }

    /// <summary>
    /// Extracts the closed trigger's enum member name from a <c>ForkTrigger.X</c> member access
    /// (or a bare <c>X</c> identifier reached via <c>using static</c>). Purely syntactic — the
    /// generator has no reference to the closed <c>ForkTrigger</c> CLR enum.
    /// </summary>
    private static bool TryGetForkTriggerName(ExpressionSyntax expression, out string triggerName)
    {
        triggerName = string.Empty;

        switch (expression)
        {
            case MemberAccessExpressionSyntax memberAccess:
                triggerName = memberAccess.Name.Identifier.Text;
                break;
            case IdentifierNameSyntax identifier:
                triggerName = identifier.Identifier.Text;
                break;
            default:
                return false;
        }

        return !string.IsNullOrEmpty(triggerName);
    }

    private static bool TryGetStringValue(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        out string value)
    {
        value = string.Empty;

        if (expression is LiteralExpressionSyntax literal
            && literal.Kind() == SyntaxKind.StringLiteralExpression)
        {
            value = literal.Token.ValueText;
            return !string.IsNullOrEmpty(value);
        }

        // Fall back to a resolved compile-time constant (e.g. a referenced const string),
        // mirroring how LoopExtractor resolves non-literal numeric bounds.
        var constantValue = semanticModel.GetConstantValue(expression);
        if (constantValue.HasValue && constantValue.Value is string constString && constString.Length > 0)
        {
            value = constString;
            return true;
        }

        return false;
    }

    private static bool TryGetIntValue(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        out int value)
    {
        value = 0;

        if (expression is LiteralExpressionSyntax literal
            && literal.Kind() == SyntaxKind.NumericLiteralExpression
            && int.TryParse(literal.Token.ValueText, out var parsed))
        {
            value = parsed;
            return true;
        }

        var constantValue = semanticModel.GetConstantValue(expression);
        if (constantValue.HasValue && constantValue.Value is int intValue)
        {
            value = intValue;
            return true;
        }

        return false;
    }

    private static LambdaExpressionSyntax? GetConfigurationLambda(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return null;
        }

        return arguments[0].Expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple,
            ParenthesizedLambdaExpressionSyntax parens => parens,
            _ => null
        };
    }
}
