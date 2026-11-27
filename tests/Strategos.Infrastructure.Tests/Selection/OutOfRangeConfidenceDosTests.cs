// =============================================================================
// <copyright file="OutOfRangeConfidenceDosTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Diagnostics;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Abstractions;
using Strategos.Infrastructure.Selection;
using Strategos.Primitives;
using Strategos.Selection;

namespace Strategos.Infrastructure.Tests.Selection;

/// <summary>
/// Outcome confidence outside [0, 1] must be rejected before a belief is stored,
/// and selection must refuse a non-positive or non-finite beta shape instead of
/// recursing in the gamma sampler.
/// </summary>
/// <remarks>
/// The sampler recursion is not a tail call. A shape far below zero overflows the
/// thread stack. <see cref="StackOverflowException"/> terminates the process, so the
/// selection check runs in a child process.
/// </remarks>
[Property("Category", "Unit")]
public sealed class OutOfRangeConfidenceDosTests
{
    private const string AgentId = "content-judge";
    private const string Category = "TextGeneration";
    private const double ExtremeConfidence = 10_000_000d;
    private const string ChildEnvironmentVariable = "STRATEGOS_DOS_CHILD";

    /// <summary>
    /// Runs the poisoned-belief selection in this process when spawned as the child.
    /// </summary>
    [ModuleInitializer]
    internal static void RunPoisonedSelectionChild()
    {
        if (Environment.GetEnvironmentVariable(ChildEnvironmentVariable) == "1")
        {
            Environment.Exit(RunChild());
        }
    }

    /// <summary>
    /// Verifies that a confidence above, below, or outside the unit interval is rejected.
    /// </summary>
    [Test]
    [Arguments(10_000_000d)]
    [Arguments(-10_000_000d)]
    [Arguments(1.0000001d)]
    [Arguments(-0.0000001d)]
    [Arguments(double.PositiveInfinity)]
    [Arguments(double.NegativeInfinity)]
    [Arguments(double.NaN)]
    public async Task WithOutcome_OutOfRangeConfidence_ThrowsAndLeavesBeliefUnchanged(double confidence)
    {
        var belief = AgentBelief.CreatePrior(AgentId, Category);

        foreach (var outcome in Outcomes(confidence))
        {
            Exception? thrown = null;
            AgentBelief? updated = null;
            try
            {
                updated = belief.WithOutcome(outcome);
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            await Assert.That(thrown).IsNotNull();
            await Assert.That(thrown!.GetType()).IsEqualTo(typeof(ArgumentOutOfRangeException));
            await Assert.That(updated).IsNull();
            await Assert.That(belief.Alpha).IsEqualTo(AgentBelief.DefaultPriorAlpha);
            await Assert.That(belief.Beta).IsEqualTo(AgentBelief.DefaultPriorBeta);
            await Assert.That(belief.ObservationCount).IsEqualTo(0);
        }
    }

    /// <summary>
    /// Verifies that recording an out-of-range confidence does not replace the stored belief.
    /// </summary>
    [Test]
    [Arguments(10_000_000d)]
    [Arguments(-10_000_000d)]
    [Arguments(1.0000001d)]
    [Arguments(-0.0000001d)]
    [Arguments(double.PositiveInfinity)]
    [Arguments(double.NegativeInfinity)]
    [Arguments(double.NaN)]
    public async Task RecordOutcomeAsync_OutOfRangeConfidence_IsRejectedBeforePersist(double confidence)
    {
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        var selector = CreateContextualSelector(store, new DefaultBeliefPriorFactory());
        var before = await store.GetBeliefAsync(AgentId, Category);

        foreach (var outcome in Outcomes(confidence))
        {
            Exception? thrown = null;
            try
            {
                await selector.RecordOutcomeAsync(AgentId, Category, outcome);
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            await Assert.That(thrown).IsNotNull();
            await Assert.That(thrown!.GetType()).IsEqualTo(typeof(ArgumentOutOfRangeException));

            var after = await store.GetBeliefAsync(AgentId, Category);
            await Assert.That(after.IsSuccess).IsTrue();
            await Assert.That(after.Value.Alpha).IsEqualTo(before.Value.Alpha);
            await Assert.That(after.Value.Beta).IsEqualTo(before.Value.Beta);
            await Assert.That(after.Value.ObservationCount).IsEqualTo(before.Value.ObservationCount);
        }
    }

    /// <summary>
    /// Verifies that a direct save cannot replace a positive belief with a non-positive shape.
    /// </summary>
    [Test]
    [Arguments(0d, 2d)]
    [Arguments(-1d, 2d)]
    [Arguments(2d, 0d)]
    [Arguments(2d, -9_999_997d)]
    [Arguments(double.NaN, 2d)]
    [Arguments(2d, double.NaN)]
    [Arguments(2d, double.PositiveInfinity)]
    [Arguments(double.NegativeInfinity, 2d)]
    public async Task SaveBeliefAsync_NonPositiveOrNonFiniteParameters_DoesNotReplaceStoredBelief(double alpha, double beta)
    {
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        var good = new AgentBelief
        {
            AgentId = AgentId,
            TaskCategory = Category,
            Alpha = 3d,
            Beta = 2d,
            ObservationCount = 1,
        };
        var saved = await store.SaveBeliefAsync(good);
        await Assert.That(saved.IsSuccess).IsTrue();

        Exception? thrown = null;
        try
        {
            await store.SaveBeliefAsync(new AgentBelief
            {
                AgentId = AgentId,
                TaskCategory = Category,
                Alpha = alpha,
                Beta = beta,
                ObservationCount = 2,
            });
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown!.GetType()).IsEqualTo(typeof(ArgumentOutOfRangeException));

        var after = await store.GetBeliefAsync(AgentId, Category);
        await Assert.That(after.Value.Alpha).IsEqualTo(3d);
        await Assert.That(after.Value.Beta).IsEqualTo(2d);
        await Assert.That(after.Value.ObservationCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that confidence inside [0, 1] still updates the belief and can be sampled.
    /// </summary>
    [Test]
    [Arguments(0d, 0d, 1d)]
    [Arguments(1d, 1d, 0d)]
    [Arguments(0.25d, 0.25d, 0.75d)]
    public async Task RecordOutcomeAsync_ConfidenceInsideUnitInterval_UpdatesBeliefAndSelectionSucceeds(
        double confidence,
        double alphaDelta,
        double betaDelta)
    {
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        var selector = CreateContextualSelector(store, new DefaultBeliefPriorFactory());

        var recorded = await selector.RecordOutcomeAsync(AgentId, Category, AgentOutcome.Succeeded(confidence));
        await Assert.That(recorded.IsSuccess).IsTrue();

        var belief = await store.GetBeliefAsync(AgentId, Category);
        await Assert.That(belief.Value.Alpha).IsEqualTo(AgentBelief.DefaultPriorAlpha + alphaDelta);
        await Assert.That(belief.Value.Beta).IsEqualTo(AgentBelief.DefaultPriorBeta + betaDelta);
        await Assert.That(belief.Value.ObservationCount).IsEqualTo(1);

        var selected = await selector.SelectAgentAsync(CreateContext());
        await Assert.That(selected.IsSuccess).IsTrue();
        await Assert.That(selected.Value.TaskCategory).IsEqualTo(TaskCategory.TextGeneration);
        await Assert.That(double.IsFinite(selected.Value.SampledTheta)).IsTrue();
    }

    /// <summary>
    /// Verifies that a positive gamma shape below one still completes a selection.
    /// </summary>
    [Test]
    public async Task SelectAgentAsync_PositiveShapeBelowOne_Completes()
    {
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        var selector = CreateContextualSelector(store, new DefaultBeliefPriorFactory(0.25d, 0.4d));

        var selected = await selector.SelectAgentAsync(CreateContext());

        await Assert.That(selected.IsSuccess).IsTrue();
        await Assert.That(selected.Value.TaskCategory).IsEqualTo(TaskCategory.TextGeneration);
        await Assert.That(double.IsFinite(selected.Value.SampledTheta)).IsTrue();
    }

    /// <summary>
    /// Verifies that recording the documented extreme score and selecting again does not abort the process.
    /// </summary>
    [Test]
    public async Task OutOfRangeConfidence_RecordThenSelect_DoesNotKillTheProcess()
    {
        if (Environment.GetEnvironmentVariable(ChildEnvironmentVariable) == "1")
        {
            Environment.Exit(RunChild());
        }

        var assemblyPath = typeof(OutOfRangeConfidenceDosTests).Assembly.Location;
        var start = new ProcessStartInfo
        {
            FileName = ResolveDotnet(),
            Arguments = $"exec \"{assemblyPath}\"",
            WorkingDirectory = Path.GetDirectoryName(assemblyPath) ?? AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment[ChildEnvironmentVariable] = "1";

        using var process = Process.Start(start);
        await Assert.That(process).IsNotNull();

        var stdoutTask = process!.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var finished = process.WaitForExit(120_000);
        if (!finished)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var summary = finished && process.ExitCode == 0
            ? "ok"
            : $"exit {(finished ? process.ExitCode : -1)} stdout={Trim(stdout)} stderr={Trim(stderr)}";
        await Assert.That(summary).IsEqualTo("ok");
    }

    private static int RunChild()
    {
        try
        {
            return RunChildAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task<int> RunChildAsync()
    {
        var store = new InMemoryBeliefStore(NullLogger<InMemoryBeliefStore>.Instance);
        var extractor = new KeywordTaskFeatureExtractor(NullLogger<KeywordTaskFeatureExtractor>.Instance);
        var selector = CreateContextualSelector(store, new DefaultBeliefPriorFactory());
        var context = CreateContext();
        var category = extractor.ExtractFeatures(context).Category.ToString();

        Exception? recordError = null;
        try
        {
            await selector.RecordOutcomeAsync(AgentId, category, AgentOutcome.Succeeded(ExtremeConfidence));
        }
        catch (Exception ex)
        {
            recordError = ex;
        }

        var stored = await store.GetBeliefAsync(AgentId, category);
        if (IsUnusableShape(stored.Value.Alpha, stored.Value.Beta) || stored.Value.ObservationCount != 0)
        {
            await selector.SelectAgentAsync(context);
            Console.Error.WriteLine(
                $"selection returned after storing alpha={stored.Value.Alpha} beta={stored.Value.Beta} n={stored.Value.ObservationCount} category={category}");
            return 2;
        }

        if (recordError is not ArgumentOutOfRangeException)
        {
            Console.Error.WriteLine($"record error was {recordError?.GetType().FullName ?? "none"}");
            return 6;
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var selected = await selector.SelectAgentAsync(context);
            if (!selected.IsSuccess
                || selected.Value.TaskCategory.ToString() != category
                || !double.IsFinite(selected.Value.SampledTheta))
            {
                Console.Error.WriteLine("clean selection failed");
                return 3;
            }
        }

        Exception? saveError = null;
        try
        {
            await store.SaveBeliefAsync(new AgentBelief
            {
                AgentId = "direct-save",
                TaskCategory = Category,
                Alpha = 2d,
                Beta = -ExtremeConfidence,
                ObservationCount = 1,
            });
        }
        catch (Exception ex)
        {
            saveError = ex;
        }

        var saved = await store.GetBeliefAsync("direct-save", Category);
        if (IsUnusableShape(saved.Value.Alpha, saved.Value.Beta) || saved.Value.ObservationCount != 0)
        {
            Console.Error.WriteLine("SaveBeliefAsync persisted a non-positive beta");
            return 4;
        }

        if (saveError is not ArgumentOutOfRangeException)
        {
            Console.Error.WriteLine($"save error was {saveError?.GetType().FullName ?? "none"}");
            return 4;
        }

        var shapes = new (double Alpha, double Beta)[]
        {
            (2d, -ExtremeConfidence),
            (-ExtremeConfidence, 2d),
            (double.NaN, 2d),
            (2d, double.PositiveInfinity),
            (0d, 2d),
            (2d, 0d),
        };

        foreach (var shape in shapes)
        {
            var code = await AssertSelectionRejectsShape(extractor, shape.Alpha, shape.Beta);
            if (code != 0)
            {
                return code;
            }
        }

        return 0;
    }

    private static async Task<int> AssertSelectionRejectsShape(
        KeywordTaskFeatureExtractor extractor,
        double alpha,
        double beta)
    {
        var belief = new AgentBelief
        {
            AgentId = AgentId,
            TaskCategory = Category,
            Alpha = alpha,
            Beta = beta,
            ObservationCount = 1,
        };
        var evil = new FixedBeliefStore(belief);
        var contextual = CreateContextualSelector(evil, new DefaultBeliefPriorFactory());
        var thompson = new ThompsonSamplingAgentSelector(
            evil,
            new TaskCategoryClassifier(),
            NullLogger<ThompsonSamplingAgentSelector>.Instance,
            randomSeed: 1);

        if (!await RejectsShape(() => contextual.SelectAgentAsync(CreateContext())))
        {
            Console.Error.WriteLine($"contextual selector accepted alpha={alpha} beta={beta}");
            return 5;
        }

        if (!await RejectsShape(() => thompson.SelectAgentAsync(CreateContext())))
        {
            Console.Error.WriteLine($"thompson selector accepted alpha={alpha} beta={beta}");
            return 5;
        }

        return 0;
    }

    private static async Task<bool> RejectsShape(Func<Task<Result<AgentSelection>>> select)
    {
        try
        {
            await select().ConfigureAwait(false);
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return true;
        }
    }

    private static bool IsUnusableShape(double alpha, double beta)
    {
        return alpha <= 0d
            || beta <= 0d
            || !double.IsFinite(alpha)
            || !double.IsFinite(beta);
    }

    private static IEnumerable<AgentOutcome> Outcomes(double confidence)
    {
        yield return AgentOutcome.Succeeded(confidence);
        yield return AgentOutcome.Failed(confidence);
        yield return new AgentOutcome { Success = true, Confidence = confidence };
        yield return new AgentOutcome { Success = false, Confidence = confidence };
    }

    private static ContextualAgentSelector CreateContextualSelector(IBeliefStore store, IBeliefPriorFactory priors)
    {
        return new ContextualAgentSelector(
            store,
            new KeywordTaskFeatureExtractor(NullLogger<KeywordTaskFeatureExtractor>.Instance),
            priors,
            randomSeed: 1);
    }

    private static AgentSelectionContext CreateContext()
    {
        return new AgentSelectionContext
        {
            WorkflowId = Guid.NewGuid(),
            StepName = "AssessQuality",
            TaskDescription = "summarize the draft",
            AvailableAgents = [AgentId],
        };
    }

    private static string ResolveDotnet()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(directory, "dotnet");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet");
        if (File.Exists(home))
        {
            return home;
        }

        throw new InvalidOperationException("dotnet was not found on PATH.");
    }

    private static string Trim(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var flattened = value.ReplaceLineEndings(" ");
        return flattened.Length <= 1500 ? flattened : flattened[..1500];
    }

    private sealed class FixedBeliefStore : IBeliefStore
    {
        private readonly AgentBelief _belief;

        public FixedBeliefStore(AgentBelief belief)
        {
            _belief = belief;
        }

        public ValueTask<Result<AgentBelief>> GetBeliefAsync(
            string agentId,
            string taskCategory,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Result<AgentBelief>.Success(_belief));
        }

        public ValueTask<Result<Unit>> UpdateBeliefAsync(
            string agentId,
            string taskCategory,
            bool success,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Result<Unit>.Success(Unit.Value));
        }

        public ValueTask<Result<IReadOnlyList<AgentBelief>>> GetBeliefsForAgentAsync(
            string agentId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Result<IReadOnlyList<AgentBelief>>.Success((IReadOnlyList<AgentBelief>)[_belief]));
        }

        public ValueTask<Result<IReadOnlyList<AgentBelief>>> GetBeliefsForCategoryAsync(
            string taskCategory,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Result<IReadOnlyList<AgentBelief>>.Success((IReadOnlyList<AgentBelief>)[_belief]));
        }

        public ValueTask<Result<Unit>> SaveBeliefAsync(
            AgentBelief belief,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Result<Unit>.Success(Unit.Value));
        }
    }
}
