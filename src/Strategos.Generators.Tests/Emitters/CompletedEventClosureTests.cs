// -----------------------------------------------------------------------
// <copyright file="CompletedEventClosureTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;

using Strategos.Generators.Emitters;
using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Cross-emitter closure guard for <c>{stem}Completed</c> message names (G-A).
/// </summary>
/// <remarks>
/// <para>
/// The defect class: an emitter spells a completed-event stem itself (from
/// <c>StepName</c>, <c>PhaseName</c> or <c>model.StepNames</c>) instead of deriving it from the
/// naming table <see cref="EventsEmitter"/> uses, so a <c>Handle</c>/<c>NotFound</c>/worker site
/// names an event no producer emits. Four instances reached review before this guard; the
/// fourth was <see cref="SagaNotFoundHandlersEmitter"/>, which post-dated
/// <see cref="ForkPathCompletedNaming"/> and never called it. Each fix added another correct
/// instance and a per-emitter test; nothing enumerated the emitters. This file does.
/// </para>
/// <para>
/// Two mechanisms, both read from policy data declared at the top of the class:
/// <list type="number">
///   <item><description>
///     A structural scan of every source file under <c>Strategos.Generators/Emitters</c>: a
///     file that spells a completed stem must cite one of <see cref="CompletedStemSources"/> in
///     code (comments are stripped first) or carry an entry in
///     <see cref="FreeHandSpellingAllowlist"/> with a reason, an owner and an expiry.
///   </description></item>
///   <item><description>
///     A behavioural closure over <see cref="ClosureFixtures"/>: for each model, every
///     <see cref="ISagaComponentEmitter"/> (found by reflection) and the top-level handler
///     emitters are run, and every completed-event identifier they consume must be one the
///     producing emitters declare. REFERENCED must be a subset of EMITTED.
///   </description></item>
/// </list>
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class CompletedEventClosureTests
{
    // =============================================================================
    // Policy data
    // =============================================================================

    /// <summary>
    /// The only sanctioned ways to obtain a completed-event stem. An emitter that spells a
    /// stem must cite at least one of these in non-comment code.
    /// </summary>
    private static readonly string[] CompletedStemSources =
    [
        "ForkPathCompletedNaming",
        "PathEndTypeCollisionFinder.CompletedEventName",
        "NamingHelper.GetCompletedEventName",
    ];

    /// <summary>
    /// Regexes that recognise a free-hand completed-stem spelling in emitter source:
    /// an interpolation hole immediately followed by <c>Completed</c>, or the bare literal.
    /// </summary>
    private static readonly Regex[] StemSpellingPatterns =
    [
        new(@"\}Completed\b", RegexOptions.Compiled),
        new(@"""Completed""", RegexOptions.Compiled),
    ];

    /// <summary>
    /// Emitter files (relative to <c>Strategos.Generators/Emitters</c>, forward slashes)
    /// that legitimately spell a stem without citing a sanctioned source. Every entry is
    /// re-checked: an entry whose file no longer needs it is itself a failure, so the list
    /// can only shrink.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, FreeHandSpelling> FreeHandSpellingAllowlist =
        new Dictionary<string, FreeHandSpelling>(StringComparer.Ordinal)
        {
            ["Saga/SagaFailureHandlerComponentEmitter.cs"] = new(
                Reason: "FailureHandler_{id}_{step}Completed mirrors EventsEmitter.EmitFailureHandlerStepCompletedEvent "
                    + "byte for byte; failure-handler steps are never fork-path instances, so the stem never needs "
                    + "path qualification. The closure test below still proves the pair agree.",
                Owner: "Strategos workflow generator maintainers",
                Expiry: "when a typed CompletedStem value replaces string stems (G-A layer 1)"),
        };

    /// <summary>
    /// The model matrix the closure runs over: every shared-type / colliding-phase fork
    /// shape the naming table distinguishes, plus a shared type that is also used linearly.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<WorkflowModel>> ClosureFixtures =
        new Dictionary<string, Func<WorkflowModel>>(StringComparer.Ordinal)
        {
            ["SharedTypeInstanceNamed"] = ForkPathMessageFixtures.SharedTypeInstanceNamed,
            ["SharedPhaseName"] = ForkPathMessageFixtures.SharedPhaseName,
            ["UniqueTypes"] = ForkPathMessageFixtures.UniqueTypes,
            ["SharedTypeInteriors"] = ForkPathMessageFixtures.SharedTypeInteriors,
            ["SharedPhaseNameWithCompensation"] = () =>
                FoldCompensationSteps(ForkPathMessageFixtures.SharedPhaseNameWithCompensation()),
            ["SharedTypeWithLinearReuse"] = SharedTypeWithLinearReuse,
        };

    /// <summary>
    /// Every <see cref="ISagaComponentEmitter"/> the generator ships. Reflection discovers
    /// the live set; this list pins it so a new emitter that reflection somehow misses (or
    /// one that is removed) is a red rather than silent shrinkage of the closure.
    /// </summary>
    private static readonly string[] ExpectedSagaComponentEmitters =
    [
        "SagaApprovalComponentEmitter",
        "SagaCompensationComponentEmitter",
        "SagaConcurrencyPolicyEmitter",
        "SagaFailureHandlerComponentEmitter",
        "SagaLoopConditionsEmitter",
        "SagaNotFoundHandlersEmitter",
        "SagaPropertiesEmitter",
        "SagaStartMethodEmitter",
        "SagaStepHandlersEmitter",
        "SagaTimeoutComponentEmitter",
    ];

    // =============================================================================
    // Recognisers
    // =============================================================================

    /// <summary>A declared completed-event type: <c>partial record {Name}Completed(</c>.</summary>
    private static readonly Regex EmittedCompletedEvent =
        new(@"\brecord\s+([A-Z]\w*Completed)\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// A consumed completed-event type: a parameter (<c>Handle(XCompleted evt</c>,
    /// <c>NotFound(XCompleted evt</c>), a worker return type (<c>Task&lt;XCompleted&gt;</c>),
    /// or a construction (<c>new XCompleted(</c>).
    /// </summary>
    private static readonly Regex[] ReferencedCompletedEvent =
    [
        new(@"\(\s*([A-Z]\w*Completed)\s+\w+", RegexOptions.Compiled),
        new(@"Task<([A-Z]\w*Completed)>", RegexOptions.Compiled),
        new(@"\bnew\s+([A-Z]\w*Completed)\s*\(", RegexOptions.Compiled),
    ];

    // =============================================================================
    // A. Structural scan
    // =============================================================================

    /// <summary>
    /// Every emitter source file that spells a completed stem cites a sanctioned naming
    /// source in code, or is allowlisted with a reason; every allowlist entry is still needed.
    /// </summary>
    [Test]
    public async Task CompletedStemSpelling_EveryEmitterFile_CitesSanctionedSourceOrIsAllowlisted()
    {
        var emittersDir = Path.Combine(FindSolutionRoot(), "Strategos.Generators", "Emitters");
        var files = Directory.EnumerateFiles(emittersDir, "*.cs", SearchOption.AllDirectories)
            .Select(path => (
                RelativePath: Path.GetRelativePath(emittersDir, path).Replace('\\', '/'),
                Text: File.ReadAllText(path)))
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .ToList();

        // Indeterminate is not pass: an empty scan means the directory moved, not that
        // every emitter is clean.
        await Assert.That(files.Count).IsGreaterThan(0)
            .Because($"no emitter sources found under '{emittersDir}'");

        var violations = EvaluateStemSpelling(files, FreeHandSpellingAllowlist);

        await Assert.That(violations).IsEmpty()
            .Because(
                "an emitter that spells a {stem}Completed name must derive the stem from "
                + string.Join(" / ", CompletedStemSources)
                + " or carry an allowlist entry; violations: " + string.Join("; ", violations));
    }

    /// <summary>
    /// Self-test: with the allowlist emptied the guard must name the one file that relies on it.
    /// </summary>
    [Test]
    public async Task CompletedStemSpelling_AllowlistRemoved_FlagsFailureHandlerEmitter()
    {
        var emittersDir = Path.Combine(FindSolutionRoot(), "Strategos.Generators", "Emitters");
        var files = Directory.EnumerateFiles(emittersDir, "*.cs", SearchOption.AllDirectories)
            .Select(path => (
                RelativePath: Path.GetRelativePath(emittersDir, path).Replace('\\', '/'),
                Text: File.ReadAllText(path)))
            .ToList();

        var violations = EvaluateStemSpelling(files, new Dictionary<string, FreeHandSpelling>(StringComparer.Ordinal));

        await Assert.That(violations.Any(v => v.StartsWith("Saga/SagaFailureHandlerComponentEmitter.cs", StringComparison.Ordinal)))
            .IsTrue()
            .Because("removing the allowlist entry must turn the guard red for the file it covers");
    }

    /// <summary>
    /// Kill fixture for the structural scan: the pre-fix <c>SagaNotFoundHandlersEmitter</c>
    /// body (revision 595a949^) spells <c>{stepName}Completed</c> and never cites a naming
    /// source. It must be rejected.
    /// </summary>
    [Test]
    public async Task CompletedStemSpelling_LegacyNotFoundEmitterSource_IsRejected()
    {
        var violations = EvaluateStemSpelling(
            [("Saga/SagaNotFoundHandlersEmitter.cs", LegacyNotFoundEmitterSource)],
            new Dictionary<string, FreeHandSpelling>(StringComparer.Ordinal));

        await Assert.That(violations).HasCount().EqualTo(1);
        await Assert.That(violations[0]).Contains("Saga/SagaNotFoundHandlersEmitter.cs");
    }

    // =============================================================================
    // B. Behavioural closure
    // =============================================================================

    /// <summary>
    /// For every fixture model, every completed-event identifier any consumer emitter
    /// references is one the producer emitters declare.
    /// </summary>
    [Test]
    public async Task CompletedEventClosure_EveryFixture_EveryConsumerNamesAnEmittedEvent()
    {
        var failures = new List<string>();

        foreach (var (fixtureName, factory) in ClosureFixtures)
        {
            var model = factory();
            var emitted = CollectEmitted(model);

            // A regex that stops matching would make REFERENCED ⊆ ∅ hold vacuously.
            if (emitted.Count == 0)
            {
                failures.Add($"{fixtureName}: no completed events recognised in producer output (indeterminate)");
                continue;
            }

            foreach (var (emitterName, consumer) in EnumerateConsumers())
            {
                failures.AddRange(Closure(fixtureName, emitterName, consumer, model, emitted));
            }
        }

        await Assert.That(failures).IsEmpty()
            .Because("every consumed {stem}Completed must be produced; " + string.Join("; ", failures));
    }

    /// <summary>
    /// Kill fixture for the closure: the pre-fix NotFound emitter, kept as a test-local class,
    /// names <c>AnalyzeStepCompleted</c> on the shared-type fixture although the events
    /// emitter no longer produces it. The closure must reject it.
    /// </summary>
    [Test]
    public async Task CompletedEventClosure_LegacyNotFoundEmitter_IsRejected()
    {
        var model = ForkPathMessageFixtures.SharedTypeInstanceNamed();
        var emitted = CollectEmitted(model);
        var legacy = new LegacyNotFoundEmitter();

        var failures = Closure(
            "SharedTypeInstanceNamed",
            nameof(LegacyNotFoundEmitter),
            m =>
            {
                var sb = new StringBuilder();
                legacy.Emit(sb, m);
                return sb.ToString();
            },
            model,
            emitted);

        await Assert.That(failures).HasCount().EqualTo(1);
        await Assert.That(failures[0]).Contains("AnalyzeStepCompleted");
    }

    /// <summary>
    /// The reflected <see cref="ISagaComponentEmitter"/> set equals the pinned list, so the
    /// closure can neither silently lose an emitter nor miss a new one.
    /// </summary>
    [Test]
    public async Task SagaComponentEmitters_ReflectedSet_MatchesPinnedList()
    {
        var reflected = SagaComponentEmitterTypes()
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        await Assert.That(reflected).IsEquivalentTo(ExpectedSagaComponentEmitters)
            .Because("a new or removed ISagaComponentEmitter must be registered here so the closure covers it");
    }

    // =============================================================================
    // Mechanism
    // =============================================================================

    private static List<string> EvaluateStemSpelling(
        IEnumerable<(string RelativePath, string Text)> files,
        IReadOnlyDictionary<string, FreeHandSpelling> allowlist)
    {
        var violations = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (relativePath, text) in files)
        {
            seen.Add(relativePath);
            var code = StripCommentLines(text);
            var spellsStem = StemSpellingPatterns.Any(p => p.IsMatch(code));
            var citesSource = CompletedStemSources.Any(s => code.Contains(s, StringComparison.Ordinal));
            var allowlisted = allowlist.TryGetValue(relativePath, out var entry);

            if (spellsStem && !citesSource && !allowlisted)
            {
                violations.Add($"{relativePath} spells a completed stem without citing a sanctioned source");
            }

            if (allowlisted && (!spellsStem || citesSource))
            {
                violations.Add($"{relativePath} has a stale allowlist entry ({entry!.Owner}); remove it");
            }
        }

        foreach (var missing in allowlist.Keys.Where(k => !seen.Contains(k)))
        {
            violations.Add($"{missing} is allowlisted but does not exist");
        }

        return violations;
    }

    private static string StripCommentLines(string text) =>
        string.Join(
            '\n',
            text.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    private static HashSet<string> CollectEmitted(WorkflowModel model)
    {
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var producer in new[] { EventsEmitter.Emit(model), CommandsEmitter.Emit(model) })
        {
            foreach (Match m in EmittedCompletedEvent.Matches(producer))
            {
                emitted.Add(m.Groups[1].Value);
            }
        }

        return emitted;
    }

    private static List<string> Closure(
        string fixtureName,
        string emitterName,
        Func<WorkflowModel, string> consumer,
        WorkflowModel model,
        HashSet<string> emitted)
    {
        string output;
        try
        {
            output = consumer(model);
        }
        catch (Exception ex)
        {
            // An emitter that throws is indeterminate for this cell, never a pass.
            return [$"{fixtureName}/{emitterName}: threw {ex.GetType().Name}: {ex.Message}"];
        }

        return ReferencedCompletedEvent
            .SelectMany(p => p.Matches(output).Select(m => m.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .Where(name => !emitted.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => $"{fixtureName}/{emitterName} references {name}, which no producer emits")
            .ToList();
    }

    private static IEnumerable<(string Name, Func<WorkflowModel, string> Consumer)> EnumerateConsumers()
    {
        foreach (var type in SagaComponentEmitterTypes())
        {
            var emitter = (ISagaComponentEmitter)Activator.CreateInstance(type)!;
            yield return (type.Name, model =>
            {
                var sb = new StringBuilder();
                emitter.Emit(sb, model);
                return sb.ToString();
            });
        }

        yield return (nameof(WorkerHandlerEmitter), WorkerHandlerEmitter.Emit);
        yield return (nameof(SagaEmitter), SagaEmitter.Emit);
    }

    private static IEnumerable<Type> SagaComponentEmitterTypes() =>
        typeof(ISagaComponentEmitter).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(ISagaComponentEmitter).IsAssignableFrom(t))
            .Where(t => t.GetConstructor(Type.EmptyTypes) is not null);

    /// <summary>
    /// Puts a hand-built model into the shape the generator hands every emitter: each
    /// compensation step TYPE is folded into <see cref="WorkflowModel.Steps"/> (never into
    /// <see cref="WorkflowModel.StepNames"/>), mirroring the fold in
    /// <c>WorkflowIncrementalGenerator</c>. Without it <see cref="EventsEmitter"/> has no
    /// step to declare <c>{Comp}Completed</c> for and the closure is trivially open.
    /// </summary>
    private static WorkflowModel FoldCompensationSteps(WorkflowModel model)
    {
        var steps = new List<StepModel>(model.Steps ?? []);
        var names = new HashSet<string>(steps.Select(s => s.StepName), StringComparer.Ordinal);
        foreach (var step in steps.ToArray())
        {
            if (step.Compensation is null)
            {
                continue;
            }

            var compTypeName = step.Compensation.CompensationStepTypeName;
            var compStepName = compTypeName[(compTypeName.LastIndexOf('.') + 1)..];
            if (names.Add(compStepName))
            {
                steps.Add(StepModel.Create(compStepName, compTypeName));
            }
        }

        return model with { Steps = steps };
    }

    private static WorkflowModel SharedTypeWithLinearReuse()
    {
        var forkOnly = ForkPathMessageFixtures.SharedTypeInstanceNamed();
        var linear = StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep");
        return forkOnly with
        {
            StepNames = ["AnalyzeStep", .. forkOnly.StepNames],
            Steps = [linear, .. forkOnly.Steps!],
        };
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

    /// <summary>An allowlist entry: why the file may spell a stem, who owns it, when it retires.</summary>
    private sealed record FreeHandSpelling(string Reason, string Owner, string Expiry);

    // =============================================================================
    // Kill fixtures: the pre-fix SagaNotFoundHandlersEmitter (595a949^)
    // =============================================================================

    /// <summary>
    /// The Emit body of <c>SagaNotFoundHandlersEmitter</c> before 595a949, kept verbatim as
    /// the structural scan's permanent failing subject.
    /// </summary>
    private const string LegacyNotFoundEmitterSource = """
        using System.Collections.Generic;
        using System.Text;

        using Strategos.Generators.Models;
        using Strategos.Generators.Polyfills;

        namespace Strategos.Generators.Emitters.Saga;

        internal sealed class SagaNotFoundHandlersEmitter : ISagaComponentEmitter
        {
            public void Emit(StringBuilder sb, WorkflowModel model)
            {
                ThrowHelper.ThrowIfNull(sb, nameof(sb));
                ThrowHelper.ThrowIfNull(model, nameof(model));

                var sagaClassName = $"{model.PascalName}Saga";
                EmitStartCommandNotFoundHandler(sb, model, sagaClassName);

                // NotFound for each step's completed event - use base step names (deduplicated)
                // Workers return events using unprefixed step type names
                var emittedStepEvents = new HashSet<string>(StringComparer.Ordinal);
                if (model.Steps is not null)
                {
                    foreach (var step in model.Steps)
                    {
                        if (emittedStepEvents.Add(step.StepName))
                        {
                            sb.AppendLine();
                            EmitStepCompletedNotFoundHandler(sb, step.StepName, sagaClassName);
                        }
                    }
                }
            }

            private static void EmitStepCompletedNotFoundHandler(
                StringBuilder sb,
                string stepName,
                string sagaClassName)
            {
                var eventName = $"{stepName}Completed";
                sb.AppendLine($"    public static void NotFound({eventName} evt, ILogger<{sagaClassName}> logger)");
            }
        }
        """;

    /// <summary>
    /// The pre-fix emitter's behaviour: one <c>NotFound({StepName}Completed)</c> per distinct
    /// step type in <c>model.Steps</c>, ignoring fork-path stem qualification.
    /// </summary>
    private sealed class LegacyNotFoundEmitter : ISagaComponentEmitter
    {
        public void Emit(StringBuilder sb, WorkflowModel model)
        {
            var sagaClassName = $"{model.PascalName}Saga";
            var emittedStepEvents = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in model.Steps ?? [])
            {
                if (emittedStepEvents.Add(step.StepName))
                {
                    var eventName = $"{step.StepName}Completed";
                    sb.AppendLine($"    public static void NotFound({eventName} evt, ILogger<{sagaClassName}> logger)");
                    sb.AppendLine("    {");
                    sb.AppendLine("    }");
                }
            }
        }
    }
}
