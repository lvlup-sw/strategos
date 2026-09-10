// -----------------------------------------------------------------------
// <copyright file="CompensationIdentityCorpusTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

using Strategos.Generators.Import;

using GeneratedWorkflow = Strategos.Contracts.Generated.WorkflowDefinitionV1;

namespace Strategos.Generators.Tests.Import;

// =============================================================================
// The "compensationStepType must be non-blank" rule has THREE independent
// encodings, none of which is derived from the others:
//
//   1. the emitted JSON Schema      — minLength: 1 + pattern ".*\S.*"
//   2. the generated contract DTO   — ContractJsonValidation.RequireNonWhitespace
//                                     in CompensationConfiguration.g.cs
//   3. the vendored analyzer reader — MinimalJsonReader.ReadRequiredCompensationStepType
//                                     (netstandard2.0, no System.Text.Json)
//
// Nothing forced them to agree: (2) and (3) are hand-written against (1) by
// convention. This corpus drives the SAME documents through all three and
// requires the same verdict from each, so a rule change in one encoding that is
// not carried into the others fails here instead of shipping as a wire/importer
// disagreement.
// =============================================================================

/// <summary>
/// One corpus for the non-blank <c>compensationStepType</c> rule: every document
/// is accepted or rejected identically by the schema, the generated DTO, and the
/// vendored analyzer reader.
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class CompensationIdentityCorpusTests
{
    /// <summary>The corpus. Each case is one wire document and the verdict all
    /// three encodings must reach.</summary>
    public static IEnumerable<Func<CompensationIdentityCase>> Corpus()
    {
        yield return () => new CompensationIdentityCase("missing", null, Accepted: false);
        yield return () => new CompensationIdentityCase("empty", string.Empty, Accepted: false);
        yield return () => new CompensationIdentityCase("whitespace-only", "   ", Accepted: false);
        yield return () => new CompensationIdentityCase("tab-and-newline", " \t ", Accepted: false);
        yield return () => new CompensationIdentityCase("valid", "FidCompensateStep", Accepted: true);
    }

    /// <summary>
    /// The generated contract DTO must reach the corpus verdict. A rejected
    /// document throws <see cref="JsonException"/> during deserialization rather
    /// than binding to a record with a blank identity.
    /// </summary>
    /// <param name="testCase">The corpus case.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodDataSource(nameof(Corpus))]
    public async Task GeneratedDto_ReachesTheCorpusVerdict(CompensationIdentityCase testCase)
    {
        var json = WorkflowDocument(testCase.Moniker);

        if (testCase.Accepted)
        {
            var document = JsonSerializer.Deserialize<GeneratedWorkflow>(json);
            await Assert.That(document).IsNotNull();
            return;
        }

        await Assert.That(() => JsonSerializer.Deserialize<GeneratedWorkflow>(json))
            .Throws<JsonException>()
            .Because($"the generated DTO must reject the '{testCase.Case}' compensation moniker.");
    }

    /// <summary>
    /// The vendored analyzer reader must reach the same verdict, and name the rule
    /// it applied. The reader is hand-written for netstandard2.0 and shares no code
    /// with the generated DTO, so this is the encoding most likely to drift.
    /// </summary>
    /// <param name="testCase">The corpus case.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodDataSource(nameof(Corpus))]
    public async Task MinimalJsonReader_ReachesTheCorpusVerdict(CompensationIdentityCase testCase)
    {
        var json = WorkflowDocument(testCase.Moniker);

        if (testCase.Accepted)
        {
            var document = WireWorkflowReader.Read(json);
            await Assert.That(document).IsNotNull();
            return;
        }

        var thrown = Assert.Throws<JsonParseException>(() => WireWorkflowReader.Read(json));

        await Assert.That(thrown).IsNotNull()
            .Because($"the vendored reader must reject the '{testCase.Case}' compensation moniker.");
        await Assert.That(thrown!.Message).Contains("compensationStepType");

        // The missing case fails the "must be a string" arm; the blank cases fail the
        // non-whitespace arm. Both are rejections, and both name the property.
        var expected = testCase.Moniker is null
            ? "must be a string"
            : "must contain at least one non-whitespace character";
        await Assert.That(thrown.Message).Contains(expected);
    }

    /// <summary>
    /// The bundled JSON Schema — the document Exarchos and Basileus validate
    /// against — must reach the same verdict. This is the encoding the other two
    /// are written to match.
    /// </summary>
    /// <param name="testCase">The corpus case.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodDataSource(nameof(Corpus))]
    public async Task BundledSchema_ReachesTheCorpusVerdict(CompensationIdentityCase testCase)
    {
        var schema = await NJsonSchema.JsonSchema.FromFileAsync(
            ContractsSchemaPaths.BundledWorkflowSchema);
        var errors = schema.Validate(WorkflowDocument(testCase.Moniker));

        if (testCase.Accepted)
        {
            await Assert.That(errors).IsEmpty()
                .Because("the valid corpus document must validate: "
                    + string.Join("; ", errors.Select(e => $"{e.Path}: {e.Kind}")));
            return;
        }

        await Assert.That(errors).IsNotEmpty()
            .Because($"the bundled schema must reject the '{testCase.Case}' compensation moniker.");

        // The step union is an anyOf, so the top-level error is a rolled-up
        // ArrayItemNotValid; the moniker rejection lives in the arm's child errors.
        var flattened = Flatten(errors).ToList();
        await Assert.That(flattened.Any(error =>
                error.Path?.Contains("compensationStepType", StringComparison.Ordinal) == true
                || error.Property?.Contains("compensationStepType", StringComparison.Ordinal) == true))
            .IsTrue()
            .Because("the rejection must be attributed to compensationStepType, not to an "
                + "unrelated part of the document: "
                + string.Join("; ", flattened.Select(e => $"{e.Path}: {e.Kind}")));
    }

    /// <summary>
    /// Flattens NJsonSchema's nested validation errors. A failure inside a union arm
    /// (or any child schema) is reported as a rolled-up parent error whose real cause
    /// hangs off <c>ChildSchemaValidationError.Errors</c>.
    /// </summary>
    private static IEnumerable<NJsonSchema.Validation.ValidationError> Flatten(
        IEnumerable<NJsonSchema.Validation.ValidationError> errors)
    {
        foreach (var error in errors)
        {
            yield return error;

            if (error is NJsonSchema.Validation.ChildSchemaValidationError child)
            {
                foreach (var nested in Flatten(child.Errors.SelectMany(pair => pair.Value)))
                {
                    yield return nested;
                }
            }
        }
    }

    /// <summary>
    /// The three encodings agree document-for-document. Running them side by side in
    /// one test makes the agreement itself the assertion: a divergence names the
    /// document and which encoding disagreed, rather than surfacing as two unrelated
    /// red tests in different suites.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AllThreeEncodings_AgreeOnEveryCorpusDocument()
    {
        var schema = await NJsonSchema.JsonSchema.FromFileAsync(
            ContractsSchemaPaths.BundledWorkflowSchema);
        var disagreements = new List<string>();

        foreach (var factory in Corpus())
        {
            var testCase = factory();
            var (name, moniker, expected) = (testCase.Case, testCase.Moniker, testCase.Accepted);
            var json = WorkflowDocument(moniker);

            var schemaAccepts = schema.Validate(json).Count == 0;
            var dtoAccepts = Accepts(() => JsonSerializer.Deserialize<GeneratedWorkflow>(json));
            var readerAccepts = Accepts(() => WireWorkflowReader.Read(json));

            if (schemaAccepts != expected || dtoAccepts != expected || readerAccepts != expected)
            {
                disagreements.Add(
                    $"{name}: expected accepted={expected} but schema={schemaAccepts}, "
                    + $"generatedDto={dtoAccepts}, minimalReader={readerAccepts}");
            }
        }

        await Assert.That(disagreements).IsEmpty()
            .Because("the schema, the generated DTO, and the vendored reader must encode ONE "
                + "non-blank rule:\n" + string.Join("\n", disagreements));
    }

    private static bool Accepts(Func<object?> parse)
    {
        try
        {
            parse();
            return true;
        }
        catch (Exception exception) when (exception is JsonException or JsonParseException)
        {
            return false;
        }
    }

    /// <summary>
    /// Builds a minimal, otherwise-valid workflow document whose single step carries
    /// a compensation block with the supplied moniker. A <see langword="null"/>
    /// moniker omits the <c>compensationStepType</c> property entirely.
    /// </summary>
    private static string WorkflowDocument(string? moniker)
    {
        var compensation = moniker is null
            ? "{}"
            : $$"""{ "compensationStepType": {{JsonSerializer.Serialize(moniker)}} }""";

        return $$"""
            {
              "schemaVersion": "1.0",
              "name": "compensation-identity-corpus",
              "steps": [
                {
                  "kind": "skill",
                  "stepId": "s1",
                  "stepName": "FidProcessStep",
                  "isTerminal": true,
                  "stepType": "FidProcessStep",
                  "configuration": {
                    "compensation": {{compensation}}
                  }
                }
              ],
              "transitions": [],
              "branchPoints": [],
              "loops": [],
              "forkPoints": [],
              "failureHandlers": [],
              "approvalPoints": [],
              "entryStepId": "s1",
              "terminalStepId": "s1"
            }
            """;
    }
}

/// <summary>
/// One corpus document: a <c>compensationStepType</c> moniker (or its absence) and
/// the verdict every encoding of the non-blank rule must reach for it.
/// </summary>
/// <param name="Case">A readable name for the case.</param>
/// <param name="Moniker">The moniker, or <see langword="null"/> to omit the property.</param>
/// <param name="Accepted">Whether every encoding must accept the document.</param>
public sealed record CompensationIdentityCase(string Case, string? Moniker, bool Accepted)
{
    /// <inheritdoc />
    public override string ToString() => this.Case;
}
