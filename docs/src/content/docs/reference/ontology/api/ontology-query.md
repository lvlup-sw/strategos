---
title: IOntologyQuery
sidebar:
  order: 2
---

`IOntologyQuery` is the runtime query surface over a composed `OntologyGraph`. The source generator emits a concrete `OntologyQueryService` that implements every member; agents and dispatcher decorators inject `IOntologyQuery` to ask "what actions are valid here?", "which properties does this action change?", "what would I touch downstream if I edited X?" without re-parsing the ontology at every call site.

Namespace: `Strategos.Ontology.Query`. Source: `src/Strategos.Ontology/Query/IOntologyQuery.cs`.

## Members

| Category | Member | Returns |
|---|---|---|
| Core | `GetObjectTypes(string? domain, string? implementsInterface, bool includeSubtypes)` | `IReadOnlyList<ObjectTypeDescriptor>` |
| Core | `GetActions(string objectType)` | `IReadOnlyList<ActionDescriptor>` |
| Core | `GetLinks(string objectType)` | `IReadOnlyList<LinkDescriptor>` |
| Core | `GetImplementors(string interfaceName)` | `IReadOnlyList<ObjectTypeDescriptor>` |
| Preconditions | `GetValidActions(string objectType, IReadOnlyDictionary<string, object?>? knownProperties)` | `IReadOnlyList<ActionDescriptor>` |
| Preconditions | `GetActionConstraintReport(string objectType, IReadOnlyDictionary<string, object?>? knownProperties)` | `IReadOnlyList<ActionConstraintReport>` |
| Preconditions | `GetActionConstraintReport(string domain, string objectType, IReadOnlyDictionary<string, object?>? knownProperties)` | `IReadOnlyList<ActionConstraintReport>` |
| Postconditions | `TracePostconditions(string objectType, string actionName, int maxDepth)` | `IReadOnlyList<PostconditionTrace>` |
| Lifecycle | `GetActionsForState(string objectType, string stateName)` | `IReadOnlyList<ActionDescriptor>` |
| Lifecycle | `GetTransitionsFrom(string objectType, string stateName)` | `IReadOnlyList<LifecycleTransitionDescriptor>` |
| Derivation | `GetAffectedProperties(string objectType, string propertyName)` | `IReadOnlyList<AffectedProperty>` |
| Derivation | `GetDerivationChain(string objectType, string propertyName)` | `IReadOnlyList<DerivationSource>` |
| Interface actions | `GetInterfaceActions(string interfaceName)` | `IReadOnlyList<InterfaceActionDescriptor>` |
| Interface actions | `ResolveInterfaceAction(string objectType, string interfaceActionName)` | `ActionDescriptor?` |
| Inverse links | `GetInverseLinks(string objectType, string linkName)` | `IReadOnlyList<LinkDescriptor>` |
| Extension points | `GetExtensionPoints(string objectType)` | `IReadOnlyList<ExternalLinkExtensionPoint>` |
| Extension points | `GetIncomingCrossDomainLinks(string objectType)` | `IReadOnlyList<ResolvedCrossDomainLink>` |
| Object sets | `GetObjectSet<T>(string objectType)` | `ObjectSet<T>` where `T : class` |
| Object sets | `GetObjectTypeNames<T>()` | `IReadOnlyList<string>` where `T : class` |
| Validation (2.5.0) | `EstimateBlastRadius(IReadOnlyList<OntologyNodeRef> touchedNodes, BlastRadiusOptions? options)` | `BlastRadius` |
| Validation (2.5.0) | `DetectPatternViolations(IReadOnlyList<OntologyNodeRef> affectedNodes, DesignIntent intent)` | `IReadOnlyList<PatternViolation>` |

## Notes per member group

### Core lookups

`GetObjectTypes` returns every registered descriptor when called with no arguments. `domain` narrows to a single domain; `implementsInterface` narrows to types whose `ImplementedInterfaces` contains the named interface; `includeSubtypes` follows the `IsA<TParent>()` hierarchy when filtering by parent. The returned list preserves registration order.

`GetActions`, `GetLinks`, and `GetImplementors` resolve their target by simple name; pass the `(domain, objectType)` overload of `GetActionConstraintReport` instead when two domains register the same name.

### Precondition evaluation

`GetValidActions` runs every registered action's precondition expression against `knownProperties` and returns the subset whose hard preconditions hold; missing keys evaluate as unsatisfied. `GetActionConstraintReport` returns one entry per action regardless of pass/fail, with the hard and soft evaluations split — agents read this to plan with awareness of why an action is currently unavailable.

The domain-qualified overload (`GetActionConstraintReport(domain, objectType, knownProperties)`) is the recommended call site: the default interface implementation falls back to the simple-name overload only for backwards compatibility with test doubles that have not been updated.

### Postcondition tracing

`TracePostconditions(objectType, actionName, maxDepth = 1)` follows declared `Modifies`, `CreatesLinked`, and `EmitsEvent` postconditions; raising `maxDepth` follows derivation chains from the modified properties, surfacing transitive effects. The result is ordered by traversal depth.

### Object sets

`GetObjectSet<T>(string objectType)` returns a typed `ObjectSet<T>` rooted at the named descriptor. `GetObjectTypeNames<T>()` returns every descriptor name `T` was registered under, in registration order; a single-registered type has a one-element list, and an unregistered type has an empty list. This lets consumers (e.g. Basileus) enumerate per-collection partitions of a shared content-carrier type without hardcoding descriptor names.

### Blast radius and pattern violations (2.5.0)

`EstimateBlastRadius` walks links, derivation chains, postconditions, and cross-domain links from a seed set of `OntologyNodeRef` values until `BlastRadiusOptions.MaxExpansionDegree` is reached. The returned `BlastRadius` carries a deterministically ordered set of affected nodes, the cross-domain hops encountered, and a classified scope (single-type, single-domain, cross-domain).

`DetectPatternViolations` evaluates the supplied `affectedNodes` against the `DesignIntent` and returns any pattern fires (empty when the intent is satisfied). The signatures are declared on `IOntologyQuery` as default-throwing methods — concrete implementations such as `OntologyQueryService` override them. Test doubles that pre-date 2.5.0 throw `NotSupportedException` when called.

### Type parameters and nullability

Every `where T : class` constraint is on `ObjectSet<T>` and `GetObjectTypeNames<T>`. `ResolveInterfaceAction` returns `null` when no concrete action implements the named interface action on `objectType`. Property dictionaries (`knownProperties`) are optional — pass `null` for an unconditional reading.
