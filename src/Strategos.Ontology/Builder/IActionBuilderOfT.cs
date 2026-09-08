using System.Linq.Expressions;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

public interface IActionBuilder<T> : IActionBuilder
    where T : class
{
    new IActionBuilder<T> Description(string description);

    new IActionBuilder<T> Accepts<TAccepts>();

    new IActionBuilder<T> Returns<TReturns>();

    new IActionBuilder<T> BoundToWorkflow(string workflowName);

    new IActionBuilder<T> BoundToWorkflow(WorkflowBindingReference workflow);

    new IActionBuilder<T> BoundToTool(string toolName, string methodName);

    /// <summary>
    /// Marks the action as read-only. Read-only actions are dispatchable via
    /// <see cref="Strategos.Ontology.Actions.IActionDispatcher.DispatchReadOnlyAsync"/>
    /// and may not declare write postconditions; the analyzer enforces both
    /// invariants at compile time.
    /// </summary>
    /// <returns>The same generic builder instance for fluent chaining.</returns>
    new IActionBuilder<T> ReadOnly();

    /// <summary>
    /// Marks the action as safe to repeat without changing its externally
    /// observable effect.
    /// </summary>
    /// <returns>The same generic builder instance for fluent chaining.</returns>
    new IActionBuilder<T> Idempotent();

    /// <summary>
    /// Requires the named domain authority to invoke this action.
    /// </summary>
    new IActionBuilder<T> RequiresAuthority(string authorityName);

    /// <summary>Adds a resource to the action's declared frame.</summary>
    new IActionBuilder<T> Touches(ActionResource resource);

    /// <summary>Names the action that restores this action's declared frame.</summary>
    new IActionBuilder<T> CompensatedBy(string actionName);

    IActionBuilder<T> BoundToTool<TTool>(Expression<Func<TTool, Delegate>> methodSelector);

    /// <summary>Adds a hard typed precondition.</summary>
    new IActionBuilder<T> Requires(ActionPredicate predicate, string? description = null);

    /// <summary>
    /// Declares a hard property-predicate precondition on the action.
    /// </summary>
    /// <param name="predicate">Expression evaluated against the object instance.</param>
    /// <returns>The same generic builder instance for fluent chaining.</returns>
    IActionBuilder<T> Requires(Expression<Func<T, bool>> predicate);

    /// <summary>Adds a soft typed precondition.</summary>
    new IActionBuilder<T> RequiresSoft(ActionPredicate predicate, string? description = null);

    IActionBuilder<T> RequiresSoft(Expression<Func<T, bool>> predicate);

    /// <summary>Adds an explicit typed post-state guarantee.</summary>
    new IActionBuilder<T> Ensures(ActionPredicate predicate, string? description = null);

    /// <summary>Adds an explicit expression-based post-state guarantee.</summary>
    IActionBuilder<T> Ensures(Expression<Func<T, bool>> predicate);

    new IActionBuilder<T> RequiresLink(string linkName);

    new IActionBuilder<T> RequiresLinkSoft(string linkName);

    /// <summary>
    /// Declares that the calling principal must be reachable from the action
    /// target by following <paramref name="linkPath"/> and then the named
    /// <paramref name="relationName"/>.
    /// </summary>
    /// <param name="relationName">Final link from the selected resource to the principal.</param>
    /// <param name="linkPath">Ordered links from the action target to that resource.</param>
    /// <returns>The same generic builder instance for fluent chaining.</returns>
    new IActionBuilder<T> RequiresRelation(string relationName, params string[] linkPath);

    new IActionBuilder<T> EnsuresLink(string linkName);

    new IActionBuilder<T> EnsuresRelation(string relationName, params string[] linkPath);

    IActionBuilder<T> Modifies(Expression<Func<T, object>> propertySelector);

    IActionBuilder<T> CreatesLinked<TTarget>(string linkName);

    IActionBuilder<T> EmitsEvent<TEvent>();

    IActionBuilder<T> ValidFromState<TEnum>(TEnum state) where TEnum : struct, Enum;
}
