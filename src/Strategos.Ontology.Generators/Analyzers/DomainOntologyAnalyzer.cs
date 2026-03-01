using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Strategos.Ontology.Generators.Analyzers
{
    /// <summary>
    /// Analyzes DomainOntology subclasses for ONTO001 (no Key) and ONTO007 (duplicate type).
    /// </summary>
    internal static class DomainOntologyAnalyzer
    {
        internal static void Register(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeDefineMethod, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeDefineMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Only analyze methods named "Define"
            if (methodDeclaration.Identifier.Text != "Define")
            {
                return;
            }

            // Check that this is an override method
            if (!methodDeclaration.Modifiers.Any(SyntaxKind.OverrideKeyword))
            {
                return;
            }

            // Check the containing type derives from DomainOntology
            var containingType = context.SemanticModel.GetDeclaredSymbol(methodDeclaration)?.ContainingType;
            if (containingType == null || !IsDomainOntologySubclass(containingType))
            {
                return;
            }

            // Find all builder.Object<T>(...) invocations
            var invocations = methodDeclaration.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .ToList();

            var objectTypeInvocations = new List<(InvocationExpressionSyntax Invocation, ITypeSymbol TypeArg)>();

            foreach (var invocation in invocations)
            {
                if (IsObjectBuilderCall(invocation, context.SemanticModel, out var typeArg))
                {
                    objectTypeInvocations.Add((invocation, typeArg));
                }
            }

            // ONTO007: Check for duplicate type registrations
            var seenTypes = new Dictionary<string, InvocationExpressionSyntax>();
            foreach (var (invocation, typeArg) in objectTypeInvocations)
            {
                var typeKey = typeArg.ToDisplayString();
                if (seenTypes.ContainsKey(typeKey))
                {
                    var diagnostic = Diagnostic.Create(
                        OntologyDiagnostics.ONTO007_DuplicateObjectType,
                        invocation.GetLocation(),
                        typeArg.Name);
                    context.ReportDiagnostic(diagnostic);
                }
                else
                {
                    seenTypes[typeKey] = invocation;
                }
            }

            // ONTO001: Check each Object<T> call for a Key() invocation in its callback
            foreach (var (invocation, typeArg) in objectTypeInvocations)
            {
                if (!HasKeyCallInCallback(invocation))
                {
                    var diagnostic = Diagnostic.Create(
                        OntologyDiagnostics.ONTO001_NoKey,
                        invocation.GetLocation(),
                        typeArg.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static bool IsDomainOntologySubclass(INamedTypeSymbol typeSymbol)
        {
            var baseType = typeSymbol.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == "DomainOntology" &&
                    baseType.ContainingNamespace?.ToDisplayString() == "Strategos.Ontology")
                {
                    return true;
                }

                baseType = baseType.BaseType;
            }

            return false;
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

            // Check that it's on IOntologyBuilder
            var containingType = methodSymbol.ContainingType;
            if (containingType?.Name != "IOntologyBuilder" &&
                containingType?.Name != "OntologyBuilder")
            {
                return false;
            }

            typeArg = methodSymbol.TypeArguments[0];
            return true;
        }

        private static bool HasKeyCallInCallback(InvocationExpressionSyntax objectInvocation)
        {
            // The Object<T>() call takes a lambda argument: builder.Object<T>(obj => { obj.Key(...); })
            var arguments = objectInvocation.ArgumentList.Arguments;
            if (arguments.Count == 0)
            {
                return false;
            }

            var lambdaArg = arguments[0].Expression;

            // Look for Key() invocations within the lambda
            var nestedInvocations = lambdaArg.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var nestedInvocation in nestedInvocations)
            {
                if (nestedInvocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name.Identifier.Text == "Key")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
