using System.Linq.Expressions;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

/// <summary>
/// Builds a reified association descriptor (DR-4). Mirrors
/// <see cref="ObjectTypeBuilder{T}"/>'s idioms — a key selector compiled to the
/// reflection-free DR-1 <see cref="ObjectTypeDescriptor.IdAccessor"/>, plus its
/// own edge-attribute properties — and adds the two typed endpoints.
/// </summary>
/// <remarks>
/// INV-8: <see cref="Between{TLeft}"/>/<see cref="And{TRight}"/> read only the
/// endpoint type's <c>Name</c> (mirroring <c>HasMany&lt;TLinked&gt;</c>) and the
/// selector's member name; the produced <see cref="AssociationEndpoint"/>
/// carries no CLR <see cref="System.Type"/>. INV-6: this builder is
/// <c>sealed</c>. INV-7: it emits an immutable <see cref="ObjectTypeDescriptor"/>.
/// </remarks>
internal sealed class AssociationBuilder<TRel>
    : IAssociationBuilder<TRel>, IAssociationEndpointBuilder<TRel>, IAssociationRightEndpointBuilder<TRel>
    where TRel : class
{
    private readonly string _domainName;
    private readonly string _name;
    private readonly List<PropertyBuilder<TRel>> _propertyBuilders = [];
    private readonly List<PropertyDescriptor> _namedProperties = [];

    private PropertyDescriptor? _keyProperty;
    private Func<object, object?>? _idAccessor;
    private AssociationEndpoint? _left;
    private AssociationEndpoint? _right;

    public AssociationBuilder(string domainName, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domainName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _domainName = domainName;
        _name = name;
    }

    public void Key(Expression<Func<TRel, object>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        var memberName = ExpressionHelper.ExtractMemberName(keySelector);
        var memberType = ExpressionHelper.ExtractMemberType(keySelector);
        _keyProperty = new PropertyDescriptor(memberName, memberType);

        // Compile the key selector once and box it to the erased accessor shape,
        // so the descriptor projects an instance's id with zero per-call
        // reflection on the instance type (INV-8) — identical to ObjectTypeBuilder.
        var compiled = keySelector.Compile();
        _idAccessor = o => compiled((TRel)o);
    }

    public IAssociationEndpointBuilder<TRel> Between<TLeft>(Expression<Func<TRel, TLeft>> endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var role = ExpressionHelper.ExtractMemberName(endpoint);
        _left = new AssociationEndpoint(role, typeof(TLeft).Name);
        return this;
    }

    public IAssociationRightEndpointBuilder<TRel> And<TRight>(Expression<Func<TRel, TRight>> endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var role = ExpressionHelper.ExtractMemberName(endpoint);
        _right = new AssociationEndpoint(role, typeof(TRight).Name);
        return this;
    }

    // DR-6: the LEFT endpoint's cardinality. WithCardinality on the
    // IAssociationEndpointBuilder step (returned by Between) reshapes _left.
    IAssociationEndpointBuilder<TRel> IAssociationEndpointBuilder<TRel>.WithCardinality(
        EndpointCardinality cardinality)
    {
        if (_left is not null)
        {
            _left = _left with { Cardinality = cardinality };
        }

        return this;
    }

    // DR-6: the RIGHT endpoint's cardinality. WithCardinality on the
    // IAssociationRightEndpointBuilder step (returned by And) reshapes _right.
    IAssociationRightEndpointBuilder<TRel> IAssociationRightEndpointBuilder<TRel>.WithCardinality(
        EndpointCardinality cardinality)
    {
        if (_right is not null)
        {
            _right = _right with { Cardinality = cardinality };
        }

        return this;
    }

    public IPropertyBuilder<TRel> Property(Expression<Func<TRel, object>> propertySelector)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);
        var memberName = ExpressionHelper.ExtractMemberName(propertySelector);
        var memberType = ExpressionHelper.ExtractMemberType(propertySelector);
        var builder = new PropertyBuilder<TRel>(memberName, memberType);
        _propertyBuilders.Add(builder);
        return builder;
    }

    public IAssociationBuilder<TRel> Property<TProp>(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _namedProperties.Add(new PropertyDescriptor(name, typeof(TProp)));
        return this;
    }

    public ObjectTypeDescriptor Build()
    {
        if (_left is null || _right is null)
        {
            throw new InvalidOperationException(
                $"Association '{_domainName}.{_name}' must declare both endpoints via "
                + "Between<L>(...).And<R>(...) before the ontology is built.");
        }

        var properties = _propertyBuilders.ConvertAll(b => b.Build());
        properties.AddRange(_namedProperties);

        return new ObjectTypeDescriptor(_name, typeof(TRel), _domainName)
        {
            Kind = ObjectKind.Association,
            KeyProperty = _keyProperty,
            IdAccessor = _idAccessor,
            Properties = properties.AsReadOnly(),
            AssociationEndpoints = new[] { _left, _right }.AsReadOnly(),
        };
    }
}
