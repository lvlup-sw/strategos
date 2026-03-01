using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Query;

public sealed record PostconditionTrace(
    string ActionName,
    ActionPostcondition Postcondition,
    string? AffectedObjectType = null);
