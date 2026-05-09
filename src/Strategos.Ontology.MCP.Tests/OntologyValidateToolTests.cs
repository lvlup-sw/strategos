using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.MCP.Tests;

public class OntologyValidateToolTests
{
    private static OntologyNodeRef Node(string typeName = "Order", string key = "ord-1") =>
        new("trading", typeName, key);

    private static ProposedAction Action(string actionName = "Ship", OntologyNodeRef? subject = null) =>
        new(actionName, subject ?? Node(), null);

    private static DesignIntent Intent(
        IReadOnlyList<OntologyNodeRef>? affected = null,
        IReadOnlyList<ProposedAction>? actions = null) =>
        new(
            affected ?? new List<OntologyNodeRef> { Node() },
            actions ?? new List<ProposedAction> { Action() },
            null);

    private static BlastRadius EmptyBlastRadius(BlastRadiusScope scope = BlastRadiusScope.Local) =>
        new(
            DirectlyAffected: new List<OntologyNodeRef>(),
            TransitivelyAffected: new List<OntologyNodeRef>(),
            CrossDomainHops: new List<CrossDomainHop>(),
            Scope: scope);

    private static ActionDescriptor MakeDescriptor(string name) => new(name, $"{name} description");

    /// <summary>
    /// Returns a constraint report that recognizes <paramref name="name"/> as a
    /// known action with no failing preconditions. Use when a test wants to
    /// assert verdict.Passed=true on an intent whose action would otherwise
    /// be flagged as "unknown action" by OntologyValidateTool.
    /// </summary>
    private static ActionConstraintReport KnownActionReport(string name = "Ship") =>
        new(MakeDescriptor(name), IsAvailable: true, Constraints: Array.Empty<ConstraintEvaluation>());

    private static ConstraintEvaluation MakeEvaluation(
        bool isSatisfied,
        ConstraintStrength strength,
        string? failureReason = null)
    {
        var precondition = new ActionPrecondition
        {
            Expression = "expr",
            Description = "desc",
            Kind = PreconditionKind.PropertyPredicate,
            Strength = strength,
        };
        return new ConstraintEvaluation(precondition, isSatisfied, strength, failureReason, null);
    }

    private static IOntologyQuery MakeQuery(
        IReadOnlyList<ActionConstraintReport>? constraintReports = null,
        IReadOnlyList<PatternViolation>? patternViolations = null,
        BlastRadius? blastRadius = null)
    {
        var query = Substitute.For<IOntologyQuery>();
        query
            .GetActionConstraintReport(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>?>())
            .Returns(constraintReports ?? Array.Empty<ActionConstraintReport>());
        // OntologyValidateTool calls the domain-qualified overload — stub
        // both paths so test doubles return the same constraint reports
        // regardless of which overload the production code chooses.
        query
            .GetActionConstraintReport(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>?>())
            .Returns(constraintReports ?? Array.Empty<ActionConstraintReport>());
        query
            .EstimateBlastRadius(Arg.Any<IReadOnlyList<OntologyNodeRef>>(), Arg.Any<BlastRadiusOptions?>())
            .Returns(blastRadius ?? EmptyBlastRadius());
        query
            .DetectPatternViolations(Arg.Any<IReadOnlyList<OntologyNodeRef>>(), Arg.Any<DesignIntent>())
            .Returns(patternViolations ?? Array.Empty<PatternViolation>());
        return query;
    }

    [Test]
    public async Task Validate_NoViolations_ReturnsPassedTrue()
    {
        // Stub the default fixture action ("Ship") as known-with-no-failures
        // so OntologyValidateTool doesn't add an unknown-action hard violation.
        var query = MakeQuery(constraintReports: new[] { KnownActionReport() });
        var tool = new OntologyValidateTool(query);

        var verdict = tool.Validate(Intent());

        await Assert.That(verdict.Passed).IsTrue();
        await Assert.That(verdict.HardViolations).IsEmpty();
        await Assert.That(verdict.SoftWarnings).IsEmpty();
        await Assert.That(verdict.PatternViolations).IsEmpty();
        await Assert.That(verdict.BlastRadius).IsNotNull();
    }

    [Test]
    public async Task Validate_HardViolations_ReturnsPassedFalse()
    {
        var descriptor = MakeDescriptor("Ship");
        var hardEval = MakeEvaluation(isSatisfied: false, ConstraintStrength.Hard, "boom");
        var report = new ActionConstraintReport(descriptor, IsAvailable: false, Constraints: new[] { hardEval });
        var query = MakeQuery(constraintReports: new[] { report });
        var tool = new OntologyValidateTool(query);

        var verdict = tool.Validate(Intent(actions: new List<ProposedAction> { Action("Ship") }));

        await Assert.That(verdict.Passed).IsFalse();
        await Assert.That(verdict.HardViolations).HasCount().EqualTo(1);
        await Assert.That(verdict.HardViolations[0]).IsEqualTo(hardEval);
    }

    [Test]
    public async Task Validate_OnlySoftWarnings_ReturnsPassedTrue()
    {
        var descriptor = MakeDescriptor("Audit");
        var softEval = MakeEvaluation(isSatisfied: false, ConstraintStrength.Soft);
        var report = new ActionConstraintReport(descriptor, IsAvailable: true, Constraints: new[] { softEval });
        var query = MakeQuery(constraintReports: new[] { report });
        var tool = new OntologyValidateTool(query);

        var verdict = tool.Validate(Intent(actions: new List<ProposedAction> { Action("Audit") }));

        await Assert.That(verdict.Passed).IsTrue();
        await Assert.That(verdict.HardViolations).IsEmpty();
        await Assert.That(verdict.SoftWarnings).HasCount().EqualTo(1);
        await Assert.That(verdict.SoftWarnings[0]).IsEqualTo(softEval);
    }

    [Test]
    public async Task Validate_PatternViolationAtErrorSeverity_ReturnsPassedFalse()
    {
        var error = new PatternViolation(
            "Orphan", "no incoming", Node(), ViolationSeverity.Error);
        var query = MakeQuery(patternViolations: new[] { error });
        var tool = new OntologyValidateTool(query);

        var verdict = tool.Validate(Intent());

        await Assert.That(verdict.Passed).IsFalse();
        await Assert.That(verdict.PatternViolations).HasCount().EqualTo(1);
        await Assert.That(verdict.PatternViolations[0].Severity).IsEqualTo(ViolationSeverity.Error);
    }

    [Test]
    public async Task Validate_PatternViolationOnlyAtWarningSeverity_PassedTrue()
    {
        var warning = new PatternViolation(
            "Style", "advisory", Node(), ViolationSeverity.Warning);
        var query = MakeQuery(
            constraintReports: new[] { KnownActionReport() },
            patternViolations: new[] { warning });
        var tool = new OntologyValidateTool(query);

        var verdict = tool.Validate(Intent());

        await Assert.That(verdict.Passed).IsTrue();
        await Assert.That(verdict.PatternViolations).HasCount().EqualTo(1);
        await Assert.That(verdict.PatternViolations[0].Severity).IsEqualTo(ViolationSeverity.Warning);
    }

    [Test]
    public async Task Validate_EmptyAffectedNodes_ReturnsTrivialVerdict()
    {
        var query = MakeQuery();
        var tool = new OntologyValidateTool(query);
        var intent = new DesignIntent(
            new List<OntologyNodeRef>(),
            new List<ProposedAction>(),
            null);

        var verdict = tool.Validate(intent);

        await Assert.That(verdict.Passed).IsTrue();
        await Assert.That(verdict.HardViolations).IsEmpty();
        await Assert.That(verdict.SoftWarnings).IsEmpty();
        await Assert.That(verdict.PatternViolations).IsEmpty();
        await Assert.That(verdict.BlastRadius).IsNotNull();
        await Assert.That(verdict.BlastRadius.DirectlyAffected).IsEmpty();
        await Assert.That(verdict.BlastRadius.TransitivelyAffected).IsEmpty();
        await Assert.That(verdict.BlastRadius.CrossDomainHops).IsEmpty();
        await Assert.That(verdict.BlastRadius.Scope).IsEqualTo(BlastRadiusScope.Local);
    }

    [Test]
    public async Task Validate_NoCoverageProviderRegistered_CoverageIsNull()
    {
        var query = MakeQuery();
        var tool = new OntologyValidateTool(query);

        var verdict = tool.Validate(Intent());

        await Assert.That(verdict.Coverage).IsNull();
    }

    [Test]
    public async Task Validate_CoverageProviderRegistered_CoveragePopulated()
    {
        var query = MakeQuery();
        var coverage = new CoverageReport(3, 5, new List<OntologyNodeRef>());
        var coverageProvider = Substitute.For<IOntologyCoverageProvider>();
        coverageProvider.GetCoverage(Arg.Any<DesignIntent>()).Returns(coverage);
        var tool = new OntologyValidateTool(query, coverageProvider);

        var verdict = tool.Validate(Intent());

        await Assert.That(verdict.Coverage).IsEqualTo(coverage);
    }

    [Test]
    public async Task Constructor_NullQuery_Throws()
    {
        await Assert.That(() => new OntologyValidateTool(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Validate_NullIntent_Throws()
    {
        var query = MakeQuery();
        var tool = new OntologyValidateTool(query);

        await Assert.That(() => tool.Validate(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Validate_UnknownAction_SurfacesAsHardViolation()
    {
        // Regression: when ProposedAction.ActionName is misspelled or not
        // registered on the subject type, the tool must surface a hard
        // violation instead of silently skipping (which previously let an
        // invalid intent produce Passed=true).
        var query = MakeQuery(); // empty reports → action is "unknown"
        var tool = new OntologyValidateTool(query);

        var verdict = tool.Validate(Intent(actions: new List<ProposedAction>
        {
            new("not_a_real_action", Node(), null),
        }));

        await Assert.That(verdict.Passed).IsFalse();
        await Assert.That(verdict.HardViolations).HasCount().EqualTo(1);
        await Assert.That(verdict.HardViolations[0].FailureReason)
            .Contains("not_a_real_action");
        await Assert.That(verdict.HardViolations[0].FailureReason)
            .Contains("not registered");
    }

    [Test]
    public async Task Validate_MergesActionArgumentsIntoConstraintLookup()
    {
        // Regression: ProposedAction.Arguments must flow into the per-action
        // constraint evaluation so preconditions that reference action
        // arguments (e.g. "quantity > 0") are evaluated correctly.
        IReadOnlyDictionary<string, object?>? captured = null;
        var query = Substitute.For<IOntologyQuery>();
        query
            .GetActionConstraintReport(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>?>())
            .Returns(call =>
            {
                captured = call.ArgAt<IReadOnlyDictionary<string, object?>?>(1);
                return Array.Empty<ActionConstraintReport>();
            });
        // OntologyValidateTool now calls the domain-qualified overload first;
        // capture from there too so this regression test works whichever
        // overload OntologyValidateTool decides to invoke.
        query
            .GetActionConstraintReport(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>?>())
            .Returns(call =>
            {
                captured = call.ArgAt<IReadOnlyDictionary<string, object?>?>(2);
                // Return a non-empty report keyed to the action name so the
                // tool doesn't surface an unknown-action hard violation that
                // would interfere with the merge assertion.
                var precond = new ActionPrecondition
                {
                    Expression = "true",
                    Description = "always-satisfied",
                    Kind = PreconditionKind.PropertyPredicate,
                    Strength = ConstraintStrength.Hard,
                };
                return new[]
                {
                    new ActionConstraintReport(
                        new ActionDescriptor("Ship", "Ship description"),
                        IsAvailable: true,
                        Constraints: new[]
                        {
                            new ConstraintEvaluation(
                                precond,
                                IsSatisfied: true,
                                Strength: ConstraintStrength.Hard,
                                FailureReason: null,
                                ExpectedShape: null),
                        }),
                };
            });
        query
            .EstimateBlastRadius(Arg.Any<IReadOnlyList<OntologyNodeRef>>(), Arg.Any<BlastRadiusOptions?>())
            .Returns(EmptyBlastRadius());
        query
            .DetectPatternViolations(Arg.Any<IReadOnlyList<OntologyNodeRef>>(), Arg.Any<DesignIntent>())
            .Returns(Array.Empty<PatternViolation>());

        var tool = new OntologyValidateTool(query);
        var arguments = new Dictionary<string, object?>
        {
            ["quantity"] = 5,
            ["status"] = "OverriddenByArg",
        };
        var knownProperties = new Dictionary<string, object?>
        {
            ["status"] = "Pending",
            ["region"] = "EU",
        };
        var intent = new DesignIntent(
            new List<OntologyNodeRef> { Node() },
            new List<ProposedAction> { new("Ship", Node(), arguments) },
            knownProperties);

        tool.Validate(intent);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.ContainsKey("quantity")).IsTrue();
        await Assert.That(captured["quantity"]).IsEqualTo(5);
        // Subject property the action does not override is still present.
        await Assert.That(captured["region"]).IsEqualTo("EU");
        // Action arguments win on key collisions.
        await Assert.That(captured["status"]).IsEqualTo("OverriddenByArg");
    }
}
