using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Strategos.Ontology.Generators.Analyzers
{
    /// <summary>
    /// Analyzes CrossDomainLink and Object&lt;T&gt;() for ONTO003 (unknown domain) and ONTO004 (no actions).
    /// </summary>
    internal static class CrossDomainLinkAnalyzer
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

            AnalyzeONTO003(invocations, context);
            AnalyzeONTO004(invocations, context);
        }

        private static void AnalyzeONTO003(List<InvocationExpressionSyntax> invocations, SyntaxNodeAnalysisContext context)
        {
            // Find ToExternal() calls and warn about unknown domains
            foreach (var invocation in invocations)
            {
                if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess))
                {
                    continue;
                }

                if (memberAccess.Name.Identifier.Text != "ToExternal")
                {
                    continue;
                }

                // Check this is on ICrossDomainLinkBuilder
                var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
                var methodSymbol = symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

                if (methodSymbol == null)
                {
                    continue;
                }

                var methodContainingType = methodSymbol.ContainingType;
                if (methodContainingType?.Name != "ICrossDomainLinkBuilder" &&
                    methodContainingType?.Name != "CrossDomainLinkBuilder")
                {
                    continue;
                }

                // Extract domain name from first argument
                if (invocation.ArgumentList.Arguments.Count < 1)
                {
                    continue;
                }

                var domainArg = invocation.ArgumentList.Arguments[0].Expression;
                if (domainArg is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    var domainName = literal.Token.ValueText;

                    // Find the CrossDomainLink("name") call that this chains from
                    var linkName = FindCrossDomainLinkName(memberAccess, context.SemanticModel);

                    // Report the warning - at compile time we cannot verify domains exist
                    // so we always report as a warning for the developer to verify
                    var diagnostic = Diagnostic.Create(
                        OntologyDiagnostics.ONTO003_UnknownDomain,
                        invocation.GetLocation(),
                        linkName ?? "unknown",
                        domainName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static void AnalyzeONTO004(List<InvocationExpressionSyntax> invocations, SyntaxNodeAnalysisContext context)
        {
            // Find Object<T>() calls and check if they have Action() calls in their lambda
            foreach (var invocation in invocations)
            {
                if (!IsObjectBuilderCall(invocation, context.SemanticModel, out var typeArg))
                {
                    continue;
                }

                if (invocation.ArgumentList.Arguments.Count == 0)
                {
                    continue;
                }

                var lambdaArg = invocation.ArgumentList.Arguments[0].Expression;
                var hasAction = lambdaArg.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(nested =>
                        nested.Expression is MemberAccessExpressionSyntax ma &&
                        ma.Name.Identifier.Text == "Action");

                if (!hasAction)
                {
                    var diagnostic = Diagnostic.Create(
                        OntologyDiagnostics.ONTO004_NoActions,
                        invocation.GetLocation(),
                        typeArg.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static string? FindCrossDomainLinkName(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel)
        {
            // Walk up the fluent chain to find CrossDomainLink("name") invocation
            var current = memberAccess.Expression;
            while (current is InvocationExpressionSyntax chainedInvocation)
            {
                if (chainedInvocation.Expression is MemberAccessExpressionSyntax chainedMember)
                {
                    if (chainedMember.Name.Identifier.Text == "CrossDomainLink")
                    {
                        if (chainedInvocation.ArgumentList.Arguments.Count > 0 &&
                            chainedInvocation.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax lit &&
                            lit.IsKind(SyntaxKind.StringLiteralExpression))
                        {
                            return lit.Token.ValueText;
                        }
                    }

                    current = chainedMember.Expression;
                }
                else
                {
                    break;
                }
            }

            return null;
        }

        private static bool IsObjectBuilderCall(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            out ITypeSymbol typeArg)
        {
            typeArg = null!;

            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            var methodSymbol = symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

            if (methodSymbol == null)
            {
                return false;
            }

            if (methodSymbol.Name != "Object" || !methodSymbol.IsGenericMethod || methodSymbol.TypeArguments.Length != 1)
            {
                return false;
            }

            var methodContainingType = methodSymbol.ContainingType;
            if (methodContainingType?.Name != "IOntologyBuilder" &&
                methodContainingType?.Name != "OntologyBuilder")
            {
                return false;
            }

            typeArg = methodSymbol.TypeArguments[0];
            return true;
        }
    }
}
