namespace Strategos.Ontology.Generators.Diagnostics;

internal static class OntologyDiagnosticIds
{
    // Core (AONT001-008)
    public const string MissingKey = "AONT001";
    public const string InvalidPropertyExpression = "AONT002";
    public const string LinkTargetNotRegistered = "AONT003";
    public const string ActionNotBound = "AONT004";
    public const string InterfaceMappingBadProperty = "AONT005";
    public const string DuplicateObjectType = "AONT006";
    public const string CrossDomainLinkUnverifiable = "AONT007";
    public const string EdgeTypeMissingProperty = "AONT008";

    // Preconditions (AONT009-013)
    public const string EmitsEventUndeclared = "AONT009";
    public const string ModifiesUndeclaredProperty = "AONT010";
    public const string CreatesLinkedUndeclared = "AONT011";
    public const string RequiresLinkUndeclared = "AONT012";
    public const string PostconditionOverlapsEvent = "AONT013";

    // Lifecycle (AONT014-021)
    public const string LifecyclePropertyUndeclared = "AONT014";
    public const string LifecycleInitialStateCount = "AONT015";
    public const string LifecycleNoTerminalState = "AONT016";
    public const string LifecycleTransitionBadState = "AONT017";
    public const string LifecycleTransitionBadAction = "AONT018";
    public const string LifecycleTransitionBadEvent = "AONT019";
    public const string LifecycleUnreachableState = "AONT020";
    public const string LifecycleDeadEndState = "AONT021";

    // Derivation (AONT022-026)
    public const string DerivedFromUndeclaredProperty = "AONT022";
    public const string DerivedFromNonComputed = "AONT023";
    public const string DerivationCycle = "AONT024";
    public const string DerivedFromExternalUnresolvable = "AONT025";
    public const string ComputedNoDerivedFrom = "AONT026";

    // Interface Actions (AONT027-030)
    public const string InterfaceActionUnmapped = "AONT027";
    public const string ActionViaBadReference = "AONT028";
    public const string InterfaceActionIncompatible = "AONT029";
    public const string InterfaceActionNoImplementors = "AONT030";

    // Extension Points (AONT031-035)
    public const string CrossDomainLinkNoExtensionPoint = "AONT031";
    public const string ExtensionPointInterfaceUnsatisfied = "AONT032";
    public const string ExtensionPointEdgeMissing = "AONT033";
    public const string ExtensionPointNoLinks = "AONT034";
    public const string ExtensionPointMaxLinksExceeded = "AONT035";
}
