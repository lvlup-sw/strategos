---
title: Typed action calculus
description: Typed predicates, post-state guarantees, composition, behavioral refinement, workflow bindings, derived compensation, and runtime enforcement for ontology actions.
---

An ontology action is an immutable state-transition contract. Its
`ActionSubject` gives it a language-neutral owner, hard requirements describe
the states from which it may run, guarantees describe facts that hold after a
successful run, and its frame says which resources it may change. Strategos can
therefore prove whether one action may safely follow another instead of parsing
display strings or assuming that a declared write produces a particular value.

This surface is source-breaking in Strategos 2.13. See the
[2.13 migration guide](/guide/ontology/migration-v2-13/) before upgrading an
existing ontology.

The design deliberately keeps the language restricted so proof remains
deterministic and decidable, following the validation posture described in the
[Cedar policy-language research](https://arxiv.org/abs/2403.04651). Sequential
implication uses the standard SMT reduction—prove `G_A AND NOT R_B`
unsatisfiable—documented by the current
[SMT-LIB standard](https://smt-lib.org/language.shtml). Guarantees are genuine
post-state assertions in the sense of
[Hoare logic](https://doi.org/10.1145/363235.363259), not facts invented from a
“may write” declaration.

## Action identity and contract shape

Every executable `ActionDescriptor` has a required subject whose identity is
formed from ontology names, never CLR types:

```csharp
var subject = new ActionSubject("Trading", "Position");
var action = new ActionDescriptor(subject, "Activate", "Activate a position")
{
    Preconditions =
    [
        new ActionPrecondition(
            ActionPredicate.Property(
                new PredicatePropertyReference(
                    "Status",
                    PredicateScalarKind.Enum,
                    enumTypeName: "PositionStatus"),
                PredicateComparisonOperator.Equal,
                PredicateLiteral.Enum("PositionStatus", "Pending")),
            "The position is pending."),
    ],
    Ensures =
    [
        new ActionGuarantee(
            ActionPredicate.Property(
                new PredicatePropertyReference(
                    "Status",
                    PredicateScalarKind.Enum,
                    enumTypeName: "PositionStatus"),
                PredicateComparisonOperator.Equal,
                PredicateLiteral.Enum("PositionStatus", "Active"))),
    ],
    TouchedResources = [ActionResource.Property("Status")],
};
```

All descriptor collections are defensively snapshotted into immutable arrays.
Changing the source list after construction does not change the action.

## The closed predicate language

`ActionPredicate` is a closed, immutable hierarchy. Instances are created by
its canonicalizing factories:

| Predicate | Meaning |
|---|---|
| `True`, `False` | Explicit logical constants. |
| `Property(reference, operator, literal)` | Compare one subject property with one typed literal. |
| `LinkExists(name)` | At least one target exists for the named link. |
| `RelationHolds(name, path...)` | The authenticated principal holds the named relation after the path is traversed. |
| `All(...)` | Conjunction (`AND`). |
| `Any(...)` | Disjunction (`OR`). |
| `Not(...)` | Negation. |
| `Custom(key, arguments, readSet)` | An explicitly opaque predicate evaluated by a registered runtime evaluator. |

Supported literals are explicit null, Boolean, arbitrary-precision integer,
exact base-10 decimal, ordinal string, named enum member, and symbolic
identifier. Integer and decimal values retain exact canonical text across
serialization. Floating-point values are deliberately excluded.

Integer and decimal properties support all six comparison operators. Boolean,
string, enum, symbol, and null values support equality and inequality only.
Null is permitted only for nullable property references and is different from
a missing fact.

Canonicalization happens during construction. `All` and `Any` flatten nested
aggregates of the same kind, remove identity values, apply absorbing values,
eliminate duplicates, and sort operands by their structural key. Property
comparisons place the property on the left, and `Not(Not(p))` becomes `p`.
Strategos does not distribute predicates into CNF or DNF. Predicate structural
equality and the semantic tokens used for graph hashing use the normalized
structure; descriptions and the human-readable `Expression` projection do not
participate in semantic identity. Serialized contracts carry both the
normalized predicate and this display projection. `Expression` is never
reparsed.

### Fluent and expression authoring

The CLR-generic action builder accepts either an `ActionPredicate` or a
restricted expression tree:

```csharp
builder.Object<Position>(obj =>
{
    obj.Action("ExecuteTrade")
        .Requires(position =>
            position.Status == PositionStatus.Active
            && (position.Quantity > 0m || !position.Suspended))
        .RequiresSoft(position => position.UnrealizedPnL > -10_000m)
        .Ensures(position => position.Status == PositionStatus.Active)
        .RequiresLink("Strategy")
        .EnsuresLink("Orders")
        .Modifies(position => position.Quantity)
        .CreatesLinked<TradeOrder>("Orders");
});
```

The expression translator accepts direct property-to-literal comparisons and
`&&`, `||`, and `!` over those comparisons. A direct Boolean property is shorthand
for equality with `true`. Integral CLR types and `BigInteger` lower to the
integer domain, `decimal` remains exact, enums use their ontology type/member
names, and a `Guid` property belongs to the symbol domain. C# has no `Guid`
literal, so author non-null `Guid`/symbol comparisons with the typed
`ActionPredicate.Property(..., PredicateLiteral.Symbol(...))` API instead of
an expression overload.

The following forms are rejected during construction and produce `AONT221`
when the analyzer can see them: floating point, property-to-property
comparisons, user-defined operators, method calls, captured values, fields,
arithmetic, quantifiers, and transitive closure. Unsupported expressions never
silently become `Custom`. A top-level `_ => true` is also rejected; write
`.Requires(ActionPredicate.True)` when an explicit wildcard is intentional.
These restrictions are in addition to the C# compiler's own
[expression-tree restrictions](https://learn.microsoft.com/dotnet/csharp/language-reference/compiler-messages/expression-tree-restrictions).

Use `ActionPredicate.Custom` only as an explicit design choice. The evaluator
key, ordered typed arguments, and canonical declared read set are semantic. A
description remains presentation metadata:

```csharp
var approved = ActionPredicate.Custom(
    "trading.credit-approved.v1",
    [PredicateLiteral.Symbol("standard")],
    [ActionResource.Property("CreditStatus")]);
```

## Guarantees are facts; effects are a frame

`ActionGuarantee` uses the same predicate vocabulary as a precondition. It is a
fact promised after successful execution. `ActionPostcondition` remains effect
and frame metadata:

| Declaration | What may be inferred after the action |
|---|---|
| `Ensures(predicate)` | The predicate itself. |
| `CreatesLinked<T>(name)` / `CreatesLink` | `LinkExists(name)`. |
| `Modifies(property)` / `ModifiesProperty` | Nothing about the resulting value; it only says the property may be written. |
| `EmitsEvent<T>()` / `EmitsEvent` | No state predicate. |

This distinction prevents an unsound inference such as “may modify `Status`”
therefore “`Status == Active`.” State the latter explicitly with `Ensures`.

`TouchedResources` is the action's frame. Fluent `Modifies`, `CreatesLinked`,
and `EmitsEvent` calls add their resources by construction. Descriptor-first
actions are checked during graph freeze, and literal initializers are checked
by the analyzer. `AONT215` reports mutation outside the frame.

For every predicate whose resources are disjoint from the frame, the predicate
has the same truth value before and after the action. This non-interference rule
is what permits requirements on untouched state to survive an action.

## Exact sequential composition

For action `A`, Strategos defines:

- `R_A`: conjunction of its hard preconditions. Soft preconditions do not enter
  static proofs.
- `D_A`: explicit guarantees plus sound facts derived from effects (currently
  link existence from `CreatesLink`).
- `W_A`: the property and link resources the frame says the action may change.
- `G_A = D_A AND Forget_W_A(R_A)`: its effective post-state guarantee.

`Forget` is semantic existential projection, not deletion of matching syntax.
For example, forgetting `x` from `x == 1 AND y == 2` yields `y == 2`, while
forgetting `x` from `x == 1 OR y == 2` yields `True`.

Before a closed action is used in a proof, Strategos checks that its hard
requirements are satisfiable, its declared guarantees are satisfiable, and its
frame is realizable:

```text
R_A  implies  exists W_A . D_A
```

The last condition rejects a guarantee about untouched state unless the
requirement already establishes it. Contradictory contracts and unrealizable
frames are `AONT221` errors.

An adjacent seam `A -> B` is legal exactly when `G_A` implies `R_B`. The proof
kernel checks whether this counterexample formula is satisfiable:

```text
G_A AND NOT R_B
```

If it is satisfiable, `AONT217` reports a stable, minimized symbolic assignment
that the upstream contract permits but the downstream requirement rejects.

### Proof kernel

The pure C# kernel partitions each referenced domain into the finitely many
cells distinguished by constants in the formula: Boolean values; null and
non-null; named strings, symbols, or enum members plus an `other` cell; numeric
constants, gaps, and rays; and Boolean link/relation atoms. It lazily enumerates
those cells with three-valued partial evaluation over a hash-consed normalized
DAG, memoized partial results and unsatisfiable prefixes, and ordinal atom
ordering. The procedure is exact and has no atom limit that can turn a closed
predicate into “unknown.” Roslyn cancellation is honored.

`Custom` is outside this closed proof fragment. An action with a custom hard
requirement or guarantee is listed in `OpaqueExclusions`; adjacent seams become
`Opaque`, and the composite is `PartiallyVerified`. Other closed actions, and
closed seams that are not adjacent to an opaque action, are still checked.
Opaque is not a proof of compatibility: runtime evaluation remains required.

### Composition API and identity

Use `AnalyzeSequential` for a nonthrowing result and `Sequential` when invalid
contracts or refuted seams should throw `ActionCompositionException`:

```csharp
var analysis = ActionCalculus.AnalyzeSequential(lattice, reserve, charge);
if (analysis.CanCompose)
{
    CompositeActionContract contract =
        ActionCalculus.Sequential(lattice, reserve, charge);
}
```

Operands may be actions, existing composites, or
`ActionCalculus.Identity(subject)`. Nested composites are flattened before
adjacent seams are checked. A composite exposes the subject, flattened action
list, first requirement, final effective guarantee, authority join, frame
union, verification status, seam results, and opaque exclusions.

The identity is a distinct empty operand, typed by `ActionSubject`, and is
removed while flattening. An ordinary action with `Requires=True` and
`Ensures=True` is still an observable action and is not identity. An all-identity
sequence produces an empty identity contract.

Strategos 2.13 composes only actions with the same subject. Two object types
with the same simple name in different domains remain distinct. Cross-subject
state transfer is deferred. Composition is conservative across longer chains:
facts do not survive an intervening action unless its own requirements and
frame justify preserving them.

## Behavioral refinement and workflow bindings

Sequential composition proves that actions can follow one another. Behavioral
refinement proves that one executable action or composite is a safe substitute
for a declared action specification. Given specification `S` and implementation
`I`, Strategos checks four variance and effect bounds:

| Obligation | Proof | Why it is required |
|---|---|---|
| Requirements are contravariant | `R_S implies R_I` | An implementation may accept more states, but cannot demand a state the advertised action accepts. |
| Guarantees are covariant | `G_I implies D_S` | Every state promised by the implementation must satisfy the specification's declared guarantee. |
| Frame is bounded | `W_I` is a subset of `W_S` | An implementation cannot write a resource the specification did not permit it to change. |
| Authority is bounded | `Authority_I <= Authority_S` pointwise | An implementation cannot require stronger authority than callers of the specification were told to provide. |

Subjects must also be equal. `G_I` is the implementation's effective guarantee,
including facts soundly preserved through its frame; `D_S` is the
specification's declared guarantee. `ModifiesProperty` remains only a frame
declaration and cannot satisfy a guarantee obligation. `CreatesLink` does
contribute its sound `LinkExists` fact.

Use `ActionCalculus.AnalyzeRefinement` with either an `ActionDescriptor` or a
`CompositeActionContract` implementation:

```csharp
var implementation = ActionCalculus.Sequential(
    authorityLattice,
    validate,
    write,
    notify);

var refinement = ActionCalculus.AnalyzeRefinement(
    publishSpecification,
    implementation,
    authorityLattice);

if (!refinement.IsRefinement)
{
    // Inspect Status and each failure's obligation and counterexample.
}
```

`ActionRefinementStatus.Proven` is the only successful substitution result.
`Refuted` carries concrete counterexamples or frame/authority failures;
`Opaque` means a custom predicate prevented a complete proof; and `Invalid`
means a contract was malformed, already refuted, or otherwise not decidable by
the closed kernel.

### Binding an ontology action to a workflow

An action binds to a workflow catalog identity with the immutable
`WorkflowBindingReference`:

```csharp
obj.Action("Publish")
    .Requires(position => position.Status == PositionStatus.Draft)
    .Ensures(position => position.Status == PositionStatus.Published)
    .Modifies(position => position.Status)
    .BoundToWorkflow(new WorkflowBindingReference("publish-position"));
```

`WorkflowId` is preserved exactly and resolved with ordinal comparison. The
constructor rejects null, empty, or whitespace-only values. The string overload
`.BoundToWorkflow("publish-position")` remains source compatible and maps to the
same typed reference.

Each reachable named workflow step occurrence identifies its leaf action with
`WorkflowActionReference(DomainName, ObjectTypeName, ActionName)` and
`.Performs(...)`:

```csharp
Workflow<PublishState>.Create("publish-position")
    .StartWith<ValidatePositionStep>(step => step.Performs(
        new WorkflowActionReference("Trading", "Position", "Validate")))
    .Then<WritePositionStep>(step => step.Performs(
        new WorkflowActionReference("Trading", "Position", "Write")))
    .Finally<NotifyPublicationStep>(step => step.Performs(
        new WorkflowActionReference("Trading", "Position", "Notify")));
```

This identity belongs to the occurrence, not the CLR step type. All three names
are required and ordinal. The closed proof graph is keyed by the occurrence's
effective phase name, however, so two configured uses that collapse to the same
phase name cannot claim different actions. Give them distinct CLR step types,
or distinct instance names where the builder exposes a combined
name-and-configuration overload. For analyzer-visible bindings, construct the
reference directly from compile-time constant strings; factories, dynamic
expressions, multiple declarations, and unnamed delegate steps do not provide a
closed occurrence identity.

The workflow-binding analyzer generalizes refinement across the workflow graph:

- the bound action requirement implies the entry action requirement;
- every non-failure internal transition satisfies the ordinary effective-guarantee to
  next-requirement seam proof;
- every successful completion's effective guarantee implies the bound action's
  declared guarantee;
- every leaf has the bound action's subject, the union of leaf frames stays
  within the bound frame, and the pointwise join of leaf authorities stays at or
  below the bound authority; and
- fork paths have neither write/write interference nor write/predicate-read
  interference.

All reachable occurrences represented by the closed main, branch, loop, fork,
approval, confidence, and failure-path graph participate. Custom predicates and
other dynamic contract inputs fail closed for workflow bindings even though
ordinary sequential analysis can report them as partially verified.

Proof is limited to topology that the generator can close without executing
user code. Branch cases, loops, fork paths, approval and confidence handlers,
and failure handlers must use analyzer-visible inline forms. Dynamic callback or
collection helpers produce `AGWF042`. Nonterminal workflow failure handlers,
fork-path failure handlers, and nested `EscalateTo` approvals are also excluded
from the proved v2.13 subset until those routes have an equivalent closed proof
representation.

### Static proof boundary

The v2.13 binding proof is compilation-local. It sees C# workflow and
`DomainOntology.Define` declarations in the current compilation, plus imported
workflow JSON supplied as `AdditionalFiles`. It does not open declarations from
referenced binaries and it does not execute runtime `IOntologySource`
contributions. A bound action, its target workflow, and the leaf-action catalog
needed for the proof must therefore be source-visible to the same generator
invocation. There is no runtime re-proof fallback for a cross-assembly binding.

The static action catalog treats each source-visible `DomainOntology.Define`
body as a declaration root. A directly constructed `ActionDescriptor` counts
only when it is placed inline in an `ObjectTypeDescriptor.Actions` collection
passed to `ObjectTypeFromDescriptor` from that root; an unrelated descriptor
construction elsewhere in the compilation cannot satisfy a workflow leaf.
Whether a declared ontology is selected by a particular host remains a
deployment concern. Portable referenced-assembly catalogs are tracked in
[#204](https://github.com/lvlup-sw/strategos/issues/204), separately from this
source-visible proof slice.

## Runtime three-valued evaluation

Static proof establishes contract compatibility; runtime discovery evaluates a
particular state using strong-Kleene three-valued logic:

- `PredicateTruthValue.Satisfied`
- `PredicateTruthValue.Unsatisfied`
- `PredicateTruthValue.Indeterminate`

`ActionFacts` is an immutable set of typed property facts and explicit link
presence/absence. A missing property or link is unknown. `PredicateLiteral.Null`
and a link value of `false` are explicit known facts.

```csharp
var facts = new ActionFacts(
    properties:
    [
        KeyValuePair.Create("Status", PredicateLiteral.Enum("PositionStatus", "Active")),
        KeyValuePair.Create("Note", PredicateLiteral.Null),
    ],
    links:
    [
        KeyValuePair.Create("Strategy", true),
        KeyValuePair.Create("Orders", false),
    ]);

var candidates = query.GetCandidateActions("Trading", "Position", facts);
```

`GetCandidateActions*` returns actions whose availability is `Available` or
`Indeterminate`, with one result per hard or soft constraint. Actions proven
`Unavailable` are excluded from discovery; their detailed outcome remains
visible through `GetActionConstraintReport*`. The `GetValidActions*`
convenience methods return the same non-unavailable action set without the
per-constraint results. Soft predicates are reported but never block discovery
or dispatch.

Register authoritative facts and explicit custom evaluators with AOT-safe
generic options methods:

```csharp
services.AddOntology(options => options
    .UseActionFactResolver<PositionFactResolver>()
    .AddCustomActionPredicateEvaluator<CreditApprovedEvaluator>());
```

A custom evaluator receives only the property and link facts named in its
declared read set. If any declared target fact is missing, the custom leaf is
`Indeterminate` and the evaluator is not invoked. External and event resources
remain evaluator-owned.

Discovery facts are planning hints, not authorization evidence. At dispatch,
Strategos resolves authoritative facts for the target with
`IActionFactResolver`. With `EnforcePreconditions = true`, every hard predicate
must evaluate `Satisfied`; both false and indeterminate fail closed. A hard
predicate containing a relation is mandatory even when general enforcement is
off, and its complete Boolean formula—not a flattened relation shortcut—is
evaluated. A missing or failing fact resolver (when target facts are needed),
relation resolver, or custom evaluator yields `Indeterminate` and a structured
log entry.

## Authority and retry safety

Authority uses a product order rather than the orchestration `Capability`
flags enum. A domain declares independent axes from weakest to strongest and
positions each authority on every axis. Sequential composition computes the
pointwise join of all component requirements.

`ReadOnly()` implies `Idempotent()` by construction. Descriptor-first contracts
that mark a read-only action as non-idempotent are rejected by `AONT213`.

## Mechanically derived compensation

Compensation is a proved inverse contract, not a name attached to an exception
handler. For a valid, closed forward action `A`, Strategos derives `A^-1` as:

| Inverse obligation | Required value |
|---|---|
| Subject | The same `ActionSubject` as `A` |
| Requirement | The effective guarantee of `A` |
| Effective guarantee | The hard requirement of `A` |
| Frame | Exactly the frame of `A` |
| Authority | Semantically equal in the domain authority lattice |

Using the effective guarantee is important. It includes both explicit
post-state facts and requirements preserved outside the forward frame. An
authored inverse must be equivalent in both directions; merely accepting fewer
states or promising a weaker restoration is not a valid inverse.

`ActionCalculus.AnalyzeInverse` returns `Proven`, `Missing`, `Refuted`,
`Opaque`, or `Invalid`, with an obligation-specific explanation and a stable
symbolic counterexample when the finite-domain solver refutes equivalence. A
non-empty frame needs executable authored inverse code. An action with an empty
frame may use the distinct identity inverse, provided it does not contain a
broken `CompensatedBy` declaration. Graph freeze resolves named inverse actions
and reports `AONT216` unless the full subject, requirement, guarantee, frame,
and authority proof succeeds.

Rollback plans are immutable syntax trees:

```text
(A ; B)^-1 = B^-1 ; A^-1
(A || B)^-1 = A^-1 || B^-1
```

`ActionCalculus.DeriveRollbackPlan` accepts only the completed forward prefix,
so a failed action is never included in its own rollback. Sequential plans
reverse and flatten; parallel plans preserve independent branches; scoped plans
retain nested compensation boundaries; and an empty plan is the subject-typed
rollback identity.

### Typed workflow compensation

Name both the forward action and the executable inverse at each step
occurrence:

```csharp
var capture = new WorkflowActionReference("Orders", "Order", "CapturePayment");
var refund = new WorkflowActionReference("Orders", "Order", "RefundPayment");

Workflow<OrderState>.Create("process-order")
    .StartWith<CapturePaymentStep>(step => step
        .Performs(capture)
        .Compensate<RefundPaymentStep>(refund));
```

The generator resolves both identities from the same compilation-local action
catalog. `AGWF044` rejects a missing, ambiguous, dynamic, opaque, or
semantically different authored inverse. Once a workflow or one of its bound
action specifications claims rollback, compensability propagates through the
scope: every rollback-reachable leaf with a non-empty frame must have a proved
inverse. `AGWF045` rejects the whole scope instead of emitting a partial plan.
Multiple same-subject action specifications may bind the workflow; each binding
is proved independently, while the shared inverse program is proved once.

The no-argument `.Compensate<T>()` overload remains a legacy runtime-only
facade. It does not establish a typed inverse. A workflow must not mix legacy,
dynamic, and typed compensation into one derived program; such a program fails
closed rather than running the provable subset.

### Durable completed-prefix rollback

Generated Wolverine sagas journal a forward occurrence only after it completes.
The journal carries stable occurrence, scope, lane, action, execution, and
inverse identities plus the state needed by the inverse worker. On failure,
the generator derives the rollback from that persisted journal rather than an
author-maintained list:

- a failure at `C` after `A ; B` completed runs `B^-1 ; A^-1`; `C` is absent;
- a failure inside a nested branch or loop iteration unwinds only that concrete
  inner scope, while a later enclosing failure can include its completed
  descendant scopes;
- a fork failure first stops successor dispatch and waits until every lane is
  terminal before deriving rollback;
- fork lanes remain parallel in the structural plan, but generated inverse
  workers fold their state updates serially in reverse completion order because
  generic workflow state has no sound merge operation. The workflow-binding
  proof has already established that the lane frames do not interfere.

Inverse completion has a separate message route from forward completion and
applies the returned state through the configured saga-document or event-sourced
reducer before the next inverse starts. A stable rollback id makes redelivery
idempotent. An inverse failure, an unmatched outcome, or a timeout is never
recursively compensated or assumed successful: the saga and its journal remain
in `Failed` for reconciliation. Failure handlers run only after a successful
rollback of the selected scope.

## TypeSpec and MCP metadata

`Strategos.Contracts` 0.12.0 includes the versioned tagged
`ActionPredicateV1`, `ActionLiteralV1`, `ActionRequirementV1`, and
`ActionGuaranteeV1` wire types introduced in 0.10.0. Contract operations author
hard or soft requirements and guarantees directly:

```typespec
@objectKind("Trading", "Position", "entity")
@requires(
  #{
    kind: "property-comparison",
    property: #{
      name: "status",
      scalarKind: ActionPredicateScalarKindV1.String,
      isNullable: false
    },
    operator: ActionComparisonOperatorV1.NotEqual,
    value: #{ kind: "string", value: "closed" }
  },
  ActionRequirementStrengthV1.Soft,
  "The position should not already be closed."
)
@requires(#{ kind: "link-exists", linkName: "auditTrail" })
@ensures(
  #{ kind: "link-exists", linkName: "auditTrail" },
  "The existing audit trail remains available after success."
)
op inspectPosition(input: InspectPositionRequest): InspectPositionResult;
```

The decorators emit structured `x-strategos-requires-v1` and
`x-strategos-ensures-v1` arrays, including the canonical display expression.
The display field is never parsed. `@relation` remains authoring sugar and
lowers to a hard `relation-holds` requirement; the old relation/link-path pair
is no longer emitted. Unknown predicate discriminators are rejected rather
than treated as `Custom` or `True`.

The 0.11 contract decorator surface does not yet author effect/frame metadata.
Consequently, a TypeSpec guarantee must already follow from the operation's
hard requirements; graph freeze rejects a contract that promises a new fact
without declaring how that resource may change. Author state-changing
guarantees in the CLR descriptor/fluent surface, where `TouchedResources` and
postcondition effects are available, until a versioned contract frame is
introduced.

Version 0.11.0 also adds the optional, occurrence-scoped `action` field to every
workflow step kind. Its `ActionReferenceV1` value contains required
`domainName`, `objectTypeName`, and `actionName` strings. Legacy workflow JSON
continues to omit the optional field byte-for-byte.

Version 0.12.0 adds an optional `inverseAction: ActionReferenceV1` inside a
step's compensation configuration. The field identifies the ontology action
implemented by the compensation step; omission retains the legacy runtime-only
shape. It also adds the closed `AGWF044` and `AGWF045` diagnostic tokens, so
generated-enum consumers must upgrade before producers emit them.

MCP action summaries expose schema-equivalent `requires` and `ensures` arrays.
The MCP assembly keeps local wire records rather than depending on the
contracts package, and parity is checked against the current emitted schemas.

## Diagnostics

| Code | Severity | Meaning |
|---|---|---|
| `AONT217` | Error | A closed adjacent seam is refuted; includes its counterexample. |
| `AONT218` | Warning | A custom requirement or guarantee excludes an action from complete static proof. |
| `AONT219` | Info | Graph or explicit-sequence composability coverage. Zero-action graphs suppress it. |
| `AONT220` | Info | An explicit sequence is too dynamic for the analyzer and receives runtime-only verification. |
| `AONT221` | Error | Invalid expression, subject mismatch, contradiction, or unrealizable frame. |
| `AGWF039` | Error | A bound workflow identity resolves to zero or multiple exact ordinal workflow definitions. |
| `AGWF040` | Error | A reachable workflow step occurrence has no single closed action reference, or its three-name identity does not resolve exactly once. |
| `AGWF041` | Error | A closed workflow binding is definitely refuted, with a counterexample or a subject/frame/authority/fork-isolation failure. |
| `AGWF042` | Error | A workflow binding cannot be proved because an identity, topology, contract, lattice, or predicate is dynamic, invalid, opaque, absent from the closed proof representation, or otherwise unprovable. |
| `AGWF043` | Error | Distinct workflow ids collide after generated PascalCase normalization. |
| `AGWF044` | Error | An authored compensation action does not equal the mechanically derived inverse contract. |
| `AGWF045` | Error | A rollback-claimed scope contains a non-compensable or otherwise unprovable leaf. |

The analyzer recognizes direct constructions, immutable single-assignment
locals, statically initialized immutable collections, nested `Sequential` calls,
`ActionCalculus.Identity`, and `ActionCompositionOperand.From` at explicit
`ActionCalculus.Sequential` calls. Arbitrary helper methods and mutable
collections remain runtime checked. Graph freeze is the exact merged-graph
backstop and reports coverage over concrete executable actions. See the
[AONT200-series reference](/reference/diagnostics/aont-200-series/) for fixes
and full ontology messages. See the
[workflow diagnostic reference](/reference/diagnostics/agwf-agsr/#workflow-binding-diagnostics)
for the binding-specific remediations.
