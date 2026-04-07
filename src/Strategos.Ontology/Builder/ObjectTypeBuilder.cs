using System.Linq.Expressions;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal sealed class ObjectTypeBuilder<T>(string domainName) : IObjectTypeBuilder<T>
    where T : class
{
    private PropertyDescriptor? _keyProperty;
    private readonly List<PropertyBuilder<T>> _propertyBuilders = [];
    private readonly List<LinkBuilder> _linkBuilders = [];
    private readonly List<ActionBuilder<T>> _actionBuilders = [];
    private readonly List<ActionDescriptor> _defaultActionDescriptors = [];
    private readonly List<EventDescriptor> _events = [];
    private readonly List<InterfaceDescriptor> _interfaces = [];
    private readonly List<InterfaceActionMapping> _interfaceActionMappings = [];
    private readonly List<InterfacePropertyMapping> _interfacePropertyMappings = [];
    private readonly List<ExtensionPointBuilder> _extensionPointBuilders = [];
    private LifecycleDescriptor? _lifecycle;
    private ObjectKind _objectKind = ObjectKind.Entity;
    private Type? _parentType;

    public void Key(Expression<Func<T, object>> keySelector)
    {
        var memberName = ExpressionHelper.ExtractMemberName(keySelector);
        var memberType = ExpressionHelper.ExtractMemberType(keySelector);
        _keyProperty = new PropertyDescriptor(memberName, memberType);
    }

    public void Kind(ObjectKind kind)
    {
        _objectKind = kind;
    }

    public IPropertyBuilder<T> Property(Expression<Func<T, object>> propertySelector)
    {
        var memberName = ExpressionHelper.ExtractMemberName(propertySelector);
        var memberType = ExpressionHelper.ExtractMemberType(propertySelector);
        var builder = new PropertyBuilder<T>(memberName, memberType);
        _propertyBuilders.Add(builder);
        return builder;
    }

    public ILinkBuilder HasOne<TLinked>(string linkName)
    {
        var linkBuilder = new LinkBuilder(new LinkDescriptor(linkName, typeof(TLinked).Name, LinkCardinality.OneToOne));
        _linkBuilders.Add(linkBuilder);
        return linkBuilder;
    }

    public ILinkBuilder HasMany<TLinked>(string linkName)
    {
        var linkBuilder = new LinkBuilder(new LinkDescriptor(linkName, typeof(TLinked).Name, LinkCardinality.OneToMany));
        _linkBuilders.Add(linkBuilder);
        return linkBuilder;
    }

    public void ManyToMany<TLinked>(string linkName)
    {
        _linkBuilders.Add(new LinkBuilder(new LinkDescriptor(linkName, typeof(TLinked).Name, LinkCardinality.ManyToMany)));
    }

    public void ManyToMany<TLinked>(string linkName, Action<IEdgeBuilder> edgeConfig)
    {
        var edgeBuilder = new EdgeBuilder();
        edgeConfig(edgeBuilder);

        _linkBuilders.Add(new LinkBuilder(new LinkDescriptor(linkName, typeof(TLinked).Name, LinkCardinality.ManyToMany)
        {
            EdgeProperties = edgeBuilder.Build(),
        }));
    }

    public IActionBuilder<T> Action(string actionName)
    {
        var builder = new ActionBuilder<T>(actionName);
        _actionBuilders.Add(builder);
        return builder;
    }

    public void Event<TEvent>(Action<IEventBuilder<TEvent>> configure)
    {
        var builder = new EventBuilder<TEvent>();
        configure(builder);
        _events.Add(builder.Build());
    }

    public void Implements<TInterface>(Action<IInterfaceMapping<T, TInterface>> configure)
    {
        var mapping = new InterfaceMapping<T, TInterface>();
        configure(mapping);
        _interfaces.Add(new InterfaceDescriptor(typeof(TInterface).Name, typeof(TInterface)));
        _interfaceActionMappings.AddRange(mapping.GetActionMappings());

        foreach (var (sourceName, targetName) in mapping.GetMappings())
        {
            _interfacePropertyMappings.Add(new InterfacePropertyMapping(
                sourceName, targetName, typeof(TInterface).Name));
        }

        // Register default actions directly to preserve full metadata
        _defaultActionDescriptors.AddRange(mapping.GetDefaultActions());
    }

    public void Lifecycle<TEnum>(
        Expression<Func<T, object>> propertySelector,
        Action<ILifecycleBuilder<TEnum>> configure)
        where TEnum : struct, Enum
    {
        var propertyName = ExpressionHelper.ExtractMemberName(propertySelector);
        var lifecycleBuilder = new LifecycleBuilder<TEnum>();
        configure(lifecycleBuilder);
        var descriptor = lifecycleBuilder.Build();
        _lifecycle = descriptor with { PropertyName = propertyName };
    }

    public void AcceptsExternalLinks(string name, Action<IExtensionPointBuilder> configure)
    {
        var builder = new ExtensionPointBuilder(name);
        configure(builder);
        _extensionPointBuilders.Add(builder);
    }

    public void IsA<TParent>() where TParent : class
    {
        _parentType = typeof(TParent);
    }

    public ObjectTypeDescriptor Build()
    {
        var actions = _actionBuilders.ConvertAll(b => b.Build());
        actions.AddRange(_defaultActionDescriptors);

        ProjectValidFromStatesIntoLifecycle();

        return new(typeof(T).Name, typeof(T), domainName)
        {
            Kind = _objectKind,
            KeyProperty = _keyProperty,
            Properties = _propertyBuilders.ConvertAll(b => b.Build()).AsReadOnly(),
            Links = _linkBuilders.ConvertAll(b => b.Build()).AsReadOnly(),
            Actions = actions.AsReadOnly(),
            Events = _events.AsReadOnly(),
            ImplementedInterfaces = _interfaces.AsReadOnly(),
            Lifecycle = _lifecycle,
            InterfaceActionMappings = _interfaceActionMappings.AsReadOnly(),
            InterfacePropertyMappings = _interfacePropertyMappings.AsReadOnly(),
            ParentType = _parentType,
            ParentTypeName = _parentType?.Name,
            ExternalLinkExtensionPoints = _extensionPointBuilders.ConvertAll(b => b.Build()).AsReadOnly(),
        };
    }

    /// <summary>
    /// Projects every <see cref="ActionBuilder{T}.ValidFromStates"/> declaration into a
    /// self-loop <see cref="LifecycleTransitionDescriptor"/> on the current lifecycle.
    /// Each (action, state) pair becomes
    /// <c>{ FromState = state, ToState = state, TriggerActionName = action.Name }</c>,
    /// which is what <see cref="Strategos.Ontology.Query.OntologyQueryService.GetActionsForState"/>
    /// reads — no read-side changes are required (Track A — 2.4.0).
    /// </summary>
    /// <remarks>
    /// AONT017 (LifecycleTransitionBadState) is a Roslyn analyzer that walks source-level
    /// transitions only and cannot see ValidFromState desugaring. A runtime guard is therefore
    /// added here so users get a clear error pointing back to ValidFromState rather than a
    /// downstream OntologyGraphBuilder failure.
    /// </remarks>
    private void ProjectValidFromStatesIntoLifecycle()
    {
        if (_lifecycle is null || !_actionBuilders.Any(ab => ab.ValidFromStates.Count > 0))
        {
            return;
        }

        var declaredStateNames = _lifecycle.States.Select(s => s.Name).ToHashSet();
        var augmentedTransitions = new List<LifecycleTransitionDescriptor>(_lifecycle.Transitions);
        foreach (var actionBuilder in _actionBuilders)
        {
            foreach (var stateName in actionBuilder.ValidFromStates)
            {
                if (!declaredStateNames.Contains(stateName))
                {
                    throw new InvalidOperationException(
                        $"Action '{actionBuilder.Name}' on '{typeof(T).Name}' declares ValidFromState '{stateName}', " +
                        $"but that state is not declared in the lifecycle. " +
                        $"Declared states: {string.Join(", ", declaredStateNames)}.");
                }

                augmentedTransitions.Add(new LifecycleTransitionDescriptor
                {
                    FromState = stateName,
                    ToState = stateName,
                    TriggerActionName = actionBuilder.Name,
                });
            }
        }

        _lifecycle = _lifecycle with { Transitions = augmentedTransitions.AsReadOnly() };
    }
}
