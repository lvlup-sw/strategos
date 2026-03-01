using Microsoft.CodeAnalysis;

namespace Strategos.Ontology.Generators.Diagnostics;

internal static class OntologyDiagnostics
{
    private const string Category = "Strategos.Ontology";

    // --- Core (AONT001-008) ---

    public static readonly DiagnosticDescriptor MissingKey = new(
        OntologyDiagnosticIds.MissingKey,
        "Object type missing Key() declaration",
        "Object type '{0}' does not declare a Key() property",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidPropertyExpression = new(
        OntologyDiagnosticIds.InvalidPropertyExpression,
        "Property expression not a simple member access",
        "Property expression in '{0}' is not a simple member access (p => p.Property)",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LinkTargetNotRegistered = new(
        OntologyDiagnosticIds.LinkTargetNotRegistered,
        "Link target type not registered in same domain",
        "Link '{0}' on '{1}' references type '{2}' which is not registered in the same domain",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ActionNotBound = new(
        OntologyDiagnosticIds.ActionNotBound,
        "Action not bound to any workflow or tool",
        "Action '{0}' on '{1}' is not bound to a workflow or tool",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InterfaceMappingBadProperty = new(
        OntologyDiagnosticIds.InterfaceMappingBadProperty,
        "Interface mapping references non-existent property",
        "Interface mapping on '{0}' references property '{1}' which is not declared",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateObjectType = new(
        OntologyDiagnosticIds.DuplicateObjectType,
        "Duplicate Object Type name within domain",
        "Object type '{0}' is declared multiple times in domain '{1}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CrossDomainLinkUnverifiable = new(
        OntologyDiagnosticIds.CrossDomainLinkUnverifiable,
        "Cross-domain link target cannot be validated locally",
        "Cross-domain link '{0}' targets '{1}' in domain '{2}' which cannot be validated at compile time",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EdgeTypeMissingProperty = new(
        OntologyDiagnosticIds.EdgeTypeMissingProperty,
        "Edge type missing required Property() declarations",
        "Edge on link '{0}' on '{1}' has no property declarations",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // --- Preconditions (AONT009-013) ---

    public static readonly DiagnosticDescriptor EmitsEventUndeclared = new(
        OntologyDiagnosticIds.EmitsEventUndeclared,
        "EmitsEvent<T>() references undeclared event",
        "Action '{0}' on '{1}' emits event '{2}' which is not declared via obj.Event<T>()",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ModifiesUndeclaredProperty = new(
        OntologyDiagnosticIds.ModifiesUndeclaredProperty,
        "Modifies() references undeclared property",
        "Action '{0}' on '{1}' modifies property '{2}' which is not declared via obj.Property()",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CreatesLinkedUndeclared = new(
        OntologyDiagnosticIds.CreatesLinkedUndeclared,
        "CreatesLinked<T>() references undeclared link",
        "Action '{0}' on '{1}' creates linked '{2}' which is not declared via obj.HasOne/HasMany/ManyToMany",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RequiresLinkUndeclared = new(
        OntologyDiagnosticIds.RequiresLinkUndeclared,
        "RequiresLink() references undeclared link",
        "Action '{0}' on '{1}' requires link '{2}' which is not declared",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PostconditionOverlapsEvent = new(
        OntologyDiagnosticIds.PostconditionOverlapsEvent,
        "Action postcondition overlaps with event",
        "Action '{0}' on '{1}' has Modifies('{2}') overlapping with event UpdatesProperty",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // --- Lifecycle (AONT014-021) ---

    public static readonly DiagnosticDescriptor LifecyclePropertyUndeclared = new(
        OntologyDiagnosticIds.LifecyclePropertyUndeclared,
        "Lifecycle bound to undeclared property",
        "Lifecycle on '{0}' is bound to property '{1}' which is not declared via obj.Property()",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LifecycleInitialStateCount = new(
        OntologyDiagnosticIds.LifecycleInitialStateCount,
        "Lifecycle has zero or more than one Initial state",
        "Lifecycle on '{0}' must have exactly one .Initial() state but has {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LifecycleNoTerminalState = new(
        OntologyDiagnosticIds.LifecycleNoTerminalState,
        "Lifecycle has no Terminal states",
        "Lifecycle on '{0}' must have at least one .Terminal() state",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LifecycleTransitionBadState = new(
        OntologyDiagnosticIds.LifecycleTransitionBadState,
        "Transition references undeclared state",
        "Lifecycle transition on '{0}' references state '{1}' which is not declared",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LifecycleTransitionBadAction = new(
        OntologyDiagnosticIds.LifecycleTransitionBadAction,
        "Transition TriggeredByAction references undeclared action",
        "Lifecycle transition on '{0}' references action '{1}' which is not declared via obj.Action()",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LifecycleTransitionBadEvent = new(
        OntologyDiagnosticIds.LifecycleTransitionBadEvent,
        "Transition TriggeredByEvent<T> references undeclared event",
        "Lifecycle transition on '{0}' references event '{1}' which is not declared via obj.Event<T>()",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LifecycleUnreachableState = new(
        OntologyDiagnosticIds.LifecycleUnreachableState,
        "Unreachable state in lifecycle",
        "State '{0}' in lifecycle on '{1}' is unreachable (no inbound transitions and not Initial)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LifecycleDeadEndState = new(
        OntologyDiagnosticIds.LifecycleDeadEndState,
        "Dead-end non-terminal state",
        "State '{0}' in lifecycle on '{1}' has no outbound transitions and is not Terminal",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // --- Derivation (AONT022-026) ---

    public static readonly DiagnosticDescriptor DerivedFromUndeclaredProperty = new(
        OntologyDiagnosticIds.DerivedFromUndeclaredProperty,
        "DerivedFrom() references undeclared property",
        "Property '{0}' on '{1}' has DerivedFrom('{2}') but '{2}' is not declared via obj.Property()",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DerivedFromNonComputed = new(
        OntologyDiagnosticIds.DerivedFromNonComputed,
        "DerivedFrom() used on non-Computed property",
        "Property '{0}' on '{1}' uses DerivedFrom() but is not marked as .Computed()",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DerivationCycle = new(
        OntologyDiagnosticIds.DerivationCycle,
        "Derivation cycle detected",
        "Derivation cycle detected on '{0}': {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DerivedFromExternalUnresolvable = new(
        OntologyDiagnosticIds.DerivedFromExternalUnresolvable,
        "DerivedFromExternal() references unresolvable property",
        "Property '{0}' on '{1}' has DerivedFromExternal reference to '{2}.{3}.{4}' which cannot be validated locally",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ComputedNoDerivedFrom = new(
        OntologyDiagnosticIds.ComputedNoDerivedFrom,
        "Computed property has no DerivedFrom()",
        "Property '{0}' on '{1}' is marked .Computed() but has no DerivedFrom() declaration",
        Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    // --- Interface Actions (AONT027-030) ---

    public static readonly DiagnosticDescriptor InterfaceActionUnmapped = new(
        OntologyDiagnosticIds.InterfaceActionUnmapped,
        "Interface action not mapped",
        "Object type '{0}' implements interface '{1}' but does not map action '{2}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ActionViaBadReference = new(
        OntologyDiagnosticIds.ActionViaBadReference,
        "ActionVia references undeclared concrete action",
        "Interface mapping on '{0}' maps '{1}' to undeclared action '{2}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InterfaceActionIncompatible = new(
        OntologyDiagnosticIds.InterfaceActionIncompatible,
        "Interface action Accepts<T> incompatible with concrete action",
        "Interface action '{0}' Accepts<{1}> is incompatible with concrete action '{2}' Accepts<{3}>",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InterfaceActionNoImplementors = new(
        OntologyDiagnosticIds.InterfaceActionNoImplementors,
        "Interface declares actions but no implementors",
        "Interface '{0}' declares action '{1}' but no object types map it yet",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // --- Extension Points (AONT031-035) ---

    public static readonly DiagnosticDescriptor CrossDomainLinkNoExtensionPoint = new(
        OntologyDiagnosticIds.CrossDomainLinkNoExtensionPoint,
        "Cross-domain link targets type with no extension point",
        "Cross-domain link '{0}' targets '{1}' which has no matching extension point",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ExtensionPointInterfaceUnsatisfied = new(
        OntologyDiagnosticIds.ExtensionPointInterfaceUnsatisfied,
        "Extension point interface constraint unsatisfied",
        "Extension point '{0}' on '{1}' requires interface '{2}' but source type does not implement it",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ExtensionPointEdgeMissing = new(
        OntologyDiagnosticIds.ExtensionPointEdgeMissing,
        "Extension point requires edge property not on link",
        "Extension point '{0}' on '{1}' requires edge property '{2}' which is missing from the matched link",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ExtensionPointNoLinks = new(
        OntologyDiagnosticIds.ExtensionPointNoLinks,
        "Extension point declared but no links match",
        "Extension point '{0}' on '{1}' has no matching cross-domain links",
        Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ExtensionPointMaxLinksExceeded = new(
        OntologyDiagnosticIds.ExtensionPointMaxLinksExceeded,
        "Cross-domain link would exceed MaxLinks",
        "Extension point '{0}' on '{1}' has MaxLinks={2} but {3} links match",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
