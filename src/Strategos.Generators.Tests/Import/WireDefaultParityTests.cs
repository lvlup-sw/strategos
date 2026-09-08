// -----------------------------------------------------------------------
// <copyright file="WireDefaultParityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;
using System.Threading;

using Strategos.Definitions;
using Strategos.Generators.Import;
using Strategos.Generators.Models;

using BuilderCompensation = Strategos.Definitions.CompensationConfiguration;

namespace Strategos.Generators.Tests.Import;

// =============================================================================
// Guard G1 — the wire schema is the single source for the compensation defaults
// and for the typed-inverse rule.
//
// Before this guard, `requiredOnFailure = true` was hand-typed in three places
// (the builder's CompensationConfiguration, the generator's CompensationModel,
// and WireToModelBridge's `?? true`) and stated nowhere on the wire, so a
// cross-product consumer reading the schema could not see it. The rule "a typed
// inverseAction requires requiredOnFailure = true" lived only in the C#
// analyzer (AGWF044).
//
// These tests read the value OUT of the emitted schema and require every C#
// encoding to agree with it. Changing one encoding without the schema — or the
// schema without the encodings — fails here.
// =============================================================================

/// <summary>
/// Binds the C# compensation defaults to the <c>default</c> the emitted JSON
/// Schema declares, and binds the schema's typed-inverse conditional to the
/// analyzer rule it encodes.
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class WireDefaultParityTests
{
    /// <summary>Gets the <c>requiredOnFailure</c> default the wire schema declares.</summary>
    private static bool SchemaRequiredOnFailureDefault
    {
        get
        {
            var schema = ContractsSchemaPaths.LoadModel("CompensationConfiguration");
            var property = schema.GetProperty("properties").GetProperty("requiredOnFailure");
            if (!property.TryGetProperty("default", out var value))
            {
                throw new InvalidOperationException(
                    "CompensationConfiguration.requiredOnFailure must declare a 'default' in the "
                    + "emitted schema — the wire contract is where the default is stated.");
            }

            return value.GetBoolean();
        }
    }

    /// <summary>
    /// The wire schema must actually declare the default. Without this the rest of
    /// the suite would vacuously pass against a missing keyword.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Schema_DeclaresRequiredOnFailureDefault()
    {
        var schema = ContractsSchemaPaths.LoadModel("CompensationConfiguration");
        var property = schema.GetProperty("properties").GetProperty("requiredOnFailure");

        await Assert.That(property.TryGetProperty("default", out var value)).IsTrue()
            .Because("the wire contract states the value applied when the property is omitted.");
        await Assert.That(value.ValueKind).IsEqualTo(JsonValueKind.True);
    }

    /// <summary>
    /// (a) The builder's configuration record — both the object-initializer form and
    /// each <c>Create</c> overload — applies the schema's default.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BuilderConfiguration_AppliesSchemaDefault()
    {
        var expected = SchemaRequiredOnFailureDefault;

        var initialized = new BuilderCompensation
        {
            CompensationStepType = typeof(FidCompensateStep),
        };
        await Assert.That(initialized.RequiredOnFailure).IsEqualTo(expected)
            .Because("Strategos.Definitions.CompensationConfiguration must apply the schema default.");

        var created = BuilderCompensation.Create(typeof(FidCompensateStep));
        await Assert.That(created.RequiredOnFailure).IsEqualTo(expected)
            .Because("Create(Type) must apply the schema default.");

        var typed = BuilderCompensation.Create(
            typeof(FidCompensateStep),
            new WorkflowActionReference("orders", "Order", "undo-process"));
        await Assert.That(typed.RequiredOnFailure).IsEqualTo(expected)
            .Because("Create(Type, inverseAction) must apply the schema default — a typed "
                + "inverse is a mandatory rollback program (AGWF044).");
    }

    /// <summary>
    /// (b) The generator's internal lowering model applies the same default.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CompensationModel_AppliesSchemaDefault()
    {
        var model = new CompensationModel("Ns.FidCompensateStep");

        await Assert.That(model.RequiredOnFailure).IsEqualTo(SchemaRequiredOnFailureDefault)
            .Because("CompensationModel's optional parameter must not drift from the wire default.");
    }

    /// <summary>
    /// (c) An imported document that OMITS <c>requiredOnFailure</c> must bridge to
    /// the schema default. This is the encoding a consumer actually depends on: the
    /// schema promises a value for the omitted case, and the importer must supply it.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImportedDocument_WithRequiredOnFailureOmitted_BridgesToSchemaDefault()
    {
        var model = BridgeCompensationDocument(
            "\"compensationStepType\": \"FidCompensateStep\"");

        await Assert.That(model.RequiredOnFailure).IsEqualTo(SchemaRequiredOnFailureDefault)
            .Because("WireToModelBridge must fill the omitted wire slot with the schema default.");
    }

    /// <summary>
    /// Negative control: an explicit wire value is carried through unchanged, so the
    /// default is a default and not an override.
    /// </summary>
    /// <param name="wireValue">The explicit wire value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ImportedDocument_WithExplicitRequiredOnFailure_CarriesTheWireValue(bool wireValue)
    {
        var model = BridgeCompensationDocument(
            "\"compensationStepType\": \"FidCompensateStep\", "
            + $"\"requiredOnFailure\": {(wireValue ? "true" : "false")}");

        await Assert.That(model.RequiredOnFailure).IsEqualTo(wireValue)
            .Because("an explicit wire value must win over the default.");
    }

    /// <summary>
    /// The emitted schema carries the typed-inverse rule as a conditional, in BOTH
    /// the per-model document and the bundled document, with exactly the predicate
    /// the analyzer's AGWF044 guard applies: when <c>inverseAction</c> is present,
    /// <c>requiredOnFailure</c> must be <c>true</c>.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Schema_CarriesTheTypedInverseConditional_InBothRepresentations()
    {
        var perModel = ContractsSchemaPaths.LoadModel("CompensationConfiguration");
        var bundled = ContractsSchemaPaths.LoadBundle()
            .GetProperty("definitions")
            .GetProperty("CompensationConfiguration");

        foreach (var schema in new[] { perModel, bundled })
        {
            await Assert.That(schema.TryGetProperty("if", out var condition)).IsTrue()
                .Because("the typed-inverse rule must be stated on the wire, not only in AGWF044.");
            var required = condition.GetProperty("required")
                .EnumerateArray()
                .Select(element => element.GetString()!)
                .ToArray();
            await Assert.That(required).IsEquivalentTo(new[] { "inverseAction" })
                .Because("the conditional fires exactly when a typed inverse is present.");

            await Assert.That(schema.TryGetProperty("then", out var consequent)).IsTrue();
            var pinned = consequent
                .GetProperty("properties")
                .GetProperty("requiredOnFailure")
                .GetProperty("const");
            await Assert.That(pinned.ValueKind).IsEqualTo(JsonValueKind.True)
                .Because("a typed inverse is a mandatory rollback program (AGWF044).");
        }
    }

    /// <summary>
    /// The two schema representations of the conditional must be byte-identical:
    /// the bundler inlines the per-model document, so a divergence means a consumer
    /// reading the bundle sees a different rule from one reading the per-model file.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Schema_ConditionalIsIdenticalAcrossBothRepresentations()
    {
        var perModel = ContractsSchemaPaths.LoadModel("CompensationConfiguration");
        var bundled = ContractsSchemaPaths.LoadBundle()
            .GetProperty("definitions")
            .GetProperty("CompensationConfiguration");

        foreach (var keyword in new[] { "if", "then" })
        {
            var equal = JsonElement.DeepEquals(
                perModel.GetProperty(keyword),
                bundled.GetProperty(keyword));
            await Assert.That(equal).IsTrue()
                .Because($"the bundled '{keyword}' must be the inlined per-model '{keyword}', but "
                    + $"per-model was {perModel.GetProperty(keyword).GetRawText()} and bundled was "
                    + $"{bundled.GetProperty(keyword).GetRawText()}.");
        }
    }

    /// <summary>
    /// The document the schema conditional rejects is exactly the document the
    /// analyzer rejects. The bridge lands it in the state AGWF044 keys on — a
    /// resolved inverse identity with <c>RequiredOnFailure</c> false — which
    /// <c>ImportedWorkflowBindingProofTests.ImportedTypedCompensation_WithRequiredOnFailureFalse_ReportsAgwf044</c>
    /// drives end-to-end through the analyzer.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImportedTypedInverse_WithRequiredOnFailureFalse_LandsInTheAgwf044State()
    {
        var model = BridgeCompensationDocument(
            """
            "compensationStepType": "FidCompensateStep",
            "requiredOnFailure": false,
            "inverseAction": {
              "domainName": "orders",
              "objectTypeName": "Order",
              "actionName": "undo-process"
            }
            """);

        await Assert.That(model.RequiredOnFailure).IsFalse();
        await Assert.That(model.InverseActionResolution)
            .IsEqualTo(WorkflowActionReferenceResolution.Resolved)
            .Because("AGWF044 fires on a resolved (or dynamic) inverse with RequiredOnFailure false — "
                + "the same predicate the schema's if/then conditional states.");
    }

    /// <summary>
    /// Bridges a synthetic workflow document carrying the supplied compensation
    /// properties and returns the lowered compensation model.
    /// </summary>
    private static CompensationModel BridgeCompensationDocument(string compensationProperties)
    {
        var json = $$"""
            {
              "schemaVersion": "1.0",
              "name": "wire-default-parity",
              "steps": [
                {
                  "kind": "skill",
                  "stepId": "s1",
                  "stepName": "FidProcessStep",
                  "isTerminal": true,
                  "stepType": "FidProcessStep",
                  "configuration": {
                    "compensation": {
                      {{compensationProperties}}
                    }
                  }
                }
              ],
              "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
              "failureHandlers": [], "approvalPoints": [],
              "entryStepId": "s1", "terminalStepId": "s1"
            }
            """;

        var dto = WireWorkflowReader.Read(json);
        var compilation = CSharpCompilation.Create(
            assemblyName: "WireDefaultParityBridgeAssembly",
            syntaxTrees: [],
            references: BridgeReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var result = WireToModelBridge.Bridge(
            dto,
            compilation,
            "wire-default-parity.workflow.json",
            CancellationToken.None);

        if (result.Model is null)
        {
            throw new InvalidOperationException(
                "expected the parity document to bridge; diagnostics: "
                + string.Join(", ", result.Diagnostics.Select(d => d.Id)));
        }

        var step = result.Model.Steps!.Single(s => s.StepName == "FidProcessStep");
        return step.Compensation
            ?? throw new InvalidOperationException(
                "expected the bridged step to carry a compensation model.");
    }

    private static List<MetadataReference> BridgeReferences()
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

        // The running test assembly carries the public Fid* step types, so the bridge
        // resolves the wire monikers against real IWorkflowStep<FidState> types.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
                catch (Exception exception) when (exception is IOException or NotSupportedException or ArgumentException)
                {
                    // Ignore assemblies that cannot be loaded as references.
                }
            }
        }

        return references;
    }
}
