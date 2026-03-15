// -----------------------------------------------------------------------
// <copyright file="BranchExtractor.cs" company="Levelup Software">
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
/// Extracts branch models from a workflow definition.
/// </summary>
internal static class BranchExtractor
{
    /// <summary>
    /// Extracts branch models from the workflow DSL for saga handler generation.
    /// </summary>
    /// <param name="context">The parse context containing pre-computed lookups.</param>
    /// <returns>A list of branch models in the order they appear in the workflow.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static IReadOnlyList<BranchModel> Extract(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // Find all Branch() method calls
        // Sort by source position to ensure branches are in workflow order
        // This is critical for identifying consecutive branches (those immediately following other branches)
        var branchInvocations = context.AllInvocations
            .Where(inv => SyntaxHelper.IsMethodCall(inv, "Branch"))
            .OrderBy(inv => inv.Span.End) // Sort by End position for correct chain order (inner invocations have smaller End)
            .ToList();

        if (branchInvocations.Count == 0)
        {
            return [];
        }

        // Get all step names in order for determining previous/rejoin steps
        var stepInfos = StepExtractor.ExtractStepInfos(context);
        var stepNames = stepInfos.Select(s => s.PhaseName).ToList();

        var branches = new List<BranchModel>();
        var branchIndex = 0;

        foreach (var branchInvocation in branchInvocations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (TryParseBranch(branchInvocation, context.SemanticModel, context.WorkflowName ?? string.Empty, branchIndex, stepNames, out var branchModel, context.CancellationToken))
            {
                branches.Add(branchModel);
                branchIndex++;
            }
        }

        // Link consecutive branches and return only head branches
        return LinkConsecutiveBranches(branches);
    }

    /// <summary>
    /// Links consecutive branches together while preserving all branches in the output list.
    /// </summary>
    /// <param name="branches">The list of all parsed branches.</param>
    /// <returns>A list containing all branches, with head branches having NextConsecutiveBranch linked.</returns>
    /// <remarks>
    /// <para>
    /// Consecutive branches are identified by having an empty <c>PreviousStepName</c>.
    /// For example, in this workflow:
    /// <code>
    /// .Join&lt;AggregateVotes&gt;()
    /// .Branch(state => state.Cond1, ...)
    /// .Branch(state => state.Cond2, ...)
    /// .Branch(state => state.Cond3, ...)
    /// .Then&lt;NextStep&gt;()
    /// </code>
    /// Branch 1 has <c>PreviousStepName = "AggregateVotes"</c> (head branch).
    /// Branches 2 and 3 have <c>PreviousStepName = ""</c> (consecutive branches).
    /// </para>
    /// <para>
    /// This method links them: Branch1.NextConsecutiveBranch → Branch2.NextConsecutiveBranch → Branch3.
    /// ALL branches are returned in the output list (needed for step handler generation),
    /// but only head branches have NextConsecutiveBranch populated.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<BranchModel> LinkConsecutiveBranches(List<BranchModel> branches)
    {
        if (branches.Count == 0)
        {
            return branches;
        }

        var result = new List<BranchModel>();
        var i = 0;

        while (i < branches.Count)
        {
            var current = branches[i];

            // If this is a consecutive branch (empty PreviousStepName), just add it as-is
            if (string.IsNullOrEmpty(current.PreviousStepName))
            {
                result.Add(current);
                i++;
                continue;
            }

            // This is a head branch - collect and link consecutive branches that follow it
            var (linkedHead, consecutiveBranches, nextIndex) = CollectAndLinkConsecutiveGroup(branches, i);
            result.Add(linkedHead);

            // Add all the consecutive branches as-is (for step handler generation)
            result.AddRange(consecutiveBranches);

            i = nextIndex;
        }

        return result;
    }

    /// <summary>
    /// Collects consecutive branches following a head branch and links them into a chain.
    /// </summary>
    /// <param name="branches">All branches in order.</param>
    /// <param name="headIndex">The index of the head branch.</param>
    /// <returns>The linked head branch, the list of consecutive branches, and the next index to process.</returns>
    private static (BranchModel LinkedHead, List<BranchModel> ConsecutiveBranches, int NextIndex) CollectAndLinkConsecutiveGroup(
        List<BranchModel> branches,
        int headIndex)
    {
        var head = branches[headIndex];
        var consecutiveBranches = new List<BranchModel>();

        var j = headIndex + 1;
        while (j < branches.Count && string.IsNullOrEmpty(branches[j].PreviousStepName))
        {
            consecutiveBranches.Add(branches[j]);
            j++;
        }

        var linkedHead = consecutiveBranches.Count > 0
            ? BuildConsecutiveChain(head, consecutiveBranches)
            : head;

        return (linkedHead, consecutiveBranches, j);
    }

    /// <summary>
    /// Builds a linked chain of consecutive branches starting from the head branch.
    /// </summary>
    /// <param name="headBranch">The head branch.</param>
    /// <param name="consecutiveBranches">The consecutive branches that follow the head.</param>
    /// <returns>The head branch with consecutive branches linked via NextConsecutiveBranch.</returns>
    private static BranchModel BuildConsecutiveChain(BranchModel headBranch, List<BranchModel> consecutiveBranches)
    {
        if (consecutiveBranches.Count == 0)
        {
            return headBranch;
        }

        // Build the chain from the end backwards
        // e.g., if we have [Branch1, Branch2, Branch3], we build:
        // Branch2.NextConsecutiveBranch = Branch3
        // Branch1.NextConsecutiveBranch = Branch2
        var linkedTail = consecutiveBranches[consecutiveBranches.Count - 1];
        for (var k = consecutiveBranches.Count - 2; k >= 0; k--)
        {
            linkedTail = consecutiveBranches[k] with { NextConsecutiveBranch = linkedTail };
        }

        // Link head to the chain
        return headBranch with { NextConsecutiveBranch = linkedTail };
    }

    private static bool TryParseBranch(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string workflowName,
        int branchIndex,
        List<string> stepNames,
        out BranchModel branchModel,
        CancellationToken cancellationToken)
    {
        branchModel = default!;

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return false;
        }

        // Skip branches that immediately follow a RepeatUntil loop
        // These are handled by the loop exit handler via BranchOnExitId
        if (IsBranchAfterLoop(invocation))
        {
            return false;
        }

        // First argument: discriminator (lambda or method reference)
        var discriminatorArg = arguments[0];
        if (!TryExtractDiscriminatorInfo(discriminatorArg, semanticModel, out var propertyPath, out var typeName, out var isEnum, out var isMethod))
        {
            return false;
        }

        // Find previous step (the receiver of the Branch call)
        var previousStepName = FindPreviousStepName(invocation, semanticModel);

        // Find rejoin step (step after this branch in the chain)
        var rejoinStepName = FindRejoinStepName(invocation, semanticModel, stepNames);

        // Determine the loop prefix for this branch (if inside a loop)
        // Branch case steps need the same prefix as the branch's previous/rejoin steps
        var loopPrefix = DetermineLoopPrefix(invocation);

        // Parse branch cases from remaining arguments
        var cases = new List<BranchCaseModel>();
        for (var i = 1; i < arguments.Count; i++)
        {
            if (TryParseBranchCase(arguments[i], semanticModel, propertyPath, cancellationToken, out var caseModel))
            {
                cases.Add(caseModel);
            }
        }

        if (cases.Count == 0)
        {
            return false;
        }

        var branchId = $"{workflowName}-Branch{branchIndex}-{propertyPath}";

        branchModel = new BranchModel(
            BranchId: branchId,
            PreviousStepName: previousStepName ?? string.Empty,
            DiscriminatorPropertyPath: propertyPath,
            DiscriminatorTypeName: typeName,
            IsEnumDiscriminator: isEnum,
            IsMethodDiscriminator: isMethod,
            Cases: cases,
            RejoinStepName: rejoinStepName,
            LoopPrefix: loopPrefix);

        return true;
    }

    private static bool TryExtractDiscriminatorInfo(
        ArgumentSyntax discriminatorArg,
        SemanticModel semanticModel,
        out string propertyPath,
        out string typeName,
        out bool isEnum,
        out bool isMethod)
    {
        propertyPath = string.Empty;
        typeName = string.Empty;
        isEnum = false;
        isMethod = false;

        // Try to extract lambda: state => state.Property or state => state.Method()
        var lambda = discriminatorArg.Expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple,
            ParenthesizedLambdaExpressionSyntax parens => (LambdaExpressionSyntax)parens,
            _ => null
        };

        if (lambda is not null)
        {
            return TryExtractLambdaDiscriminator(lambda, semanticModel, out propertyPath, out typeName, out isEnum, out isMethod);
        }

        // Try to extract method reference: DetermineOutcome (IdentifierNameSyntax)
        if (discriminatorArg.Expression is IdentifierNameSyntax identifier)
        {
            return TryExtractMethodReferenceDiscriminator(identifier, semanticModel, out propertyPath, out typeName, out isEnum, out isMethod);
        }

        return false;
    }

    /// <summary>
    /// Extracts discriminator info from a lambda expression.
    /// Handles both property access (state => state.Prop) and method invocation (state => state.Method()).
    /// </summary>
    private static bool TryExtractLambdaDiscriminator(
        LambdaExpressionSyntax lambda,
        SemanticModel semanticModel,
        out string propertyPath,
        out string typeName,
        out bool isEnum,
        out bool isMethod)
    {
        propertyPath = string.Empty;
        typeName = string.Empty;
        isEnum = false;
        isMethod = false;

        var body = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Body,
            ParenthesizedLambdaExpressionSyntax parens => parens.Body,
            _ => null
        };

        // Handle property access: state => state.GateRequiresHitl
        if (body is MemberAccessExpressionSyntax memberAccess)
        {
            return TryExtractPropertyDiscriminator(memberAccess, semanticModel, out propertyPath, out typeName, out isEnum);
        }

        // Handle method invocation on state: state => state.IsInCrisisMode()
        if (body is InvocationExpressionSyntax invocationBody &&
            invocationBody.Expression is MemberAccessExpressionSyntax invokedMember)
        {
            isMethod = true;
            return TryExtractMethodInvocationDiscriminator(invocationBody, invokedMember, semanticModel, out propertyPath, out typeName, out isEnum);
        }

        return false;
    }

    /// <summary>
    /// Extracts discriminator info from a property access expression (state => state.Property).
    /// </summary>
    private static bool TryExtractPropertyDiscriminator(
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel semanticModel,
        out string propertyPath,
        out string typeName,
        out bool isEnum)
    {
        propertyPath = SyntaxHelper.ExtractPropertyPath(memberAccess);
        typeName = "Object";
        isEnum = false;

        var typeInfo = semanticModel.GetTypeInfo(memberAccess);
        if (typeInfo.Type is INamedTypeSymbol namedType)
        {
            typeName = namedType.Name;
            isEnum = namedType.TypeKind == TypeKind.Enum;
        }

        return !string.IsNullOrEmpty(propertyPath);
    }

    /// <summary>
    /// Extracts discriminator info from a method invocation on state (state => state.IsInCrisisMode()).
    /// Treats as property-style access but includes () in the path for method call syntax.
    /// </summary>
    private static bool TryExtractMethodInvocationDiscriminator(
        InvocationExpressionSyntax invocationBody,
        MemberAccessExpressionSyntax invokedMember,
        SemanticModel semanticModel,
        out string propertyPath,
        out string typeName,
        out bool isEnum)
    {
        // Extract method name with parentheses for proper code generation
        // This generates: State.IsInCrisisMode() (like property but with method call syntax)
        propertyPath = invokedMember.Name.Identifier.Text + "()";
        typeName = "Boolean"; // Most method discriminators return bool
        isEnum = false;

        var typeInfo = semanticModel.GetTypeInfo(invocationBody);
        if (typeInfo.Type is INamedTypeSymbol namedType)
        {
            typeName = namedType.Name;
            isEnum = namedType.TypeKind == TypeKind.Enum;
        }

        return !string.IsNullOrEmpty(propertyPath);
    }

    /// <summary>
    /// Extracts discriminator info from a method reference identifier (e.g., DetermineOutcome).
    /// </summary>
    private static bool TryExtractMethodReferenceDiscriminator(
        IdentifierNameSyntax identifier,
        SemanticModel semanticModel,
        out string propertyPath,
        out string typeName,
        out bool isEnum,
        out bool isMethod)
    {
        propertyPath = identifier.Identifier.Text;
        typeName = "Object";
        isEnum = false;
        isMethod = true;

        var symbolInfo = semanticModel.GetSymbolInfo(identifier);
        if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
        {
            var returnType = methodSymbol.ReturnType;
            if (returnType is INamedTypeSymbol returnNamedType)
            {
                typeName = returnNamedType.Name;
                isEnum = returnNamedType.TypeKind == TypeKind.Enum;
            }
            else
            {
                typeName = returnType.Name;
            }
        }

        return !string.IsNullOrEmpty(propertyPath);
    }

    /// <summary>
    /// Checks if this Branch invocation immediately follows a RepeatUntil loop.
    /// </summary>
    /// <param name="branchInvocation">The Branch invocation to check.</param>
    /// <returns>True if the Branch's receiver is a RepeatUntil call.</returns>
    private static bool IsBranchAfterLoop(InvocationExpressionSyntax branchInvocation)
    {
        // Get the receiver of the Branch call (what .Branch() is called on)
        if (branchInvocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is InvocationExpressionSyntax previousInvocation)
            {
                // Check if the receiver is a RepeatUntil call
                return SyntaxHelper.IsMethodCall(previousInvocation, "RepeatUntil");
            }
        }

        return false;
    }

    private static string? FindPreviousStepName(InvocationExpressionSyntax branchInvocation, SemanticModel semanticModel)
    {
        // Determine the loop prefix for this branch (if inside a loop)
        var loopPrefix = DetermineLoopPrefix(branchInvocation);

        // Walk backwards to find the previous step
        if (branchInvocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is InvocationExpressionSyntax previousInvocation)
            {
                if (StepExtractor.TryGetStepName(previousInvocation, semanticModel, out var stepName))
                {
                    // Apply loop prefix to match the prefixed step names used in saga handlers
                    return ApplyPrefix(stepName, loopPrefix);
                }

                // If the previous call is also a Branch, don't recurse - this branch follows another branch
                // Return null to indicate this branch is part of a chain and should be evaluated
                // after the previous branch's rejoin (handled by RejoinStepName chain)
                if (SyntaxHelper.IsMethodCall(previousInvocation, "Branch"))
                {
                    return null;
                }

                // Recurse backwards if the previous call isn't a step and isn't a branch
                return FindPreviousStepName(previousInvocation, semanticModel);
            }
        }

        return null;
    }

    private static string? FindRejoinStepName(
        InvocationExpressionSyntax branchInvocation,
        SemanticModel semanticModel,
        List<string> stepNames)
    {
        // Determine if this branch is inside a loop and get the loop prefix
        var loopPrefix = DetermineLoopPrefix(branchInvocation);

        // Walk the chain of calls starting from this branch to find the next step
        // For consecutive branches like .Branch(cond1).Branch(cond2).Then<Step>(),
        // we need to walk through all the branches to find the final step
        var currentInvocation = branchInvocation;

        while (currentInvocation is not null)
        {
            var nextInvocation = FindNextChainedInvocation(currentInvocation);

            if (nextInvocation is null)
            {
                break;
            }

            // If it's a step, we found the rejoin point
            if (StepExtractor.TryGetStepName(nextInvocation, semanticModel, out var stepName))
            {
                return ApplyPrefix(stepName, loopPrefix);
            }

            // If it's another branch, continue walking from that branch
            if (SyntaxHelper.IsMethodCall(nextInvocation, "Branch"))
            {
                currentInvocation = nextInvocation;
                continue;
            }

            break;
        }

        return null;
    }

    /// <summary>
    /// Finds the next invocation that is chained off the given invocation (i.e., the caller of this invocation).
    /// </summary>
    private static InvocationExpressionSyntax? FindNextChainedInvocation(InvocationExpressionSyntax invocation)
    {
        var parent = invocation.Parent;
        while (parent is not null)
        {
            if (parent is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression == invocation &&
                memberAccess.Parent is InvocationExpressionSyntax nextInvocation)
            {
                return nextInvocation;
            }

            parent = parent.Parent;
        }

        return null;
    }

    /// <summary>
    /// Determines the loop prefix for a branch invocation by walking up the syntax tree
    /// to find parent RepeatUntil lambda bodies.
    /// </summary>
    /// <param name="branchInvocation">The branch invocation syntax.</param>
    /// <returns>The combined loop prefix (e.g., "Outer_Inner"), or null if not in a loop.</returns>
    private static string? DetermineLoopPrefix(InvocationExpressionSyntax branchInvocation)
    {
        var loopNames = new List<string>();
        var current = branchInvocation.Parent;

        while (current is not null)
        {
            if (current is LambdaExpressionSyntax lambda)
            {
                var loopName = FindContainingLoopName(lambda);
                if (loopName is not null)
                {
                    loopNames.Insert(0, loopName);
                }
            }

            current = current.Parent;
        }

        if (loopNames.Count == 0)
        {
            return null;
        }

        return string.Join("_", loopNames);
    }

    /// <summary>
    /// Finds the loop name if the given lambda is the body argument of a RepeatUntil call.
    /// </summary>
    /// <param name="lambda">The lambda expression to check.</param>
    /// <returns>The loop name, or null if not a RepeatUntil body.</returns>
    private static string? FindContainingLoopName(LambdaExpressionSyntax lambda)
    {
        // The lambda's parent should be ArgumentSyntax -> ArgumentListSyntax -> InvocationExpressionSyntax (RepeatUntil)
        if (lambda.Parent is not ArgumentSyntax arg)
        {
            return null;
        }

        if (arg.Parent is not ArgumentListSyntax argList)
        {
            return null;
        }

        if (argList.Parent is not InvocationExpressionSyntax invocation)
        {
            return null;
        }

        if (!SyntaxHelper.IsMethodCall(invocation, "RepeatUntil"))
        {
            return null;
        }

        // RepeatUntil has 4 arguments: condition, loopName, body, maxIterations
        // The body is the 3rd argument (index 2)
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 3)
        {
            return null;
        }

        // Check if this lambda is the body argument
        var bodyArgIndex = 2;
        if (arguments.Count <= bodyArgIndex || arguments[bodyArgIndex] != arg)
        {
            return null;
        }

        // Extract the loop name from the second argument
        var loopNameArg = arguments[1];
        if (loopNameArg.Expression is LiteralExpressionSyntax literal
            && literal.Kind() == SyntaxKind.StringLiteralExpression)
        {
            return literal.Token.ValueText;
        }

        return null;
    }

    /// <summary>
    /// Applies a loop prefix to a step name.
    /// </summary>
    /// <param name="stepName">The step name.</param>
    /// <param name="prefix">The loop prefix, or null for no prefix.</param>
    /// <returns>The prefixed step name, or the original if no prefix.</returns>
    private static string? ApplyPrefix(string? stepName, string? prefix)
    {
        if (prefix is null || stepName is null)
        {
            return stepName;
        }

        return $"{prefix}_{stepName}";
    }

    private static bool TryParseBranchCase(
        ArgumentSyntax caseArg,
        SemanticModel semanticModel,
        string branchPropertyPath,
        CancellationToken cancellationToken,
        out BranchCaseModel caseModel)
    {
        caseModel = default!;

        if (caseArg.Expression is not InvocationExpressionSyntax caseInvocation)
        {
            return false;
        }

        // Extract the case value and path builder argument from When() or Otherwise()
        if (!TryExtractCaseArguments(caseInvocation, out var caseValueLiteral, out var pathBuilderArg))
        {
            return false;
        }

        // Extract path builder lambda
        var pathLambda = pathBuilderArg.Expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple,
            ParenthesizedLambdaExpressionSyntax parens => (LambdaExpressionSyntax)parens,
            _ => null
        };

        if (pathLambda is null)
        {
            return false;
        }

        // Extract steps from the path builder
        var stepNames = new List<string>();
        var isTerminal = false;
        ParseBranchPathBody(pathLambda, semanticModel, stepNames, ref isTerminal, cancellationToken);

        if (stepNames.Count == 0)
        {
            return false;
        }

        var branchPathPrefix = $"{branchPropertyPath}_{caseValueLiteral.Replace(".", "_").Replace(" ", "_")}";

        caseModel = new BranchCaseModel(
            CaseValueLiteral: caseValueLiteral,
            BranchPathPrefix: branchPathPrefix,
            StepNames: stepNames,
            IsTerminal: isTerminal);

        return true;
    }

    /// <summary>
    /// Extracts the case value literal and path builder argument from a When() or Otherwise() invocation.
    /// </summary>
    private static bool TryExtractCaseArguments(
        InvocationExpressionSyntax caseInvocation,
        out string caseValueLiteral,
        out ArgumentSyntax pathBuilderArg)
    {
        caseValueLiteral = string.Empty;
        pathBuilderArg = default!;

        var isOtherwise = SyntaxHelper.IsMethodCall(caseInvocation, "Otherwise");
        var isWhen = SyntaxHelper.IsMethodCall(caseInvocation, "When");

        if (!isWhen && !isOtherwise)
        {
            return false;
        }

        var caseArgs = caseInvocation.ArgumentList.Arguments;

        if (isOtherwise)
        {
            if (caseArgs.Count < 1)
            {
                return false;
            }

            caseValueLiteral = "default";
            pathBuilderArg = caseArgs[0];
            return true;
        }

        // isWhen
        if (caseArgs.Count < 2)
        {
            return false;
        }

        caseValueLiteral = ExtractCaseValueLiteral(caseArgs[0]);
        pathBuilderArg = caseArgs[1];
        return true;
    }

    private static string ExtractCaseValueLiteral(ArgumentSyntax valueArg)
    {
        var expression = valueArg.Expression;

        return expression switch
        {
            // Enum value: ClaimType.Auto
            MemberAccessExpressionSyntax memberAccess => memberAccess.ToString(),
            // String literal: "pdf"
            LiteralExpressionSyntax literal when literal.Kind() == SyntaxKind.StringLiteralExpression => literal.Token.ValueText,
            // Boolean: true/false
            LiteralExpressionSyntax literal when literal.Kind() == SyntaxKind.TrueLiteralExpression => "true",
            LiteralExpressionSyntax literal when literal.Kind() == SyntaxKind.FalseLiteralExpression => "false",
            // Numeric: 1, 2, etc.
            LiteralExpressionSyntax literal when literal.Kind() == SyntaxKind.NumericLiteralExpression => literal.Token.ValueText,
            // Default fallback
            _ => expression.ToString()
        };
    }

    private static void ParseBranchPathBody(
        LambdaExpressionSyntax pathLambda,
        SemanticModel semanticModel,
        List<string> stepNames,
        ref bool isTerminal,
        CancellationToken cancellationToken)
    {
        // Find all invocations in the path body, reversed for correct order
        var allInvocations = pathLambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Reverse()
            .ToList();

        foreach (var inv in allInvocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SyntaxHelper.IsMethodCall(inv, "Then"))
            {
                if (StepExtractor.TryGetStepName(inv, semanticModel, out var stepName))
                {
                    stepNames.Add(stepName);
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Complete"))
            {
                isTerminal = true;
            }
        }
    }
}
