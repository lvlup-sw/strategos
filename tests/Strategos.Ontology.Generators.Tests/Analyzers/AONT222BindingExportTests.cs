using Microsoft.CodeAnalysis;

using Strategos.Ontology.Generators.Analyzers;
using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

/// <summary>
/// AONT222 (#204): an assembly that declares a workflow binding and exports no proof
/// catalog has declared an obligation no compilation can discharge.
/// </summary>
/// <remarks>
/// <para>
/// This is the layout the maintainer's own example names — a project referencing the
/// ontology analyzer and not the workflow generator. It compiled silent and dangling:
/// the binding was declared, the declaring compilation could not see the workflow, no
/// referencing compilation could see the contract, and no generator ran to say so.
/// </para>
/// <para>
/// The check has to live in the analyzer for exactly that reason. There is no
/// generator in that build to report it.
/// </para>
/// </remarks>
/// <summary>The two channels a consumer can use to silence a configurable diagnostic.</summary>
public enum SuppressionChannel
{
    /// <summary>The <c>&lt;NoWarn&gt;</c> project property.</summary>
    NoWarn,

    /// <summary>An <c>.editorconfig</c> <c>severity = none</c> entry.</summary>
    EditorConfig,
}

public sealed class AONT222BindingExportTests
{
    /// <summary>A binding with no exported catalog is reported.</summary>
    [Test]
    public async Task BindingWithoutAnExportedCatalog_ReportsAont222()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source(withCatalogAttribute: false),
            OntologyDiagnosticIds.BindingNotExported);

        await Assert.That(diagnostics).HasCount().EqualTo(1)
            .Because("an unexported binding is an obligation that reaches nobody.");
        await Assert.That(diagnostics[0].GetMessage()).Contains("fulfill-order");
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    /// <summary>The same binding is silent once the assembly exports a catalog.</summary>
    /// <remarks>
    /// The observable fact is the generated attribute. The workflow generator emits it
    /// whenever the assembly declares an action contract, so its presence means the
    /// generator ran here and the contract travelled.
    /// </remarks>
    [Test]
    public async Task BindingWithAnExportedCatalog_IsSilent()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source(withCatalogAttribute: true),
            OntologyDiagnosticIds.BindingNotExported);

        await Assert.That(diagnostics).IsEmpty()
            .Because("an exported binding is deferred to whoever lowers the workflow.");
    }

    /// <summary>An ontology with no binding at all has nothing to export.</summary>
    [Test]
    public async Task OntologyWithNoBinding_IsSilent()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source(withCatalogAttribute: false, binding: string.Empty),
            OntologyDiagnosticIds.BindingNotExported);

        await Assert.That(diagnostics).IsEmpty()
            .Because("the diagnostic is about an undischargeable obligation, not about "
                + "whether an assembly exports a catalog.");
    }

    /// <summary>The diagnostic survives each consumer suppression channel.</summary>
    /// <remarks>
    /// One execution per channel, each carrying its own control. Driving both channels
    /// at once and controlling only one of them would let a channel that stopped working
    /// hide behind the channel that still does: the control would vanish, the assertion
    /// would pass, and nothing would have tested the dead channel.
    /// </remarks>
    /// <param name="channel">Which channel this execution drives.</param>
    [Test]
    [Arguments(SuppressionChannel.NoWarn)]
    [Arguments(SuppressionChannel.EditorConfig)]
    public async Task Aont222_SurvivesSuppressionChannel(SuppressionChannel channel)
    {
        var source = Source(withCatalogAttribute: false, includeConfigurableControl: true);

        var control = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.MissingKey);
        await Assert.That(control).IsNotEmpty()
            .Because("the control must fire before it can be shown to be silenceable.");

        string[] silenced = [OntologyDiagnosticIds.BindingNotExported, OntologyDiagnosticIds.MissingKey];
        var suppressed = await AnalyzerTestHelper.GetDiagnosticsUnderSuppressionAsync(
            source,
            noWarn: channel == SuppressionChannel.NoWarn ? silenced : [],
            editorConfigNone: channel == SuppressionChannel.EditorConfig ? silenced : []);

        await Assert.That(suppressed.Any(d => d.Id == OntologyDiagnosticIds.MissingKey)).IsFalse()
            .Because($"the configurable control must be removed, proving the {channel} channel applied.");
        await Assert.That(suppressed.Count(d => d.Id == OntologyDiagnosticIds.BindingNotExported))
            .IsEqualTo(1)
            .Because("a binding nobody can prove is not a thing a consumer may silence.");
    }

    /// <summary>
    /// The analyzer and the generator agree on the attribute name they do not share a
    /// reference to.
    /// </summary>
    /// <remarks>
    /// The ontology analyzer and the workflow generator are separate assemblies that
    /// reference neither each other nor a common one, so this coupling is a string on
    /// both sides. A rename on one side would make AONT222 fire on every exporting
    /// assembly — loudly — or never fire at all. This pins the pair.
    /// </remarks>
    [Test]
    public async Task AttributeName_MatchesTheNameTheGeneratorEmits()
    {
        await Assert.That(BindingExportAnalyzer.ProofCatalogAttributeMetadataName)
            .IsEqualTo("Strategos.Generated.StrategosProofCatalogAttribute")
            .Because("the generator emits this exact metadata name; see "
                + "Strategos.Generators/Proof/ProofCatalogExport.cs, whose own test asserts "
                + "the same literal from the other side.");
    }

    private static string Source(
        bool withCatalogAttribute,
        string binding = ".BoundToWorkflow(\"fulfill-order\")",
        bool includeConfigurableControl = false)
    {
        // The control is a deliberately key-less object type: AONT001 is an Error from
        // the same analyzer that carries no NotConfigurable tag. It has to come from
        // this analyzer, and it has to be genuinely silenceable, or "AONT222 survived"
        // would only mean the suppression options never reached the analyzer at all.
        var control = includeConfigurableControl
            ? """

                    builder.Object<Keyless>(item =>
                    {
                        item.Property(value => value.Name);
                    });
            """
            : string.Empty;

        var attribute = withCatalogAttribute
            ? """
              [assembly: global::Strategos.Generated.StrategosProofCatalogAttribute("{}")]

              namespace Strategos.Generated
              {
                  [global::System.AttributeUsage(global::System.AttributeTargets.Assembly)]
                  internal sealed class StrategosProofCatalogAttribute : global::System.Attribute
                  {
                      public StrategosProofCatalogAttribute(string catalog) { }
                  }
              }

              """
            : string.Empty;

        return attribute + $$"""
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;

            namespace ExportProbe;

            public sealed class Order
            {
                public string Id { get; set; } = string.Empty;
                public int Stage { get; set; }
            }

            public sealed class Keyless
            {
                public string Name { get; set; } = string.Empty;
            }

            public sealed class OrdersOntology : DomainOntology
            {
                public override string DomainName => "orders";

                protected override void Define(IOntologyBuilder builder)
                {
                    builder.Object<Order>(obj =>
                    {
                        obj.Key(order => order.Id);
                        obj.Property(order => order.Stage);

                        obj.Action("fulfill")
                            .Requires(order => order.Stage == 0)
                            .Ensures(order => order.Stage == 1)
                            .Modifies(order => order.Stage)
                            {{binding}};
                    });{{control}}
                }
            }
            """;
    }
}
