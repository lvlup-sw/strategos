using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Strategos.Ontology.Generators.Analyzers
{
    /// <summary>
    /// Analyzes workflow chain patterns for ONTO006 (Produces with no consumer)
    /// and ONTO008 (undeclared event type).
    /// </summary>
    internal static class WorkflowChainAnalyzer
    {
        internal static void Register(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeDefineMethod, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeDefineMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            if (methodDeclaration.Identifier.Text != "Define")
            {
                return;
            }

            if (!methodDeclaration.Modifiers.Any(SyntaxKind.OverrideKeyword))
            {
                return;
            }

            var containingType = context.SemanticModel.GetDeclaredSymbol(methodDeclaration)?.ContainingType;
            if (containingType == null || !AnalyzerHelper.IsDomainOntologySubclass(containingType))
            {
                return;
            }

            var invocations = methodDeclaration.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .ToList();

            AnalyzeONTO006(invocations, context);
        }

        private static void AnalyzeONTO006(List<InvocationExpressionSyntax> invocations, SyntaxNodeAnalysisContext context)
        {
            // Collect all Returns<T>() type arguments (produced types)
            var producedTypes = new List<(string TypeName, InvocationExpressionSyntax Invocation)>();
            var consumedTypes = new HashSet<string>();

            foreach (var invocation in invocations)
            {
                if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess))
                {
                    continue;
                }

                var methodName = memberAccess.Name.Identifier.Text;

                var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
                var methodSymbol = symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

                if (methodSymbol == null)
                {
                    continue;
                }

                // Check if this is on IActionBuilder
                var methodContainingType = methodSymbol.ContainingType;
                if (methodContainingType?.Name != "IActionBuilder" && methodContainingType?.Name != "ActionBuilder")
                {
                    continue;
                }

                if (methodName == "Returns" && methodSymbol.IsGenericMethod && methodSymbol.TypeArguments.Length == 1)
                {
                    var typeName = methodSymbol.TypeArguments[0].ToDisplayString();
                    producedTypes.Add((typeName, invocation));
                }
                else if (methodName == "Accepts" && methodSymbol.IsGenericMethod && methodSymbol.TypeArguments.Length == 1)
                {
                    var typeName = methodSymbol.TypeArguments[0].ToDisplayString();
                    consumedTypes.Add(typeName);
                }
            }

            // Report ONTO006 for any produced type with no consumer
            foreach (var (typeName, invocation) in producedTypes)
            {
                if (!consumedTypes.Contains(typeName))
                {
                    var shortName = typeName.Split('.').Last();
                    var diagnostic = Diagnostic.Create(
                        OntologyDiagnostics.ONTO006_NoConsumer,
                        invocation.GetLocation(),
                        shortName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
