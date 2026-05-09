using Strategos.Ontology.Descriptors;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Query;

public interface IOntologyQuery
{
    // Core queries
    IReadOnlyList<ObjectTypeDescriptor> GetObjectTypes(
        string? domain = null,
        string? implementsInterface = null,
        bool includeSubtypes = false);

    IReadOnlyList<ActionDescriptor> GetActions(string objectType);

    IReadOnlyList<LinkDescriptor> GetLinks(string objectType);

    IReadOnlyList<ObjectTypeDescriptor> GetImplementors(string interfaceName);

    // Precondition & Postcondition queries (§4.14.5)
    IReadOnlyList<ActionDescriptor> GetValidActions(
        string objectType,
        IReadOnlyDictionary<string, object?>? knownProperties = null);

    IReadOnlyList<ActionConstraintReport> GetActionConstraintReport(
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

    // Inverse Link queries
    IReadOnlyList<LinkDescriptor> GetInverseLinks(string objectType, string linkName);

    // Extension Point queries (§4.14.9)
    IReadOnlyList<ExternalLinkExtensionPoint> GetExtensionPoints(
        string objectType);

    IReadOnlyList<ResolvedCrossDomainLink> GetIncomingCrossDomainLinks(
        string objectType);

    // Object set queries
    ObjectSet<T> GetObjectSet<T>(string objectType) where T : class;

    /// <summary>
    /// Returns all descriptor names registered for the given CLR type across
    /// the composed ontology, in registration order. Returns an empty list if
    /// <typeparamref name="T"/> is not registered. Enables consumers (e.g. Basileus)
    /// to enumerate per-collection partitions of a shared content-carrier type
    /// without hardcoding descriptor names at call sites.
    /// </summary>
    IReadOnlyList<string> GetObjectTypeNames<T>() where T : class;

    /// <summary>
    /// Estimates the downstream blast radius for the supplied seed nodes by
    /// walking links, derivation chains, postconditions, and cross-domain
    /// links until <see cref="BlastRadiusOptions.MaxExpansionDegree"/> is
    /// reached.
    /// </summary>
    /// <param name="touchedNodes">Seed nodes to begin expansion from.</param>
    /// <param name="options">
    /// Traversal options controlling depth limits; defaults are applied when null.
    /// </param>
    /// <returns>
    /// A <see cref="BlastRadius"/> with deterministically ordered affected
    /// nodes, cross-domain hops, and a classified scope.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown by the default interface implementation; concrete query types
    /// (e.g. <c>OntologyQueryService</c>) must override.
    /// </exception>
    BlastRadius EstimateBlastRadius(
        IReadOnlyList<OntologyNodeRef> touchedNodes,
        BlastRadiusOptions? options = null)
        => throw new NotSupportedException(
            "EstimateBlastRadius is not implemented by this IOntologyQuery; consult the design ADR for the reference algorithm.");

    /// <summary>
    /// Detects ontology pattern violations for the supplied affected nodes
    /// and design intent.
    /// </summary>
    /// <param name="affectedNodes">Nodes within scope of validation.</param>
    /// <param name="intent">The design intent driving validation.</param>
    /// <returns>Detected violations; empty when no patterns fire.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown by the default interface implementation; concrete query types
    /// (e.g. <c>OntologyQueryService</c>) must override.
    /// </exception>
    IReadOnlyList<PatternViolation> DetectPatternViolations(
        IReadOnlyList<OntologyNodeRef> affectedNodes,
        DesignIntent intent)
        => throw new NotSupportedException(
            "DetectPatternViolations is not implemented by this IOntologyQuery; consult the design ADR for the reference algorithm.");
}
