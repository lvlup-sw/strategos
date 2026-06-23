// -----------------------------------------------------------------------
// <copyright file="GeneratedCodeStamper.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Generators.Helpers;

/// <summary>
/// Central, structural enforcement point for marking generated code (#148). Every
/// emitted top-level type (and top-level delegate) is stamped with
/// <c>[global::System.CodeDom.Compiler.GeneratedCode("LevelUp.Strategos", version)]</c>, and
/// every class/struct/record additionally with
/// <c>[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]</c> (the compiler rejects
/// that attribute on enum/interface/delegate, which carry no executable code to cover), so that
/// every consumer's coverage report excludes the generated saga/events/commands/etc. types by
/// default — no per-repo <c>.runsettings</c> required, because the standard .NET coverage
/// collectors (Coverlet here; Microsoft.CodeCoverage in consumers) honour
/// <c>[ExcludeFromCodeCoverage]</c> out of the box.</summary>
/// <remarks>
/// Stamping is applied here — at the single <see cref="AddStampedSource"/> chokepoint —
/// rather than hand-rolled at each emit site, so the guarantee holds by construction for
/// every present and future emitter. Both generators route <c>AddSource</c> through this
/// type; the <c>NoBypass</c> architecture guard test fails the build if any emit path
/// bypasses it. The rewrite is idempotent: a type that already declares one of the two
/// attributes keeps it and only the missing attribute is added, so intermediate states
/// (and any deliberately pre-stamped type) never produce a duplicate-attribute error.
/// </remarks>
internal static class GeneratedCodeStamper
{
    private const string GeneratedCodeName = "global::System.CodeDom.Compiler.GeneratedCode";
    private const string GeneratedCodeSimpleName = "GeneratedCode";
    private const string ExcludeFromCoverageName = "global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage";
    private const string ExcludeFromCoverageSimpleName = "ExcludeFromCodeCoverage";

    private static readonly CSharpParseOptions ParseOptions =
        new CSharpParseOptions(LanguageVersion.Latest);

    /// <summary>
    /// Stamps <paramref name="source"/> and adds it to the compilation. This is the only
    /// supported way for a generator to emit a source file (enforced by the NoBypass test).
    /// </summary>
    /// <param name="context">The source production context to add the stamped source to.</param>
    /// <param name="hintName">The hint name for the generated file.</param>
    /// <param name="source">The unstamped generated source.</param>
    public static void AddStampedSource(SourceProductionContext context, string hintName, string source)
    {
        context.AddSource(hintName, SourceText.From(Stamp(source), Encoding.UTF8));
    }

    /// <summary>
    /// Returns <paramref name="source"/> with the generated-code attributes added to every
    /// top-level type and top-level delegate declaration. Nested types are left untouched — the
    /// enclosing top-level type's <c>[ExcludeFromCodeCoverage]</c> already excludes them from
    /// coverage. All other trivia (the <c>// &lt;auto-generated/&gt;</c> header,
    /// <c>#nullable enable</c>, XML doc comments, raw-string literals) is preserved verbatim.
    /// </summary>
    /// <param name="source">The unstamped generated source.</param>
    /// <returns>The stamped source, or the original text when there is nothing to stamp.</returns>
    public static string Stamp(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (CSharpSyntaxTree.ParseText(source, ParseOptions).GetRoot() is not CompilationUnitSyntax root)
        {
            return source;
        }

        var targets = GetTopLevelStampTargets(root).ToList();
        if (targets.Count == 0)
        {
            return source;
        }

        // Match the source's dominant newline so inserted attribute lines do not mix EOLs.
        var endOfLine = source.IndexOf("\r\n", StringComparison.Ordinal) >= 0
            ? SyntaxFactory.EndOfLine("\r\n")
            : SyntaxFactory.EndOfLine("\n");

        var newRoot = root.ReplaceNodes(
            targets,
            (original, _) => StampMember(original, endOfLine));

        return newRoot.ToFullString();
    }

    /// <summary>
    /// Enumerates every top-level stamp target — type and delegate declarations whose parent is
    /// the compilation unit or a (possibly nested) namespace. Members nested inside a type are
    /// excluded (a nested member is covered transitively by its enclosing type's attribute).
    /// </summary>
    private static IEnumerable<MemberDeclarationSyntax> GetTopLevelStampTargets(CompilationUnitSyntax root)
        => EnumerateNamespaceScopedMembers(root.Members);

    private static IEnumerable<MemberDeclarationSyntax> EnumerateNamespaceScopedMembers(
        SyntaxList<MemberDeclarationSyntax> members)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case BaseTypeDeclarationSyntax or DelegateDeclarationSyntax:
                    yield return member;
                    break;

                case BaseNamespaceDeclarationSyntax nestedNamespace:
                    foreach (var nested in EnumerateNamespaceScopedMembers(nestedNamespace.Members))
                    {
                        yield return nested;
                    }

                    break;
            }
        }
    }

    private static MemberDeclarationSyntax StampMember(MemberDeclarationSyntax member, SyntaxTrivia endOfLine)
    {
        // [GeneratedCode] is valid on every type and on delegates. [ExcludeFromCodeCoverage] is
        // only valid on class/struct/record — the compiler rejects it on enum/interface/delegate
        // with CS0592, and those carry no executable code to cover, so they get only the
        // [GeneratedCode] marker.
        var supportsExcludeFromCoverage = member is ClassDeclarationSyntax
            or StructDeclarationSyntax
            or RecordDeclarationSyntax;

        var needsGeneratedCode = !HasAttribute(member, GeneratedCodeSimpleName);
        var needsExcludeFromCoverage = supportsExcludeFromCoverage
            && !HasAttribute(member, ExcludeFromCoverageSimpleName);

        if (!needsGeneratedCode && !needsExcludeFromCoverage)
        {
            return member;
        }

        var attributesToAdd = new List<AttributeListSyntax>(2);
        if (needsGeneratedCode)
        {
            attributesToAdd.Add(BuildGeneratedCodeAttribute());
        }

        if (needsExcludeFromCoverage)
        {
            attributesToAdd.Add(BuildExcludeFromCoverageAttribute());
        }

        // Move the member's leading trivia (XML doc + blank lines) onto the first inserted
        // attribute list so the doc comment still precedes the attributes, then strip it from
        // the member. Top-level members sit at column 0, so there is no indentation to realign.
        var leadingTrivia = member.GetLeadingTrivia();

        for (var i = 0; i < attributesToAdd.Count; i++)
        {
            var attributeList = attributesToAdd[i].WithTrailingTrivia(endOfLine);
            attributeList = i == 0
                ? attributeList.WithLeadingTrivia(leadingTrivia)
                : attributeList.WithLeadingTrivia();
            attributesToAdd[i] = attributeList;
        }

        var memberWithoutLeadingTrivia = member.WithLeadingTrivia();
        var combinedAttributeLists = memberWithoutLeadingTrivia.AttributeLists.InsertRange(0, attributesToAdd);
        return memberWithoutLeadingTrivia.WithAttributeLists(combinedAttributeLists);
    }

    private static AttributeListSyntax BuildGeneratedCodeAttribute()
    {
        var arguments = SyntaxFactory.ParseAttributeArgumentList(
            $"(\"{GeneratorMetadata.ToolName}\", \"{GeneratorMetadata.ToolVersion}\")");
        var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(GeneratedCodeName), arguments);
        return SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));
    }

    private static AttributeListSyntax BuildExcludeFromCoverageAttribute()
    {
        var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(ExcludeFromCoverageName));
        return SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));
    }

    private static bool HasAttribute(MemberDeclarationSyntax member, string simpleName)
    {
        foreach (var attributeList in member.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = GetSimpleAttributeName(attribute);
                if (name == simpleName || name == simpleName + "Attribute")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetSimpleAttributeName(AttributeSyntax attribute)
    {
        var name = attribute.Name;
        while (true)
        {
            switch (name)
            {
                case QualifiedNameSyntax qualified:
                    name = qualified.Right;
                    continue;
                case AliasQualifiedNameSyntax aliasQualified:
                    name = aliasQualified.Name;
                    continue;
                case SimpleNameSyntax simple:
                    return simple.Identifier.Text;
                default:
                    return name.ToString();
            }
        }
    }
}
