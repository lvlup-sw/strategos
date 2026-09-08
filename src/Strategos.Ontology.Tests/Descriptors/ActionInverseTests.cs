using System.Collections.Immutable;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public sealed class ActionInverseTests
{
    private static readonly ActionSubject Subject = new("orders", "Order");
    private static readonly AuthorityLattice EmptyLattice = new([], []);

    [Test]
    public async Task ActionContractIdentityUsesValidatedOntologyValueIdentity()
    {
        var identity = new ActionContractIdentity(
            new ActionSubject("orders", "Order"),
            "capture");
        var equivalent = new ActionContractIdentity(
            new ActionSubject("orders", "Order"),
            "capture");
        var different = new ActionContractIdentity(
            new ActionSubject("orders", "Order"),
            "refund");

        await Assert.That(identity).IsEqualTo(equivalent);
        await Assert.That(identity.GetHashCode()).IsEqualTo(equivalent.GetHashCode());
        await Assert.That(identity).IsNotEqualTo(different);
        await Assert.That(identity.ToString()).IsEqualTo("orders/Order/capture");
        await Assert.That(() => new ActionContractIdentity(Subject, " "))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task DerivationSwapsEffectiveGuaranteeAndHardRequirement()
    {
        var forward = Action(
            "activate",
            requires: ActionPredicate.All(Integer("status", 0), Integer("tenant", 7)),
            ensures: Integer("status", 1),
            frame: [ActionResource.Property("status")]);

        var analysis = ActionCalculus.AnalyzeInverse(forward, EmptyLattice);

        await Assert.That(analysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Missing);
        await Assert.That(analysis.DerivedContract).IsNotNull();
        await Assert.That(analysis.DerivedContract!.Requirement).IsEqualTo(
            ActionPredicate.All(Integer("status", 1), Integer("tenant", 7)));
        await Assert.That(analysis.DerivedContract.Guarantee).IsEqualTo(
            ActionPredicate.All(Integer("status", 0), Integer("tenant", 7)));
        await Assert.That(analysis.DerivedContract.Frame.Resources)
            .IsEquivalentTo([ActionResource.Property("status")]);
    }

    [Test]
    public async Task AuthoredInverseUsesSemanticEquivalenceRatherThanPredicateShape()
    {
        var one = Integer("status", 1);
        var two = Integer("status", 2);
        var forward = Action(
            "choose-status",
            ensures: ActionPredicate.Any(one, two),
            frame: [ActionResource.Property("status")],
            compensatingActionName: "restore-status");
        var authored = Action(
            "restore-status",
            requires: ActionPredicate.Not(ActionPredicate.All(
                ActionPredicate.Not(one),
                ActionPredicate.Not(two))),
            frame: [ActionResource.Property("status")]);

        var analysis = ActionCalculus.AnalyzeInverse(
            forward,
            authored,
            EmptyLattice);

        await Assert.That(analysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Proven);
        await Assert.That(analysis.IsCompensable).IsTrue();
        await Assert.That(analysis.Failures).IsEmpty();
    }

    [Test]
    public async Task AuthoredRequirementMustBeEquivalentInBothDirections()
    {
        var forward = Action(
            "choose-status",
            ensures: ActionPredicate.Any(Integer("status", 1), Integer("status", 2)),
            frame: [ActionResource.Property("status")],
            compensatingActionName: "restore-status");
        var authored = Action(
            "restore-status",
            requires: Integer("status", 1),
            frame: [ActionResource.Property("status")]);

        var analysis = ActionCalculus.AnalyzeInverse(forward, authored, EmptyLattice);

        await Assert.That(analysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Refuted);
        var failure = analysis.Failures.Single(item =>
            item.Obligation == ActionInverseObligation.Requirements);
        await Assert.That(failure.Message).Contains(
            "Derived inverse requirement does not imply");
        await Assert.That(failure.Counterexample.Single().Resource)
            .IsEqualTo("property|status");
    }

    [Test]
    public async Task AuthoredGuaranteeMustRestoreTheForwardRequirement()
    {
        var forward = Action(
            "activate",
            requires: Integer("status", 0),
            ensures: Integer("status", 1),
            frame: [ActionResource.Property("status")],
            compensatingActionName: "deactivate");
        var authored = Action(
            "deactivate",
            requires: Integer("status", 1),
            ensures: Integer("status", 2),
            frame: [ActionResource.Property("status")]);

        var analysis = ActionCalculus.AnalyzeInverse(forward, authored, EmptyLattice);

        await Assert.That(analysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Refuted);
        await Assert.That(analysis.Failures.Count(item =>
            item.Obligation == ActionInverseObligation.Guarantees)).IsEqualTo(2);
        await Assert.That(analysis.Failures.Where(item =>
                item.Obligation == ActionInverseObligation.Guarantees)
            .All(item => !item.Counterexample.IsEmpty)).IsTrue();
    }

    [Test]
    public async Task AuthoredEffectiveGuaranteeMayRestorePreservedForwardRequirement()
    {
        var forward = Action(
            "activate",
            requires: ActionPredicate.All(Integer("status", 0), Integer("tenant", 7)),
            ensures: Integer("status", 1),
            frame: [ActionResource.Property("status")],
            compensatingActionName: "deactivate");
        var authored = Action(
            "deactivate",
            requires: ActionPredicate.All(Integer("status", 1), Integer("tenant", 7)),
            ensures: Integer("status", 0),
            frame: [ActionResource.Property("status")]);

        var analysis = ActionCalculus.AnalyzeInverse(forward, authored, EmptyLattice);

        await Assert.That(analysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Proven);
        await Assert.That(analysis.Failures).IsEmpty();
    }

    [Test]
    public async Task SubjectAndFrameMustMatchExactly()
    {
        var forward = Action(
            "activate",
            frame: [ActionResource.Property("status")],
            compensatingActionName: "deactivate");
        var authored = Action(
            "deactivate",
            frame: [ActionResource.Property("other")],
            subject: new ActionSubject("billing", "Order"));

        var analysis = ActionCalculus.AnalyzeInverse(forward, authored, EmptyLattice);

        await Assert.That(analysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Refuted);
        await Assert.That(analysis.Failures.Select(item => item.Obligation))
            .Contains(ActionInverseObligation.Subject);
        await Assert.That(analysis.Failures.Select(item => item.Obligation))
            .Contains(ActionInverseObligation.Frame);
    }

    [Test]
    public async Task AuthorityComparisonUsesLatticeMeaningRatherThanLiteralName()
    {
        var lattice = AliasLattice();
        var forward = Action(
            "activate",
            requires: Integer("status", 0),
            ensures: Integer("status", 1),
            frame: [ActionResource.Property("status")],
            authority: "writer",
            compensatingActionName: "deactivate");
        var authored = Action(
            "deactivate",
            requires: Integer("status", 1),
            ensures: Integer("status", 0),
            frame: [ActionResource.Property("status")],
            authority: "editor");

        var analysis = ActionCalculus.AnalyzeInverse(forward, authored, lattice);

        await Assert.That(analysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Proven);
        await Assert.That(analysis.DerivedContract!.RequiredAuthority.Coordinates["access"])
            .IsEqualTo("write");
    }

    [Test]
    public async Task StrongerInverseAuthorityIsRefuted()
    {
        var lattice = OrderedLattice();
        var forward = Action(
            "activate",
            requires: Integer("status", 0),
            ensures: Integer("status", 1),
            frame: [ActionResource.Property("status")],
            authority: "reader",
            compensatingActionName: "deactivate");
        var authored = Action(
            "deactivate",
            requires: Integer("status", 1),
            ensures: Integer("status", 0),
            frame: [ActionResource.Property("status")],
            authority: "writer");

        var analysis = ActionCalculus.AnalyzeInverse(forward, authored, lattice);

        await Assert.That(analysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Refuted);
        await Assert.That(analysis.Failures.Single(item =>
            item.Obligation == ActionInverseObligation.Authority).Message)
            .Contains("semantically equal");
    }

    [Test]
    public async Task WeakerInverseAuthorityIsRefuted()
    {
        var lattice = OrderedLattice();
        var forward = Action(
            "activate",
            requires: Integer("status", 0),
            ensures: Integer("status", 1),
            frame: [ActionResource.Property("status")],
            authority: "writer",
            compensatingActionName: "deactivate");
        var authored = Action(
            "deactivate",
            requires: Integer("status", 1),
            ensures: Integer("status", 0),
            frame: [ActionResource.Property("status")],
            authority: "reader");

        var analysis = ActionCalculus.AnalyzeInverse(forward, authored, lattice);

        await Assert.That(analysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Refuted);
        await Assert.That(analysis.Failures.Single(item =>
            item.Obligation == ActionInverseObligation.Authority).Message)
            .Contains("semantically equal");
    }

    [Test]
    public async Task EquivalentFrameIgnoresAuthoredOrderAndDuplicates()
    {
        var status = ActionResource.Property("status");
        var audit = ActionResource.External("audit-log");
        var forward = Action(
            "activate",
            requires: Integer("status", 0),
            ensures: Integer("status", 1),
            frame: [status, audit],
            compensatingActionName: "deactivate");
        var authored = Action(
            "deactivate",
            requires: Integer("status", 1),
            ensures: Integer("status", 0),
            frame: [audit, status, audit]);

        var analysis = ActionCalculus.AnalyzeInverse(forward, authored, EmptyLattice);

        await Assert.That(analysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Proven);
    }

    [Test]
    public async Task InvalidAndOpaqueContractsCannotBecomeCompensable()
    {
        var invalid = Action(
            "invalid",
            requires: ActionPredicate.All(Integer("status", 0), Integer("status", 1)));
        var opaque = Action(
            "opaque",
            requires: ActionPredicate.Custom("orders.runtime.v1"));

        var invalidAnalysis = ActionCalculus.AnalyzeInverse(invalid, EmptyLattice);
        var opaqueAnalysis = ActionCalculus.AnalyzeInverse(opaque, EmptyLattice);

        await Assert.That(invalidAnalysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Invalid);
        await Assert.That(invalidAnalysis.DerivedContract).IsNull();
        await Assert.That(opaqueAnalysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Opaque);
        await Assert.That(opaqueAnalysis.IsCompensable).IsFalse();
    }

    [Test]
    public async Task EmptyFrameDerivesExecutableIdentityUnlessAnExplicitInverseIsBroken()
    {
        var implicitIdentity = Action(
            "observe",
            requires: Integer("tenant", 7));
        var brokenExplicit = Action(
            "observe-with-explicit",
            requires: Integer("tenant", 7),
            compensatingActionName: "missing");

        var identityAnalysis = ActionCalculus.AnalyzeInverse(implicitIdentity, EmptyLattice);
        var missingAnalysis = ActionCalculus.AnalyzeInverse(brokenExplicit, EmptyLattice);

        await Assert.That(identityAnalysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Proven);
        await Assert.That(identityAnalysis.UsesIdentityInverse).IsTrue();
        await Assert.That(identityAnalysis.IsCompensable).IsTrue();
        await Assert.That(missingAnalysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Refuted);
        await Assert.That(missingAnalysis.UsesIdentityInverse).IsFalse();
    }

    [Test]
    public async Task EmptyFrameIdentityProducesANonExecutableCompensableLeaf()
    {
        var analysis = ActionCalculus.AnalyzeInverse(
            Action("observe", requires: Integer("tenant", 7)),
            EmptyLattice);

        var plan = ActionCalculus.DeriveRollbackPlan(analysis);

        await Assert.That(plan.Kind).IsEqualTo(ActionRollbackPlanKind.Leaf);
        await Assert.That(plan.IsCompensable).IsTrue();
        await Assert.That(plan.Leaf!.UsesIdentityInverse).IsTrue();
        await Assert.That(plan.Leaf.InverseAction).IsNull();
    }

    [Test]
    public async Task NonEmptyFrameWithoutExecutableInverseIsMissing()
    {
        var forward = Action(
            "activate",
            requires: Integer("status", 0),
            ensures: Integer("status", 1),
            frame: [ActionResource.Property("status")]);

        var analysis = ActionCalculus.AnalyzeInverse(forward, EmptyLattice);

        await Assert.That(analysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Missing);
        await Assert.That(analysis.IsCompensable).IsFalse();
        await Assert.That(analysis.Failures.Single().Obligation)
            .IsEqualTo(ActionInverseObligation.ExecutableInverse);
    }

    [Test]
    public async Task InvalidNullFrameStillProducesANonCompensableRollbackLeaf()
    {
        var analysis = ActionCalculus.AnalyzeInverse(
            Action("invalid-frame", frame: [null!]),
            EmptyLattice);

        var plan = ActionCalculus.DeriveRollbackPlan(analysis);

        await Assert.That(analysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Invalid);
        await Assert.That(plan.Kind).IsEqualTo(ActionRollbackPlanKind.Leaf);
        await Assert.That(plan.IsCompensable).IsFalse();
        await Assert.That(plan.Frame.Resources).IsEmpty();
        await Assert.That(plan.NonCompensableLeaves).HasSingleItem();
    }

    [Test]
    public async Task SequentialRollbackReversesTheSuppliedCompletedPrefix()
    {
        var first = ProvenPair("first", "undo-first", "a");
        var second = ProvenPair("second", "undo-second", "b");

        var plan = ActionCalculus.DeriveRollbackPlan(Subject, [first, second]);

        await Assert.That(plan.Kind).IsEqualTo(ActionRollbackPlanKind.Sequence);
        await Assert.That(string.Join(",", plan.Children.Select(child =>
                child.Leaf!.ForwardAction.ActionName)))
            .IsEqualTo("second,first");
        await Assert.That(string.Join(",", plan.Children.Select(child =>
                child.Leaf!.InverseAction!.ActionName)))
            .IsEqualTo("undo-second,undo-first");
        await Assert.That(plan.IsCompensable).IsTrue();
    }

    [Test]
    public async Task EmptyCompletedPrefixDerivesTheSubjectTypedIdentity()
    {
        var plan = ActionCalculus.DeriveRollbackPlan(
            Subject,
            Array.Empty<ActionInverseAnalysis>());

        await Assert.That(plan.Kind).IsEqualTo(ActionRollbackPlanKind.Identity);
        await Assert.That(plan.Subject).IsEqualTo(Subject);
        await Assert.That(plan.Children).IsEmpty();
        await Assert.That(plan.IsCompensable).IsTrue();
    }

    [Test]
    public async Task ParallelRollbackRemainsParallelAndScopesRemainNested()
    {
        var left = ActionCalculus.DeriveRollbackPlan(
            ProvenPair("left", "undo-left", "left-state"));
        var right = ActionCalculus.DeriveRollbackPlan(
            ProvenPair("right", "undo-right", "right-state"));
        var parallel = ActionCalculus.DeriveParallelRollbackPlan(Subject, [left, right]);
        var innerScope = ActionCalculus.DeriveScopedRollbackPlan(parallel);
        var nestedScope = ActionCalculus.DeriveScopedRollbackPlan(innerScope);
        var outer = ActionCalculus.DeriveSequentialRollbackPlan(
            Subject,
            [innerScope, ActionCalculus.RollbackIdentity(Subject)]);

        await Assert.That(parallel.Kind).IsEqualTo(ActionRollbackPlanKind.Parallel);
        await Assert.That(parallel.Frame.Resources).IsEquivalentTo(
            [ActionResource.Property("left-state"), ActionResource.Property("right-state")]);
        await Assert.That(string.Join(",", parallel.Children.Select(child =>
                child.Leaf!.ForwardAction.ActionName)))
            .IsEqualTo("left,right");
        await Assert.That(innerScope.Kind).IsEqualTo(ActionRollbackPlanKind.Scope);
        await Assert.That(innerScope.Children.Single().Kind)
            .IsEqualTo(ActionRollbackPlanKind.Parallel);
        await Assert.That(nestedScope.Kind).IsEqualTo(ActionRollbackPlanKind.Scope);
        await Assert.That(nestedScope.Children.Single().Kind)
            .IsEqualTo(ActionRollbackPlanKind.Scope);
        await Assert.That(outer.Kind).IsEqualTo(ActionRollbackPlanKind.Scope);
        await Assert.That(outer.Frame.Resources).IsEquivalentTo(parallel.Frame.Resources);
    }

    [Test]
    public async Task ParallelRollbackRejectsOverlappingNestedFrames()
    {
        var first = ActionCalculus.DeriveRollbackPlan(
            ProvenPair("first", "undo-first", "shared"));
        var nested = ActionCalculus.DeriveScopedRollbackPlan(
            ActionCalculus.DeriveSequentialRollbackPlan(
                Subject,
                [
                    ActionCalculus.DeriveRollbackPlan(
                        ProvenPair("nested-a", "undo-nested-a", "nested")),
                    ActionCalculus.DeriveRollbackPlan(
                        ProvenPair("nested-shared", "undo-nested-shared", "shared")),
                ]));

        var exception = await Assert.That(() => ActionCalculus.DeriveParallelRollbackPlan(
                Subject,
                [nested, first]))
            .Throws<ArgumentException>();
        await Assert.That(exception!.Message).Contains("'Property:shared'");
        await Assert.That(nested.Frame.Resources).IsEquivalentTo(
            [ActionResource.Property("nested"), ActionResource.Property("shared")]);
    }

    [Test]
    public async Task DisjointParallelRollbackCarriesCanonicalAggregateFrame()
    {
        var zeta = ActionCalculus.DeriveRollbackPlan(
            ProvenPair("zeta", "undo-zeta", "zeta"));
        var alpha = ActionCalculus.DeriveRollbackPlan(
            ProvenPair("alpha", "undo-alpha", "alpha"));

        var plan = ActionCalculus.DeriveParallelRollbackPlan(Subject, [zeta, alpha]);

        await Assert.That(plan.Kind).IsEqualTo(ActionRollbackPlanKind.Parallel);
        await Assert.That(string.Join(",", plan.Frame.Resources.Select(static resource => resource.Name)))
            .IsEqualTo("alpha,zeta");
        await Assert.That(plan.Children.Select(static child => child.Frame.Resources.Single()))
            .IsEquivalentTo([ActionResource.Property("alpha"), ActionResource.Property("zeta")]);
    }

    [Test]
    public async Task ParallelRollbackRejectsWriteReadInterferenceInBothDirections()
    {
        // A requires y=0 and writes x; B writes y. Both inverse contracts prove in
        // isolation and their frames are disjoint, but A^-1 still reads y.
        var actionA = ActionCalculus.DeriveRollbackPlan(
            ProvenPairWithRead("set-x", "restore-x", "x", "y"));
        var actionB = ActionCalculus.DeriveRollbackPlan(
            ProvenPair("set-y", "restore-y", "y"));

        var readerFirst = await Assert.That(() => ActionCalculus.DeriveParallelRollbackPlan(
                Subject,
                [actionA, actionB]))
            .Throws<ArgumentException>();
        var writerFirst = await Assert.That(() => ActionCalculus.DeriveParallelRollbackPlan(
                Subject,
                [actionB, actionA]))
            .Throws<ArgumentException>();

        await Assert.That(readerFirst!.Message)
            .Contains("'Property:y' is restored by branch 1 and read by branch 0");
        await Assert.That(writerFirst!.Message)
            .Contains("'Property:y' is restored by branch 0 and read by branch 1");
    }

    [Test]
    public async Task ParallelRollbackPropagatesNestedReadFootprintsAndSnapshotsChildren()
    {
        var mutableChildren = new List<ActionRollbackPlan>
        {
            ActionCalculus.DeriveRollbackPlan(
                ProvenPairWithRead("nested-reader", "undo-nested-reader", "x", "y")),
        };
        var nested = ActionCalculus.DeriveScopedRollbackPlan(
            ActionCalculus.DeriveSequentialRollbackPlan(Subject, mutableChildren));
        mutableChildren.Clear();
        var writer = ActionCalculus.DeriveRollbackPlan(
            ProvenPair("writer", "undo-writer", "y"));

        var exception = await Assert.That(() => ActionCalculus.DeriveParallelRollbackPlan(
                Subject,
                [nested, writer]))
            .Throws<ArgumentException>();

        await Assert.That(exception!.Message).Contains("'Property:y'");
        await Assert.That(nested.Children).HasSingleItem();
        await Assert.That(nested.Children.Single().Leaf).IsNotNull();
    }

    [Test]
    public async Task ParallelRollbackAllowsReadReadSharingAcrossDisjointFrames()
    {
        var left = ActionCalculus.DeriveRollbackPlan(
            ProvenPairWithRead("left", "undo-left", "left-state", "shared"));
        var right = ActionCalculus.DeriveRollbackPlan(
            ProvenPairWithRead("right", "undo-right", "right-state", "shared"));

        var plan = ActionCalculus.DeriveParallelRollbackPlan(Subject, [left, right]);

        await Assert.That(plan.Kind).IsEqualTo(ActionRollbackPlanKind.Parallel);
        await Assert.That(plan.IsCompensable).IsTrue();
        await Assert.That(plan.Frame.Resources).IsEquivalentTo(
            [ActionResource.Property("left-state"), ActionResource.Property("right-state")]);
    }

    [Test]
    public async Task ParallelRollbackAllowsRepeatedResourcesWithinOneSequentialBranch()
    {
        var serial = ActionCalculus.DeriveSequentialRollbackPlan(
            Subject,
            [
                ActionCalculus.DeriveRollbackPlan(
                    ProvenPair("first", "undo-first", "serial")),
                ActionCalculus.DeriveRollbackPlan(
                    ProvenPair("second", "undo-second", "serial")),
            ]);
        var independent = ActionCalculus.DeriveRollbackPlan(
            ProvenPair("independent", "undo-independent", "other"));

        var plan = ActionCalculus.DeriveParallelRollbackPlan(Subject, [serial, independent]);

        await Assert.That(plan.Kind).IsEqualTo(ActionRollbackPlanKind.Parallel);
        await Assert.That(serial.Frame.Resources).HasSingleItem();
        await Assert.That(plan.Frame.Resources).IsEquivalentTo(
            [ActionResource.Property("other"), ActionResource.Property("serial")]);
    }

    [Test]
    public async Task NestedSequentialRollbackFlattensInExactReverseOrder()
    {
        var first = ActionCalculus.DeriveRollbackPlan(
            ProvenPair("first", "undo-first", "a"));
        var second = ActionCalculus.DeriveRollbackPlan(
            ProvenPair("second", "undo-second", "b"));
        var third = ActionCalculus.DeriveRollbackPlan(
            ProvenPair("third", "undo-third", "c"));
        var inner = ActionCalculus.DeriveSequentialRollbackPlan(
            Subject,
            [first, second]);

        var outer = ActionCalculus.DeriveSequentialRollbackPlan(
            Subject,
            [inner, third]);

        await Assert.That(outer.Kind).IsEqualTo(ActionRollbackPlanKind.Sequence);
        await Assert.That(string.Join(",", outer.Children.Select(child =>
                child.Leaf!.InverseAction!.ActionName)))
            .IsEqualTo("undo-third,undo-second,undo-first");
    }

    [Test]
    public async Task NonCompensableLeafPropagatesThroughEveryCompositeKind()
    {
        var proved = ActionCalculus.DeriveRollbackPlan(
            ProvenPair("first", "undo-first", "a"));
        var missingAction = Action(
            "missing",
            ensures: Integer("b", 1),
            frame: [ActionResource.Property("b")]);
        var missing = ActionCalculus.DeriveRollbackPlan(
            ActionCalculus.AnalyzeInverse(missingAction, EmptyLattice));
        var sequence = ActionCalculus.DeriveSequentialRollbackPlan(
            Subject,
            [proved, missing]);
        var parallel = ActionCalculus.DeriveParallelRollbackPlan(
            Subject,
            [proved, missing]);
        var scope = ActionCalculus.DeriveScopedRollbackPlan(sequence);

        await Assert.That(sequence.IsCompensable).IsFalse();
        await Assert.That(parallel.IsCompensable).IsFalse();
        await Assert.That(scope.IsCompensable).IsFalse();
        await Assert.That(scope.NonCompensableLeaves.Single().ForwardAction.ActionName)
            .IsEqualTo("missing");
    }

    [Test]
    public async Task RefutedInverseIsNeverExposedAsExecutableRollbackCode()
    {
        var forward = Action(
            "forward",
            ensures: Integer("state", 1),
            frame: [ActionResource.Property("state")]);
        var wrongInverse = Action(
            "wrong-inverse",
            requires: Integer("state", 2),
            ensures: ActionPredicate.True,
            frame: [ActionResource.Property("state")]);
        var analysis = ActionCalculus.AnalyzeInverse(
            forward,
            wrongInverse,
            EmptyLattice);

        var plan = ActionCalculus.DeriveRollbackPlan(analysis);

        await Assert.That(analysis.Status).IsEqualTo(ActionInverseAnalysisStatus.Refuted);
        await Assert.That(plan.IsCompensable).IsFalse();
        await Assert.That(plan.Leaf!.InverseAction).IsNull();
    }

    [Test]
    public async Task RollbackFactoriesSnapshotInputsAndRejectSubjectMixing()
    {
        var mutable = new List<ActionRollbackPlan>
        {
            ActionCalculus.DeriveRollbackPlan(ProvenPair("first", "undo-first", "a")),
        };
        var parallel = ActionCalculus.DeriveParallelRollbackPlan(Subject, mutable);
        mutable.Add(ActionCalculus.DeriveRollbackPlan(
            ProvenPair("second", "undo-second", "b")));
        var foreign = ActionCalculus.RollbackIdentity(new ActionSubject("billing", "Order"));

        await Assert.That(parallel.Children).HasSingleItem();
        await Assert.That(parallel.Frame.Resources).IsEquivalentTo(
            [ActionResource.Property("a")]);
        await Assert.That(() => ActionCalculus.DeriveSequentialRollbackPlan(
                Subject,
                [parallel, foreign]))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task InverseAnalysisHonorsCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.That(() => ActionCalculus.AnalyzeInverse(
                Action("forward"),
                EmptyLattice,
                source.Token))
            .Throws<OperationCanceledException>();
    }

    private static ActionInverseAnalysis ProvenPair(
        string forwardName,
        string inverseName,
        string property)
    {
        var forward = Action(
            forwardName,
            requires: Integer(property, 0),
            ensures: Integer(property, 1),
            frame: [ActionResource.Property(property)],
            compensatingActionName: inverseName);
        var inverse = Action(
            inverseName,
            requires: Integer(property, 1),
            ensures: Integer(property, 0),
            frame: [ActionResource.Property(property)]);
        var analysis = ActionCalculus.AnalyzeInverse(forward, inverse, EmptyLattice);
        if (!analysis.IsCompensable)
        {
            throw new InvalidOperationException(string.Join(" ", analysis.Failures.Select(item => item.Message)));
        }

        return analysis;
    }

    private static ActionInverseAnalysis ProvenPairWithRead(
        string forwardName,
        string inverseName,
        string property,
        string readProperty)
    {
        var forwardRequirement = ActionPredicate.All(
            Integer(property, 0),
            Integer(readProperty, 0));
        var inverseRequirement = ActionPredicate.All(
            Integer(property, 1),
            Integer(readProperty, 0));
        var forward = Action(
            forwardName,
            requires: forwardRequirement,
            ensures: Integer(property, 1),
            frame: [ActionResource.Property(property)],
            compensatingActionName: inverseName);
        var inverse = Action(
            inverseName,
            requires: inverseRequirement,
            ensures: Integer(property, 0),
            frame: [ActionResource.Property(property)]);
        var analysis = ActionCalculus.AnalyzeInverse(forward, inverse, EmptyLattice);
        if (!analysis.IsCompensable)
        {
            throw new InvalidOperationException(string.Join(" ", analysis.Failures.Select(item => item.Message)));
        }

        return analysis;
    }

    private static ActionDescriptor Action(
        string name,
        ActionPredicate? requires = null,
        ActionPredicate? ensures = null,
        IReadOnlyList<ActionResource>? frame = null,
        string? authority = null,
        string? compensatingActionName = null,
        ActionSubject? subject = null) => new(subject ?? Subject, name, name)
        {
            RequiredAuthority = authority,
            CompensatingActionName = compensatingActionName,
            Preconditions = requires is null
                ? []
                : [new ActionPrecondition(requires, requires.Expression)],
            Ensures = ensures is null
                ? []
                : [new ActionGuarantee(ensures)],
            TouchedResources = frame ?? [],
        };

    private static ActionPredicate Integer(string property, int value) =>
        ActionPredicate.Property(
            new PredicatePropertyReference(
                property,
                PredicateScalarKind.Integer,
                isNullable: false),
            PredicateComparisonOperator.Equal,
            PredicateLiteral.Integer(value));

    private static AuthorityLattice AliasLattice() => new(
        [new AuthorityAxisDescriptor("access", ["read", "write"])],
        [
            new AuthorityDescriptor("writer")
            {
                Coordinates = ImmutableDictionary<string, string>.Empty.Add("access", "write"),
            },
            new AuthorityDescriptor("editor")
            {
                Coordinates = ImmutableDictionary<string, string>.Empty.Add("access", "write"),
            },
        ]);

    private static AuthorityLattice OrderedLattice() => new(
        [new AuthorityAxisDescriptor("access", ["read", "write"])],
        [
            new AuthorityDescriptor("reader")
            {
                Coordinates = ImmutableDictionary<string, string>.Empty.Add("access", "read"),
            },
            new AuthorityDescriptor("writer")
            {
                Coordinates = ImmutableDictionary<string, string>.Empty.Add("access", "write"),
            },
        ]);
}
