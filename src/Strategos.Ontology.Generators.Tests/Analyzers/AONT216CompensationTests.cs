using Microsoft.CodeAnalysis;
using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public sealed class AONT216CompensationTests
{
    [Test]
    public async Task FluentCompensationWithDifferentFrame_IsRejectedAtCompileTime()
    {
        var diagnostics = await AnalyzeAsync("""
            obj.Action("publish")
                .Modifies(item => item.Status)
                .CompensatedBy("unpublish");
            obj.Action("unpublish").Modifies(item => item.Title);
            """);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostics[0].GetMessage()).Contains("unpublish");
    }

    [Test]
    public async Task FluentCompensationWithSameFrame_IsAccepted()
    {
        var diagnostics = await AnalyzeAsync("""
            obj.Action("publish")
                .Modifies(item => item.Status)
                .CompensatedBy("unpublish");
            obj.Action("unpublish").Modifies(item => item.Status);
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task FluentCompensationWithSameFrameButWrongGuarantee_IsRejected()
    {
        var diagnostics = await AnalyzeAsync("""
            obj.Action("publish")
                .Requires(item => item.Stage == 0)
                .Ensures(item => item.Stage == 1)
                .Modifies(item => item.Stage)
                .CompensatedBy("unpublish");
            obj.Action("unpublish")
                .Requires(item => item.Stage == 1)
                .Ensures(item => item.Stage == 2)
                .Modifies(item => item.Stage);
            """);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("effective guarantee");
        await Assert.That(diagnostics[0].GetMessage()).Contains("counterexample");
    }

    /// <summary>An inverse must accept exactly the forward action's effective post-state.</summary>
    [Test]
    public async Task FluentCompensationWithSameFrameButWrongRequirement_IsRejected()
    {
        var diagnostics = await AnalyzeAsync("""
            obj.Action("publish")
                .Requires(item => item.Stage == 0)
                .Ensures(item => item.Stage == 1)
                .Modifies(item => item.Stage)
                .CompensatedBy("unpublish");
            obj.Action("unpublish")
                .Requires(item => item.Stage == 2)
                .Ensures(item => item.Stage == 0)
                .Modifies(item => item.Stage);
            """);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("authored inverse requirement");
        await Assert.That(diagnostics[0].GetMessage()).Contains("counterexample");
    }

    [Test]
    public async Task FluentCompensationWithExactSemanticInverse_IsAccepted()
    {
        var diagnostics = await AnalyzeAsync("""
            obj.Action("publish")
                .Requires(item => item.Stage == 0)
                .Ensures(item => item.Stage == 1)
                .Modifies(item => item.Stage)
                .CompensatedBy("unpublish");
            obj.Action("unpublish")
                .Requires(item => item.Stage == 1)
                .Ensures(item => item.Stage == 0)
                .Modifies(item => item.Stage);
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task FluentCompensationWithEquivalentAuthorityAliases_IsAccepted()
    {
        var diagnostics = await AnalyzeAsync(
            """
            obj.Action("publish")
                .Modifies(item => item.Status)
                .RequiresAuthority("operator")
                .CompensatedBy("unpublish");
            obj.Action("unpublish")
                .Modifies(item => item.Status)
                .RequiresAuthority("operator-alias");
            """,
            """
            builder.AuthorityAxis("access", "read", "write");
            builder.Authority("operator").At("access", "write");
            builder.Authority("operator-alias").At("access", "write");
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Textually distinct authorities at different lattice coordinates are not equivalent.</summary>
    [Test]
    public async Task FluentCompensationWithDifferentSemanticAuthority_IsRejected()
    {
        var diagnostics = await AnalyzeAsync(
            """
            obj.Action("publish")
                .Modifies(item => item.Status)
                .RequiresAuthority("operator")
                .CompensatedBy("unpublish");
            obj.Action("unpublish")
                .Modifies(item => item.Status)
                .RequiresAuthority("reader");
            """,
            """
            builder.AuthorityAxis("access", "read", "write");
            builder.Authority("reader").At("access", "read");
            builder.Authority("operator").At("access", "write");
            """);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("required authority differs semantically");
    }

    /// <summary>Requirements outside the frame survive into each action's effective guarantee.</summary>
    [Test]
    public async Task FluentCompensationWithPreservedRequirement_UsesEffectiveGuarantees()
    {
        var diagnostics = await AnalyzeAsync("""
            obj.Action("publish")
                .Requires(item => item.Stage == 0 && item.Status == "ready")
                .Ensures(item => item.Stage == 1)
                .Modifies(item => item.Stage)
                .CompensatedBy("unpublish");
            obj.Action("unpublish")
                .Requires(item => item.Stage == 1 && item.Status == "ready")
                .Ensures(item => item.Stage == 0)
                .Modifies(item => item.Stage);
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task FluentCompensationWithUnreadableSelector_DoesNotEmitFalsePositive()
    {
        var diagnostics = await AnalyzeAsync("""
            System.Linq.Expressions.Expression<System.Func<Model, object>> selector = item => item.Status;
            obj.Action("publish")
                .Modifies(selector)
                .CompensatedBy("unpublish");
            obj.Action("unpublish").Modifies(selector);
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// A same-named overload cannot hide the semantic <c>DomainOntology.Define</c> override,
    /// regardless of its source position.
    /// </summary>
    [Test]
    public async Task DefineOverload_DoesNotHideInverseDisagreement_RegardlessOfSourceOrder()
    {
        const string actions = """
            obj.Action("publish")
                .Modifies(item => item.Status)
                .CompensatedBy("unpublish");
            obj.Action("unpublish").Modifies(item => item.Title);
            """;
        const string decoy = "private void Define(int _) { }";

        var before = await AnalyzeAsync(actions, membersBeforeDefine: decoy);
        var after = await AnalyzeAsync(actions, membersAfterDefine: decoy);

        await Assert.That(before).HasCount().EqualTo(1);
        await Assert.That(after).HasCount().EqualTo(1);
        await Assert.That(before[0].GetMessage()).IsEqualTo(after[0].GetMessage());
        await Assert.That(before[0].GetMessage()).Contains("unpublish");
    }

    /// <summary>
    /// AONT216 is a refutation, not a preference. Its descriptor must say so: an error that is
    /// on by default and carries <see cref="WellKnownDiagnosticTags.NotConfigurable"/>, matching
    /// its workflow-side counterparts AGWF044/AGWF045.
    /// </summary>
    [Test]
    public async Task Descriptor_IsANonConfigurableCompilationEndError()
    {
        var descriptor = OntologyDiagnostics.CompensationDisagreesWithInverse;

        await Assert.That(descriptor.Id).IsEqualTo(OntologyDiagnosticIds.CompensationDisagreesWithInverse);
        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(descriptor.IsEnabledByDefault).IsTrue();
        await Assert.That(descriptor.CustomTags).Contains(WellKnownDiagnosticTags.NotConfigurable)
            .Because("a consumer must not be able to ship a refuted inverse by writing NoWarn");
        await Assert.That(descriptor.CustomTags).Contains(WellKnownDiagnosticTags.CompilationEnd)
            .Because("the check needs the whole compilation's actions before it can decide");
    }

    /// <summary>
    /// Control for <see cref="RefutedInverse_UnderEveryConsumerSuppressionChannel_StillFailsTheBuild"/>:
    /// without suppression both the refutation and the configurable control diagnostic are reported.
    /// </summary>
    [Test]
    public async Task RefutedInverse_WithoutSuppression_ReportsRefutationAndControlDiagnostic()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsUnderSuppressionAsync(
            SuppressionProbeSource,
            noWarn: [],
            editorConfigNone: []);

        await Assert.That(diagnostics.Count(diagnostic =>
                diagnostic.Id == OntologyDiagnosticIds.CompensationDisagreesWithInverse))
            .IsEqualTo(1);
        await Assert.That(diagnostics.Count(diagnostic =>
                diagnostic.Id == OntologyDiagnosticIds.MissingKey))
            .IsEqualTo(1)
            .Because("the control must exist before its removal can prove the suppression channels ran");
    }

    /// <summary>
    /// Each suppression channel a packaged consumer owns — <c>&lt;NoWarn&gt;</c>,
    /// <c>.editorconfig</c> <c>severity = none</c>, <c>#pragma warning disable</c>, and all three
    /// together — removes the configurable control (AONT001) and leaves AONT216 standing. Every
    /// case asserts the control disappeared first, so a channel that silently failed to apply
    /// cannot make the surviving refutation look like proof. Only <c>RunAnalyzers=false</c>
    /// removes AONT216, because it unloads every analyzer; that case is outside any descriptor
    /// tag's reach and is caught at host start by ontology graph freeze.
    /// </summary>
    /// <param name="channel">The suppression channel under test.</param>
    [Test]
    [Arguments("nowarn")]
    [Arguments("editorconfig")]
    [Arguments("pragma")]
    [Arguments("all")]
    public async Task RefutedInverse_UnderConsumerSuppressionChannel_StillFailsTheBuild(string channel)
    {
        string[] suppressed =
        [
            OntologyDiagnosticIds.CompensationDisagreesWithInverse,
            OntologyDiagnosticIds.MissingKey,
        ];
        var usesNoWarn = channel is "nowarn" or "all";
        var usesEditorConfig = channel is "editorconfig" or "all";
        var usesPragma = channel is "pragma" or "all";

        var source = usesPragma
            ? $"""
              #pragma warning disable {OntologyDiagnosticIds.CompensationDisagreesWithInverse}
              #pragma warning disable {OntologyDiagnosticIds.MissingKey}
              {SuppressionProbeSource}
              """
            : SuppressionProbeSource;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsUnderSuppressionAsync(
            source,
            noWarn: usesNoWarn ? suppressed : [],
            editorConfigNone: usesEditorConfig ? suppressed : []);

        await Assert.That(diagnostics.Any(diagnostic =>
                diagnostic.Id == OntologyDiagnosticIds.MissingKey))
            .IsFalse()
            .Because($"the configurable control must vanish, proving the '{channel}' channel was applied");

        var refutations = diagnostics
            .Where(diagnostic => diagnostic.Id == OntologyDiagnosticIds.CompensationDisagreesWithInverse)
            .ToArray();
        await Assert.That(refutations).HasCount().EqualTo(1)
            .Because($"NotConfigurable must carry AONT216 past the '{channel}' channel");
        await Assert.That(refutations[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(refutations[0].IsSuppressed).IsFalse();
    }

    /// <summary>
    /// A domain whose authored inverse is refuted (different frame), plus a second object type
    /// with no <c>Key()</c> so the compilation also carries the configurable AONT001 control.
    /// </summary>
    private const string SuppressionProbeSource = """
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;

        public sealed class SuppressionModel
        {
            public string Id { get; set; } = "";
            public string Status { get; set; } = "";
            public string Title { get; set; } = "";
        }

        public sealed class KeylessModel
        {
            public string Name { get; set; } = "";
        }

        public sealed class SuppressionDomain : DomainOntology
        {
            public override string DomainName => "suppression";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.Object<SuppressionModel>(obj =>
                {
                    obj.Key(item => item.Id);
                    obj.Property(item => item.Status);
                    obj.Property(item => item.Title);

                    obj.Action("publish")
                        .Modifies(item => item.Status)
                        .CompensatedBy("unpublish");
                    obj.Action("unpublish").Modifies(item => item.Title);
                });

                builder.Object<KeylessModel>(obj =>
                {
                    obj.Property(item => item.Name);
                });
            }
        }
        """;

    private static Task<System.Collections.Immutable.ImmutableArray<Diagnostic>> AnalyzeAsync(
        string actions,
        string domainDeclarations = "",
        string membersBeforeDefine = "",
        string membersAfterDefine = "") =>
        AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            $$"""
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;

            public sealed class Model
            {
                public string Id { get; set; } = "";
                public string Status { get; set; } = "";
                public string Title { get; set; } = "";
                public int Stage { get; set; }
            }

            public sealed class TestDomain : DomainOntology
            {
                public override string DomainName => "test";
                {{membersBeforeDefine}}
                protected override void Define(IOntologyBuilder builder)
                {
                    {{domainDeclarations}}
                    builder.Object<Model>(obj =>
                    {
                        obj.Key(item => item.Id);
                        obj.Property(item => item.Status);
                        obj.Property(item => item.Title);
                        obj.Property(item => item.Stage);
                        {{actions}}
                    });
                }
                {{membersAfterDefine}}
            }
            """,
            OntologyDiagnosticIds.CompensationDisagreesWithInverse);
}
