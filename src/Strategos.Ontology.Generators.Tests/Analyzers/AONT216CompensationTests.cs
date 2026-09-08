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
