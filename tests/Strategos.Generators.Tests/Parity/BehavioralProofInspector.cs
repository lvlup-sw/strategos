// -----------------------------------------------------------------------
// <copyright file="BehavioralProofInspector.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Tests.Parity;

/// <summary>
/// The status of a named behavioral proof, as observed in the file that is claimed to
/// contain it.
/// </summary>
internal enum BehavioralProofStatus
{
    /// <summary>
    /// The name resolves to a test method that a default run executes: it carries a test
    /// attribute and neither it nor its declaring type is suppressed. This is the only
    /// status that counts as a proof.
    /// </summary>
    Running,

    /// <summary>
    /// The name resolves to a test method, but the method or its declaring type is
    /// suppressed (skipped, or opt-in only), so a default run never executes it.
    /// </summary>
    Suppressed,

    /// <summary>
    /// The name is declared in the file but not as a test method — for example a plain
    /// helper that happens to share the name.
    /// </summary>
    NotAnExecutableTest,

    /// <summary>
    /// The name appears in the file's text but is not declared anywhere in it — a
    /// doc-comment reference, a plain comment, or a commented-out method body. This is the
    /// case a substring check cannot see.
    /// </summary>
    ReferencedButNotDeclared,

    /// <summary>
    /// The name does not appear in the file at all.
    /// </summary>
    Absent,

    /// <summary>
    /// The file that is claimed to contain the proof does not exist on disk.
    /// </summary>
    FileMissing,
}

/// <summary>
/// Decides whether a named behavioral test method is a proof that actually <em>runs</em>.
/// </summary>
/// <remarks>
/// <para>
/// A substring search over the proof file cannot make this call: a suppressed test, a
/// commented-out test and a name that only appears inside a <c>&lt;see cref&gt;</c> all
/// contain the method name, and all three prove nothing. This inspector parses the file
/// instead and requires a real test-method declaration that a default run would execute.
/// </para>
/// <para>
/// The check is deliberately syntactic. It parses the proof file in isolation, with no
/// compilation or project reference to the behavioral suite — pulling that suite in would
/// drag Testcontainers and Marten into a unit-test project.
/// </para>
/// </remarks>
internal static class BehavioralProofInspector
{
    /// <summary>
    /// Attribute simple names (with any <c>Attribute</c> suffix trimmed) that mark a method
    /// as a test case.
    /// </summary>
    private static readonly HashSet<string> TestAttributeNames =
        new(StringComparer.Ordinal) { "Test" };

    /// <summary>
    /// Attribute simple names (with any <c>Attribute</c> suffix trimmed) that keep a test
    /// out of a default run. Both mean the same thing for this guard's purpose: the named
    /// proof does not execute, so it proves nothing.
    /// </summary>
    private static readonly HashSet<string> SuppressionAttributeNames =
        new(StringComparer.Ordinal) { "Skip", "Explicit" };

    /// <summary>
    /// Inspects the proof file on disk for a running test method with the supplied name.
    /// </summary>
    /// <param name="proofFilePath">The absolute path to the file claimed to contain the proof.</param>
    /// <param name="methodName">The unqualified test-method name to look for.</param>
    /// <returns>The inspection result.</returns>
    public static BehavioralProofInspection InspectFile(string proofFilePath, string methodName)
    {
        ArgumentException.ThrowIfNullOrEmpty(proofFilePath);
        ArgumentException.ThrowIfNullOrEmpty(methodName);

        if (!File.Exists(proofFilePath))
        {
            return new BehavioralProofInspection(
                BehavioralProofStatus.FileMissing,
                $"no file exists at '{proofFilePath}'");
        }

        return Inspect(File.ReadAllText(proofFilePath), methodName);
    }

    /// <summary>
    /// Inspects proof source text for a running test method with the supplied name.
    /// </summary>
    /// <param name="proofSource">The C# source text of the file claimed to contain the proof.</param>
    /// <param name="methodName">The unqualified test-method name to look for.</param>
    /// <returns>The inspection result.</returns>
    public static BehavioralProofInspection Inspect(string proofSource, string methodName)
    {
        ArgumentNullException.ThrowIfNull(proofSource);
        ArgumentException.ThrowIfNullOrEmpty(methodName);

        var root = CSharpSyntaxTree.ParseText(proofSource).GetRoot();

        // Commented-out code and doc comments are trivia, so they never produce a method
        // declaration here — which is exactly the distinction the substring check missed.
        var declarations = root
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => string.Equals(m.Identifier.ValueText, methodName, StringComparison.Ordinal))
            .ToList();

        if (declarations.Count == 0)
        {
            return proofSource.Contains(methodName, StringComparison.Ordinal)
                ? new BehavioralProofInspection(
                    BehavioralProofStatus.ReferencedButNotDeclared,
                    $"'{methodName}' appears in the file's text but is not declared in it "
                    + "(a doc-comment reference or a commented-out method proves nothing)")
                : new BehavioralProofInspection(
                    BehavioralProofStatus.Absent,
                    $"'{methodName}' does not appear in the file at all");
        }

        var testMethods = declarations.Where(HasTestAttribute).ToList();
        if (testMethods.Count == 0)
        {
            return new BehavioralProofInspection(
                BehavioralProofStatus.NotAnExecutableTest,
                $"'{methodName}' is declared but carries no test attribute, so no runner executes it");
        }

        foreach (var method in testMethods)
        {
            if (FindSuppression(method) is null)
            {
                return new BehavioralProofInspection(
                    BehavioralProofStatus.Running,
                    $"'{methodName}' is a test method that a default run executes");
            }
        }

        var suppression = FindSuppression(testMethods[0])!;
        return new BehavioralProofInspection(
            BehavioralProofStatus.Suppressed,
            $"'{methodName}' is a test method but is suppressed by [{suppression}], so a default run "
            + "never executes it and it cannot fail");
    }

    /// <summary>
    /// Determines whether the method declaration carries a test attribute.
    /// </summary>
    /// <param name="method">The method declaration to examine.</param>
    /// <returns><see langword="true"/> when a test attribute is present.</returns>
    private static bool HasTestAttribute(MethodDeclarationSyntax method) =>
        EnumerateAttributeNames(method.AttributeLists).Any(TestAttributeNames.Contains);

    /// <summary>
    /// Finds the suppression attribute that keeps the method out of a default run, either
    /// on the method itself or on any declaring type that encloses it.
    /// </summary>
    /// <param name="method">The method declaration to examine.</param>
    /// <returns>The suppression attribute's simple name, or <see langword="null"/> when the method runs.</returns>
    private static string? FindSuppression(MethodDeclarationSyntax method)
    {
        var onMethod = EnumerateAttributeNames(method.AttributeLists)
            .FirstOrDefault(SuppressionAttributeNames.Contains);
        if (onMethod is not null)
        {
            return onMethod;
        }

        // A suppressed declaring type suppresses every test it contains.
        foreach (var type in method.Ancestors().OfType<TypeDeclarationSyntax>())
        {
            var onType = EnumerateAttributeNames(type.AttributeLists)
                .FirstOrDefault(SuppressionAttributeNames.Contains);
            if (onType is not null)
            {
                return onType;
            }
        }

        return null;
    }

    /// <summary>
    /// Enumerates the simple names of every attribute in the supplied lists, with any
    /// namespace qualification and the conventional <c>Attribute</c> suffix removed, so
    /// <c>[Skip]</c>, <c>[SkipAttribute]</c> and <c>[TUnit.Core.SkipAttribute]</c> all
    /// reduce to <c>Skip</c>.
    /// </summary>
    /// <param name="attributeLists">The attribute lists to walk.</param>
    /// <returns>The normalized attribute simple names.</returns>
    private static IEnumerable<string> EnumerateAttributeNames(
        SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var list in attributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                yield return TrimAttributeSuffix(GetSimpleName(attribute.Name));
            }
        }
    }

    /// <summary>
    /// Reduces an attribute name syntax to its right-most identifier.
    /// </summary>
    /// <param name="name">The attribute name syntax.</param>
    /// <returns>The right-most identifier text.</returns>
    private static string GetSimpleName(NameSyntax name) => name switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => name.ToString(),
    };

    /// <summary>
    /// Removes the conventional <c>Attribute</c> suffix from an attribute simple name.
    /// </summary>
    /// <param name="simpleName">The attribute simple name.</param>
    /// <returns>The name without its <c>Attribute</c> suffix.</returns>
    private static string TrimAttributeSuffix(string simpleName) =>
        simpleName.EndsWith("Attribute", StringComparison.Ordinal)
            ? simpleName[..^"Attribute".Length]
            : simpleName;
}

/// <summary>
/// The outcome of inspecting a file for a named behavioral proof.
/// </summary>
/// <param name="Status">What the name resolved to in the inspected file.</param>
/// <param name="Detail">A human-readable explanation, used in assertion messages.</param>
internal sealed record BehavioralProofInspection(BehavioralProofStatus Status, string Detail)
{
    /// <summary>
    /// Gets a value indicating whether the named proof is a test a default run executes —
    /// the only status the parity guard accepts.
    /// </summary>
    public bool IsRunningProof => this.Status == BehavioralProofStatus.Running;
}
