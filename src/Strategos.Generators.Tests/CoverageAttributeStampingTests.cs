// -----------------------------------------------------------------------
// <copyright file="CoverageAttributeStampingTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Structural enforcement for #148: every type the generators emit into a consumer assembly
/// must carry <c>[GeneratedCode("LevelUp.Strategos", ...)]</c> and <c>[ExcludeFromCodeCoverage]</c>,
/// and the only way a generator may emit a source is through the central
/// <c>GeneratedCodeStamper.AddStampedSource</c> chokepoint.
/// </summary>
/// <remarks>
/// These two tests are the forcing functions that lock the invariant in place:
/// <list type="bullet">
///   <item><description>
///     <see cref="Stamping_EveryEmittedTopLevelType_HasBothAttributes"/> drives the real
///     generators over representative workflows + a state type and enumerates EVERY emitted
///     top-level type — it fails if the stamper ever misses one (a new type kind, a new emitter).
///   </description></item>
///   <item><description>
///     <see cref="AddSource_IsOnlyReachedThroughTheStamper"/> fails the build if any generator
///     source calls <c>AddSource</c> directly instead of routing through the stamper — so a
///     future emit path cannot bypass the stamp.
///   </description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public sealed class CoverageAttributeStampingTests
{
    private const string GeneratedCodeAttribute = "GeneratedCode";
    private const string ExcludeFromCoverageAttribute = "ExcludeFromCodeCoverage";
    private const string ExpectedToolName = "LevelUp.Strategos";

    /// <summary>
    /// Representative workflow definitions that, between them, exercise the full emitter set
    /// (phase enum, commands, events interface + records, transitions, saga, worker handlers,
    /// DI extensions, mermaid diagram) plus fork, validation, and failure-handler variants.
    /// </summary>
    private static readonly string[] WorkflowSources =
    [
        SourceTexts.LinearWorkflow,
        SourceTexts.WorkflowWithFork,
        SourceTexts.WorkflowWithValidation,
        SourceTexts.WorkflowWithOnFailure,
    ];

    [Test]
    public async Task Stamping_EveryEmittedTopLevelType_HasBothAttributes()
    {
        var sawAnyType = false;

        foreach (var workflowSource in WorkflowSources)
        {
            var result = GeneratorTestHelper.RunGenerator(workflowSource);
            sawAnyType |= await AssertEveryTopLevelTypeStamped(result);
        }

        // The state-reducer generator emits into consumer assemblies too.
        var stateResult = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithMixedProperties);
        sawAnyType |= await AssertEveryTopLevelTypeStamped(stateResult);

        await Assert.That(sawAnyType)
            .IsTrue()
            .Because("the generators must have emitted at least one top-level type to validate");
    }

    [Test]
    public async Task AddSource_IsOnlyReachedThroughTheStamper()
    {
        var generatorDir = Path.Combine(FindSolutionRoot(), "Strategos.Generators");
        var stamperFileName = "GeneratedCodeStamper.cs";

        var sourceFiles = Directory
            .EnumerateFiles(generatorDir, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Replace('\\', '/').Contains("/obj/"))
            .ToList();

        await Assert.That(sourceFiles).IsNotEmpty();

        // Every direct AddSource call must live in the stamper; every other generator source must
        // route through AddStampedSource. Detect AddSource via a Roslyn invocation scan rather than
        // a substring match, so whitespace variants (`AddSource (`) are still caught and AddSource
        // mentioned in a comment or string literal is not falsely flagged.
        var bypassingFiles = sourceFiles
            .Where(path => Path.GetFileName(path) != stamperFileName)
            .Where(CallsAddSource)
            .Select(Path.GetFileName)
            .ToList();

        await Assert.That(bypassingFiles)
            .IsEmpty()
            .Because(
                "no generator may call AddSource directly — it must route through " +
                "GeneratedCodeStamper.AddStampedSource so the coverage attributes are stamped; " +
                "bypassing file(s): " + string.Join(", ", bypassingFiles));

        // The chokepoint itself must still exist (guards against the call being removed).
        var stamperPath = Path.Combine(generatorDir, "Helpers", stamperFileName);
        await Assert.That(File.Exists(stamperPath)).IsTrue();
        await Assert.That(CallsAddSource(stamperPath))
            .IsTrue()
            .Because("the central stamper must own the single AddSource call");
    }

    // Parses a C# file and reports whether it contains an actual `AddSource` method invocation
    // (member-access `x.AddSource(...)` or bare `AddSource(...)`), ignoring comments/strings.
    private static bool CallsAddSource(string filePath)
    {
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath)).GetRoot();

        return root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text == "AddSource",
                IdentifierNameSyntax identifier => identifier.Identifier.Text == "AddSource",
                _ => false,
            });
    }

    private static async Task<bool> AssertEveryTopLevelTypeStamped(GeneratorDriverRunResult result)
    {
        var sawType = false;

        foreach (var tree in result.GeneratedTrees)
        {
            var root = tree.GetRoot();
            foreach (var member in EnumerateTopLevelStampTargets(root))
            {
                sawType = true;
                var attributes = AttributeSimpleNames(member);
                var typeId = $"{MemberName(member)} ({Path.GetFileName(tree.FilePath)})";

                await Assert.That(attributes)
                    .Contains(GeneratedCodeAttribute)
                    .Because($"top-level type {typeId} must carry [GeneratedCode]");

                // [ExcludeFromCodeCoverage] is valid on class/struct/record but rejected on
                // enum/interface/delegate (CS0592) — assert both directions to lock the boundary.
                var supportsExclude = member is ClassDeclarationSyntax
                    or StructDeclarationSyntax
                    or RecordDeclarationSyntax;
                if (supportsExclude)
                {
                    await Assert.That(attributes)
                        .Contains(ExcludeFromCoverageAttribute)
                        .Because($"class/struct/record {typeId} must carry [ExcludeFromCodeCoverage]");
                }
                else
                {
                    await Assert.That(attributes)
                        .DoesNotContain(ExcludeFromCoverageAttribute)
                        .Because($"enum/interface {typeId} must NOT carry [ExcludeFromCodeCoverage] (CS0592)");
                }

                await Assert.That(tree.ToString())
                    .Contains($"\"{ExpectedToolName}\"")
                    .Because($"generated file for {typeId} must advertise the {ExpectedToolName} tool name");
            }
        }

        return sawType;
    }

    private static IEnumerable<MemberDeclarationSyntax> EnumerateTopLevelStampTargets(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            yield break;
        }

        foreach (var member in EnumerateNamespaceScopedMembers(compilationUnit.Members))
        {
            yield return member;
        }
    }

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

    private static string MemberName(MemberDeclarationSyntax member) => member switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier.Text,
        DelegateDeclarationSyntax @delegate => @delegate.Identifier.Text,
        _ => member.Kind().ToString(),
    };

    private static IReadOnlyList<string> AttributeSimpleNames(MemberDeclarationSyntax member)
        => member.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(SimpleName)
            .ToList();

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

    // Walks up to the directory containing strategos.sln (the src dir), mirroring the
    // existing StepConfigParityTests source-root resolution.
    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "strategos.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the solution root (no ancestor of "
            + $"'{AppContext.BaseDirectory}' contains strategos.sln).");
    }
}
