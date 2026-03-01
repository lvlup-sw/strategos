using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Query;

public interface IOntologyQuery
{
    // Core queries
    IReadOnlyList<ObjectTypeDescriptor> GetObjectTypes(string? domain = null, string? implementsInterface = null);

    IReadOnlyList<ActionDescriptor> GetActions(string objectType);

    IReadOnlyList<LinkDescriptor> GetLinks(string objectType);

    IReadOnlyList<ObjectTypeDescriptor> GetImplementors(string interfaceName);

    // Precondition & Postcondition queries (§4.14.5)
    IReadOnlyList<ActionDescriptor> GetValidActions(
        string objectType,
        IReadOnlyDictionary<string, object?>? knownProperties = null);

    IReadOnlyList<PostconditionTrace> TracePostconditions(
        string objectType, string actionName, int maxDepth = 1);

    // Lifecycle queries (§4.14.6)
    IReadOnlyList<ActionDescriptor> GetActionsForState(
        string objectType, string stateName);

    IReadOnlyList<LifecycleTransitionDescriptor> GetTransitionsFrom(
        string objectType, string stateName);

    // Derivation queries (§4.14.7)
    IReadOnlyList<AffectedProperty> GetAffectedProperties(
        string objectType, string propertyName);

    IReadOnlyList<DerivationSource> GetDerivationChain(
        string objectType, string propertyName);

    // Interface Action queries (§4.14.8)
    IReadOnlyList<InterfaceActionDescriptor> GetInterfaceActions(
        string interfaceName);

    ActionDescriptor? ResolveInterfaceAction(
        string objectType, string interfaceActionName);

    // Extension Point queries (§4.14.9)
    IReadOnlyList<ExternalLinkExtensionPoint> GetExtensionPoints(
        string objectType);

    IReadOnlyList<ResolvedCrossDomainLink> GetIncomingCrossDomainLinks(
        string objectType);
}
