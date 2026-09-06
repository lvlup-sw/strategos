using System.Linq.Expressions;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal sealed class InterfaceMapping<TObject, TInterface> : IInterfaceMapping<TObject, TInterface>
{
    private readonly List<(string SourceName, string TargetName)> _mappings = [];
    private readonly List<InterfaceActionMapping> _actionMappings = [];
    private readonly List<ActionBuilder> _defaultActionBuilders = [];

    public IInterfaceMapping<TObject, TInterface> Via(
        Expression<Func<TObject, object>> source,
        Expression<Func<TInterface, object>> target)
    {
        var sourceName = ExpressionHelper.ExtractMemberName(source);
        var targetName = ExpressionHelper.ExtractMemberName(target);
        _mappings.Add((sourceName, targetName));
        return this;
    }

    public IInterfaceMapping<TObject, TInterface> ActionVia(
        string interfaceActionName,
        string concreteActionName)
    {
        _actionMappings.Add(new InterfaceActionMapping
        {
            InterfaceActionName = interfaceActionName,
            ConcreteActionName = concreteActionName,
        });
        return this;
    }

    public IInterfaceMapping<TObject, TInterface> ActionDefault(
        string interfaceActionName,
        Action<IActionBuilder> configure)
    {
        // Create a default action with a generated name
        var concreteActionName = $"__{typeof(TInterface).Name}_{interfaceActionName}";
        var actionBuilder = new ActionBuilder(concreteActionName);
        configure(actionBuilder);
        _defaultActionBuilders.Add(actionBuilder);

        _actionMappings.Add(new InterfaceActionMapping
        {
            InterfaceActionName = interfaceActionName,
            ConcreteActionName = concreteActionName,
        });
        return this;
    }

    public IReadOnlyList<(string SourceName, string TargetName)> GetMappings() =>
        _mappings.AsReadOnly();

    public IReadOnlyList<InterfaceActionMapping> GetActionMappings() =>
        _actionMappings.AsReadOnly();

    public IReadOnlyList<ActionDescriptor> GetDefaultActions(ActionSubject subject) =>
        _defaultActionBuilders.ConvertAll(b => b.Build(subject)).AsReadOnly();
}
