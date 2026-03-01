namespace Strategos.Ontology.Query;

public sealed record AffectedProperty(
    string Domain,
    string ObjectTypeName,
    string PropertyName,
    bool IsDirect);
