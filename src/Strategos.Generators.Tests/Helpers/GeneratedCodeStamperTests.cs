// -----------------------------------------------------------------------
// <copyright file="GeneratedCodeStamperTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Strategos.Generators.Helpers;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="GeneratedCodeStamper"/> — the central enforcement point that
/// stamps every emitted top-level type with <c>[GeneratedCode]</c> + <c>[ExcludeFromCodeCoverage]</c> (#148).
/// </summary>
[Property("Category", "Unit")]
public sealed class GeneratedCodeStamperTests
{
    private const string GeneratedCodeMarker = "global::System.CodeDom.Compiler.GeneratedCode(\"LevelUp.Strategos\"";
    private const string ExcludeMarker = "global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage";
    private const string GeneratedCodeAttributeName = "GeneratedCode";
    private const string ExcludeAttributeName = "ExcludeFromCodeCoverage";

    // =============================================================================
    // A. Every top-level type kind is stamped with both attributes
    // =============================================================================

    [Test]
    [Arguments("public class Widget { }")]
    [Arguments("public struct Widget { }")]
    [Arguments("public sealed record Widget(int Value);")]
    [Arguments("public readonly record struct Widget(int Value);")]
    public async Task Stamp_ClassStructRecord_GetsBothAttributes(string typeDeclaration)
    {
        var stamped = GeneratedCodeStamper.Stamp(Wrap(typeDeclaration));

        var attributes = AttributesOf(stamped, "Widget");
        await Assert.That(attributes).Contains(GeneratedCodeAttributeName);
        await Assert.That(attributes).Contains(ExcludeAttributeName);
    }

    [Test]
    [Arguments("public interface Widget { }")]
    [Arguments("public enum Widget { A, B }")]
    [Arguments("public delegate void Widget();")]
    public async Task Stamp_EnumInterfaceOrDelegate_GetsGeneratedCodeOnly(string typeDeclaration)
    {
        // [ExcludeFromCodeCoverage] is rejected by the compiler (CS0592) on enum/interface/delegate,
        // and they carry no executable code to cover, so only the [GeneratedCode] marker is applied.
        var stamped = GeneratedCodeStamper.Stamp(Wrap(typeDeclaration));

        var attributes = AttributesOf(stamped, "Widget");
        await Assert.That(attributes).Contains(GeneratedCodeAttributeName);
        await Assert.That(attributes).DoesNotContain(ExcludeAttributeName);
    }

    [Test]
    public async Task Stamp_UsesLevelUpStrategosToolName_GloballyQualified()
    {
        var stamped = GeneratedCodeStamper.Stamp(Wrap("public class Widget { }"));

        await Assert.That(stamped).Contains(GeneratedCodeMarker);
        await Assert.That(stamped).Contains(ExcludeMarker);
    }

    [Test]
    public async Task Stamp_DoesNotRetainOldStrategosGeneratorsToolName()
    {
        // The corrected tool name is the package family, not the assembly name.
        var stamped = GeneratedCodeStamper.Stamp(Wrap("public class Widget { }"));

        await Assert.That(stamped).DoesNotContain("\"Strategos.Generators\"");
    }

    // =============================================================================
    // B. Nested types are NOT stamped (top-level only)
    // =============================================================================

    [Test]
    public async Task Stamp_NestedType_IsNotStamped()
    {
        var source = Wrap("public class Outer\n{\n    public sealed record Inner(int Value);\n}");

        var stamped = GeneratedCodeStamper.Stamp(source);

        var outerAttributes = AttributesOf(stamped, "Outer");
        var innerAttributes = AttributesOf(stamped, "Inner");

        await Assert.That(outerAttributes).Contains("GeneratedCode");
        await Assert.That(outerAttributes).Contains("ExcludeFromCodeCoverage");
        await Assert.That(innerAttributes).DoesNotContain("GeneratedCode");
        await Assert.That(innerAttributes).DoesNotContain("ExcludeFromCodeCoverage");
    }

    // =============================================================================
    // C. Multiple top-level types in one file are all stamped
    // =============================================================================

    [Test]
    public async Task Stamp_MultipleTopLevelTypes_AllStamped()
    {
        var source = Wrap("public sealed record First(int A);\n\npublic sealed class Second { }\n\npublic enum Third { X }");

        var stamped = GeneratedCodeStamper.Stamp(source);

        // Every top-level type gets [GeneratedCode]; only the class/record get [ExcludeFromCodeCoverage].
        foreach (var typeName in new[] { "First", "Second", "Third" })
        {
            await Assert.That(AttributesOf(stamped, typeName)).Contains(GeneratedCodeAttributeName);
        }

        await Assert.That(AttributesOf(stamped, "First")).Contains(ExcludeAttributeName);
        await Assert.That(AttributesOf(stamped, "Second")).Contains(ExcludeAttributeName);
        await Assert.That(AttributesOf(stamped, "Third")).DoesNotContain(ExcludeAttributeName);
    }

    // =============================================================================
    // D. Idempotency — stamping twice is a no-op; pre-existing attributes not duplicated
    // =============================================================================

    [Test]
    public async Task Stamp_IsIdempotent()
    {
        var once = GeneratedCodeStamper.Stamp(Wrap("public class Widget { }"));
        var twice = GeneratedCodeStamper.Stamp(once);

        await Assert.That(twice).IsEqualTo(once);
    }

    [Test]
    public async Task Stamp_TypeAlreadyHasGeneratedCode_DoesNotDuplicateButAddsExclude()
    {
        // Models the intermediate state where the per-site literal still exists (before its
        // deletion): the stamper must add the missing ExcludeFromCodeCoverage WITHOUT producing
        // a second GeneratedCode (which would be CS0579 duplicate-attribute).
        var source = Wrap("[System.CodeDom.Compiler.GeneratedCode(\"Strategos.Generators\", \"1.0.0\")]\npublic class Widget { }");

        var stamped = GeneratedCodeStamper.Stamp(source);

        var generatedCodeCount = CountOccurrences(AttributesOf(stamped, "Widget"), "GeneratedCode");
        await Assert.That(generatedCodeCount).IsEqualTo(1);
        await Assert.That(AttributesOf(stamped, "Widget")).Contains("ExcludeFromCodeCoverage");
    }

    // =============================================================================
    // E. Trivia preservation
    // =============================================================================

    [Test]
    public async Task Stamp_PreservesAutoGeneratedHeaderAndNullableDirective()
    {
        var stamped = GeneratedCodeStamper.Stamp(Wrap("public class Widget { }"));

        await Assert.That(stamped).Contains("// <auto-generated/>");
        await Assert.That(stamped).Contains("#nullable enable");
    }

    [Test]
    public async Task Stamp_PreservesXmlDocComment_BeforeAttributes()
    {
        var source = Wrap("/// <summary>A widget.</summary>\npublic class Widget { }");

        var stamped = GeneratedCodeStamper.Stamp(source);

        // The doc comment survives and still precedes the inserted attributes.
        var docIndex = stamped.IndexOf("<summary>A widget.</summary>", StringComparison.Ordinal);
        var attrIndex = stamped.IndexOf(GeneratedCodeMarker, StringComparison.Ordinal);
        await Assert.That(docIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(attrIndex).IsGreaterThan(docIndex);
    }

    [Test]
    public async Task Stamp_PreservesRawStringLiteralConstant()
    {
        // Mirrors the Mermaid diagram emitter, whose body is a raw-string constant.
        var rawString = "\"\"\"\n%% diagram\nstateDiagram-v2\n\"\"\"";
        var source = Wrap("public static class WidgetDiagram\n{\n    public const string Diagram = " + rawString + ";\n}");

        var stamped = GeneratedCodeStamper.Stamp(source);

        await Assert.That(stamped).Contains("%% diagram");
        await Assert.That(stamped).Contains("stateDiagram-v2");
        await Assert.That(AttributesOf(stamped, "WidgetDiagram")).Contains("GeneratedCode");
    }

    // =============================================================================
    // F. Namespace shapes
    // =============================================================================

    [Test]
    public async Task Stamp_BlockNamespace_IsStamped()
    {
        var source = "// <auto-generated/>\n#nullable enable\n\nnamespace Test\n{\n    public class Widget { }\n}\n";

        var stamped = GeneratedCodeStamper.Stamp(source);

        var attributes = AttributesOf(stamped, "Widget");
        await Assert.That(attributes).Contains("GeneratedCode");
        await Assert.That(attributes).Contains("ExcludeFromCodeCoverage");
    }

    [Test]
    public async Task Stamp_NoNamespace_TopLevelType_IsStamped()
    {
        var source = "// <auto-generated/>\n#nullable enable\n\npublic class Widget { }\n";

        var stamped = GeneratedCodeStamper.Stamp(source);

        var attributes = AttributesOf(stamped, "Widget");
        await Assert.That(attributes).Contains("GeneratedCode");
        await Assert.That(attributes).Contains("ExcludeFromCodeCoverage");
    }

    // =============================================================================
    // G. Degenerate inputs
    // =============================================================================

    [Test]
    [Arguments("")]
    [Arguments("// <auto-generated/>\n#nullable enable\n\nnamespace Test;\n")]
    public async Task Stamp_NoTypeDeclarations_ReturnsInputUnchanged(string source)
    {
        var stamped = GeneratedCodeStamper.Stamp(source);

        await Assert.That(stamped).IsEqualTo(source);
    }

    [Test]
    public async Task Stamp_Output_IsValidCSharp()
    {
        var source = Wrap("/// <summary>A widget.</summary>\npublic sealed partial record Widget(int Value);");

        var stamped = GeneratedCodeStamper.Stamp(source);

        var diagnostics = CSharpSyntaxTree.ParseText(stamped, new CSharpParseOptions(LanguageVersion.Latest))
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    [Arguments("public class Widget { }")]
    [Arguments("public struct Widget { }")]
    [Arguments("public sealed record Widget(int Value);")]
    [Arguments("public interface Widget { }")]
    [Arguments("public enum Widget { A, B }")]
    [Arguments("public delegate void Widget();")]
    public async Task Stamp_StampedOutput_HasNoAttributeTargetErrors(string typeDeclaration)
    {
        // Semantic guard: a parse-only check cannot see CS0592 ([ExcludeFromCodeCoverage] on an
        // invalid target like enum/interface). Compile the stamped output and assert no errors.
        var stamped = GeneratedCodeStamper.Stamp(Wrap(typeDeclaration));

        var compilation = CSharpCompilation.Create(
            "StampSemanticTest",
            [CSharpSyntaxTree.ParseText(stamped, new CSharpParseOptions(LanguageVersion.Latest))],
            CoreReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        await Assert.That(errors)
            .IsEmpty()
            .Because("stamped output must compile (CS0592 fires if a coverage attribute lands on an "
                + "invalid target); errors: " + string.Join("; ", errors.Select(e => e.ToString())));
    }

    // =============================================================================
    // Helpers
    // =============================================================================

    private static string Wrap(string typeDeclaration)
        => "// <auto-generated/>\n#nullable enable\n\nnamespace Test;\n\n" + typeDeclaration + "\n";

    private static IReadOnlyList<string> AttributesOf(string source, string typeName)
    {
        var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)).GetRoot();

        var member = root.DescendantNodes()
            .OfType<MemberDeclarationSyntax>()
            .First(m => MemberName(m) == typeName);

        return member.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(SimpleName)
            .ToList();
    }

    private static string MemberName(MemberDeclarationSyntax member) => member switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier.Text,
        DelegateDeclarationSyntax @delegate => @delegate.Identifier.Text,
        _ => string.Empty,
    };

    private static int CountOccurrences(IReadOnlyList<string> attributes, string simpleName)
        => attributes.Count(a => a == simpleName || a == simpleName + "Attribute");

    private static IReadOnlyList<MetadataReference> CoreReferences()
    {
        var references = new List<MetadataReference>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
            {
                continue;
            }

            try
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
            catch (Exception)
            {
                // Ignore assemblies that cannot be loaded as metadata references.
            }
        }

        return references;
    }

    private static string SimpleName(AttributeSyntax attribute)
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
