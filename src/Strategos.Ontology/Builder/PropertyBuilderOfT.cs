using System.Linq.Expressions;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal sealed class PropertyBuilder<T>(string name, Type propertyType) : IPropertyBuilder<T>
    where T : class
{
    private bool _isRequired;
    private bool _isComputed;
    private int? _vectorDimensions;
    private readonly List<DerivationSource> _derivationSources = [];

    IPropertyBuilder IPropertyBuilder.Required() => Required();
    IPropertyBuilder IPropertyBuilder.Computed() => Computed();
    IPropertyBuilder IPropertyBuilder.Vector(int dimensions) => Vector(dimensions);

    public IPropertyBuilder<T> Required()
    {
        _isRequired = true;
        return this;
    }

    public IPropertyBuilder<T> Computed()
    {
        _isComputed = true;
        return this;
    }

    public IPropertyBuilder<T> DerivedFrom(params Expression<Func<T, object>>[] sources)
    {
        foreach (var source in sources)
        {
            var memberName = ExpressionHelper.ExtractMemberName(source);
            _derivationSources.Add(new DerivationSource
            {
                Kind = DerivationSourceKind.Local,
                PropertyName = memberName,
            });
        }

        return this;
    }

    public IPropertyBuilder<T> Vector(int dimensions)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dimensions, 1);

        _vectorDimensions = dimensions;
        return this;
    }

    public IPropertyBuilder<T> DerivedFromExternal(string domain, string objectType, string property)
    {
        _derivationSources.Add(new DerivationSource
        {
            Kind = DerivationSourceKind.External,
            ExternalDomain = domain,
            ExternalObjectType = objectType,
            ExternalPropertyName = property,
        });
        return this;
    }

    public PropertyDescriptor Build() =>
        new(name, propertyType, _isRequired, _isComputed)
        {
            Kind = _vectorDimensions.HasValue ? PropertyKind.Vector : PropertyKind.Scalar,
            VectorDimensions = _vectorDimensions,
            DerivedFrom = _derivationSources.ToList().AsReadOnly(),
        };
}
