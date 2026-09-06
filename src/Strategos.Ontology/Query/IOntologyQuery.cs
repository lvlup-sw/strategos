using Strategos.Ontology.Actions;
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
    /// <summary>
    /// Returns actions that are available or indeterminate using an empty fact
    /// set. Actions proven unavailable are excluded.
    /// </summary>
    IReadOnlyList<ActionCandidateEvaluation> GetCandidateActions(string objectType) =>
        GetCandidateActions(objectType, facts: null);

    IReadOnlyList<ActionCandidateEvaluation> GetCandidateActions(
        string objectType,
        ActionFacts? facts);

    /// <summary>
    /// Returns available and indeterminate candidates for a domain-qualified
    /// object type, excluding actions proven unavailable.
    /// </summary>
    IReadOnlyList<ActionCandidateEvaluation> GetCandidateActions(
        string domain,
        string objectType) =>
        GetCandidateActions(domain, objectType, facts: null);

    /// <summary>
    /// Returns available and indeterminate candidates for a domain-qualified
    /// object type, excluding actions proven unavailable.
    /// </summary>
    IReadOnlyList<ActionCandidateEvaluation> GetCandidateActions(
        string domain,
        string objectType,
        ActionFacts? facts) =>
        GetCandidateActions(objectType, facts);

    IReadOnlyList<ActionDescriptor> GetValidActions(
        string objectType,
        ActionFacts? facts = null);

    /// <summary>
    /// Returns actions whose hard requirements are not proven false for a
    /// specific target instance. Indeterminate actions are retained.
    /// </summary>
    /// <param name="principal">Authenticated principal requesting an action.</param>
    /// <param name="domain">Domain that owns the target object type.</param>
    /// <param name="objectType">Target object descriptor name.</param>
    /// <param name="objectId">Target object identifier.</param>
    /// <param name="facts">Optional typed facts for local predicate evaluation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Actions whose hard preconditions are satisfied or indeterminate.</returns>
    Task<IReadOnlyList<ActionDescriptor>> GetValidActionsAsync(
        ActionPrincipal principal,
        string domain,
        string objectType,
        string objectId,
        ActionFacts? facts = null,
        CancellationToken ct = default) =>
        throw new NotSupportedException(
            "Principal-aware action discovery requires a runtime relation resolver.");

    /// <summary>
    /// Returns available and indeterminate candidates, excluding actions proven
    /// unavailable by the supplied discovery facts and evaluators.
    /// </summary>
    Task<IReadOnlyList<ActionCandidateEvaluation>> GetCandidateActionsAsync(
        ActionPrincipal principal,
        string domain,
        string objectType,
        string objectId,
        ActionFacts? facts = null,
        CancellationToken ct = default) =>
        throw new NotSupportedException(
            "Principal-aware action discovery requires runtime predicate evaluators.");

    IReadOnlyList<ActionConstraintReport> GetActionConstraintReport(string objectType) =>
        GetActionConstraintReport(objectType, facts: null);

    IReadOnlyList<ActionConstraintReport> GetActionConstraintReport(
        string objectType,
        ActionFacts? facts);

    /// <summary>
    /// Domain-qualified overload of <see cref="GetActionConstraintReport(string, ActionFacts?)"/>.
    /// Implementations that walk multiple domains should resolve the
    /// descriptor by <c>(domain, objectType)</c> to avoid returning
    /// constraints from a same-named type in a different domain.
    /// </summary>
    /// <param name="domain">Domain that owns <paramref name="objectType"/>.</param>
    /// <param name="objectType">Simple object type name within <paramref name="domain"/>.</param>
    /// <param name="facts">
    /// Optional typed facts consumed by precondition evaluation.
    /// </param>
    /// <returns>
    /// One <see cref="ActionConstraintReport"/> per registered action on the
    /// resolved type; empty when the <c>(domain, objectType)</c> pair is
    /// unknown.
    /// </returns>
    /// <remarks>
    /// Default implementation falls back to the simple-name overload for
    /// backwards compatibility with test doubles that have not been updated;
    /// concrete implementations should override to honor the domain context.
    /// </remarks>
    IReadOnlyList<ActionConstraintReport> GetActionConstraintReport(
        string domain,
        string objectType) =>
        GetActionConstraintReport(domain, objectType, facts: null);

    /// <summary>
    /// Domain-qualified overload accepting explicit typed facts.
    /// </summary>
    IReadOnlyList<ActionConstraintReport> GetActionConstraintReport(
        string domain,
        string objectType,
        ActionFacts? facts)
        => GetActionConstraintReport(objectType, facts);

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
