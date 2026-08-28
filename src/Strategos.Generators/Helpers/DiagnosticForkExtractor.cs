// -----------------------------------------------------------------------
// <copyright file="DiagnosticForkExtractor.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Diagnostics;
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
    /// <param name="diagnostics">
    /// Optional sink for extract-time rejections: a duplicate permitted trigger on one
    /// edge (#156.2) or two edges that share a sanitized compensation-seed
    /// moniker (#156.3). Colliding edges are rejected (no model) rather than
    /// first-wins-deduped or merged onto a shared <c>DiagnosticForkCount_{seed}</c>
    /// counter.
    /// </param>
    /// <returns>
    /// A list of diagnostic-fork models in the order they appear in the workflow. Empty when the
    /// workflow declares no <c>AllowDiagnosticFork(...)</c> edge, or when every edge is rejected.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static IReadOnlyList<DiagnosticForkModel> Extract(
        FluentDslParseContext context,
        ICollection<Diagnostic>? diagnostics = null)
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
        var parsedInvocations = new List<InvocationExpressionSyntax>();

        foreach (var invocation in forkInvocations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (TryParseDiagnosticFork(invocation, context, diagnostics, out var model))
            {
                forks.Add(model);
                parsedInvocations.Add(invocation);
            }
        }

        // Two edges that sanitize to the same DiagnosticForkCount_{seed} key (#156.3).
        // Reject rather than share a counter: the later edge's seed is reported, and
        // none of the colliding models are returned so the generator cannot lower a
        // shared tally (and WorkflowIncrementalGenerator gates the whole saga).
        var collidingSeeds = DiagnosticForkModel.FindDuplicateCompensationSeeds(
            forks.Select(static f => f.CompensationSeedMoniker));
        if (collidingSeeds.Count > 0)
        {
            var reported = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < forks.Count; i++)
            {
                var seed = forks[i].CompensationSeedMoniker;
                var key = DiagnosticForkModel.SanitizeCompensationSeedMoniker(seed);
                if (collidingSeeds.Any(d =>
                        string.Equals(
                            DiagnosticForkModel.SanitizeCompensationSeedMoniker(d),
                            key,
                            StringComparison.Ordinal))
                    && reported.Add(key))
                {
                    // Report on the later colliding edge (first-seen key is the original).
                    var laterIndex = -1;
                    for (var j = 0; j < forks.Count; j++)
                    {
                        if (string.Equals(
                            DiagnosticForkModel.SanitizeCompensationSeedMoniker(forks[j].CompensationSeedMoniker),
                            key,
                            StringComparison.Ordinal))
                        {
                            laterIndex = j;
                        }
                    }

                    diagnostics?.Add(Diagnostic.Create(
                        WorkflowDiagnostics.DuplicateCompensationSeed,
                        parsedInvocations[laterIndex].GetLocation(),
                        context.WorkflowName ?? "(unnamed)",
                        "AllowDiagnosticFork",
                        forks[laterIndex].CompensationSeedMoniker));
                }
            }

            return [];
        }

        return forks;
    }

    private static bool TryParseDiagnosticFork(
        InvocationExpressionSyntax invocation,
        FluentDslParseContext context,
        ICollection<Diagnostic>? diagnostics,
        out DiagnosticForkModel model)
    {
        model = default!;

        var configureLambda = GetConfigurationLambda(invocation);
        if (configureLambda is null)
        {
            return false;
        }

        var semanticModel = context.SemanticModel;
        var cancellationToken = context.CancellationToken;

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
        var seenTriggerNames = new HashSet<string>(StringComparer.Ordinal);
        var hasDuplicateTrigger = false;
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
                    if (!seenTriggerNames.Add(trigger.TriggerName))
                    {
                        hasDuplicateTrigger = true;
                        diagnostics?.Add(Diagnostic.Create(
                            WorkflowDiagnostics.DuplicatePermittedForkTrigger,
                            inv.GetLocation(),
                            context.WorkflowName ?? "(unnamed)",
                            "AllowDiagnosticFork",
                            trigger.TriggerName));
                    }

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

        // Reject the whole edge when a trigger is permitted twice — do not first-wins-dedup
        // and do not call Create (which would throw). Two same-trigger declarations can
        // carry different evidence schemas (#156.2).
        if (hasDuplicateTrigger)
        {
            return false;
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
