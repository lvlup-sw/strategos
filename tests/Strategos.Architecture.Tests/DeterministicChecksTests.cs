// =============================================================================
// <copyright file="DeterministicChecksTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.RegularExpressions;

namespace Strategos.Architecture.Tests;

/// <summary>
/// Runs the mechanically expressible checks of
/// <c>docs/architecture/invariants/deterministic-checks.md</c> against the repository
/// tree, one test per check id. Each scan is anchored at the repo root and requires
/// its target directory or file to exist, so a renamed path or a rotted deny-list
/// fails naming what went missing instead of returning zero hits.
/// </summary>
/// <remarks>
/// Ported: 1.1, 1.2, 1.3, 2.1, 2.2, 2.3, 3.2, 3.3, 3.4, 3.5, 4.1, 5.1, 5.1b, 6.1, 6.2,
/// 7.1, 7.2, 8.3. Not ported, per the doc: 5.2 is an informational id-gap listing
/// with no pass/fail; 5.3 diffs against the last release tag (needs git history and
/// a tag to name); 8.1 and 8.2 need the context around each hit (a preceding null
/// check, a <c>ClrType is not null</c> guard) that a line scan cannot see, so they
/// stay manual under U-8's audit prompt. 3.1 (every *Result/*Response record
/// mentions <c>_meta</c>) is a file-level scan like 3.2 and 3.4, but it reports
/// <c>SemanticQueryResult.cs</c>, whose <c>Meta</c> member is inherited from
/// <c>QueryResult</c>; a scan cannot see inheritance, so 3.1 stays manual.
/// Build output (<c>bin/</c>, <c>obj/</c>) is always skipped; tracked
/// <c>Generated/</c> sources are scanned like the bash scans them.
/// </remarks>
public class DeterministicChecksTests
{
    private static readonly string[] SealedByDefaultDirectories =
    [
        "src/Strategos/Builders",
        "src/Strategos/Definitions",
        "src/Strategos/Steps",
        "src/Strategos.Ontology/Descriptors",
    ];

    private static readonly string[] WorkflowDslDirectories =
    [
        "src/Strategos/Builders",
        "src/Strategos/Abstractions",
        "src/Strategos/Definitions",
    ];

    private static readonly string[] McpDirectories =
    [
        "src/Strategos.Ontology.MCP",
        "src/Strategos.Ontology.MCP.Hosting",
        "src/Strategos.Agents.Mcp",
    ];

    private static readonly string[] StateDirectories = ["src/Strategos", "samples"];

    private static readonly Regex SagaBase = new(@":\s*Saga\b", RegexOptions.CultureInvariant);
    private static readonly Regex RuntimePackageReference = new(@"<PackageReference\s+Include=""(Wolverine|Marten)", RegexOptions.CultureInvariant);
    private static readonly Regex SagaStorage = new(@"class\s+\w*Saga(Store|Repository|Persistence)\b", RegexOptions.CultureInvariant);
    private static readonly Regex WolverineOrMarten = new(@"\b(Wolverine|Marten)", RegexOptions.CultureInvariant);
    private static readonly Regex SourceGeneratorMarker = new(@"IIncrementalGenerator|ISourceGenerator|\[Generator\b", RegexOptions.CultureInvariant);
    private static readonly Regex RoslynComponent = new(@"<IsRoslynComponent>\s*true\s*</IsRoslynComponent>", RegexOptions.CultureInvariant);
    private static readonly Regex GeneratorProjectMetadata = new(@"<OutputItemType>|<ReferenceOutputAssembly>", RegexOptions.CultureInvariant);
    private static readonly Regex ToolDescriptorMention = new(@"OntologyToolDescriptor|McpToolDescriptor", RegexOptions.CultureInvariant);
    private static readonly Regex OutputSchema = new(@"OutputSchema", RegexOptions.CultureInvariant);
    private static readonly Regex DeprecatedProtocolRevision = new(@"2024-11-05|2025-03-26|2025-06-18|2025-11-25", RegexOptions.CultureInvariant);
    private static readonly Regex CallToolResult = new(@"CallToolResult", RegexOptions.CultureInvariant);
    private static readonly Regex ResultType = new(@"ResultType", RegexOptions.CultureInvariant);
    private static readonly Regex GraphTheoryTerm = new(@"\b(graph|node|edge|vertex)\w*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex DiagnosticIdLiteral = new(@"""(AGWF|AONT)[0-9]{3,}""", RegexOptions.CultureInvariant);
    private static readonly Regex AgwfLiteral = new(@"""AGWF[0-9]{3,}""", RegexOptions.CultureInvariant);
    private static readonly Regex PublicClass = new(@"^\s*public\s+(?:\w+\s+)*class\s+\w", RegexOptions.CultureInvariant);
    private static readonly Regex AllowedClassModifier = new(@"\b(sealed|static|abstract|partial)\b", RegexOptions.CultureInvariant);
    private static readonly Regex PublicVirtual = new(@"^\s*public virtual ", RegexOptions.CultureInvariant);
    private static readonly Regex ImplementsWorkflowState = new(@":\s*IWorkflowState\b", RegexOptions.CultureInvariant);
    private static readonly Regex MutableSetter = new(@"\{\s*get;\s*set;\s*\}", RegexOptions.CultureInvariant);
    private static readonly Regex MutableCollection = new(@"\b(List|Dictionary|HashSet)<", RegexOptions.CultureInvariant);
    private static readonly Regex SymbolKeyAssignment = new(@"SymbolKey\s*=", RegexOptions.CultureInvariant);

    /// <summary>U-1 / 1.1: no class outside the emitter derives from Wolverine's Saga.</summary>
    [Test]
    public async Task Check_1_1_NoHandAuthoredSagasOutsideTheEmitter()
    {
        var files = RepoTree.Files(["src/Strategos", "src/Strategos.Infrastructure", "src/Strategos.Agents"], "*.cs");
        var hits = RepoTree.Grep(files, SagaBase);

        await AssertNoHits("1.1", "sagas are emitted by SagaEmitter only; no hand-authored ': Saga' base", hits);
    }

    /// <summary>U-1 / 1.2: the core authoring project takes no Wolverine or Marten PackageReference.</summary>
    [Test]
    public async Task Check_1_2_CoreAuthoringProjectDoesNotReferenceWolverineOrMarten()
    {
        var csproj = RepoTree.RequireFile("src/Strategos/Strategos.csproj");
        var hits = RepoTree.Grep([csproj], RuntimePackageReference);

        await AssertNoHits("1.2", "src/Strategos/Strategos.csproj must not reference Wolverine or Marten; consumers get them from the emitted DI extensions", hits);
    }

    /// <summary>U-1 / 1.3: no ad-hoc saga storage type in non-generator code.</summary>
    [Test]
    public async Task Check_1_3_NoAdHocSagaStorageOutsideTheGenerator()
    {
        var files = RepoTree.Files(["src/Strategos", "src/Strategos.Infrastructure"], "*.cs");
        var hits = RepoTree.Grep(files, SagaStorage);

        await AssertNoHits("1.3", "saga persistence is Marten's, wired by the generator; no *SagaStore/*SagaRepository/*SagaPersistence class", hits);
    }

    /// <summary>
    /// U-2 / 2.1: no Wolverine or Marten coupling in any Strategos.Ontology* project.
    /// The doc's grep also matches XML-doc prose ("no Marten/Wolverine") and test
    /// strings asserting their absence, so the port scans code only: C# with comments
    /// and string literals blanked, csproj with XML comments blanked.
    /// </summary>
    [Test]
    public async Task Check_2_1_NoWolverineOrMartenCouplingInOntologyProjects()
    {
        var ontologyDirectories = RepoTree.DirectoriesStartingWith("src", "Strategos.Ontology");
        await Assert.That(ontologyDirectories).IsNotEmpty()
            .Because("Check 2.1: no src/Strategos.Ontology* directory found; the scan would be empty");

        var codeHits = RepoTree.Grep(
            RepoTree.Files(ontologyDirectories, "*.cs"),
            WolverineOrMarten,
            SourceText.StripCSharpCommentsAndStrings);
        var projectHits = RepoTree.Grep(
            RepoTree.Files(ontologyDirectories, "*.csproj"),
            WolverineOrMarten,
            SourceText.StripXmlComments);

        await AssertNoHits("2.1", "the ontology is self-contained: no Wolverine/Marten using, type, or PackageReference in src/Strategos.Ontology*/", [.. codeHits, .. projectHits]);
    }

    /// <summary>U-2 / 2.2: the ontology generators project stays analyzer-only.</summary>
    [Test]
    public async Task Check_2_2_NoSourceGeneratorInOntologyGeneratorsProject()
    {
        var files = RepoTree.Files("src/Strategos.Ontology.Generators", "*.cs");
        var hits = RepoTree.Grep(files, SourceGeneratorMarker);

        await AssertNoHits("2.2", "src/Strategos.Ontology.Generators is analyzers only; no IIncrementalGenerator, ISourceGenerator or [Generator]", hits);
    }

    /// <summary>U-2 / 2.3: the ontology generators csproj keeps analyzer-style metadata.</summary>
    [Test]
    public async Task Check_2_3_OntologyGeneratorsProjectRoleHasNotDrifted()
    {
        var csproj = RepoTree.RequireFile("src/Strategos.Ontology.Generators/Strategos.Ontology.Generators.csproj");
        var roslynComponent = RepoTree.Grep([csproj], RoslynComponent);
        var drift = RepoTree.Grep([csproj], GeneratorProjectMetadata);

        await Assert.That(roslynComponent).IsNotEmpty()
            .Because("Check 2.3: src/Strategos.Ontology.Generators/Strategos.Ontology.Generators.csproj must declare <IsRoslynComponent>true</IsRoslynComponent>");
        await AssertNoHits("2.3", "generator-style MSBuild metadata (<OutputItemType>, <ReferenceOutputAssembly>) in the analyzer project flags role drift", drift);
    }

    /// <summary>U-3 / 3.2: every file that mentions a tool descriptor mentions OutputSchema.</summary>
    [Test]
    public async Task Check_3_2_ToolDescriptorFilesMentionOutputSchema()
    {
        var files = RepoTree.Files("src/Strategos.Ontology.MCP", "*.cs");
        var subjects = RepoTree.WhereContent(files, ToolDescriptorMention);
        await Assert.That(subjects).IsNotEmpty()
            .Because("Check 3.2: no file under src/Strategos.Ontology.MCP mentions OntologyToolDescriptor or McpToolDescriptor; the check has lost its subject");

        var offenders = subjects
            .Where(file => !OutputSchema.IsMatch(File.ReadAllText(file)))
            .Select(RepoTree.Relative)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("Check 3.2: every tool descriptor carries an OutputSchema (2026-07-28 wire shape). Offending files:\n" + string.Join("\n", offenders));
    }

    /// <summary>U-3 / 3.3: no pre-2026-07-28 protocol revision string in MCP code or package READMEs.</summary>
    [Test]
    public async Task Check_3_3_NoDeprecatedProtocolRevisionStrings()
    {
        var files = RepoTree.Files(McpDirectories, "*.cs", "*.md");
        var hits = RepoTree.Grep(files, DeprecatedProtocolRevision);

        await AssertNoHits("3.3", "MCP targets protocol revision 2026-07-28 only; older revision strings must not reappear", hits);
    }

    /// <summary>U-3 / 3.4: every Hosting file that constructs CallToolResult assigns ResultType.</summary>
    [Test]
    public async Task Check_3_4_CallToolResultConstructionsAssignResultType()
    {
        var files = RepoTree.Files("src/Strategos.Ontology.MCP.Hosting", "*.cs");
        var subjects = RepoTree.WhereContent(files, CallToolResult);
        await Assert.That(subjects).IsNotEmpty()
            .Because("Check 3.4: no file under src/Strategos.Ontology.MCP.Hosting mentions CallToolResult; the check has lost its subject");

        var offenders = subjects
            .Where(file => !ResultType.IsMatch(File.ReadAllText(file)))
            .Select(RepoTree.Relative)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("Check 3.4: a CallToolResult without ResultType is the pre-2026-07-28 shape. Offending files:\n" + string.Join("\n", offenders));
    }

    /// <summary>U-3 / 3.5: the tool descriptor keeps its optional Icons property.</summary>
    [Test]
    public async Task Check_3_5_ToolDescriptorCarriesIcons()
    {
        var descriptor = RepoTree.RequireFile("src/Strategos.Ontology.MCP/OntologyToolDescriptor.cs");
        var mentionsIcons = File.ReadAllText(descriptor).Contains("Icons", StringComparison.Ordinal);

        await Assert.That(mentionsIcons).IsTrue()
            .Because("Check 3.5: src/Strategos.Ontology.MCP/OntologyToolDescriptor.cs must expose the optional Icons property (null when unset)");
    }

    /// <summary>U-4 / 4.1: no graph-theory term on the workflow DSL surface, comments excluded.</summary>
    [Test]
    public async Task Check_4_1_NoGraphTheoryTermsInWorkflowDslSurface()
    {
        var files = RepoTree.Files(WorkflowDslDirectories, "*.cs");
        var hits = RepoTree.Grep(files, GraphTheoryTerm, lineFilter: line => !IsCommentLine(line));

        await AssertNoHits("4.1", "the workflow DSL never says Graph/Node/Edge/Vertex; use the words a workflow author thinks in", hits);
    }

    /// <summary>U-5 / 5.1: no duplicate diagnostic id in the authoritative catalogs.</summary>
    [Test]
    public async Task Check_5_1_NoDuplicateDiagnosticIdsInTheCatalogs()
    {
        var catalogs = new[]
        {
            RepoTree.RequireFile("src/Strategos.Contracts/Generated/AgwfCodes.g.cs"),
            RepoTree.RequireFile("src/Strategos.Ontology.Generators/Diagnostics/OntologyDiagnosticIds.cs"),
        };
        var ids = catalogs
            .SelectMany(file => DiagnosticIdLiteral.Matches(File.ReadAllText(file)))
            .Select(m => m.Value)
            .ToList();

        await Assert.That(ids).IsNotEmpty()
            .Because("Check 5.1: no \"AGWF###\"/\"AONT###\" literal found in the catalogs; the check has lost its subject");

        var duplicates = ids.GroupBy(id => id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} (x{g.Count()})")
            .ToList();

        await Assert.That(duplicates).IsEmpty()
            .Because("Check 5.1: consumers suppress by id, so a duplicated diagnostic id is a contract violation: " + string.Join(", ", duplicates));
    }

    /// <summary>
    /// U-5 / 5.1b: no hand-quoted AGWF literal in production diagnostics. The doc's
    /// command ends in <c>uniq -d</c>, which only reports duplicated literals; the
    /// prose ("must consume AgwfCodes.*, not quote AGWF0xx") is the gate, so any
    /// literal fails.
    /// </summary>
    [Test]
    public async Task Check_5_1b_NoHandAuthoredAgwfLiteralsInProductionDiagnostics()
    {
        var files = RepoTree.Files("src/Strategos.Generators/Diagnostics", "*.cs");
        var hits = RepoTree.Grep(files, AgwfLiteral);

        await AssertNoHits("5.1b", "workflow diagnostics consume AgwfCodes.* from the generated catalog, never a quoted \"AGWF###\"", hits);
    }

    /// <summary>U-6 / 6.1: no public non-sealed concrete class in DSL/descriptor namespaces.</summary>
    [Test]
    public async Task Check_6_1_NoPublicNonSealedConcreteClassesInSealedByDefaultSurfaces()
    {
        var files = RepoTree.Files(SealedByDefaultDirectories, "*.cs");
        var hits = RepoTree.Grep(files, PublicClass)
            .Where(hit => !AllowedClassModifier.IsMatch(hit.Text))
            .ToList();

        await AssertNoHits("6.1", "DSL and descriptor classes are sealed (or static/abstract/partial with a sealed emitted half); extension goes through interfaces and extension methods", hits);
    }

    /// <summary>U-6 / 6.2: no public virtual member in sealed-by-default surfaces.</summary>
    [Test]
    public async Task Check_6_2_NoPublicVirtualMembersInSealedByDefaultSurfaces()
    {
        var files = RepoTree.Files(SealedByDefaultDirectories, "*.cs");
        var hits = RepoTree.Grep(files, PublicVirtual);

        await AssertNoHits("6.2", "a public virtual member advertises an extension point the generator does not know about", hits);
    }

    /// <summary>U-7 / 7.1: no settable property in an IWorkflowState type.</summary>
    [Test]
    public async Task Check_7_1_NoMutableSettersInStateTypes()
    {
        var hits = RepoTree.Grep(await StateFilesAsync(), MutableSetter);

        await AssertNoHits("7.1", "IWorkflowState properties are init-only; steps return a new record through StepResult", hits);
    }

    /// <summary>U-7 / 7.2: no mutable collection type in an IWorkflowState type.</summary>
    [Test]
    public async Task Check_7_2_NoMutableCollectionTypesInStateTypes()
    {
        var hits = RepoTree.Grep(await StateFilesAsync(), MutableCollection);

        await AssertNoHits("7.2", "IWorkflowState collections are ImmutableList/ImmutableDictionary (or another frozen type), never List/Dictionary/HashSet", hits);
    }

    /// <summary>U-8 / 8.3: the ontology tests exercise the SymbolKey-only identity path.</summary>
    [Test]
    public async Task Check_8_3_OntologyTestsCoverSymbolKeyIdentity()
    {
        var files = RepoTree.Files("src/Strategos.Ontology.Tests", "*.cs");
        var hits = RepoTree.Grep(files, SymbolKeyAssignment);

        await Assert.That(hits).IsNotEmpty()
            .Because("Check 8.3: no 'SymbolKey =' in src/Strategos.Ontology.Tests; the polyglot identity path is unexercised by tests");
    }

    private static bool IsCommentLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('*');
    }

    private static async Task<IReadOnlyList<string>> StateFilesAsync()
    {
        var files = RepoTree.Files(StateDirectories, "*.cs");
        var stateFiles = RepoTree.WhereContent(files, ImplementsWorkflowState);
        await Assert.That(stateFiles).IsNotEmpty()
            .Because("Check 7.x: no file under src/Strategos or samples mentions ': IWorkflowState'; the check has lost its subject");
        return stateFiles;
    }

    private static async Task AssertNoHits(string checkId, string expectation, IReadOnlyList<Hit> hits)
    {
        await Assert.That(hits).IsEmpty()
            .Because($"Check {checkId}: {expectation}. Offending lines:\n{RepoTree.Format(hits)}");
    }
}
