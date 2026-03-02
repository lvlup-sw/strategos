using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal sealed class PropertyBuilder(string name, Type propertyType) : IPropertyBuilder
{
    private bool _isRequired;
    private bool _isComputed;
    private int? _vectorDimensions;

    public IPropertyBuilder Required()
    {
        _isRequired = true;
        return this;
    }

    public IPropertyBuilder Computed()
    {
        _isComputed = true;
        return this;
    }

    public IPropertyBuilder Vector(int dimensions)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dimensions, 1);
        _vectorDimensions = dimensions;
        return this;
    }

    public PropertyDescriptor Build() =>
        new(name, propertyType, _isRequired, _isComputed)
        {
            Kind = _vectorDimensions.HasValue ? PropertyKind.Vector : PropertyKind.Scalar,
            VectorDimensions = _vectorDimensions,
        };
}
