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

    // ReadOnly (AONT036)
    public const string ReadOnlyConflictsWithMutation = "AONT036";

    // Polyglot identity invariant (AONT037)
    public const string PolyglotInvariantViolated = "AONT037";

    // Polyglot graph-freeze diagnostics (AONT201-208) — DR-7
    public const string HandPropertyMissingFromIngested = "AONT201";
    public const string HandPropertyTypeMismatch = "AONT202";
    public const string IngestedPropertyMissingFromHandStrict = "AONT203";
    public const string IngestedTypeNotReferencedByHand = "AONT204";
    public const string IngestedContributesToIntentOnly = "AONT205";
    public const string HandPropertyAlsoIngestedHygieneHint = "AONT206";
    public const string BranchHandConflict = "AONT207";
    public const string LanguageIdDisagreement = "AONT208";

    // Reified-association endpoint cardinality (AONT210) — DR-6 (#121).
    // A reified association is a junction object: a valid reified relation
    // requires two ManyToOne endpoints folding INTO the association object.
    // AONT210 flags an endpoint declared with any other cardinality.
    // (AONT209 is reserved for a sibling task; ids are monotonic, never
    // reused — INV-5.)
    public const string AssociationEndpointCardinalityInvalid = "AONT210";
    // Edge-property removal migration (AONT209) — DR-5 (#120, closes #114)
    public const string EdgePropertyAuthoringRemoved = "AONT209";

    // Ambiguous-traversal-without-override guard (AONT211) — DR-10/DR-6
    // (#128, #121). Compile-time half of the DR-10 identity-flow fix
    // (INV-5: earliest-tier). Fires when TraverseLink<TLinked>("role")
    // targets an ambiguously multi-registered descriptor (same CLR type
    // registered under 2+ names, mirroring AONT041 multi-registration
    // detection) AND no descriptorName override is supplied to disambiguate.
    // An override → no diagnostic; a single-registered target → no diagnostic.
    // INV-2: analyzer-only (no runtime counterpart in this task). Ids are
    // monotonic and never reused (INV-5) — AONT209/AONT210 are untouched.
    public const string AmbiguousTraversalWithoutDescriptor = "AONT211";

    // Polymorphic-target-no-junction-table guard (AONT212) — DR-11 (#128).
    // Under the per-(link, target-descriptor) junction posture, a link to a
    // registered interface fans out into one junction table per IMPLEMENTOR
    // descriptor. AONT212 fires when the interface link target has ZERO
    // implementor object descriptors in the compilation: the fan-out set is
    // empty, so NO junction table can be provisioned and the link is dead.
    // Distinct from AONT003 (concrete target not registered) and AONT030
    // (interface declares ACTIONS but no implementors). INV-5: monotonic, next
    // free past the AONT211 ceiling; AONT209/AONT210/AONT211 untouched.
    public const string PolymorphicTargetNoJunctionTable = "AONT212";
}
