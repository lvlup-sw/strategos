using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public enum TestOrderStatus
{
    New,
    PartiallyFilled,
    Filled,
    Cancelled,
}

public record TestOrderWithStatus(Guid Id, TestOrderStatus Status);

public record TestOrderPartialFill;
public record TestOrderFilled;
public record TestPositionClosed;

public class LifecycleBuilderTests
{
    [Test]
    public async Task State_CreatesStateDescriptor()
    {
        var builder = new LifecycleBuilder<TestPositionStatus>();

        builder.State(TestPositionStatus.Pending).Initial();
        builder.State(TestPositionStatus.Active);
        builder.State(TestPositionStatus.Closed).Terminal();
        var descriptor = builder.Build();

        await Assert.That(descriptor.States.Count).IsEqualTo(3);
        await Assert.That(descriptor.States[0].Name).IsEqualTo("Pending");
        await Assert.That(descriptor.States[0].IsInitial).IsTrue();
        await Assert.That(descriptor.States[2].Name).IsEqualTo("Closed");
        await Assert.That(descriptor.States[2].IsTerminal).IsTrue();
    }

    [Test]
    public async Task State_Description_SetsDescription()
    {
        var builder = new LifecycleBuilder<TestPositionStatus>();

        builder.State(TestPositionStatus.Pending)
            .Description("Position created but not yet active")
            .Initial();
        var descriptor = builder.Build();

        await Assert.That(descriptor.States[0].Description)
            .IsEqualTo("Position created but not yet active");
    }

    [Test]
    public async Task Transition_CreatesTransitionDescriptor()
    {
        var builder = new LifecycleBuilder<TestPositionStatus>();

        builder.Transition(TestPositionStatus.Pending, TestPositionStatus.Active)
            .TriggeredByAction("ActivatePosition");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Transitions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Transitions[0].FromState).IsEqualTo("Pending");
        await Assert.That(descriptor.Transitions[0].ToState).IsEqualTo("Active");
        await Assert.That(descriptor.Transitions[0].TriggerActionName).IsEqualTo("ActivatePosition");
    }

    [Test]
    public async Task Transition_TriggeredByEvent_SetsEventTypeName()
    {
        var builder = new LifecycleBuilder<TestPositionStatus>();

        builder.Transition(TestPositionStatus.Active, TestPositionStatus.Closed)
            .TriggeredByEvent<TestPositionClosed>();
        var descriptor = builder.Build();

        await Assert.That(descriptor.Transitions[0].TriggerEventTypeName).IsEqualTo("TestPositionClosed");
        await Assert.That(descriptor.Transitions[0].TriggerActionName).IsNull();
    }

    [Test]
    public async Task Transition_Description_SetsDescription()
    {
        var builder = new LifecycleBuilder<TestPositionStatus>();

        builder.Transition(TestPositionStatus.Active, TestPositionStatus.Active)
            .TriggeredByAction("ExecuteTrade")
            .Description("Trade modifies quantity, position stays active");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Transitions[0].Description)
            .IsEqualTo("Trade modifies quantity, position stays active");
    }

    [Test]
    public async Task SelfTransition_Supported()
    {
        var builder = new LifecycleBuilder<TestPositionStatus>();

        builder.Transition(TestPositionStatus.Active, TestPositionStatus.Active)
            .TriggeredByAction("ExecuteTrade");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Transitions[0].FromState).IsEqualTo("Active");
        await Assert.That(descriptor.Transitions[0].ToState).IsEqualTo("Active");
    }

    [Test]
    public async Task Build_SetsStateEnumTypeName()
    {
        var builder = new LifecycleBuilder<TestPositionStatus>();
        builder.State(TestPositionStatus.Pending).Initial();
        builder.State(TestPositionStatus.Closed).Terminal();
        var descriptor = builder.Build();

        await Assert.That(descriptor.StateEnumTypeName).IsEqualTo("TestPositionStatus");
    }

    [Test]
    public async Task InitialState_Shorthand_CallsStateThenInitial()
    {
        var builder = new LifecycleBuilder<TestPositionStatus>();

        builder.InitialState(TestPositionStatus.Pending);
        var descriptor = builder.Build();

        await Assert.That(descriptor.States.Count).IsEqualTo(1);
        await Assert.That(descriptor.States[0].Name).IsEqualTo("Pending");
        await Assert.That(descriptor.States[0].IsInitial).IsTrue();
    }

    [Test]
    public async Task TerminalState_Shorthand_CallsStateThenTerminal()
    {
        var builder = new LifecycleBuilder<TestPositionStatus>();

        builder.TerminalState(TestPositionStatus.Closed);
        var descriptor = builder.Build();

        await Assert.That(descriptor.States.Count).IsEqualTo(1);
        await Assert.That(descriptor.States[0].Name).IsEqualTo("Closed");
        await Assert.That(descriptor.States[0].IsTerminal).IsTrue();
    }

    [Test]
    public async Task Transition_WithTriggerOverload_ChainsTriggeredByAction()
    {
        var builder = new LifecycleBuilder<TestPositionStatus>();

        builder.Transition(TestPositionStatus.Pending, TestPositionStatus.Active, trigger: "ActivatePosition");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Transitions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Transitions[0].FromState).IsEqualTo("Pending");
        await Assert.That(descriptor.Transitions[0].ToState).IsEqualTo("Active");
        await Assert.That(descriptor.Transitions[0].TriggerActionName).IsEqualTo("ActivatePosition");
    }

    [Test]
    public async Task FullLifecycle_ComplexStateMachine()
    {
        var builder = new LifecycleBuilder<TestOrderStatus>();

        builder.State(TestOrderStatus.New).Initial();
        builder.State(TestOrderStatus.PartiallyFilled);
        builder.State(TestOrderStatus.Filled).Terminal();
        builder.State(TestOrderStatus.Cancelled).Terminal();

        builder.Transition(TestOrderStatus.New, TestOrderStatus.PartiallyFilled)
            .TriggeredByEvent<TestOrderPartialFill>();
        builder.Transition(TestOrderStatus.New, TestOrderStatus.Filled)
            .TriggeredByEvent<TestOrderFilled>();
        builder.Transition(TestOrderStatus.New, TestOrderStatus.Cancelled)
            .TriggeredByAction("CancelOrder");
        builder.Transition(TestOrderStatus.PartiallyFilled, TestOrderStatus.Filled)
            .TriggeredByEvent<TestOrderFilled>();
        builder.Transition(TestOrderStatus.PartiallyFilled, TestOrderStatus.Cancelled)
            .TriggeredByAction("CancelOrder");

        var descriptor = builder.Build();

        await Assert.That(descriptor.States.Count).IsEqualTo(4);
        await Assert.That(descriptor.Transitions.Count).IsEqualTo(5);
    }
}
