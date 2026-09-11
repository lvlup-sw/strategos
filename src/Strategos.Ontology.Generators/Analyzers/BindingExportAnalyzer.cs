// -----------------------------------------------------------------------
// <copyright file="BindingExportAnalyzer.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Analyzers;

/// <summary>
/// Reports a workflow binding this assembly declares and does not export, so no
/// compilation can ever prove it (#204).
/// </summary>
/// <remarks>
/// <para>
/// This is the case the maintainer's own example names: a project that references
/// the ontology analyzer and NOT the workflow generator. It declares a binding, no
/// generator runs to notice, and the assembly ships with an obligation that reaches
/// nobody. Before #204 it compiled silent and dangling.
/// </para>
/// <para>
/// It has to be an analyzer for exactly that reason — there is no generator in that
/// build to report it. The observable fact is whether the generated catalog
/// attribute exists in THIS assembly: the workflow generator emits it whenever the
/// assembly declares an action contract, so its absence beside a binding means the
/// generator never ran here.
/// </para>
/// <para>
/// The attribute is matched by name across an assembly seam the two analyzers do not
/// share, which is a coupling rather than a reference.
/// <c>BindingExportContract</c> pins the two constants together.
/// </para>
/// </remarks>
internal static class BindingExportAnalyzer
{
    /// <summary>The metadata name of the catalog attribute the workflow generator emits.</summary>
    internal const string ProofCatalogAttributeMetadataName =
        "Strategos.Generated.StrategosProofCatalogAttribute";

    private const string BindingMethodName = "BoundToWorkflow";

    /// <summary>Runs the export check over a whole compilation.</summary>
    /// <param name="context">The compilation analysis context.</param>
    internal static void Analyze(CompilationAnalysisContext context)
    {
        var bindings = FindBindings(context).ToList();
        if (bindings.Count == 0)
        {
            return;
        }

        // A catalog is emitted whenever this assembly declares an action contract, and
        // an assembly with a binding has one. Its absence therefore means the workflow
        // generator is not installed here — not that there was nothing to export.
        if (context.Compilation.Assembly
                .GetTypeByMetadataName(ProofCatalogAttributeMetadataName) is not null)
        {
            return;
        }

        foreach (var (location, workflowName) in bindings)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.BindingNotExported,
                location,
                workflowName));
        }
    }

    private static IEnumerable<(Location Location, string WorkflowName)> FindBindings(
        CompilationAnalysisContext context)
    {
        foreach (var tree in context.Compilation.SyntaxTrees
            .OrderBy(item => item.FilePath, StringComparer.Ordinal))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var root = tree.GetRoot(context.CancellationToken);

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax member
                    || !string.Equals(
                        member.Name.Identifier.ValueText,
                        BindingMethodName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                // The name is read for the message only. A binding whose name is not a
                // constant is still a binding, and is still unexportable here.
                var argument = invocation.ArgumentList.Arguments.FirstOrDefault();
                var name = argument?.Expression is LiteralExpressionSyntax literal
                    && literal.IsKind(SyntaxKind.StringLiteralExpression)
                        ? literal.Token.ValueText
                        : "(not a compile-time constant)";

                yield return (invocation.GetLocation(), name);
            }
        }
    }
}
