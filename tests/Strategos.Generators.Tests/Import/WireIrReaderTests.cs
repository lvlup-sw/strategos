// -----------------------------------------------------------------------
// <copyright file="WireIrReaderTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis.Text;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// DR-12 (#100) — generator-driver coverage for the JSON import front-end (ingestion
/// half). Drives <see cref="WorkflowIncrementalGenerator"/> over
/// <c>*.workflow.json</c> <c>AdditionalFiles</c> and pins the two failure modes the
/// front-end must surface as STABLE diagnostics rather than a generator crash:
/// malformed JSON (AGWF023) and <c>schemaVersion</c> skew (AGWF024). Also pins the
/// discovery convention, the no-crash/no-diagnostic happy path, and incremental-cache
/// correctness (a JSON edit re-runs the import pipeline; an unchanged run is cached).
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class WireIrReaderTests
{
    private const string MalformedInputCode = "AGWF023";
    private const string SchemaVersionSkewCode = "AGWF024";

    private const string ValidV1Workflow = """
        {
          "schemaVersion": "1.0",
          "name": "imported-sample",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "Intake", "isTerminal": false, "stepType": "IntakeStep" }
          ],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": []
        }
        """;

    /// <summary>A syntactically broken document — a value is missing after the colon.</summary>
    private const string MalformedWorkflow = """
        { "schemaVersion": "1.0", "name": }
        """;

    /// <summary>A well-formed document declaring an unsupported schema version.</summary>
    private const string FutureVersionWorkflow = """
        {
          "schemaVersion": "2.0",
          "name": "future-sample",
          "steps": [],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": []
        }
        """;

    /// <summary>A well-formed document that omits <c>schemaVersion</c> entirely.</summary>
    private const string MissingVersionWorkflow = """
        {
          "name": "no-version",
          "steps": [],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": []
        }
        """;

    /// <summary>A valid workflow that differs from <see cref="ValidV1Workflow"/> only by the step name.</summary>
    private const string ValidV1WorkflowEdited = """
        {
          "schemaVersion": "1.0",
          "name": "imported-sample",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "Triage", "isTerminal": false, "stepType": "TriageStep" }
          ],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": []
        }
        """;

    /// <summary>Malformed <c>*.workflow.json</c> reports the stable malformed-input diagnostic and never crashes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MalformedWorkflowJson_ReportsStableDiagnostic()
    {
        var result = RunImport(new InMemoryAdditionalText("broken.workflow.json", MalformedWorkflow));

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == MalformedInputCode);
        await Assert.That(diagnostic).IsNotNull()
            .Because("malformed workflow-definition JSON must surface as the stable AGWF023 diagnostic.");
        await Assert.That(diagnostic!.GetMessage()).Contains("broken.workflow.json")
            .Because("the diagnostic names the offending import file.");

        // No AGWF024 — a document that never parses cannot also be a version-skew report.
        await Assert.That(result.Diagnostics.Any(d => d.Id == SchemaVersionSkewCode)).IsFalse();
    }

    /// <summary>A well-formed file with an unsupported <c>schemaVersion</c> reports the stable skew diagnostic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnsupportedSchemaVersion_ReportsStableDiagnostic()
    {
        var result = RunImport(new InMemoryAdditionalText("future.workflow.json", FutureVersionWorkflow));

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == SchemaVersionSkewCode);
        await Assert.That(diagnostic).IsNotNull()
            .Because("a schemaVersion other than \"1.0\" must surface as the stable AGWF024 diagnostic.");
        await Assert.That(diagnostic!.GetMessage()).Contains("2.0")
            .Because("the diagnostic reports the offending declared schema version.");

        await Assert.That(result.Diagnostics.Any(d => d.Id == MalformedInputCode)).IsFalse()
            .Because("a well-formed document is not a malformed-input failure.");
    }

    /// <summary>An absent <c>schemaVersion</c> is treated as skew — it is not the supported "1.0".</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingSchemaVersion_ReportsSkewDiagnostic()
    {
        var result = RunImport(new InMemoryAdditionalText("noversion.workflow.json", MissingVersionWorkflow));

        await Assert.That(result.Diagnostics.Any(d => d.Id == SchemaVersionSkewCode)).IsTrue()
            .Because("a missing schemaVersion is not the supported \"1.0\" and must be rejected as skew.");
    }

    /// <summary>A valid v1.0 <c>*.workflow.json</c> parses cleanly — no import diagnostics, no crash.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ValidWorkflowJson_ProducesNoImportDiagnostics()
    {
        var result = RunImport(new InMemoryAdditionalText("valid.workflow.json", ValidV1Workflow));

        await Assert.That(result.Diagnostics.Any(d => d.Id == MalformedInputCode || d.Id == SchemaVersionSkewCode))
            .IsFalse()
            .Because("a well-formed v1.0 workflow-definition import produces neither import failure diagnostic.");
    }

    /// <summary>Files that do not match the <c>*.workflow.json</c> convention are ignored, even when malformed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NonWorkflowJsonFile_IsIgnored()
    {
        // A malformed plain .json that is NOT a *.workflow.json must not be picked up.
        var result = RunImport(new InMemoryAdditionalText("appsettings.json", MalformedWorkflow));

        await Assert.That(result.Diagnostics.Any(d => d.Id == MalformedInputCode || d.Id == SchemaVersionSkewCode))
            .IsFalse()
            .Because("only files matching the *.workflow.json convention participate in JSON import.");
    }

    /// <summary>Editing the JSON invalidates the import pipeline and re-runs it; an unchanged run is cached.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EditingWorkflowJson_InvalidatesAndReruns()
    {
        var original = new InMemoryAdditionalText("wf.workflow.json", ValidV1Workflow);
        var (driver, compilation) = CreateTrackingDriver(original);

        // First run establishes the baseline.
        driver = driver.RunGenerators(compilation);

        // Second run with the SAME inputs: the import read step must be cached (not re-run).
        driver = driver.RunGenerators(compilation);
        var unchangedReasons = StepReasons(driver.GetRunResult(), WorkflowIncrementalGenerator.ImportReadTrackingName);
        await Assert.That(unchangedReasons).IsNotEmpty()
            .Because("the import read step must be tracked so incrementality is observable.");
        await Assert.That(unchangedReasons.All(r =>
                r == IncrementalStepRunReason.Cached || r == IncrementalStepRunReason.Unchanged))
            .IsTrue()
            .Because("re-running with an unchanged import file must not re-execute the read step.");

        // Edit the JSON content: the read step must re-run (Modified) — the edit invalidated the cache.
        var edited = new InMemoryAdditionalText("wf.workflow.json", ValidV1WorkflowEdited);
        driver = driver.ReplaceAdditionalText(original, edited).RunGenerators(compilation);
        var editedReasons = StepReasons(driver.GetRunResult(), WorkflowIncrementalGenerator.ImportReadTrackingName);
        await Assert.That(editedReasons.Any(r => r == IncrementalStepRunReason.Modified))
            .IsTrue()
            .Because("editing the workflow JSON must invalidate the import cache and re-run the pipeline.");
    }

    /// <summary>Editing a valid file into a malformed one flips the reported diagnostic (functional incrementality).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EditingWorkflowJsonToMalformed_ChangesReportedDiagnostic()
    {
        var original = new InMemoryAdditionalText("wf.workflow.json", ValidV1Workflow);
        var (driver, compilation) = CreateTrackingDriver(original);

        driver = driver.RunGenerators(compilation);
        var before = driver.GetRunResult();
        await Assert.That(before.Diagnostics.Any(d => d.Id == MalformedInputCode)).IsFalse();

        var malformed = new InMemoryAdditionalText("wf.workflow.json", MalformedWorkflow);
        driver = driver.ReplaceAdditionalText(original, malformed).RunGenerators(compilation);
        var after = driver.GetRunResult();

        await Assert.That(after.Diagnostics.Any(d => d.Id == MalformedInputCode)).IsTrue()
            .Because("after the edit the now-malformed file must report the stable AGWF023 diagnostic.");
    }

    private static GeneratorDriverRunResult RunImport(params AdditionalText[] additionalTexts)
    {
        var (driver, compilation) = CreateTrackingDriver(additionalTexts);
        return driver.RunGenerators(compilation).GetRunResult();
    }

    private static (GeneratorDriver Driver, Compilation Compilation) CreateTrackingDriver(
        params AdditionalText[] additionalTexts)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "ImportTestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText("namespace ImportPlaceholder { }")],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: [new WorkflowIncrementalGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts,
            parseOptions: null,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

        return (driver, compilation);
    }

    private static IReadOnlyList<IncrementalStepRunReason> StepReasons(
        GeneratorDriverRunResult result,
        string trackingName)
    {
        return result.Results
            .SelectMany(r => r.TrackedSteps.TryGetValue(trackingName, out var steps)
                ? steps
                : ImmutableArray<IncrementalGeneratorRunStep>.Empty)
            .SelectMany(s => s.Outputs)
            .Select(o => o.Reason)
            .ToList();
    }

    /// <summary>An in-memory <see cref="AdditionalText"/> for driving the generator over synthetic import files.</summary>
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryAdditionalText(string path, string content)
        {
            this.Path = path;
            this._text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => this._text;
    }
}
