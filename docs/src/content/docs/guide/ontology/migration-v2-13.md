---
title: Migrate action contracts to 2.13
description: Upgrade string-shaped action preconditions to typed predicates, explicit guarantees, ActionFacts, and tri-state discovery.
sidebar:
  order: 7
---

Strategos 2.13 replaces string-shaped action preconditions with a typed,
immutable contract model and exact sequential proofs. This is an intentional
source-breaking change: there is no compatibility initializer that accepts or
parses a legacy expression string.

## Approved versioning exception

The maintainers approved issue #168 as a source-breaking **minor-version
exception** for the 2.13 release. The old precondition surface could not express
sound post-state reasoning, and retaining a string bridge would preserve two
semantic authorities. The release therefore removes that surface in one step
instead of carrying an obsolete parser through the 2.x line.

This exception applies to the source API. The cross-language contracts package
is independently versioned and moves from 0.9.0 to 0.10.0. Contract consumers
must adopt the 0.10 tagged predicate schema before receiving newly generated
action metadata.

## 1. Give every action an ontology subject

`ActionDescriptor` now requires `ActionSubject(DomainName, ObjectTypeName)`.
Use stable ontology names rather than `typeof(T)`, assembly-qualified names, or
other CLR identity:

```csharp
// 2.12
var action = new ActionDescriptor("Activate", "Activate a position");

// 2.13
var subject = new ActionSubject("Trading", "Position");
var action = new ActionDescriptor(subject, "Activate", "Activate a position");
```

Actions created through `obj.Action(...)` receive the containing domain and
object descriptor names automatically. Descriptor-first and polyglot actions
must supply the subject directly. Graph freeze rejects a subject that does not
match the containing object with `AONT221`.

The same identity is now required when constructing an `ObjectSet<T>` directly.
The descriptor-name-only constructor could not identify a domain and has been
removed; use `IOntologyQuery.GetObjectSet<T>(name)` when possible, or pass an
explicit subject:

```csharp
var positions = new ObjectSet<Position>(
    new ActionSubject("Trading", "Position"),
    provider,
    dispatcher,
    eventStreamProvider);
```

## 2. Replace legacy precondition initializers

`ActionPrecondition.Expression`, `Kind`, `LinkName`, and legacy relation
fields are no longer writable inputs. `PreconditionKind` is removed. Construct
the precondition from an `ActionPredicate`; `Expression` remains a read-only
canonical display projection:

```csharp
// 2.12
new ActionPrecondition
{
    Expression = "Status == Active",
    Description = "The position is active.",
    Kind = PreconditionKind.PropertyPredicate,
    Strength = ConstraintStrength.Hard,
};

// 2.13
new ActionPrecondition(
    ActionPredicate.Property(
        new PredicatePropertyReference(
            "Status",
            PredicateScalarKind.Enum,
            enumTypeName: "PositionStatus"),
        PredicateComparisonOperator.Equal,
        PredicateLiteral.Enum("PositionStatus", "Active")),
    "The position is active.",
    ConstraintStrength.Hard);
```

Fluent CLR-generic authoring remains concise:

```csharp
obj.Action("ExecuteTrade")
    .Requires(position => position.Status == PositionStatus.Active)
    .RequiresSoft(position => position.UnrealizedPnL > -10_000m);
```

Only direct property-to-literal comparisons and `&&`, `||`, and `!` over
them are translated. Captures, method calls, arithmetic, property-to-property
comparisons, floating point, user-defined operators, and fields now fail at
construction and produce `AONT221` when statically visible. Replace deliberate
runtime logic with an explicit `ActionPredicate.Custom` and a stable evaluator
key; unsupported syntax never becomes custom automatically.

Use `ActionPredicate.True` for an explicit wildcard. A top-level
`_ => true` expression is rejected so an accidentally vacuous lambda is not
silently accepted.

## 3. Separate guarantees from effects

Add `Ensures` entries for facts callers may rely on after success:

```csharp
obj.Action("Activate")
    .Requires(position => position.Status == PositionStatus.Pending)
    .Ensures(position => position.Status == PositionStatus.Active)
    .Modifies(position => position.Status);
```

`ModifiesProperty` means “may write” and does not imply a new value.
`EmitsEvent` also derives no predicate fact. `CreatesLink` is the one sound
effect-derived fact: it guarantees `LinkExists`.

Actions without a nontrivial effective guarantee remain valid, but coverage
classifies them as vacuous because they usually cannot establish a useful next
requirement. Do not add `Ensures(True)` to improve coverage; a true/true action
is still a concrete action, not composition identity.

Graph freeze also verifies that a guarantee is realizable within the frame. A
guarantee about an untouched resource must already follow from the action's
hard requirements. Expand `TouchedResources` only when the implementation may
really write the resource; otherwise strengthen the requirement or remove the
unsound guarantee.

## 4. Use typed ActionFacts

Runtime precondition APIs no longer accept
`IReadOnlyDictionary<string, object?>`. Build immutable, typed facts:

```csharp
// 2.12
var knownProperties = new Dictionary<string, object?>
{
    ["Status"] = PositionStatus.Active,
    ["Note"] = null,
};

// 2.13
var facts = new ActionFacts(
    properties:
    [
        KeyValuePair.Create(
            "Status",
            PredicateLiteral.Enum("PositionStatus", "Active")),
        KeyValuePair.Create("Note", PredicateLiteral.Null),
    ],
    links:
    [
        KeyValuePair.Create("Strategy", true),
        KeyValuePair.Create("Orders", false),
    ]);
```

A missing entry is unknown. An explicit null literal and a link value of
`false` are known negative information. This distinction is required for
sound three-valued evaluation.

## 5. Handle tri-state discovery

`GetCandidateActions*` is the primary discovery API. It excludes actions proven
unavailable and returns an `ActionCandidateEvaluation` for every available or
indeterminate action:

- `ActionAvailability.Available` when every hard requirement is satisfied;
- `Indeterminate` when no hard requirement is false but available facts or
  evaluators cannot decide at least one.

`ActionAvailability.Unavailable` remains visible in
`GetActionConstraintReport*`, where it identifies an action whose hard
requirement was proven false.

```csharp
var candidates = query.GetCandidateActions("Trading", "Position", facts);

foreach (var candidate in candidates)
{
    if (candidate.Availability == ActionAvailability.Available)
    {
        // Safe to present as known available under these discovery facts.
    }
}
```

`GetValidActions*` remains as a convenience, but now means “not proven
unavailable.” It returns the same available and indeterminate action set without
the per-constraint evaluations. Code that previously treated its result as a
proof of availability must move to `GetCandidateActions*` and inspect
`Availability`.

Per-constraint results expose `PredicateTruthValue`. Any retained
`IsSatisfied` member is only a read-only convenience for
`TruthValue == Satisfied`; it cannot represent indeterminate by itself.

## 6. Register runtime resolvers and enforce authoritative facts

Register the resolver for target facts and each custom evaluator through the
generic, trimming-safe options surface:

```csharp
services.AddOntology(options => options
    .AddDomain<TradingOntology>()
    .UseActionFactResolver<PositionFactResolver>()
    .AddCustomActionPredicateEvaluator<CreditApprovedEvaluator>());
```

`CustomActionPredicateContext.Facts` is a projection containing only the
property and link facts declared by that custom predicate. Every declared
target fact must be present or the predicate is `Indeterminate` without
invoking the evaluator. External and event reads remain the evaluator's
responsibility.

Caller-supplied discovery facts are not trusted at dispatch. The dispatcher
loads authoritative facts for the target with `IActionFactResolver`. When
`ActionDispatchOptions.EnforcePreconditions` is true, every hard predicate
must evaluate `Satisfied`; unsatisfied and indeterminate both fail closed.

Hard predicates containing a relation remain mandatory even when general
enforcement is disabled. The complete `AND`/`OR`/`NOT` formula is evaluated.
A missing resolver, absent custom evaluator, or evaluator failure produces
`Indeterminate` and a structured log entry rather than an allow decision.
Soft requirements remain advisory.

## 7. Migrate explicit composition

Replace assumptions about declared writes with explicit guarantees, then use
the nonthrowing analysis API while adopting:

```csharp
var analysis = ActionCalculus.AnalyzeSequential(
    authorityLattice,
    activate,
    executeTrade);

if (!analysis.CanCompose)
{
    // Inspect analysis.Errors and refuted seam counterexamples.
}

var contract = ActionCalculus.Sequential(
    authorityLattice,
    activate,
    executeTrade);
```

`Sequential` throws the sealed `ActionCompositionException` for invalid
contracts or refuted seams. Custom predicates produce a partially verified
contract and explicit opaque exclusions instead.

Nested composites flatten before their adjacent seams are checked. All operands
must have the same `ActionSubject` in 2.13. Use
`ActionCalculus.Identity(subject)` for the distinct empty operand; do not model
identity as an ordinary true/true action.

## 8. Upgrade TypeSpec metadata

Upgrade `LevelUp.Strategos.Contracts` to 0.10.0 and replace relation-only or
consumer-parsed metadata with `ActionPredicateV1`:

- `@requires(predicate, strength?, description?)` emits a typed requirement;
- `@ensures(predicate, description?)` emits a typed guarantee;
- `@relation(name, ...path)` remains sugar for a hard `relation-holds`
  requirement;
- `x-strategos-relation` and `x-strategos-link-path` are no longer emitted;
- unknown predicate discriminators must be rejected.

Integers and decimals are canonical strings on the wire. Do not round-trip them
through JSON floating-point numbers. The `expression` field is presentation
only and must not be parsed.

Contracts 0.10 does not yet expose frame/effect decorators. A TypeSpec
`@ensures` fact must therefore already follow from a hard `@requires` fact;
otherwise graph freeze rejects it as an unrealizable guarantee about untouched
state. Use the CLR descriptor/fluent surface for actions that establish new
facts and need `TouchedResources` or postcondition effects.

## 9. Invalidate graph-version caches once

The canonical graph hash now includes action subjects, normalized typed
requirements, guarantees, custom evaluator keys/arguments/read sets, and the
expanded action contract metadata. Descriptions and display expressions remain
excluded.

Every existing action-bearing graph receives a different
`OntologyGraph.Version` after this upgrade even when its apparent business
meaning is unchanged. This is intentional. Treat the first 2.13 deployment as
a cache-key rollover: discard stored MCP schema views, planner tool lists,
action-availability snapshots, and any other artifact keyed by the old hash.
Do not translate or pin the previous hash.

After the rollover, registration order and presentation-only edits remain
hash-stable.

## Upgrade checklist

- Upgrade Strategos packages together and upgrade Contracts consumers to 0.10.0
  before publishing typed action metadata.
- Add an ontology-named `ActionSubject` to every descriptor-first action.
- Replace direct descriptor-name-only `ObjectSet<T>` construction with
  `IOntologyQuery` or the `ActionSubject` constructor.
- Replace every writable/string precondition initializer with a typed
  `ActionPredicate`.
- Add real post-state facts through `Ensures`; keep “may write” metadata in
  postconditions and the frame.
- Replace property dictionaries with `ActionFacts`.
- Audit every `GetValidActions*` caller for the new “non-unavailable”
  semantics; use `GetCandidateActions*` where the distinction matters.
- Register authoritative fact and custom predicate resolvers and test
  indeterminate, failure, and cancellation paths.
- Resolve `AONT217` and `AONT221`; review `AONT218`, `AONT219`, and
  `AONT220` coverage.
- Invalidate caches keyed by the pre-2.13 graph hash.
- Run the full solution and documentation builds before deployment.

For the formal contract and exact proof rules, see
[Typed action calculus](/reference/action-calculus/).
