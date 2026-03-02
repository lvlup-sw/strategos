namespace Strategos.Ontology.Builder;

public interface IPropertyBuilder
{
    IPropertyBuilder Required();

    IPropertyBuilder Computed();

    IPropertyBuilder Vector(int dimensions);
}
