namespace Strategos.Ontology.Builder;

public interface ILifecycleBuilder<TEnum>
    where TEnum : struct, Enum
{
    ILifecycleStateBuilder State(TEnum state);

    ILifecycleStateBuilder InitialState(TEnum state);

    ILifecycleStateBuilder TerminalState(TEnum state);

    ILifecycleTransitionBuilder Transition(TEnum from, TEnum to);

    ILifecycleTransitionBuilder Transition(TEnum from, TEnum to, string trigger);
}
