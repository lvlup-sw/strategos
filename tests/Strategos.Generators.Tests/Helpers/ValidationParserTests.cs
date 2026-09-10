// -----------------------------------------------------------------------
// <copyright file="ValidationParserTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="ValidationParser"/>.
/// </summary>
[Property("Category", "Unit")]
public class ValidationParserTests
{
    // =============================================================================
    // A. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract throws ArgumentNullException when invocation is null.
    /// </summary>
    [Test]
    public void Extract_NullInvocation_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ValidationParser.Extract(null!));
    }

    // =============================================================================
    // B. Non-ValidateState Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract returns null tuple when invocation is not ValidateState.
    /// </summary>
    [Test]
    public async Task Extract_NonValidateStateCall_ReturnsNullTuple()
    {
        // Arrange
        var invocation = ParseInvocation("builder.Then<Step>()");

        // Act
        var (predicate, errorMessage) = ValidationParser.Extract(invocation);

        // Assert
        await Assert.That(predicate).IsNull();
        await Assert.That(errorMessage).IsNull();
    }

    /// <summary>
    /// Verifies that Extract returns null tuple when ValidateState has no arguments.
    /// </summary>
    [Test]
    public async Task Extract_ValidateStateNoArguments_ReturnsNullTuple()
    {
        // Arrange
        var invocation = ParseInvocation("builder.ValidateState()");

        // Act
        var (predicate, errorMessage) = ValidationParser.Extract(invocation);

        // Assert
        await Assert.That(predicate).IsNull();
        await Assert.That(errorMessage).IsNull();
    }

    // =============================================================================
    // C. Predicate Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract extracts predicate from ValidateState call.
    /// </summary>
    [Test]
    public async Task Extract_ValidateStateWithPredicate_ExtractsPredicate()
    {
        // Arrange
        var invocation = ParseInvocation("builder.ValidateState(state => state.IsValid, \"Invalid state\")");

        // Act
        var (predicate, errorMessage) = ValidationParser.Extract(invocation);

        // Assert
        await Assert.That(predicate).IsEqualTo("state.IsValid");
    }

    /// <summary>
    /// Verifies that Extract extracts error message from ValidateState call.
    /// </summary>
    [Test]
    public async Task Extract_ValidateStateWithErrorMessage_ExtractsMessage()
    {
        // Arrange
        var invocation = ParseInvocation("builder.ValidateState(state => state.IsValid, \"Invalid state\")");

        // Act
        var (predicate, errorMessage) = ValidationParser.Extract(invocation);

        // Assert
        await Assert.That(errorMessage).IsEqualTo("Invalid state");
    }

    /// <summary>
    /// Verifies that Extract extracts a complex predicate expression and normalizes the
    /// lambda parameter to the canonical name <c>state</c> so the downstream emitter's
    /// <c>state.</c> -&gt; <c>State.</c> rewrite applies regardless of the author's chosen
    /// parameter name. The non-receiver tokens (member names, literals, operators) are
    /// preserved.
    /// </summary>
    [Test]
    public async Task Extract_ComplexPredicate_ExtractsFullExpressionAndNormalizesParameter()
    {
        // Arrange — author used parameter name "s".
        var invocation = ParseInvocation("builder.ValidateState(s => s.Amount > 0 && s.Status == \"Active\", \"Must have positive amount and active status\")");

        // Act
        var (predicate, errorMessage) = ValidationParser.Extract(invocation);

        // Assert — "s" receivers normalized to "state"; everything else preserved.
        await Assert.That(predicate).IsEqualTo("state.Amount > 0 && state.Status == \"Active\"");
        await Assert.That(errorMessage).IsEqualTo("Must have positive amount and active status");
    }

    /// <summary>
    /// Verifies that Extract normalizes ANY single-parameter lambda name (here a
    /// parenthesized lambda with parameter <c>ctx</c>) to the canonical <c>state</c>, and
    /// that a member named the same as the parameter is NOT rewritten (only the receiver
    /// identifier is).
    /// </summary>
    [Test]
    public async Task Extract_NonStateParameterName_NormalizesReceiverOnly()
    {
        // Arrange — parenthesized lambda, parameter "ctx", with a member also named "ctx".
        var invocation = ParseInvocation("builder.ValidateState((ctx) => ctx.ctx == true, \"msg\")");

        // Act
        var (predicate, _) = ValidationParser.Extract(invocation);

        // Assert — only the receiver "ctx" became "state"; the member ".ctx" is untouched.
        await Assert.That(predicate).IsEqualTo("state.ctx == true");
    }

    // =============================================================================
    // Private Helpers
    // =============================================================================

    private static InvocationExpressionSyntax ParseInvocation(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {code}; }} }}");
        return syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .First();
    }
}
