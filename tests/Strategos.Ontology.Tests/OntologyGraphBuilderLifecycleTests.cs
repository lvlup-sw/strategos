using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.Builder;

namespace Strategos.Ontology.Tests;

public record LifecycleTestPosition(Guid Id, TestPositionStatus Status);

public class ValidLifecycleDomainOntology : DomainOntology
{
    public override string DomainName => "lifecycle-test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<LifecycleTestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);

            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<TestPositionStatus>>)(lifecycle =>
            {
                lifecycle.State(TestPositionStatus.Pending)
                    .Description("Position created but not yet active")
                    .Initial();
                lifecycle.State(TestPositionStatus.Active)
                    .Description("Position is open and tradeable");
                lifecycle.State(TestPositionStatus.Closed)
                    .Description("Position has been fully closed")
                    .Terminal();

                lifecycle.Transition(TestPositionStatus.Pending, TestPositionStatus.Active)
                    .TriggeredByAction("ActivatePosition");
                lifecycle.Transition(TestPositionStatus.Active, TestPositionStatus.Active)
                    .TriggeredByAction("ExecuteTrade")
                    .Description("Trade modifies quantity, position stays active");
                lifecycle.Transition(TestPositionStatus.Active, TestPositionStatus.Closed)
                    .TriggeredByAction("ClosePosition");
            }));

            obj.Action("ActivatePosition").Description("Activate a pending position");
            obj.Action("ExecuteTrade").Description("Execute a trade");
            obj.Action("ClosePosition").Description("Close position");
        });
    }
}

public class NoInitialStateDomainOntology : DomainOntology
{
    public override string DomainName => "no-initial-test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<LifecycleTestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);

            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<TestPositionStatus>>)(lifecycle =>
            {
                lifecycle.State(TestPositionStatus.Pending); // No .Initial()
                lifecycle.State(TestPositionStatus.Active);
                lifecycle.State(TestPositionStatus.Closed).Terminal();
            }));
        });
    }
}

public class TwoInitialStatesDomainOntology : DomainOntology
{
    public override string DomainName => "two-initial-test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<LifecycleTestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);

            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<TestPositionStatus>>)(lifecycle =>
            {
                lifecycle.State(TestPositionStatus.Pending).Initial();
                lifecycle.State(TestPositionStatus.Active).Initial(); // Second initial
                lifecycle.State(TestPositionStatus.Closed).Terminal();
            }));
        });
    }
}

public class NoTerminalStateDomainOntology : DomainOntology
{
    public override string DomainName => "no-terminal-test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<LifecycleTestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);

            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<TestPositionStatus>>)(lifecycle =>
            {
                lifecycle.State(TestPositionStatus.Pending).Initial();
                lifecycle.State(TestPositionStatus.Active);
                lifecycle.State(TestPositionStatus.Closed); // No .Terminal()
            }));
        });
    }
}

public class BadTransitionStateDomainOntology : DomainOntology
{
    public override string DomainName => "bad-transition-test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<LifecycleTestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);

            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<TestPositionStatus>>)(lifecycle =>
            {
                lifecycle.State(TestPositionStatus.Pending).Initial();
                lifecycle.State(TestPositionStatus.Closed).Terminal();
                // Transition references Active, which is not declared
                lifecycle.Transition(TestPositionStatus.Pending, TestPositionStatus.Active)
                    .TriggeredByAction("Activate");
            }));
        });
    }
}

public class OntologyGraphBuilderLifecycleTests
{
    [Test]
    public async Task ValidLifecycle_BuildsSuccessfully()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new ValidLifecycleDomainOntology());

        var graph = graphBuilder.Build();

        var objectType = graph.GetObjectType("lifecycle-test", "LifecycleTestPosition")!;
        await Assert.That(objectType.Lifecycle).IsNotNull();
        await Assert.That(objectType.Lifecycle!.PropertyName).IsEqualTo("Status");
        await Assert.That(objectType.Lifecycle.States.Count).IsEqualTo(3);
        await Assert.That(objectType.Lifecycle.Transitions.Count).IsEqualTo(3);
    }

    [Test]
    public async Task ValidLifecycle_StateDescriptorsCorrect()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new ValidLifecycleDomainOntology());

        var graph = graphBuilder.Build();
        var lifecycle = graph.GetObjectType("lifecycle-test", "LifecycleTestPosition")!.Lifecycle!;

        var initial = lifecycle.States.Single(s => s.IsInitial);
        await Assert.That(initial.Name).IsEqualTo("Pending");
        await Assert.That(initial.Description).IsEqualTo("Position created but not yet active");

        var terminal = lifecycle.States.Single(s => s.IsTerminal);
        await Assert.That(terminal.Name).IsEqualTo("Closed");
    }

    [Test]
    public async Task NoInitialState_ThrowsOntologyCompositionException()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new NoInitialStateDomainOntology());

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("exactly 1 initial state");
    }

    [Test]
    public async Task TwoInitialStates_ThrowsOntologyCompositionException()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new TwoInitialStatesDomainOntology());

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("exactly 1 initial state");
    }

    [Test]
    public async Task NoTerminalState_ThrowsOntologyCompositionException()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new NoTerminalStateDomainOntology());

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("at least 1 terminal state");
    }

    [Test]
    public async Task TransitionReferencesUndeclaredState_ThrowsOntologyCompositionException()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new BadTransitionStateDomainOntology());

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("undeclared state");
    }

    [Test]
    public async Task ObjectTypeBuilderLifecycle_IntegrationTest()
    {
        var builder = new ObjectTypeBuilder<LifecycleTestPosition>("test");
        builder.Key(p => p.Id);
        builder.Property(p => p.Status);

        builder.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<TestPositionStatus>>)(lifecycle =>
        {
            lifecycle.State(TestPositionStatus.Pending).Initial();
            lifecycle.State(TestPositionStatus.Active);
            lifecycle.State(TestPositionStatus.Closed).Terminal();
            lifecycle.Transition(TestPositionStatus.Pending, TestPositionStatus.Active)
                .TriggeredByAction("Activate");
        }));

        var descriptor = builder.Build();

        await Assert.That(descriptor.Lifecycle).IsNotNull();
        await Assert.That(descriptor.Lifecycle!.PropertyName).IsEqualTo("Status");
        await Assert.That(descriptor.Lifecycle.StateEnumTypeName).IsEqualTo("TestPositionStatus");
    }

    [Test]
    public async Task NoLifecycle_DescriptorIsNull()
    {
        var builder = new ObjectTypeBuilder<TestPosition>("test");
        builder.Key(p => p.Id);

        var descriptor = builder.Build();

        await Assert.That(descriptor.Lifecycle).IsNull();
    }
}
