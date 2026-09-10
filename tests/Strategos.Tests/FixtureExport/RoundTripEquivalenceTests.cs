// =============================================================================
// <copyright file="RoundTripEquivalenceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using Strategos.Contracts;
using Strategos.Generators;

namespace Strategos.Tests.FixtureExport;

/// <summary>
/// Task 019 (#100), DR-15 + DR-3 — the CAPSTONE round-trip partition gate. Proves the
/// two-bucket partition over the WHOLE #53 <see cref="WorkflowCorpus"/>: every fixture is
/// exported to wire JSON (<c>ToContract()</c> + the contracts canonical serializer), driven
/// through the source generator's import front-end, and lands in EXACTLY ONE bucket —
/// <list type="bullet">
///   <item><description>
///     <b>(a) importable</b> — the bridge lowers a model and a saga is emitted, with NO import
///     rejection diagnostic. The importable combinator families (<c>startWith</c>, <c>then</c>,
///     <c>fork-join</c>, <c>onFailure</c>, <c>config</c>) populate this bucket.
///   </description></item>
///   <item><description>
///     <b>(b) carrier-bearing</b> — the specific task-018 DR-14 rejection diagnostic fires and NO
///     saga is emitted. The corpus's <c>branch</c> (AGWF028), <c>repeatUntil</c> (AGWF029), and
///     context-bearing <c>awaitApproval</c> (AGWF031) families populate this bucket, so the
///     rejection proof is not vacuous.
///   </description></item>
/// </list>
/// The <c>awaitApproval</c> family straddles BOTH buckets: a context-bearing approval is a carrier
/// (bucket b, AGWF031), while a CONTEXT-FREE approval (<c>approval-free-*</c>) is importable (bucket a,
/// M10) — so this family is classified PER CASE, not by tag.
/// A fixture in NEITHER bucket (no saga AND no rejection — e.g. a bridge crash or an unresolvable
/// moniker) FAILS the gate.
/// </summary>
/// <remarks>
/// <para>
/// The operational test of bucket (a) is "the bridge lowered a <c>*Saga.g.cs</c>" — the tractable
/// per-fixture proxy for "importable". Actual saga COMPILATION for the importable families is proven
/// separately on a real host by the round-trip behavioral twins
/// (<c>Strategos.Generators.Behavioral.Tests.RoundTripBehavioralTests</c>) and by the
/// <c>Strategos.Generators.Behavioral.Tests</c> BUILD, and field-for-field JSON→model IR fidelity
/// by <c>Strategos.Generators.Tests.Import.RoundTripIrFidelityTests</c>.
/// </para>
/// <para>
/// The generator is driven over an in-memory host compilation that declares public step types whose
/// simple names match the corpus monikers; moniker resolution is by simple name (LB-2), so those
/// declarations let the importable subset resolve and lower. The generated trees are inspected, not
/// compiled — a saga tree existing is the bucket-(a) signal; a rejection diagnostic is the
/// bucket-(b) signal.
/// </para>
/// </remarks>
[Property("Category", "FixtureExport")]
public sealed class RoundTripEquivalenceTests
{
    // Stable AGWF ids under test. Literal ids are permitted in tests (the single-source grep gate
    // excludes *.Tests projects). These mirror the task-018 DR-14 rejection diagnostics.
    private const string DelegateCode = "AGWF027";
    private const string BranchPointCode = "AGWF028";
    private const string LoopCode = "AGWF029";
    private const string ValidationCode = "AGWF030";
    private const string ApprovalContextCode = "AGWF031";
    private const string DanglingGateIdCode = "AGWF032";
    private const string ReliabilityGateCode = "AGWF033";

    /// <summary>The carrier-rejection diagnostic ids (AGWF027–AGWF033) — a bucket-(b) signal.</summary>
    private static readonly HashSet<string> RejectionCodes = new(StringComparer.Ordinal)
    {
        DelegateCode, BranchPointCode, LoopCode, ValidationCode,
        ApprovalContextCode, DanglingGateIdCode, ReliabilityGateCode,
    };

    /// <summary>
    /// The corpus combinator tags that must land in bucket (a) — importable ⇒ saga emitted, no
    /// rejection.
    /// </summary>
    private static readonly HashSet<string> ImportableTags = new(StringComparer.Ordinal)
    {
        "startWith", "then", "fork-join", "onFailure", "config",
    };

    /// <summary>
    /// The corpus combinator tags that map to a bucket-(b) carrier code when the case is NOT importable.
    /// Each maps to the ONE AGWF id its carrier triggers. The <c>awaitApproval</c> tag is here for its
    /// context-BEARING cases (AGWF031); its context-FREE cases (<c>approval-free-*</c>) are importable
    /// and classified as bucket (a) by <see cref="IsContextFreeApproval"/> — see the M10 note in
    /// <c>WorkflowCorpus.AwaitApprovalCases</c>.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> CarrierTagToCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["branch"] = BranchPointCode,
            ["repeatUntil"] = LoopCode,
            ["awaitApproval"] = ApprovalContextCode,
        };

    /// <summary>The corpus name prefix of the importable, CONTEXT-FREE approval fixtures (bucket a, M10).</summary>
    private const string ContextFreeApprovalNamePrefix = "approval-free";

    /// <summary>
    /// Whether a corpus case is a CONTEXT-FREE approval — importable (bucket a) despite sharing the
    /// <c>awaitApproval</c> tag with the context-bearing carriers (bucket b). Context-free approval
    /// fixtures are named <c>approval-free-*</c> (see <c>WorkflowCorpus.AwaitApprovalCases</c>).
    /// </summary>
    private static bool IsContextFreeApproval(WorkflowCorpus.Case c) =>
        string.Equals(c.Tag, "awaitApproval", StringComparison.Ordinal)
        && c.Name.StartsWith(ContextFreeApprovalNamePrefix, StringComparison.Ordinal);

    // Public step types whose SIMPLE NAMES match every moniker the corpus emits, so the importable
    // subset resolves its monikers (LB-2, by simple name) and lowers. Declared in a real namespace
    // so the bridge has a non-global home for the generated saga. The state type is inferred from the
    // step's IWorkflowStep<RoundTripState> implementation (the wire IR carries no state type).
    private const string CorpusStepTypes = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;

        namespace Strategos.Tests.RoundTrip.Generated;

        public sealed record RoundTripState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public decimal QualityScore { get; init; }
        }

        public sealed class ValidateStep : IWorkflowStep<RoundTripState>
        {
            public Task<StepResult<RoundTripState>> ExecuteAsync(RoundTripState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RoundTripState>.FromState(s));
        }

        public sealed class ProcessStep : IWorkflowStep<RoundTripState>
        {
            public Task<StepResult<RoundTripState>> ExecuteAsync(RoundTripState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RoundTripState>.FromState(s));
        }

        public sealed class NotifyStep : IWorkflowStep<RoundTripState>
        {
            public Task<StepResult<RoundTripState>> ExecuteAsync(RoundTripState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RoundTripState>.FromState(s));
        }

        public sealed class CompleteStep : IWorkflowStep<RoundTripState>
        {
            public Task<StepResult<RoundTripState>> ExecuteAsync(RoundTripState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RoundTripState>.FromState(s));
        }

        public sealed class AutoProcessStep : IWorkflowStep<RoundTripState>
        {
            public Task<StepResult<RoundTripState>> ExecuteAsync(RoundTripState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RoundTripState>.FromState(s));
        }

        public sealed class ManualProcessStep : IWorkflowStep<RoundTripState>
        {
            public Task<StepResult<RoundTripState>> ExecuteAsync(RoundTripState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RoundTripState>.FromState(s));
        }

        public sealed class CritiqueStep : IWorkflowStep<RoundTripState>
        {
            public Task<StepResult<RoundTripState>> ExecuteAsync(RoundTripState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RoundTripState>.FromState(s));
        }

        public sealed class RefineStep : IWorkflowStep<RoundTripState>
        {
            public Task<StepResult<RoundTripState>> ExecuteAsync(RoundTripState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RoundTripState>.FromState(s));
        }

        public sealed class LogFailureStep : IWorkflowStep<RoundTripState>
        {
            public Task<StepResult<RoundTripState>> ExecuteAsync(RoundTripState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RoundTripState>.FromState(s));
        }

        public sealed class NotifyAdminStep : IWorkflowStep<RoundTripState>
        {
            public Task<StepResult<RoundTripState>> ExecuteAsync(RoundTripState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RoundTripState>.FromState(s));
        }

        // The context-free approval fixture's approver moniker resolves through the same step resolver
        // as any step (the established import contract), so it is declared as a step here; its simple
        // name (ReviewerApprover) derives the approval-point name (Reviewer).
        public sealed class ReviewerApprover : IWorkflowStep<RoundTripState>
        {
            public Task<StepResult<RoundTripState>> ExecuteAsync(RoundTripState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RoundTripState>.FromState(s));
        }
        """;

    /// <summary>
    /// Every <see cref="WorkflowCorpus"/> fixture lands in EXACTLY ONE bucket: importable ⇒ saga
    /// emitted &amp; no rejection, or carrier-bearing ⇒ the specific DR-14 diagnostic &amp; no saga.
    /// A fixture in NEITHER bucket fails the gate. Also asserts the corpus is ≥100 fixtures and that
    /// bucket (b) is populated by the branch / repeatUntil / awaitApproval families (non-vacuous
    /// rejection).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EveryCorpusFixture_LandsInExactlyOneBucket()
    {
        var cases = WorkflowCorpus.All();
        var compilation = CreateHostCompilation();

        var importableCount = 0;
        var carrierCount = 0;
        var carrierByCode = new Dictionary<string, int>(StringComparer.Ordinal);
        var violations = new List<string>();

        foreach (var c in cases)
        {
            var json = ContractsJson.Serialize(c.Workflow.ToContract());
            var result = RunImport(compilation, c.Name + ".workflow.json", json);

            var sagaEmitted = result.GeneratedTrees.Any(
                t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal));
            var rejectionCode = result.Diagnostics
                .Select(d => d.Id)
                .FirstOrDefault(id => RejectionCodes.Contains(id));

            // The partition invariant: exactly one of {saga emitted, carrier rejected}.
            if (sagaEmitted == (rejectionCode is not null))
            {
                var allDiags = string.Join(" || ", result.Diagnostics.Select(d => d.Id + ": " + d.GetMessage()).Distinct());
                violations.Add(
                    $"{c.Tag}/{c.Name}: NOT exactly one bucket (sagaEmitted={sagaEmitted}, " +
                    $"rejection={rejectionCode ?? "<none>"}, allDiagnostics=[{allDiags}]).");
                continue;
            }

            // A context-free approval (approval-free-*) is importable (bucket a) even though it shares
            // the awaitApproval tag with the context-bearing carriers (bucket b) — classify per case.
            if (ImportableTags.Contains(c.Tag) || IsContextFreeApproval(c))
            {
                if (!sagaEmitted)
                {
                    violations.Add($"{c.Tag}/{c.Name}: expected bucket (a) importable but no saga was emitted.");
                    continue;
                }

                importableCount++;
            }
            else if (CarrierTagToCode.TryGetValue(c.Tag, out var expectedCode))
            {
                if (rejectionCode != expectedCode)
                {
                    violations.Add(
                        $"{c.Tag}/{c.Name}: expected bucket (b) rejection {expectedCode} but got " +
                        $"{rejectionCode ?? "<none>"} (sagaEmitted={sagaEmitted}).");
                    continue;
                }

                carrierCount++;
                carrierByCode[expectedCode] = carrierByCode.GetValueOrDefault(expectedCode) + 1;
            }
            else
            {
                violations.Add($"{c.Tag}/{c.Name}: unclassified corpus tag '{c.Tag}'.");
            }
        }

        await Assert.That(violations).IsEmpty()
            .Because("every corpus fixture must land in exactly one partition bucket:\n" + string.Join("\n", violations));

        await Assert.That(cases.Count).IsGreaterThanOrEqualTo(100)
            .Because("the #53 corpus must contribute at least 100 fixtures to the partition gate.");

        await Assert.That(importableCount + carrierCount).IsEqualTo(cases.Count)
            .Because("every fixture must be counted into exactly one bucket.");

        // The rejection proof is NOT vacuous: each carrier family populates bucket (b).
        await Assert.That(carrierByCode.GetValueOrDefault(BranchPointCode)).IsGreaterThan(0)
            .Because("the branch family must populate bucket (b) via AGWF028.");
        await Assert.That(carrierByCode.GetValueOrDefault(LoopCode)).IsGreaterThan(0)
            .Because("the repeatUntil family must populate bucket (b) via AGWF029.");
        await Assert.That(carrierByCode.GetValueOrDefault(ApprovalContextCode)).IsGreaterThan(0)
            .Because("the context-bearing awaitApproval family must populate bucket (b) via AGWF031.");

        await Assert.That(importableCount).IsGreaterThan(0)
            .Because("the importable families must populate bucket (a).");
    }

    /// <summary>
    /// The corpus's importable families each import cleanly with NO carrier-rejection diagnostic of
    /// ANY id — pinning that the importable subset carries none of the AGWF027–AGWF033 carriers, so
    /// bucket (a) is a clean lowering rather than an accidentally-tolerated carrier.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImportableFamilies_LowerWithNoRejectionDiagnostic()
    {
        var compilation = CreateHostCompilation();
        var offenders = new List<string>();

        foreach (var c in WorkflowCorpus.All().Where(x => ImportableTags.Contains(x.Tag)))
        {
            var json = ContractsJson.Serialize(c.Workflow.ToContract());
            var result = RunImport(compilation, c.Name + ".workflow.json", json);

            var anyRejection = result.Diagnostics.Any(d => RejectionCodes.Contains(d.Id));
            if (anyRejection)
            {
                offenders.Add($"{c.Tag}/{c.Name}: {string.Join(",", result.Diagnostics.Select(d => d.Id).Distinct())}");
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("no importable-family fixture may surface a carrier-rejection diagnostic:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// A carrier-bearing fixture's rejection diagnostic NAMES the offending JSON path — the loud,
    /// actionable rejection DR-14 requires. Spot-checks one branch, one repeatUntil, and one
    /// context-bearing approval fixture.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CarrierRejection_NamesTheOffendingJsonPath()
    {
        var compilation = CreateHostCompilation();
        var cases = WorkflowCorpus.All();

        await AssertCarrierNamesPath(compilation, cases, "branch", BranchPointCode, "$.branchPoints[");
        await AssertCarrierNamesPath(compilation, cases, "repeatUntil", LoopCode, "$.loops[");
        await AssertCarrierNamesPath(compilation, cases, "awaitApproval", ApprovalContextCode, "$.approvalPoints[");
    }

    private static async Task AssertCarrierNamesPath(
        Compilation compilation,
        IReadOnlyList<WorkflowCorpus.Case> cases,
        string tag,
        string expectedCode,
        string expectedJsonPathPrefix)
    {
        // Pick a CARRIER case for this tag: exclude the importable context-free approval so the
        // awaitApproval spot-check lands on a context-bearing carrier (bucket b), not approval-free-*.
        var c = cases.First(x => x.Tag == tag && !IsContextFreeApproval(x));
        var json = ContractsJson.Serialize(c.Workflow.ToContract());
        var result = RunImport(compilation, c.Name + ".workflow.json", json);

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == expectedCode);
        await Assert.That(diagnostic).IsNotNull()
            .Because($"the {tag} carrier must surface {expectedCode}.");
        await Assert.That(diagnostic!.GetMessage()).Contains(expectedJsonPathPrefix)
            .Because($"{expectedCode} must name the offending JSON path ({expectedJsonPathPrefix}…).");
        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsFalse()
            .Because($"a {tag} carrier rejected by {expectedCode} must not emit a saga.");
    }

    /// <summary>
    /// Builds the in-memory host compilation whose symbol table declares the corpus step monikers, so
    /// the import bridge can resolve them and lower the importable subset.
    /// </summary>
    private static CSharpCompilation CreateHostCompilation() =>
        CSharpCompilation.Create(
            assemblyName: "RoundTripHostAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(CorpusStepTypes)],
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>
    /// Drives <see cref="WorkflowIncrementalGenerator"/> over a single exported corpus fixture
    /// (as a <c>*.workflow.json</c> AdditionalFile) against <paramref name="compilation"/>.
    /// </summary>
    private static GeneratorDriverRunResult RunImport(Compilation compilation, string path, string json)
    {
        var driver = CSharpGeneratorDriver.Create(
            generators: [new WorkflowIncrementalGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText(path, json)],
            parseOptions: null,
            optionsProvider: null);

        return driver.RunGenerators(compilation).GetRunResult();
    }

    private static List<MetadataReference> GetReferences()
    {
        var references = new List<MetadataReference>();

        var runtimePath = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (var assembly in new[] { "System.Runtime.dll", "System.Private.CoreLib.dll", "netstandard.dll" })
        {
            var path = System.IO.Path.Combine(runtimePath, assembly);
            if (System.IO.File.Exists(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
                catch
                {
                    // Ignore assemblies that can't be loaded as references.
                }
            }
        }

        var abstractions = typeof(Strategos.Abstractions.IWorkflowState).Assembly;
        if (!string.IsNullOrEmpty(abstractions.Location))
        {
            references.Add(MetadataReference.CreateFromFile(abstractions.Location));
        }

        return references;
    }

    /// <summary>An in-memory <see cref="AdditionalText"/> for driving the generator over an exported fixture.</summary>
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText text;

        public InMemoryAdditionalText(string path, string content)
        {
            this.Path = path;
            this.text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => this.text;
    }
}
