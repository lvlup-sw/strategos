using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public sealed class ActionCompositionContractShapeAnalyzerTests
{
    [Test]
    public async Task PostconditionOutsideFrameReportsAont221()
    {
        var diagnostics = await InvalidDiagnosticsAsync("""
            var action = new ActionDescriptor(subject, "change", "change")
            {
                Postconditions =
                [
                    new ActionPostcondition
                    {
                        Kind = PostconditionKind.ModifiesProperty,
                        PropertyName = "status",
                    },
                ],
            };
            ActionCalculus.Sequential(lattice, action);
            """);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("outside the declared frame");
    }

    [Test]
    public async Task EveryFrameAndPostconditionKindIsResolvedStatically()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(Source("""
            var action = new ActionDescriptor(subject, "change", "change")
            {
                TouchedResources =
                [
                    ActionResource.Property("status"),
                    ActionResource.Link("invoice"),
                    ActionResource.Event("Changed"),
                    ActionResource.External("ledger"),
                ],
                Postconditions =
                [
                    new ActionPostcondition
                    {
                        Kind = PostconditionKind.ModifiesProperty,
                        PropertyName = "status",
                    },
                    new ActionPostcondition
                    {
                        Kind = PostconditionKind.CreatesLink,
                        LinkName = "invoice",
                    },
                    new ActionPostcondition
                    {
                        Kind = PostconditionKind.EmitsEvent,
                        EventTypeName = "Changed",
                    },
                ],
            };
            ActionCalculus.Sequential(lattice, action);
            """));

        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id is
                OntologyDiagnosticIds.InvalidActionContract or
                OntologyDiagnosticIds.DynamicActionSequence))
            .IsEmpty();
    }

    [Test]
    public async Task MissingPostconditionNameAndNullEntriesReportAont221()
    {
        var diagnostics = await InvalidDiagnosticsAsync("""
            var missingName = new ActionDescriptor(subject, "missing", "missing")
            {
                TouchedResources = [ActionResource.Event("Changed")],
                Postconditions =
                [
                    new ActionPostcondition { Kind = PostconditionKind.EmitsEvent },
                ],
            };
            var nullEntries = new ActionDescriptor(subject, "nulls", "nulls")
            {
                Preconditions = [null],
                Ensures = [null],
                TouchedResources = [null],
                Postconditions = [null],
            };
            ActionCalculus.Sequential(lattice, missingName, nullEntries);
            """);

        await Assert.That(diagnostics).HasCount().EqualTo(2);
        await Assert.That(diagnostics[0].GetMessage()).Contains("event type");
        await Assert.That(diagnostics[1].GetMessage()).Contains("null entry");
    }

    [Test]
    public async Task CreatesLinkDerivesAStaticallyProvableFact()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(Source("""
            var upstream = new ActionDescriptor(subject, "attach", "attach")
            {
                TouchedResources = [ActionResource.Link("invoice")],
                Postconditions =
                [
                    new ActionPostcondition
                    {
                        Kind = PostconditionKind.CreatesLink,
                        LinkName = "invoice",
                    },
                ],
            };
            var downstream = new ActionDescriptor(subject, "send", "send")
            {
                Preconditions =
                [
                    new ActionPrecondition(
                        ActionPredicate.LinkExists("invoice"),
                        "invoice exists"),
                ],
            };
            ActionCalculus.Sequential(lattice, upstream, downstream);
            """));

        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id is
                OntologyDiagnosticIds.IllegalActionSeam or
                OntologyDiagnosticIds.InvalidActionContract or
                OntologyDiagnosticIds.DynamicActionSequence))
            .IsEmpty();
    }

    [Test]
    public async Task RelationFrameInterferenceUsesOnlyTheSubjectLocalHop()
    {
        var invalid = await InvalidDiagnosticsAsync("""
            var relation = ActionPredicate.RelationHolds("owner", ["space"]);
            var action = new ActionDescriptor(subject, "wrong-link", "wrong-link")
            {
                TouchedResources = [ActionResource.Link("owner")],
                Ensures = [new ActionGuarantee(relation)],
            };
            ActionCalculus.Sequential(lattice, action);
            """);
        var valid = await InvalidDiagnosticsAsync("""
            var relation = ActionPredicate.RelationHolds("owner", ["space"]);
            var action = new ActionDescriptor(subject, "first-hop", "first-hop")
            {
                TouchedResources = [ActionResource.Link("space")],
                Ensures = [new ActionGuarantee(relation)],
            };
            ActionCalculus.Sequential(lattice, action);
            """);

        await Assert.That(invalid).HasCount().EqualTo(1);
        await Assert.That(invalid[0].GetMessage()).Contains("untouched state");
        await Assert.That(valid).IsEmpty();
    }

    private static async Task<IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic>> InvalidDiagnosticsAsync(
        string body) => await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
        Source(body),
        OntologyDiagnosticIds.InvalidActionContract);

    private static string Source(string body) => $$"""
        using Strategos.Ontology.Descriptors;

        public static class Consumer
        {
            public static void Compose(AuthorityLattice lattice)
            {
                var subject = new ActionSubject("orders", "Order");
                {{body}}
            }
        }
        """;
}
