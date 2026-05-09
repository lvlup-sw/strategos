using System.Linq.Expressions;

namespace Strategos.Ontology.Builder;

public interface IActionBuilder<T> : IActionBuilder
    where T : class
{
    new IActionBuilder<T> Description(string description);

    new IActionBuilder<T> Accepts<TAccepts>();

    new IActionBuilder<T> Returns<TReturns>();

    new IActionBuilder<T> BoundToWorkflow(string workflowName);

    new IActionBuilder<T> BoundToTool(string toolName, string methodName);

    /// <summary>
    /// Marks the action as read-only. Read-only actions are dispatchable via
    /// <see cref="Strategos.Ontology.Actions.IActionDispatcher.DispatchReadOnlyAsync"/>
    /// and may not declare write postconditions; the analyzer enforces both
    /// invariants at compile time.
    /// </summary>
    /// <returns>The same generic builder instance for fluent chaining.</returns>
    new IActionBuilder<T> ReadOnly();

    IActionBuilder<T> BoundToTool<TTool>(Expression<Func<TTool, Delegate>> methodSelector);

    IActionBuilder<T> Requires(Expression<Func<T, bool>> predicate);

    IActionBuilder<T> RequiresSoft(Expression<Func<T, bool>> predicate);

    IActionBuilder<T> RequiresLink(string linkName);

    IActionBuilder<T> RequiresLinkSoft(string linkName);

    IActionBuilder<T> Modifies(Expression<Func<T, object>> propertySelector);

    IActionBuilder<T> CreatesLinked<TTarget>(string linkName);

    IActionBuilder<T> EmitsEvent<TEvent>();

    IActionBuilder<T> ValidFromState<TEnum>(TEnum state) where TEnum : struct, Enum;
}
