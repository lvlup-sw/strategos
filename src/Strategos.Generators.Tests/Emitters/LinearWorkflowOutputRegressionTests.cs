// -----------------------------------------------------------------------
// <copyright file="LinearWorkflowOutputRegressionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Output-equality guard for the non-event-sourced saga emit of a linear workflow.
/// </summary>
/// <remarks>
/// <para>
/// A linear workflow has no off-main-flow steps at all — no fork or branch paths, no
/// failure handlers, no approval rejection or escalation steps, no confidence handlers.
/// Classifying off-main-flow steps and reordering the step list must therefore be
/// completely INERT for it. This compares the regenerated emit against a baseline captured
/// from the generator before any of that work, so a leak into linear output is caught
/// without anyone having to read a diff.
/// </para>
/// <para>
/// The baseline and the reasoning behind it live next to it in
/// <c>Baselines/LinearWorkflowSaga.baseline.provenance.txt</c>, including the commit it was
/// captured at. Read that file before touching the baseline: making a failing comparison
/// pass by re-capturing destroys the only "before" this guard has.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class LinearWorkflowOutputRegressionTests
{
    /// <summary>
    /// The generated hint name of the linear fixture's saga.
    /// </summary>
    private const string SagaHintName = "ProcessOrderSaga.g.cs";

    /// <summary>
    /// The baseline file, relative to the test assembly's output directory.
    /// </summary>
    private const string BaselineRelativePath = "Baselines/LinearWorkflowSaga.baseline.txt";

    /// <summary>
    /// Where the baseline lives in the repository, quoted in failure messages so a reader
    /// lands on the provenance note rather than on the build output copy.
    /// </summary>
    private const string BaselineSourcePath =
        "src/Strategos.Generators.Tests/Baselines/LinearWorkflowSaga.baseline.txt";

    /// <summary>
    /// The placeholder the volatile tool-version argument is stored as in the baseline.
    /// </summary>
    private const string VersionPlaceholder = "{generator-version}";

    /// <summary>
    /// Matches the emitted <c>[GeneratedCode("LevelUp.Strategos", "…")]</c> version argument.
    /// It is resolved from the generator assembly's MinVer-stamped informational version, so
    /// it moves with every commit's height even when no generator source changes. It is the
    /// ONLY token this guard normalizes; every other byte is compared exactly.
    /// </summary>
    private static readonly Regex ToolVersionArgument = new(
        "(?<prefix>\\[global::System\\.CodeDom\\.Compiler\\.GeneratedCode\\(\"LevelUp\\.Strategos\", \")[^\"]*(?<suffix>\"\\)\\])",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The non-event-sourced saga emitted for a linear workflow is unchanged from the
    /// baseline captured before the off-main-flow classification and step-list ordering
    /// work.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LinearWorkflowSaga_NonEventSourcedOutput_MatchesPreChangeBaseline()
    {
        var baselinePath = Path.Combine(AppContext.BaseDirectory, BaselineRelativePath);

        await Assert.That(File.Exists(baselinePath))
            .IsTrue()
            .Because(
                $"the pre-change baseline must be present at '{baselinePath}'; it is committed at "
                + $"'{BaselineSourcePath}' and copied to the test output directory");

        var expected = Normalize(File.ReadAllText(baselinePath));

        var generated = GeneratorTestHelper.GetGeneratedSource(
            GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow),
            SagaHintName);

        await Assert.That(generated)
            .IsNotEmpty()
            .Because($"the linear fixture must still emit a saga under hint name '{SagaHintName}'");

        var actual = Normalize(generated);

        await Assert.That(actual)
            .IsEqualTo(expected)
            .Because(
                "a linear workflow has no off-main-flow steps, so classifying them and reordering "
                + "the step list must leave its saga emit untouched. A difference here is either a "
                + "leak into linear output — fix the change, not the baseline — or a deliberate, "
                + $"stated change, in which case re-capture '{BaselineSourcePath}' and rewrite its "
                + "provenance note. Do not re-capture to make this pass");
    }

    /// <summary>
    /// The emit is stable across repeated generator runs, so a difference against the
    /// baseline is always attributable to a change and never to generator nondeterminism.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LinearWorkflowSaga_RepeatedGeneratorRuns_ProduceIdenticalOutput()
    {
        var first = GeneratorTestHelper.GetGeneratedSource(
            GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow), SagaHintName);
        var second = GeneratorTestHelper.GetGeneratedSource(
            GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow), SagaHintName);

        await Assert.That(second)
            .IsEqualTo(first)
            .Because(
                "an output-equality guard is only meaningful over a deterministic emit; if the "
                + "generator varied run to run, a baseline mismatch would be unattributable");
    }

    /// <summary>
    /// The guard is sensitive to the emit's content: an altered emit does not match the
    /// baseline. Without this, a normalization that flattened too much — or a comparison
    /// that silently succeeded on empty input — would make the guard unable to fail.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LinearWorkflowSaga_AlteredEmit_DoesNotMatchBaseline()
    {
        var baselinePath = Path.Combine(AppContext.BaseDirectory, BaselineRelativePath);
        var expected = Normalize(File.ReadAllText(baselinePath));

        // The shape a leak would take: one more phase transition in the emitted saga.
        var altered = Normalize(File.ReadAllText(baselinePath))
            .Replace("MarkCompleted();", "MarkCompleted(); // leaked", StringComparison.Ordinal);

        await Assert.That(altered)
            .IsNotEqualTo(expected)
            .Because("a changed emit must not compare equal to the baseline");

        // The declared normalization covers the tool version and nothing else: two emits that
        // differ only in that argument still compare equal, and that is the whole exemption.
        var restamped = expected.Replace(
            VersionPlaceholder,
            "9.9.9-only-the-version-differs",
            StringComparison.Ordinal);

        await Assert.That(Normalize(restamped))
            .IsEqualTo(expected)
            .Because("the tool-version argument is the only token the comparison normalizes away");
    }

    /// <summary>
    /// Applies the two declared normalizations: line endings to LF, and the MinVer-stamped
    /// tool-version argument to a fixed placeholder.
    /// </summary>
    /// <param name="source">The emitted or baseline source text.</param>
    /// <returns>The normalized text.</returns>
    private static string Normalize(string source) =>
        ToolVersionArgument.Replace(
            source.Replace("\r\n", "\n", StringComparison.Ordinal),
            "${prefix}" + VersionPlaceholder + "${suffix}");
}
