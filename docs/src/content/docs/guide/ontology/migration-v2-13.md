---
title: Migrate action contracts to 2.13
description: Upgrade string-shaped action preconditions to typed predicates, explicit guarantees, typed workflow bindings and compensation, ActionFacts, and tri-state discovery.
sidebar:
  order: 7
---

Strategos 2.13 replaces string-shaped action preconditions with a typed,
immutable contract model and exact sequential proofs. This is an intentional
source-breaking change: there is no compatibility initializer that accepts or
parses a legacy expression string.

## Upgrading from the last published release

The examples below contrast 2.12 with 2.13 because the changes were staged
that way, but 2.11 and 2.12 were never published: the last released package
set is 2.10.0. A consumer that restores 2.13.0 absorbs the 2.11.0 changes in
the same restore. Read the 2.11.0 section of the
[CHANGELOG](https://github.com/lvlup-sw/strategos/blob/main/CHANGELOG.md)
first; its action-calculus, identity-routing, and authentication changes are
not repeated here.

## Approved versioning exception

The maintainers approved issue #168 as a source-breaking **minor-version
exception** for the 2.13 release. The old precondition surface could not express
sound post-state reasoning, and retaining a string bridge would preserve two
semantic authorities. The release therefore removes that surface in one step
instead of carrying an obsolete parser through the 2.x line.

This exception applies to the source API. The cross-language contracts package
is independently versioned: 0.10.0 introduces the tagged predicate schema,
0.11.0 adds occurrence-scoped workflow action identity, and 0.12.0 adds typed
inverse identity. Both workflow changes are additive. Contract consumers should
adopt 0.12.0 before receiving workflow definitions that carry the new optional
`action` or `compensation.inverseAction` fields, or diagnostics carrying one of
the new `AGWF039`–`AGWF045` closed-enum tokens.

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

## 8. Bind workflow implementations by typed identity

Workflow-bound actions now carry an immutable `WorkflowBindingReference`
instead of a writable workflow-name string:

```csharp
obj.Action("Publish")
    .BoundToWorkflow(
        new WorkflowBindingReference("publish-position"));
```

`WorkflowBindingReference.WorkflowId` is the exact ordinal name used to look up
the workflow catalog. The constructor rejects null, empty, or whitespace-only
identifiers and otherwise preserves the string as supplied; it does not trim or
case-normalize it. The existing
`.BoundToWorkflow("publish-position")` overload remains supported and constructs
the same typed reference.

If code initialized descriptors directly, replace the pre-release string
property:

```csharp
// Before
new ActionDescriptor(subject, "Publish", "Publish a position")
{
    BindingType = ActionBindingType.Workflow,
    BoundWorkflowName = "publish-position",
};

// 2.13
new ActionDescriptor(subject, "Publish", "Publish a position")
{
    BindingType = ActionBindingType.Workflow,
    BoundWorkflow = new WorkflowBindingReference("publish-position"),
};
```

Then identify the ontology action performed by every reachable named step
occurrence in that workflow. `WorkflowActionReference` contains the ontology
domain, object type, and action names; `.Performs(...)` attaches it to one use
of a step, not to the CLR step type:

```csharp
Workflow<PublishState>.Create("publish-position")
    .StartWith<ValidatePositionStep>(step => step.Performs(
        new WorkflowActionReference("Trading", "Position", "Validate")))
    .Then<WritePositionStep>(step => step.Performs(
        new WorkflowActionReference("Trading", "Position", "Write")))
    .Finally<NotifyPublicationStep>(step => step.Performs(
        new WorkflowActionReference("Trading", "Position", "Notify")));
```

The same CLR step type may therefore name different actions at different
occurrences, including occurrences inside branch, loop, fork, failure, and
low-confidence paths. Declare `.Performs(...)` at most once per occurrence.
All three identity names reject null, empty, or whitespace-only values.

For static workflow-binding proof, use a direct
`new WorkflowActionReference(...)` whose three arguments are compile-time
constant strings. Factories, mutable locals, dynamic expressions, and delegate
steps cannot provide the closed identity required by the analyzer. Imported
workflow definitions may carry the equivalent structured action reference.

The workflow must be a behavioral subtype of the bound action:

| Obligation | Required relation |
|---|---|
| Requirements (contravariant) | `requires(bound action) implies requires(workflow entry)` |
| Guarantees (covariant) | Every successful workflow exit implies `ensures(bound action)` |
| Frame | The union of leaf-action writes is a subset of the bound action's frame |
| Authority | The join of leaf-action requirements is no stronger than the bound action's authority limit |

Every leaf action must have the bound action's subject, every internal workflow
seam must compose, and parallel paths must not have write/write or
write/predicate-read interference. A closed counterexample is a definite
refinement failure; a custom predicate or dynamic contract is not treated as a
successful proof.

The proof also requires a statically closed workflow topology. Keep branch
cases, loop bodies and names, fork paths, approval handlers, confidence
handlers, and failure handlers inline and analyzer-visible. Dynamic callback or
collection helpers fail with `AGWF042` instead of being silently omitted.
Declare at most one `OnRejection` and one `OnTimeout` callback per approval;
duplicates also fail closed because the runtime builder uses last-wins semantics.
Nonterminal workflow `OnFailure`, fork-path `OnFailure`, and nested
`EscalateTo` approval routing also remain outside the proved v2.13 subset because
those routes are not yet represented by the closed proof graph.
No binding proof or runtime enforcement is added to an unbound workflow. The
topology lowering fixes shipped with this release still apply to every workflow.

The v2.13 proof is compilation-local. It sees source declarations in the
current compilation and imported workflow JSON supplied as `AdditionalFiles`;
it does not inspect ontology or workflow declarations inside referenced
binaries, execute `IOntologySource`, or repeat the proof at runtime. Keep the
bound action, target workflow, and referenced leaf actions source-visible to
one generator invocation. Source-visible `DomainOntology.Define` bodies form
the declaration catalog regardless of which domains a particular host later
registers. A direct `ActionDescriptor` construction is cataloged only when it
is inline in the `ObjectTypeDescriptor.Actions` collection passed through
`ObjectTypeFromDescriptor` from such a body.

Portable proof catalogs for referenced assemblies are deferred to
[#204](https://github.com/lvlup-sw/strategos/issues/204).

The proof runs inside the `LevelUp.Strategos.Generators` source generator. A
project that references `LevelUp.Strategos.Ontology` but not the generator
package compiles a `BoundToWorkflow(...)` binding with no diagnostic at all:
nothing proves it and nothing reports that it is unproved. Add the generator
package to every project that declares a workflow-bound action, or move the
binding into the project that owns the workflow.

The generator catalogs a binding only when the whole fluent chain from
`obj.Action(...)` to `.BoundToWorkflow(...)` is one expression inside
`DomainOntology.Define`. A `Define` body factored into helper methods, a
binding applied to a builder held in a local, or a chain routed through an
extension method fails closed as `AGWF042`. Inline the chain before upgrading.
The same closure rule applies to the workflow side: a bound workflow's
`Definition` must be one direct `Workflow<TState>.Create(...)...Finally<TStep>()`
chain. A `Definition` that delegates to a helper method still generates the
saga it generated on 2.12, but it cannot be proved and reports `AGWF042` while
bound.

The proof outcomes `AGWF041` (refuted), `AGWF042` (unprovable), `AGWF043`
(emission collision), `AGWF044` (invalid inverse), and `AGWF045` (underivable
rollback scope) carry the `NotConfigurable` tag. `<NoWarn>`, `#pragma warning
disable`, and `.editorconfig` severity entries do not suppress or downgrade
them. The resolution diagnostics `AGWF039` (bound workflow not found) and
`AGWF040` (action reference invalid) are Errors by default but stay
configurable: a project whose workflow or ontology lives in another assembly,
which the compilation-local proof cannot see until
[#204](https://github.com/lvlup-sw/strategos/issues/204) lands, silences them
explicitly with `<NoWarn>AGWF039</NoWarn>` in its project file. That entry is
the visible, greppable record that the binding is declared but unproved; delete
it when the cross-assembly proof ships. An internal failure of the proof itself
is reported as `AGWF042` in any compilation that calls `BoundToWorkflow`, rather
than as the Roslyn generator-crash warning, so a bound workflow never builds
green because the analyzer did not run. A compilation that binds nothing is not
affected by such a failure.

The proof graph is keyed by effective phase name. Reusing one step type in
multiple configured positions is valid only when the positions do not collapse
to one phase identity with different action references. Use distinct step types,
or distinct instance names where a builder exposes a combined
name-and-configuration overload.

The new workflow diagnostics all have error severity and fail closed:

| Code | Meaning and migration action |
|---|---|
| `AGWF039` | The exact ordinal `WorkflowId` resolves to zero or multiple C# or imported workflows. Declare exactly one matching workflow. |
| `AGWF040` | A reachable C# step has a missing, dynamic, duplicate, or unresolved action reference, or an accepted imported reference does not resolve exactly once. Add one direct, constant `.Performs(...)` declaration whose three names resolve exactly once. Malformed imported JSON is rejected earlier as `AGWF023`. |
| `AGWF041` | The closed workflow contract definitely fails a refinement, seam, subject, frame, authority, or fork-isolation obligation. Use the reported counterexample to strengthen the leaf contract or relax the bound specification only when that is semantically correct. |
| `AGWF042` | The analyzer cannot construct a closed proof because a binding, workflow topology, contract, authority lattice, or predicate is dynamic, invalid, opaque, contradictory, absent from the closed proof representation, or otherwise unprovable. Replace the input with a supported closed form; runtime checking is not accepted as a binding proof. |
| `AGWF043` | Distinct workflow ids normalize to the same generated PascalCase identity. Rename them so generated type and source-hint names are unique. |
| `AGWF044` | An authored compensation is missing a closed inverse identity or disagrees with the mechanically derived inverse. Use the typed compensation overload and correct the reported subject, requirement, guarantee, frame, or authority obligation. |
| `AGWF045` | A rollback-claimed scope contains a state-changing leaf without a proved inverse, or typed rollback lacks one closed same-subject binding boundary. Make the entire reported scope compensable. |

See the [Workflow API](/reference/api/workflow/#workflow-action-identity) for
occurrence authoring and [Typed action calculus](/reference/action-calculus/#behavioral-refinement-and-workflow-bindings)
for the complete proof rules.


## 9. Replace authored rollback lists with typed inverse actions

The no-argument compensation overload remains available for legacy,
runtime-only workflows — that is, for a workflow no `BoundToWorkflow` action
names:

```csharp
.Then<CapturePaymentStep>(step => step
    .Compensate<RefundPaymentStep>())
```

It names executable code but says nothing about the ontology contract that code
implements. It therefore cannot establish rollback safety. In a workflow that a
`BoundToWorkflow` action does name, the same call is `AGWF044`, a
`NotConfigurable` build error that `NoWarn` and `.editorconfig` severities
cannot suppress; and legacy, dynamic, and typed compensation declarations
cannot be mixed in one derived program. Binding such a workflow therefore means
authoring an ontology inverse action for every compensated occurrence, not
deleting a call.

For a proved workflow, give the forward occurrence a typed action and give the
compensation step its typed inverse action:

```csharp
.Then<CapturePaymentStep>(step => step
    .Performs(new WorkflowActionReference(
        "Orders", "Order", "CapturePayment"))
    .Compensate<RefundPaymentStep>(new WorkflowActionReference(
        "Orders", "Order", "RefundPayment")))
```

`IStepConfiguration<TState>` therefore has a new abstract typed overload;
external implementations and API mirrors must add it. Each occurrence accepts
exactly one compensation declaration. A second call to either overload now
throws `InvalidOperationException` instead of replacing the first declaration.

Strategos mechanically derives the inverse of forward action `A`:

- it requires `A`'s effective guarantee, including requirements preserved
  outside `A`'s frame;
- its effective guarantee equals `A`'s hard requirement;
- it has the same subject, exact frame, and semantically equal authority.

An inverse reported as `Proven` re-enters the set of states described by `A`'s
hard requirement. It does not restore a recorded concrete pre-forward state,
prove that each property in the frame regained its earlier value, or reverse
external and event effects. The shared frame is the same may-change boundary.
If the hard requirement admits multiple states, any of them can satisfy the
proved inverse contract.

The authored inverse must be equivalent in both directions. A non-empty frame
requires executable inverse code; an empty-frame action may use the distinct
identity inverse. `AGWF044` reports a legacy, dynamic, unresolved, opaque, or
semantically different authored inverse. `AGWF045` reports a rollback-claimed
scope in which any state-changing leaf lacks a proved inverse. Do not mix typed
and legacy compensation in one derived rollback program. A typed compensation
also requires `RequiredOnFailure` (wire `requiredOnFailure`) to remain `true`:
rollback of the completed prefix is derived and mandatory, not an optional
per-leaf declaration.

Rollback is no longer inferred from the list of compensation declarations.
Generated sagas persist a forward-dispatch authority claim before external
work starts, convert that exact claim into a completion-journal entry, and
derive the reverse plan from the completed prefix. A completion, timeout, or
failure whose execution identity was never dispatched fails closed. Which
prefix is selected depends on the failure ingress. If `C` fails in flight after
`A ; B` completed, only `B^-1 ; A^-1` runs: `C` never journaled a completed
entry, so it is absent. If the failure is reported after `C` completed — a
post-completion failure, where the reducer flags `Failed` on an occurrence
whose journal entry already reads `Completed` — the rollback is
`C^-1 ; B^-1 ; A^-1`, because the plan reverses every `Completed` entry in the
selected scope and the post-completion failure claim requires and preserves
that entry. Write each inverse so that it is safe to run against a forward step
that did complete. A failure inside a branch or loop iteration unwinds only
that concrete inner scope; a later outer failure may include completed
descendant scopes. Fork rollback waits for every lane to become terminal before
it begins. The structural plan retains parallel lanes, while generic state
updates are conservatively folded through inverse workers in reverse completion
order after the #167 noninterference proof.

Inverse success is applied through the configured state reducer before the next
inverse dispatches. An inverse failure, unmatched outcome, or timeout fails
closed and retains the saga journal for reconciliation; it does not recursively
enter compensation. Update operational tooling so a retained failed saga and
`CompensationOutcomeUnknown` are treated as operator-visible incidents rather
than ordinary terminal completion.

Generated inverse handlers now set `StepContext.IsCompensation` and expose the
stable delivery identity as `StepContext.RollbackId`. External effects remain
at-least-once, so migrate inverse steps to use `RollbackId` as their durable
idempotency key:

```csharp
if (context is { IsCompensation: true, RollbackId: Guid rollbackId })
{
    await payments.RefundOnceAsync(rollbackId, state.PaymentId, ct);
}
```

`CorrelationId` continues to contain the rollback id in N format for tracing
compatibility. Do not parse it for idempotency. Ordinary forward contexts keep
`IsCompensation == false` and `RollbackId == null`.

In v2.13, typed derived compensation requires `SagaDocument` persistence.
`EventSourced` workflows own their `ApplyEvent` implementation, and that method
may legally ignore an unfamiliar generated rollback-completed event. Strategos
cannot use method presence as proof that the inverse `UpdatedState` will be
folded identically in the live saga and during Marten replay, so the source
generator reports `AGWF045` instead of emitting a rollback-safety claim.

There is no in-place migration from event-sourced persistence to
`SagaDocument` persistence. The two substrates have different identity and
different replay semantics, and this release ships no procedure, tool, or
supported query for converting a live event stream into a saga document. An
event-sourced workflow has two options in v2.13: keep it on legacy untyped
compensation, or publish a *new* `SagaDocument`-persisted workflow under a
different workflow name or a new workflow version, direct new instances to it,
and let the in-flight event-sourced instances drain on the old definition.
Do not change the `Persistence` mode of a definition that has instances in
flight.

The derived runtime uses completion-journal schema version 1. Legacy,
untyped compensation continues to use the legacy runtime. When converting an
existing workflow definition to typed compensation, drain its in-flight legacy
instances or publish the typed definition under a new workflow version; a
persisted derived saga with missing or unknown journal metadata is retained in
`Failed` for reconciliation rather than guessed or upgraded in place.

### Rollout across the derived-runtime boundary

The derived runtime adds a block of persisted members to a typed workflow's
saga document, among them `CompensationJournal`, `CompensationJournalSequence`,
`CompensationJournalSchemaVersion`, and `ForwardDispatchClaims`. Marten's
default serializer is `System.Text.Json` with the default
`JsonUnmappedMemberHandling.Skip`, so a host that does not know those members
does not fail on them — it drops them.

That makes a rolling deploy across this boundary lossy. An instance still
running a build without derived compensation loads a typed saga document, those
members are dropped from the in-memory document, and its next
`session.Update(...)` writes the row back without them. A newer instance then
picks up the same saga, `CompensationJournalSchemaVersion` reads `0` rather
than `1`, the structural-validity check fails, and the saga is parked in
`Failed` with `CompensationFailureMessage` set to *"Completion journal changed
or became corrupt before a forward result; saga retained."* Nothing is reported
during the window: the older build succeeds on every message it handles, and
the message an operator finally reads names the journal, not the deploy. A
stripped saga does not recover, because the completed-prefix journal its
rollback needed is gone.

Three rollouts are supported for a host running typed workflows:

1. **Drain, then deploy.** Stop starting new instances of the typed workflows,
   let the in-flight ones reach a terminal state, then roll the build.
2. **Stop the world.** Take every instance that handles those workflows out of
   service, deploy, and bring them back. No two builds share the database.
3. **Publish under a new workflow version.** Generated saga class names come
   from `NamingHelper.GetSagaClassName(pascalName, version)`, so a new version
   is a new CLR type and therefore a new Marten table. Old documents stay on
   the old table and the old build; new instances start on the new one. This is
   the only option that keeps both builds live at once.

A package rollback has the same shape in reverse and is one-way: rolling back
after typed sagas exist strips their journals silently, and rolling forward
again finds them unusable. Treat the boundary as a versioned data migration,
not a code deploy.

<!-- PLACEHOLDER:AREA-A-MIGRATION -->
<!-- PLACEHOLDER:AREA-GH-MIGRATION -->

See [Mechanically derived compensation](/reference/action-calculus/#mechanically-derived-compensation)
for the runtime and proof contract.

## 10. Upgrade TypeSpec and workflow wire metadata

Upgrade `LevelUp.Strategos.Contracts` to 0.12.0. It includes the 0.10.0 change
from relation-only or consumer-parsed metadata to `ActionPredicateV1`:

- `@requires(predicate, strength?, description?)` emits a typed requirement;
- `@ensures(predicate, description?)` emits a typed guarantee;
- `@relation(name, ...path)` remains sugar for a hard `relation-holds`
  requirement;
- `x-strategos-relation` and `x-strategos-link-path` are no longer emitted;
- unknown predicate discriminators must be rejected.

Integers and decimals are canonical strings on the wire. Do not round-trip them
through JSON floating-point numbers. The `expression` field is presentation
only and must not be parsed.

Contracts 0.12 does not yet expose frame/effect decorators. A TypeSpec
`@ensures` fact must therefore already follow from a hard `@requires` fact;
otherwise graph freeze rejects it as an unrealizable guarantee about untouched
state. Use the CLR descriptor/fluent surface for actions that establish new
facts and need `TouchedResources` or postcondition effects.

Version 0.11.0 also adds `ActionReferenceV1` as the optional `action` property
shared by every workflow step kind:

```json
{
  "action": {
    "domainName": "Trading",
    "objectTypeName": "Position",
    "actionName": "Write"
  }
}
```

When `action` is present, all three name fields are required. The property is
additive and occurrence-scoped; legacy or unconfigured workflow JSON continues
to omit it byte-for-byte. Consumers that need the new identity should upgrade
to the generated 0.11.0 models before producers begin populating it. The same
release adds `AGWF039`–`AGWF043`; consumers of the generated closed `AgwfCode`
enum must upgrade before Strategos can emit those tokens.

Version 0.12.0 reuses `ActionReferenceV1` for the optional inverse action inside
compensation metadata:

```json
{
  "compensation": {
    "compensationStepType": "RefundPaymentStep",
    "inverseAction": {
      "domainName": "Orders",
      "objectTypeName": "Order",
      "actionName": "RefundPayment"
    }
  }
}
```

Omitting `inverseAction` retains the legacy runtime-only shape. The field is
additive, but `AGWF044` and `AGWF045` are new members of the generated closed
diagnostic enum; all consumers must upgrade before producers emit them.

## 11. Invalidate graph-version caches once

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

The typed workflow-binding wrapper is not itself part of that rollover. For an
unchanged binding, the hasher writes only `BoundWorkflow.WorkflowId` at the same
byte position where it previously wrote `BoundWorkflowName`. Moving from the
string property or overload to `new WorkflowBindingReference(sameId)` therefore
preserves the legacy graph hash. Changing the identifier still changes the
hash, as a routing change should.

After the action-contract rollover, registration order and presentation-only
edits remain hash-stable.

## Upgrade checklist

- Upgrade Strategos packages together and upgrade Contracts consumers to 0.12.0
  before publishing typed action, workflow-step, or inverse-action metadata.
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
- Replace direct `BoundWorkflowName` initializers with
  `BoundWorkflow = new WorkflowBindingReference(...)`, and replace every
  reader of `descriptor.BoundWorkflowName` with `descriptor.BoundWorkflow?.WorkflowId`;
  the string property no longer exists. Existing `.BoundToWorkflow(string)`
  calls compile unchanged; they are proved (or rejected) once the project also
  references `LevelUp.Strategos.Generators`.
- Note that `.BoundToWorkflow(...)` and `.BoundToTool(...)` are now mutually
  exclusive on a builder: the later call clears the other carrier, so a
  descriptor can no longer report `BindingType.Tool` while still holding a
  stale `BoundWorkflow`.
- Add one direct, constant `.Performs(new WorkflowActionReference(...))` to
  every reachable named step occurrence in each bound workflow.
- Replace proved-workflow `.Compensate<T>()` calls with
  `.Compensate<T>(new WorkflowActionReference(...))`, and make every
  state-changing leaf in a rollback-claimed scope compensable.
- Update external `IStepConfiguration<TState>` implementations and API mirrors
  for the typed `Compensate<T>(WorkflowActionReference)` overload, and remove
  repeated compensation calls that previously relied on last-write-wins.
- Update reconciliation tooling for retained inverse failures and unknown
  timeout outcomes.
- Resolve `AGWF039` through `AGWF045`; opaque or dynamic workflow contracts do
  not pass the binding proof.
- Keep each bound workflow, the ontology that binds it, and every action it
  names in one compilation. The binding and compensation proofs read the
  action catalog of the compilation being built, so a `BoundToWorkflow`
  declaration in another project is invisible and reports `AGWF039`,
  `AGWF042`, or `AGWF045` even though the declaration exists and is correct.
- Remove `AllowDiagnosticFork` from any workflow you bind with
  `BoundToWorkflow` or give typed compensation. The edge is not represented in
  the statically closed workflow proof, so it reports `AGWF042` in a bound
  workflow and the unsuppressible `AGWF045` when typed or dynamic compensation
  is also present. It keeps working in workflows that are neither.
- Resolve `AONT216`, `AONT217`, and `AONT221`; review `AONT218`, `AONT219`,
  and `AONT220` coverage. `AONT216` is the one that also throws at host start:
  graph freeze now requires every `CompensatedBy` action to have a `Proven`
  inverse, so an authored compensator that is merely frame-equal, or a forward
  contract carrying a custom predicate evaluator or a comparison outside the
  decidable finite-domain fragment, throws `OntologyCompositionException` even
  when the build-time diagnostic was configured away.
- Plan the rollout across the derived-runtime boundary before deploying: drain
  in-flight typed workflows, stop the world for the hosts that run them, or
  publish the typed definition under a new workflow version. A rolling window
  in which two builds share one Marten database strips live journals.
- Invalidate caches keyed by the pre-2.13 graph hash.
- Run the full solution and documentation builds before deployment.

For the formal contract and exact proof rules, see
[Typed action calculus](/reference/action-calculus/).
