using System.Linq.Expressions;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

public interface IObjectTypeBuilder<T>
    where T : class
{
    void Key(Expression<Func<T, object>> keySelector);

    IPropertyBuilder<T> Property(Expression<Func<T, object>> propertySelector);

    void Kind(ObjectKind kind);

    ILinkBuilder HasOne<TLinked>(string linkName);

    ILinkBuilder HasMany<TLinked>(string linkName);

    void ManyToMany<TLinked>(string linkName);

    void ManyToMany<TLinked>(string linkName, Action<IEdgeBuilder> edgeConfig);

    IActionBuilder<T> Action(string actionName);

    void Event<TEvent>(Action<IEventBuilder<TEvent>> configure);

    void Implements<TInterface>(Action<IInterfaceMapping<T, TInterface>> configure);

    void Lifecycle<TEnum>(
        Expression<Func<T, object>> propertySelector,
        Action<ILifecycleBuilder<TEnum>> configure)
        where TEnum : struct, Enum;

    void AcceptsExternalLinks(string name, Action<IExtensionPointBuilder> configure);

    void IsA<TParent>() where TParent : class;
}
