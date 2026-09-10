// -----------------------------------------------------------------------
// <copyright file="SyntaxHelperTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="SyntaxHelper"/>.
/// </summary>
[Property("Category", "Unit")]
public class SyntaxHelperTests
{
    // =============================================================================
    // A. IsMethodCall Tests
    // =============================================================================

    /// <summary>
    /// Verifies that IsMethodCall returns true when method name matches.
    /// </summary>
    [Test]
    public async Task IsMethodCall_MatchingMethodName_ReturnsTrue()
    {
        // Arrange
        var invocation = ParseInvocation("obj.Then<Step>()");

        // Act
        var result = SyntaxHelper.IsMethodCall(invocation, "Then");

        // Assert
        await Assert.That(result).IsTrue();
    }

    /// <summary>
    /// Verifies that IsMethodCall returns false when method name does not match.
    /// </summary>
    [Test]
    public async Task IsMethodCall_NonMatchingMethodName_ReturnsFalse()
    {
        // Arrange
        var invocation = ParseInvocation("obj.Then<Step>()");

        // Act
        var result = SyntaxHelper.IsMethodCall(invocation, "StartWith");

        // Assert
        await Assert.That(result).IsFalse();
    }

    /// <summary>
    /// Verifies that IsMethodCall returns false for simple invocation (not member access).
    /// </summary>
    [Test]
    public async Task IsMethodCall_SimpleInvocation_ReturnsFalse()
    {
        // Arrange
        var invocation = ParseInvocation("Method()");

        // Act
        var result = SyntaxHelper.IsMethodCall(invocation, "Method");

        // Assert
        await Assert.That(result).IsFalse();
    }

    // =============================================================================
    // B. GetMethodName Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetMethodName returns correct name for generic method.
    /// </summary>
    [Test]
    public async Task GetMethodName_GenericMethod_ReturnsIdentifier()
    {
        // Arrange
        var memberAccess = ParseMemberAccess("obj.Then<Step>()");

        // Act
        var result = SyntaxHelper.GetMethodName(memberAccess);

        // Assert
        await Assert.That(result).IsEqualTo("Then");
    }

    /// <summary>
    /// Verifies that GetMethodName returns correct name for simple method.
    /// </summary>
    [Test]
    public async Task GetMethodName_SimpleMethod_ReturnsIdentifier()
    {
        // Arrange
        var memberAccess = ParseMemberAccess("obj.Create()");

        // Act
        var result = SyntaxHelper.GetMethodName(memberAccess);

        // Assert
        await Assert.That(result).IsEqualTo("Create");
    }

    // =============================================================================
    // C. GetTypeNameFromSyntax Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetTypeNameFromSyntax returns correct name for identifier.
    /// </summary>
    [Test]
    public async Task GetTypeNameFromSyntax_IdentifierName_ReturnsName()
    {
        // Arrange
        var typeSyntax = ParseTypeSyntax("ValidateOrder");

        // Act
        var result = SyntaxHelper.GetTypeNameFromSyntax(typeSyntax);

        // Assert
        await Assert.That(result).IsEqualTo("ValidateOrder");
    }

    /// <summary>
    /// Verifies that GetTypeNameFromSyntax returns right part for qualified name.
    /// </summary>
    [Test]
    public async Task GetTypeNameFromSyntax_QualifiedName_ReturnsRightPart()
    {
        // Arrange
        var typeSyntax = ParseTypeSyntax("MyNamespace.ValidateOrder");

        // Act
        var result = SyntaxHelper.GetTypeNameFromSyntax(typeSyntax);

        // Assert
        await Assert.That(result).IsEqualTo("ValidateOrder");
    }

    // =============================================================================
    // D. ExtractPropertyPath Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractPropertyPath returns correct path for simple property.
    /// </summary>
    [Test]
    public async Task ExtractPropertyPath_SimpleProperty_ReturnsPath()
    {
        // Arrange
        var memberAccess = ParsePropertyAccess("state.Type");

        // Act
        var result = SyntaxHelper.ExtractPropertyPath(memberAccess);

        // Assert
        await Assert.That(result).IsEqualTo("Type");
    }

    /// <summary>
    /// Verifies that ExtractPropertyPath returns correct path for nested property.
    /// </summary>
    [Test]
    public async Task ExtractPropertyPath_NestedProperty_ReturnsPath()
    {
        // Arrange
        var memberAccess = ParsePropertyAccess("state.Claim.Type");

        // Act
        var result = SyntaxHelper.ExtractPropertyPath(memberAccess);

        // Assert
        await Assert.That(result).IsEqualTo("Claim.Type");
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

    private static MemberAccessExpressionSyntax ParseMemberAccess(string code)
    {
        var invocation = ParseInvocation(code);
        return (MemberAccessExpressionSyntax)invocation.Expression;
    }

    private static TypeSyntax ParseTypeSyntax(string typeName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText($"class C {{ {typeName} field; }}");
        return syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .First()
            .Declaration
            .Type;
    }

    private static MemberAccessExpressionSyntax ParsePropertyAccess(string expression)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ var x = {expression}; }} }}");
        // Use First() because DescendantNodes visits outer expressions before inner
        return syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .First();
    }
}
