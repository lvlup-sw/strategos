// -----------------------------------------------------------------------
// <copyright file="UnvalidatedEntryPointRatchetTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Strategos.Generators.Tests.Fixtures;

/// <summary>
/// Ratchet over the NON-validating generator/parser harness entry points (G-B).
/// </summary>
/// <remarks>
/// <para>
/// The defect class: a test drives the generator or parser over a fixture that does not
/// compile (or over a crashed driver) and still reports pass, because the legacy entry points
/// never check the subject. The validating overloads
/// (<c>RunGeneratorWithValidInput</c>, <c>CreateParseContextValidated</c>, and friends) exist
/// but are opt-in; the fifth instance of the class recurred inside PR #205 while they existed.
/// </para>
/// <para>
/// Policy is data: <see cref="UnvalidatedCallCeilings"/> records, per test file, the number
/// of legacy call sites at the time this guard landed. The number may only go DOWN. A file
/// that is not listed must contain none. When a legacy call is removed the ceiling must be
/// lowered in the same change, so the recorded policy always equals reality and nobody can
/// spend the headroom. A new test must use the validating overloads.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class UnvalidatedEntryPointRatchetTests
{
    // =============================================================================
    // Policy data
    // =============================================================================

    /// <summary>
    /// The legacy (non-validating) entry points. The <c>(\(|&lt;\w)</c> tail matches a call
    /// or a generic call and nothing else, so prose mentions do not count.
    /// </summary>
    private static readonly Regex[] UnvalidatedEntryPointPatterns =
    [
        new(@"\bGeneratorTestHelper\.RunGenerator\s*(\(|<\w)", RegexOptions.Compiled),
        new(@"\bParserTestHelper\.CreateParseContext\s*\(", RegexOptions.Compiled),
    ];

    /// <summary>
    /// Files that define the entry points; their own bodies are not call sites.
    /// </summary>
    private static readonly string[] EntryPointDefinitionFiles =
    [
        "Fixtures/GeneratorTestHelper.cs",
        "Fixtures/ParserTestHelper.cs",
    ];

    /// <summary>
    /// Legacy call sites per test file (relative to <c>Strategos.Generators.Tests</c>) as of
    /// 2026-09-07, PR #205. Total 350. Owner: Strategos test-infrastructure maintainers.
    /// Expiry: each entry is deleted when its file reaches zero. Numbers only go down.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> UnvalidatedCallCeilings =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["GeneratorIntegrationTests.cs"] = 54,
            ["SagaEmitterIntegrationTests.cs"] = 49,
            ["DiagnosticTests.cs"] = 38,
            ["OnFailureIntegrationTests.cs"] = 21,
            ["Helpers/ApprovalExtractorTests.cs"] = 18,
            ["FluentDslParserTests.cs"] = 16,
            ["EventSourcedEmitterIntegrationTests.cs"] = 16,
            ["Diagnostics/DeclaredButInertTests.cs"] = 15,
            ["Diagnostics/ResilienceDiagnosticsTests.cs"] = 13,
            ["Emitters/DiagnosticForkLoweringSourceTests.cs"] = 12,
            ["PhaseEnumEmitterTests.cs"] = 11,
            ["Emitters/CompensationLoweringTests.cs"] = 7,
            ["Helpers/FailureHandlerExtractorTests.cs"] = 6,
            ["ExtensionsIntegrationTests.cs"] = 6,
            ["Emitters/ContextOnHandlerStepsTests.cs"] = 6,
            ["Emitters/ConfidenceLoweringTests.cs"] = 5,
            ["WorkerHandlerIntegrationTests.cs"] = 5,
            ["SagaIdentityNegationTests.cs"] = 5,
            ["Emitters/Saga/SagaApprovalConstructTests.cs"] = 5,
            ["InvariantGuardTests.cs"] = 4,
            ["Emitters/Saga/SagaStepHandlersEmitterTests.cs"] = 4,
            ["Emitters/LinearWorkflowOutputRegressionTests.cs"] = 4,
            ["SagaIdentityEmitterTests.cs"] = 3,
            ["Diagnostics/DuplicateCompensationSeedTests.cs"] = 3,
            ["Helpers/StepExtractorContextTests.cs"] = 2,
            ["Emitters/TimeoutLoweringTests.cs"] = 2,
            ["Emitters/SagaEmitterForkConfigTests.cs"] = 2,
            ["Emitters/ContextWireInTests.cs"] = 2,
            ["Diagnostics/TerminalReachabilityDiagnosticTests.cs"] = 2,
            ["Diagnostics/DuplicatePermittedForkTriggerTests.cs"] = 2,
            ["SagaSnapshotInspectionTests.cs"] = 1,
            ["_SagaEmitDumpTests.cs"] = 1,
            ["Parity/StepConfigParityTests.cs"] = 1,
            ["Helpers/ContextModelExtractorTests.cs"] = 1,
            ["EndToEndStubIntegrationTests.cs"] = 1,
            ["Emitters/TransitionGraphLoweringTests.cs"] = 1,
            ["Emitters/NoConfigBaselineTests.cs"] = 1,
            ["Emitters/LoopExitBranchFinallyTests.cs"] = 1,
            ["Emitters/BranchTerminalCaseTests.cs"] = 1,
            ["Emitters/BranchBoolDiscriminatorTests.cs"] = 1,
            ["CoverageAttributeStampingTests.cs"] = 1,
        };

    /// <summary>
    /// Test files PR #205 added (git diff --diff-filter=A origin/main...HEAD) plus the two
    /// added during its review. Each must exist and contain zero legacy call sites: the
    /// validating overloads were available for all of them.
    /// </summary>
    private static readonly string[] FilesAddedByPr205 =
    [
        "Fixtures/GeneratorTestHelperTests.cs",
        "Helpers/DefinitionShapeExtractionTests.cs",
        "Helpers/StepExtractorActionReferenceTests.cs",
        "Import/WireStepFingerprintCoverageTests.cs",
        "Proof/ActionRefinementProofVectorGeneratorTests.cs",
        "Proof/ImportedWorkflowBindingProofTests.cs",
        "Proof/OntologyActionCatalogFailClosedTests.cs",
        "Proof/TopologyClosureProofTests.cs",
        "Proof/WorkflowBindingProofAnalyzerTests.cs",
        "Proof/WorkflowBindingProofFailClosedTests.cs",
        "Proof/WorkflowBindingTopologySemanticsTests.cs",
    ];

    // =============================================================================
    // Guards
    // =============================================================================

    /// <summary>
    /// Every test file is at exactly its recorded ceiling; unlisted files have none.
    /// </summary>
    [Test]
    public async Task UnvalidatedEntryPoints_PerFileCallSites_EqualRecordedCeilings()
    {
        var files = ReadTestSources();

        // An empty scan is a moved directory, not a clean suite.
        await Assert.That(files.Count).IsGreaterThan(0)
            .Because("no test sources found under the Generators.Tests project");

        var violations = EvaluateRatchet(files, UnvalidatedCallCeilings);

        await Assert.That(violations).IsEmpty()
            .Because(
                "legacy harness call sites may only decrease; new tests must use the validating "
                + "overloads (RunGeneratorWithValidInput / CreateParseContextValidated); "
                + string.Join("; ", violations));
    }

    /// <summary>
    /// Files PR #205 added carry no legacy call sites.
    /// </summary>
    [Test]
    public async Task UnvalidatedEntryPoints_FilesAddedByPr205_HaveNone()
    {
        var files = ReadTestSources().ToDictionary(f => f.RelativePath, f => f.Text, StringComparer.Ordinal);
        var violations = new List<string>();

        foreach (var added in FilesAddedByPr205)
        {
            if (!files.TryGetValue(added, out var text))
            {
                violations.Add($"{added} is listed as added by PR #205 but does not exist");
                continue;
            }

            var count = CountLegacyCalls(text);
            if (count > 0)
            {
                violations.Add($"{added} has {count} legacy call site(s)");
            }
        }

        await Assert.That(violations).IsEmpty()
            .Because("tests added by PR #205 must use the validating overloads; " + string.Join("; ", violations));
    }

    /// <summary>
    /// Self-test: a file outside the policy with one legacy call, a listed file over its
    /// ceiling, and a listed file under its ceiling are each reported. The legacy call is
    /// assembled at runtime so this file's own text stays clean under the scan.
    /// </summary>
    [Test]
    public async Task UnvalidatedEntryPoints_Drift_IsFlagged()
    {
        var legacyCall = "var r = " + "GeneratorTestHelper" + ".RunGenerator" + "(source);";
        var ceilings = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Listed/OverTests.cs"] = 1,
            ["Listed/UnderTests.cs"] = 2,
        };

        var violations = EvaluateRatchet(
            [
                ("Unlisted/NewTests.cs", legacyCall),
                ("Listed/OverTests.cs", legacyCall + "\n" + legacyCall),
                ("Listed/UnderTests.cs", legacyCall),
                ("Clean/FineTests.cs", "var r = GeneratorTestHelper.RunGeneratorWithValidInput(source);"),
            ],
            ceilings);

        await Assert.That(violations).HasCount().EqualTo(3);
        await Assert.That(violations.Any(v => v.StartsWith("Unlisted/NewTests.cs", StringComparison.Ordinal))).IsTrue();
        await Assert.That(violations.Any(v => v.StartsWith("Listed/OverTests.cs", StringComparison.Ordinal))).IsTrue();
        await Assert.That(violations.Any(v => v.StartsWith("Listed/UnderTests.cs", StringComparison.Ordinal))).IsTrue();
    }

    // =============================================================================
    // Mechanism
    // =============================================================================

    private static List<string> EvaluateRatchet(
        IEnumerable<(string RelativePath, string Text)> files,
        IReadOnlyDictionary<string, int> ceilings)
    {
        var violations = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (relativePath, text) in files)
        {
            seen.Add(relativePath);
            if (EntryPointDefinitionFiles.Contains(relativePath, StringComparer.Ordinal))
            {
                continue;
            }

            var count = CountLegacyCalls(text);
            var ceiling = ceilings.TryGetValue(relativePath, out var c) ? c : 0;

            if (count > ceiling)
            {
                violations.Add($"{relativePath} has {count} legacy call site(s), ceiling {ceiling}");
            }
            else if (count < ceiling)
            {
                violations.Add($"{relativePath} has {count} legacy call site(s); lower its ceiling from {ceiling} to {count}");
            }
        }

        foreach (var missing in ceilings.Keys.Where(k => !seen.Contains(k)))
        {
            violations.Add($"{missing} has a ceiling but does not exist; remove the entry");
        }

        return violations;
    }

    private static int CountLegacyCalls(string text) =>
        UnvalidatedEntryPointPatterns.Sum(p => p.Matches(text).Count);

    private static List<(string RelativePath, string Text)> ReadTestSources()
    {
        var testDir = Path.Combine(FindSolutionRoot(), "Strategos.Generators.Tests");
        return Directory.EnumerateFiles(testDir, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(testDir, path).Replace('\\', '/'))
            .Where(rel => !rel.StartsWith("bin/", StringComparison.Ordinal)
                && !rel.StartsWith("obj/", StringComparison.Ordinal))
            .OrderBy(rel => rel, StringComparer.Ordinal)
            .Select(rel => (rel, File.ReadAllText(Path.Combine(testDir, rel))))
            .ToList();
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "strategos.slnx")))
            {
                // The solution file sits at the repository root; the projects live under src/.
                return Path.Combine(dir.FullName, "src");
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the solution root (no ancestor of '{AppContext.BaseDirectory}' contains strategos.slnx).");
    }
}
