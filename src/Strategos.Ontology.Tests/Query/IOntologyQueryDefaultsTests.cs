using Strategos.Ontology.Descriptors;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests.Query;

public class IOntologyQueryDefaultsTests
{
    [Test]
    public async Task EstimateBlastRadius_OnNonImplementingQuery_ThrowsNotSupported()
    {
        IOntologyQuery query = new MinimalOntologyQueryStub();
        var touchedNodes = new List<OntologyNodeRef>
        {
            new("trading", "Order", "ord-1"),
        };

        await Assert.That(() => query.EstimateBlastRadius(touchedNodes))
            .ThrowsException()
            .WithExceptionType(typeof(NotSupportedException));

        await Assert.That(() => query.EstimateBlastRadius(touchedNodes))
            .ThrowsException()
            .WithMessageContaining("EstimateBlastRadius");
    }

    [Test]
    public async Task DetectPatternViolations_OnNonImplementingQuery_ThrowsNotSupported()
    {
        IOntologyQuery query = new MinimalOntologyQueryStub();
        var node = new OntologyNodeRef("trading", "Order", "ord-1");
        var intent = new DesignIntent(
            AffectedNodes: [node],
            Actions: [],
            KnownProperties: null);

        await Assert.That(() => query.DetectPatternViolations([node], intent))
            .ThrowsException()
            .WithExceptionType(typeof(NotSupportedException));

        await Assert.That(() => query.DetectPatternViolations([node], intent))
            .ThrowsException()
            .WithMessageContaining("DetectPatternViolations");
    }

    // Hand-written stub: implements every non-default member of IOntologyQuery
    // and explicitly does NOT override EstimateBlastRadius / DetectPatternViolations,
    // so the interface's default body is exercised. NSubstitute is unsuitable here —
    // it generates an override for every interface member, which would mask the
    // default impl we are trying to test.
    private sealed class MinimalOntologyQueryStub : IOntologyQuery
    {
        public IReadOnlyList<ObjectTypeDescriptor> GetObjectTypes(
            string? domain = null,
            string? implementsInterface = null,
            bool includeSubtypes = false) => [];

        public IReadOnlyList<ActionDescriptor> GetActions(string objectType) => [];

        public IReadOnlyList<LinkDescriptor> GetLinks(string objectType) => [];

        public IReadOnlyList<ObjectTypeDescriptor> GetImplementors(string interfaceName) => [];

        public IReadOnlyList<ActionDescriptor> GetValidActions(
            string objectType,
            IReadOnlyDictionary<string, object?>? knownProperties = null) => [];

        public IReadOnlyList<ActionConstraintReport> GetActionConstraintReport(
            string objectType,
            IReadOnlyDictionary<string, object?>? knownProperties = null) => [];

        public IReadOnlyList<PostconditionTrace> TracePostconditions(
            string objectType, string actionName, int maxDepth = 1) => [];

        public IReadOnlyList<ActionDescriptor> GetActionsForState(
            string objectType, string stateName) => [];

        public IReadOnlyList<LifecycleTransitionDescriptor> GetTransitionsFrom(
            string objectType, string stateName) => [];

        public IReadOnlyList<AffectedProperty> GetAffectedProperties(
            string objectType, string propertyName) => [];

        public IReadOnlyList<DerivationSource> GetDerivationChain(
            string objectType, string propertyName) => [];

        public IReadOnlyList<InterfaceActionDescriptor> GetInterfaceActions(
            string interfaceName) => [];

        public ActionDescriptor? ResolveInterfaceAction(
            string objectType, string interfaceActionName) => null;

        public IReadOnlyList<LinkDescriptor> GetInverseLinks(
            string objectType, string linkName) => [];

        public IReadOnlyList<ExternalLinkExtensionPoint> GetExtensionPoints(
            string objectType) => [];

        public IReadOnlyList<ResolvedCrossDomainLink> GetIncomingCrossDomainLinks(
            string objectType) => [];

        public ObjectSet<T> GetObjectSet<T>(string objectType) where T : class =>
            throw new NotImplementedException();

        public IReadOnlyList<string> GetObjectTypeNames<T>() where T : class => [];
    }
}
