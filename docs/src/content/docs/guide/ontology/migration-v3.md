---
title: Migrate to Strategos 3.0
description: Upgrade from 2.10.0 (or 2.9.1) to 3.0.0-rc.1 — Contracts 0.12.0 first, then the correctness-core, principal and authority, and typed action-contract changes, in upgrade order.
sidebar:
  order: 7
---

Strategos 3.0.0-rc.1 is the first release since 2.10.0 (2026-08-07). It folds
three programs that were planned as 2.11, 2.12, and 2.13 minor releases — and
never shipped under those labels — into one major pre-release, alongside
`LevelUp.Strategos.Contracts` 0.12.0. A consumer that restores 3.0.0-rc.1
absorbs all three programs in one restore, so this page covers all of them,
ordered the way an upgrade should proceed.

The headline breaking changes are:

- Every action dispatch, action discovery call, and MCP `ontology_action` call
  now carries an authenticated `ActionPrincipal`. Missing or incomplete
  principals are refused before the dispatcher runs.
- Action preconditions are typed, immutable predicates with a required
  ontology subject. There is no compatibility parser for the old string form.
- Workflows that `Fork`, `Branch`, or `AwaitApproval` now terminate. The fix
  changes the generated `Phase` enum's member order and the emitted
  `ValidTransitions` table, and it adds build-time diagnostics that reject
  shapes that used to compile.
- Workflow-bound actions are proved against their workflow, and compensation
  is mechanically derived. The proofs fail closed with diagnostics that cannot
  be suppressed.

## Where you are starting from

**From 2.10.0.** Read this page top to bottom. Every section applies.

**From 2.9.1** (where basileus is pinned). You also absorb 2.10.0, the
strategy-compiler contract layer: `LevelUp.Strategos.Contracts` 0.4.0,
`GateClass` / `GateDeclaration`, execution-profile metadata, JSON workflow
import through `AdditionalFiles`, and the export-only workflow wire contract.
That release is not re-documented here. Read the
[2.10.0 section of the CHANGELOG](https://github.com/lvlup-sw/strategos/blob/main/CHANGELOG.md#2100---2026-08-07)
first, then continue with this page.

**Ontology-only consumers** (`LevelUp.Strategos.Ontology*` without the
workflow package). Sections 5 to 21 apply; sections 2 to 4 describe the
workflow generator and can be skipped, except for
`DescriptorSource.HandAuthoredContract` in section 4.

**Contracts consumers** (anything that deserializes `WorkflowDefinitionV1`,
`AgwfCode`, or the ontology contract schemas). Section 1 applies to you before
any producer upgrades, whether or not you consume the .NET packages.

## Why this is a major release

Issue #168 was originally approved as a source-breaking *minor-version
exception* for a 2.13 release: the old precondition surface could not express
sound post-state reasoning, and retaining a string bridge would have preserved
two semantic authorities. That release never shipped. With the correctness-core
and principal programs folded in, the set is cut as 3.0.0-rc.1 instead. Under a
major version these are ordinary breaking changes; no exception is claimed or
needed, and none of the removed surfaces carries an obsolete shim.

The cross-language contracts package is versioned independently and moves
0.4.0 → 0.12.0 across the same window. While it is pre-1.0, a minor may narrow
the schema in place; every narrowing is listed in
`src/Strategos.Contracts/schemas/breaking-changes.allowlist.json` and in the
package's own CHANGELOG. After 1.0 a breaking change will require a new
schema root.

## Upgrade order

1. Upgrade every **Contracts consumer** to 0.12.0 (section 1). Nothing else
   may emit the new diagnostic tokens or wire fields until this is done.
2. Restore the **3.0.0-rc.1 package set together** — core, generators,
   ontology, MCP, and hosting packages move as one. Do not mix a 3.0 ontology
   package with a 2.10 generator package.
3. Fix compile errors in the order of sections 2 to 20. The diagnostics that
   carry `NotConfigurable` (`AGWF041`–`AGWF045`, `AONT216`) cannot be silenced;
   plan to resolve them, not to suppress them.
4. Run the **data checks** before deploying: `Phase` storage representation
   (section 3), the derived-runtime rollout boundary (section 19), and the
   graph-version cache rollover (section 21).

## 1. Upgrade Contracts consumers to 0.12.0 first

`LevelUp.Strategos.Contracts` moves from the published 0.4.0 to 0.12.0. The
generated `AgwfCode` enum is decorated with
`JsonStringEnumConverter<AgwfCode>` and per-member `JsonStringEnumMemberName`,
so it round-trips by *name* and **throws on a member it does not know**. A
consumer still on 0.4.0 cannot deserialize any payload that carries
`AGWF035`–`AGWF045`. The same rule applies to the other generated closed enums
in the package: they accept and emit only their exact wire tokens.

Move consumers first, producers second. In practice: land the Contracts bump
in each consumer before you restore 3.0.0-rc.1 in any producer that can emit
diagnostics or workflow definitions to it.

What each intermediate version added:

| Version | Change | Compatibility |
|---|---|---|
| 0.5.0 | `AGWF035` unreachable termination. | Additive enum member; consumer-first. |
| 0.6.0 | `AGWF036` path-end type collision. Retired in the same release window — kept as history, no longer emitted. | Additive. |
| 0.7.0 | `AGWF037` duplicate permitted fork trigger. | Additive. |
| 0.8.0 | `AGWF038` duplicate diagnostic-fork compensation seed. | Additive. |
| 0.9.0 | Ontology action contract decorators (`@objectKind`, `@authority`, `@relation`, `@clients`, `@confirm`, `@readOnly`, `@idempotent`) emitting `x-strategos-*` JSON Schema metadata; generated `HandAuthoredContract` descriptors. | Additive. |
| 0.10.0 | Versioned tagged `ActionPredicateV1` / `ActionLiteralV1`; typed `@requires` and `@ensures`; `@relation` lowers to a `relation-holds` predicate; `x-strategos-relation` and `x-strategos-link-path` no longer emitted; generated required properties fail deserialization when absent. | **Narrowing.** Unknown predicate discriminators are rejected. |
| 0.11.0 | Optional `action: ActionReferenceV1` on every workflow step kind; `AGWF039`–`AGWF043`; `WorkflowDefinitionV1.name` must be non-blank. | Additive field; narrowing on `name`. |
| 0.12.0 | Optional `compensation.inverseAction: ActionReferenceV1`; `AGWF044`–`AGWF045`; `requiredOnFailure` carries `default: true` and an `if`/`then` rule that a typed inverse requires it; empty or whitespace `compensationStepType` rejected. | Additive fields; narrowing on `compensationStepType`. |

If your project pins the Contracts package **directly** as well as receiving
it **transitively** from the core package, move both in one commit. The 3.0
core package's nuspec depends on 0.12.0; leaving a direct 0.4.0 pin in place
splits the dependency graph, and a test that asserts one resolved Contracts
version across every `project.assets.json` (basileus has one) goes red.

Section 20 covers what changes for a consumer that *authors* TypeSpec or
workflow JSON, as opposed to one that only deserializes it.

## 2. Workflows that fork, branch, or await approval — correctness core

A C#-authored workflow using `Fork` or `Branch` never terminated on any
published version, including 2.10.0. Five generator blocks appended off-main-
flow steps (fork paths, branch cases, failure handlers, rejection and
escalation chains) to the step list *after* the declared terminal, and the
successor scans did not filter them out, so the terminal's completed handler
chained back into a path step. The 3.0 generator classifies off-main-flow steps
once, routes every successor scan through that classification, and restores
document order to the step list.

No DSL change is required to receive the fixes. What follows is what changes
in the *emitted* artifacts and diagnostics.

### What now runs correctly

- `.Fork(...).Join<T>().Finally<T>()` and `Branch` sagas reach `Completed` and
  delete their saga document.
- Multi-step `OnRejection` and `OnTimeout` approval chains run past their
  first step and either `Complete()` or resume onto the main-flow step the
  approval resumes onto.
- An `AwaitApproval` that is last on the main flow dispatches its rejection
  chain; an approval immediately before a `Fork` or `Branch` parks at the
  checkpoint, and resume is the single dispatch owner.
- `RequireConfidence` / `OnLowConfidence` on the last step of a branch case now
  lowers; an `OnLowConfidence` chain declared inside a branch case is no longer
  mistaken for a case step.
- Instance-named fork-path steps no longer emit a duplicate phase, command, and
  handler (`CS0111` in the consuming compilation).
- Bool exclusive branches (`When(true)` + `When(false)`) no longer emit an
  unreachable discard arm (`CS8510`); loop-exit `Finally` runs.

If a workflow of these shapes has instances in flight on 2.10.0, they are stuck
in the old routing and will not recover under the new build. Drain or
terminate them before deploying, then start new instances.

### `ValidTransitions` and `IsValidTransition` describe the real graph

The generated transition table was a flat linear chain over the step list. It
now follows the constructs: a fork predecessor dispatches every path, each
path's last step reaches the join, a branch discriminator dispatches every
case, a case's last step rejoins or completes, a loop publishes its continue
edge, and a terminal `OnFailure` handler loses its fall-through edge. **This is
emitted public API and its content changes for every fork, branch, loop-only,
and `OnFailure`-only workflow.** Nothing in the generated saga consults the
table at runtime, so the impact is on tests and tooling that assert on it.

### Path-qualified completed events for exclusive paths

Routing maps for fork paths and branch cases key by `PathRoutingKey` (phase
name plus construct and path identity), not by bare CLR step type. Fork-path
instances that share one step type publish a **path-qualified completed
event** — `{PhaseName}Completed`, or `{PathId}_{PhaseName}Completed` when
unnamed paths collide — and the saga `Handle` overload and the worker dispatch
bind the same stem. Branch completions remain one `Handle({StepType}Completed)`
that routes by the live case.

Code or tests that named the generated completed-event type for a fork path
whose step type is reused on another path must switch to the path-qualified
name. `AGWF036` (path-end type collision), which briefly rejected that shape,
is no longer emitted; the catalog member remains as history.

### New and changed diagnostics

| Code | Severity | What changed |
|---|---|---|
| `AGWF003` | Error | Now also reports **duplicate step names on `BranchPath`**. Exclusive cases that shared a step name compiled with last-write-wins routing; they now fail the build. Rename the steps. This is the one *breaking* diagnostic of the correctness core. |
| `AGWF035` | Error | New. Unreachable termination: a declared `Finally<T>` is not the last main-flow step, a main-flow step's successor is construct-owned, or a rejoin construct's last step never dispatches the terminal. Silent when every exclusive path already `Complete()`s. |
| `AGWF037` | Error | New. Two `PermitTrigger` declarations on one diagnostic-fork edge name the same closed trigger. Declare each trigger once. |
| `AGWF038` | Error | New. Two diagnostic-fork edges share a compensation seed. `DiagnosticForkCount` is now keyed by the sanitized seed, not a call-site index; the 2.10.0 positional `DiagnosticForkCount_{i}` property is kept as a read-only migration shim that folds forward into the seed-keyed property. |
| `AGWF022` | Warning | Re-aimed. It no longer reports intermediate fork-path or loop-body confidence gating (those were false positives). It now reports confidence gating on the step an `AwaitApproval` checkpoint follows, which is genuinely dropped. |
| `AGWF036` | — | Retired. No longer emitted. |

## 3. Check how your Marten store persists `Phase`

Restoring document order changes the **member order** of the generated `Phase`
enum for every fork and branch workflow. Under a System.Text.Json-serializing
Marten store the phase persists by *name*, so the reorder is not a migration.
Under a Newtonsoft-serializing store it persists by *ordinal* by default, and a
reorder silently loads the wrong phase for every saga document written before
the upgrade. Strategos never sees your `StoreOptions` and cannot detect this.

Inspect the raw document before you deploy:

```sql
select jsonb_typeof(data->'Phase') as json_type,
       data->>'Phase'              as stored_phase
  from mt_doc_<yoursaga>
 limit 5;
```

`json_type = 'string'` means name storage and nothing to do. `json_type =
'number'` means ordinal storage: rewrite the stored values (or move the store
to `EnumStorage.AsString`) before the new build starts. The full procedure and
the serializer matrix are on
[Phase Enum Persistence](/reference/phase-persistence/).

## 4. Smaller correctness-core API changes

**`DescriptorSource.HandAuthoredContract`** is appended as `2`, after
`HandAuthored = 0` and `Ingested = 1`. Descriptors authored through TypeSpec or
JSON contracts carry it, and `AONT205` (mechanical ingester contributed to an
intent-only field) now applies only to `Ingested`, so contract-authored actions
survive graph merge. A `switch` over `DescriptorSource` needs a new arm.

**`IActionBuilder<T>.Requires` (#115).** The 2.11 milestone marked the
expression `Requires` overload obsolete in favour of descriptor
`Preconditions`. The typed-contract program then made `Requires` the typed
authoring path — it translates a restricted expression subset into an
`ActionPredicate`, or takes an `ActionPredicate` directly — so nothing on the
builder is marked obsolete in 3.0.0-rc.1. What *is* gone is the string-shaped
precondition surface; see section 12.

**MCP protocol revision 2026-07-28.** `Strategos.Ontology.MCP.Hosting` pins the
`ModelContextProtocol` SDK at 2.2.0 (`VersionOverride`) so every constructed
`CallToolResult` sets the `resultType` discriminator; the other packages stay
on 1.3.0. `OntologyToolDescriptor.Icons` is optional and stays null when
unset. A host that references the hosting package receives the 2.2.0 SDK
transitively.

## 5. Every action dispatch carries an `ActionPrincipal`

`ActionPrincipal` is a sealed, ontology-owned record: the principal's
ontology descriptor name (`PrincipalType`, for example `User` or
`ServiceAccount`), the instance identifier (`PrincipalId`), and the
`GrantedAuthorities` literal names. Both identity values are required and
non-blank; the constructor throws otherwise. Nothing about it assumes a CLR
identity type.

`ActionContext` now requires the principal as its first constructor argument
and rejects `null`:

```csharp
using Strategos.Ontology.Actions;

// 2.10.0
var context = new ActionContext("Trading", "Position", positionId, "Activate");

// 3.0
var principal = new ActionPrincipal("User", userId)
{
    GrantedAuthorities = ["Trader"],
};

var context = new ActionContext(principal, "Trading", "Position", positionId, "Activate")
{
    ActionDescriptor = descriptor, // optional; must be the graph's own instance
};
```

When you set `ActionContext.ActionDescriptor`, pass the descriptor resolved
from the frozen `OntologyGraph`. The authority dispatcher compares it by
reference with the graph's descriptor and refuses a copy as an unknown action.

The same principal threads through the other dispatch and discovery entry
points:

```csharp
// ObjectSet<T>: the principal is now the first argument.
await positions.ApplyAsync(principal, "Activate", request, ct);
await positions.ApplyAsync(principal, "Activate", request,
    new ActionDispatchOptions { EnforcePreconditions = true }, ct);

// IOntologyQuery: principal-aware, instance-scoped discovery.
var candidates = await query.GetCandidateActionsAsync(
    principal, "Trading", "Position", positionId, facts, ct);
```

`GetValidActionsAsync` and `GetCandidateActionsAsync` are the principal-aware
overloads; they take a target instance so `RelationHolds` predicates can be
decided during discovery, and a query implementation without a relation
resolver reports `NotSupportedException` rather than guessing. The synchronous
descriptor-only overloads remain for fact-only evaluation.

`OntologyActionTool.ExecuteAsync` takes `ActionPrincipal?` as its first
parameter. A `null` principal returns a failed `ActionResult` (*"An
authenticated action principal is required."*) and never reaches
`IActionDispatcher`.

## 6. Bind MCP callers through `IActionPrincipalResolver`

The hosting package resolves the principal per call from the MCP transport's
`ClaimsPrincipal`. An unauthenticated caller is refused before any resolver
runs. The default `ClaimsActionPrincipalResolver` reads:

- `PrincipalId` from `ClaimTypes.NameIdentifier`, falling back to `sub`;
- `PrincipalType` from the `strategos:principal_type` claim
  (`ActionPrincipalClaimTypes.PrincipalType`);
- `GrantedAuthorities` from every `strategos:authority` claim
  (`ActionPrincipalClaimTypes.Authority`), de-duplicated ordinally.

A missing type or id yields `null`, and `null` refuses dispatch. Register your
own resolver when the host's claims are shaped differently:

```csharp
using System.Security.Claims;
using Strategos.Ontology.Actions;
using Strategos.Ontology.MCP.Hosting;

public sealed class TenantActionPrincipalResolver : IActionPrincipalResolver
{
    public ActionPrincipal? Resolve(ClaimsPrincipal caller)
    {
        var id = caller.FindFirst("oid")?.Value;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null; // refuse dispatch
        }

        return new ActionPrincipal("User", id)
        {
            GrantedAuthorities = [.. caller.FindAll("roles").Select(c => c.Value)],
        };
    }
}

builder.Services.AddSingleton<IActionPrincipalResolver, TenantActionPrincipalResolver>();
```

The tool factory resolves `IActionPrincipalResolver` from the request's
service provider and falls back to `ClaimsActionPrincipalResolver.Instance`.
Tool annotations are unchanged: `ontology_action` keeps `DestructiveHint =
true`, so interactive clients still prompt; the per-action tools derive
`IdempotentHint` from the descriptor (section 9).

## 7. Relation preconditions are enforced at the dispatcher

`AddOntology` now wraps whichever `IActionDispatcher` you register — including
one supplied through `UseActionDispatcher<T>()` — in two decorators before
your own `DispatcherDecorators` run:

1. `RelationAuthorizationActionDispatcher` loads authoritative target facts
   through `IActionFactResolver`, evaluates the action's hard formula, and
   fails closed. A hard formula that contains `RelationHolds` is **always**
   enforced, regardless of `ActionDispatchOptions.EnforcePreconditions`; other
   hard predicates are enforced when that option is set. Unknown is not
   allowed: a missing resolver, an absent custom evaluator, or an evaluator
   failure produces `Indeterminate`, which is refused with a structured log
   entry.
2. `AuthorityAuthorizationActionDispatcher` enforces `RequiredAuthority`
   (section 8).

`RelationHolds(name, path...)` is decided against the calling principal: the
principal must be reachable from the action target by following the link path
and then the named relation. Register an `IActionRelationResolver` (the default
walks `IObjectSetProvider`) and, if relation facts live outside the object
sets, an `IActionFactResolver`. A precondition that was declared with
`RequiresRelation(...)` on 2.10.0 and never enforced is enforced now.

Discovery is principal-aware for the same reason: the async overloads in
section 5 evaluate relation predicates for the calling principal, so an action
whose relation the caller does not hold is reported `Unavailable` rather than
offered.

## 8. Declare the authority lattice

Authority is a product order, not the orchestration `Capability` flags enum. A
domain declares independent axes ordered weakest to strongest, positions each
named authority on **every** axis, and actions name the authority they
require:

```csharp
protected override void Define(IOntologyBuilder builder)
{
    builder.AuthorityAxis("Clearance", "Public", "Internal", "Restricted");
    builder.AuthorityAxis("Role", "Viewer", "Trader", "RiskOfficer");

    builder.Authority("Trader")
        .At("Clearance", "Internal")
        .At("Role", "Trader");

    builder.Authority("RiskOfficer")
        .At("Clearance", "Restricted")
        .At("Role", "RiskOfficer")
        .Implies("Trader"); // verified against the product order at graph freeze

    builder.Object<Position>(obj =>
    {
        obj.Action("Activate")
            .RequiresAuthority("Trader")
            .Requires(p => p.Status == PositionStatus.Pending)
            .Ensures(p => p.Status == PositionStatus.Active)
            .Modifies(p => p.Status);

        obj.Action("Close")
            .RequiresAuthority("RiskOfficer")
            .Modifies(p => p.Status);
    });
}
```

Descriptor-first authoring sets `ActionDescriptor.RequiredAuthority`; the
lattice itself is built from `AuthorityAxisDescriptor` and
`AuthorityDescriptor` (`Coordinates` per axis, optional
`ExplicitImplications`) and exposed as `OntologyGraph.GetAuthorityLattice(domainName)`.
`AuthorityLattice.Satisfies(granted, required)` is the pointwise comparison,
`Join(names)` the least requirement stronger than every named authority (this
is what sequential composition and workflow bindings compute), and
`IsAtMost(candidate, limit)` the authority arm of refinement.

Dispatch is fail-closed. When an action declares `RequiredAuthority`, at least
one literal in the principal's `GrantedAuthorities` must satisfy it on every
axis; a grant the domain's lattice does not define satisfies nothing; and an
action absent from the frozen graph is refused. An action without
`RequiredAuthority` passes through unchanged. The result is a failed
`ActionResult` naming the principal and the missing authority, not an
exception.

`AONT214` rejects an invalid lattice at both the analyzer and graph-freeze
tiers: an authority that omits an axis, names an unknown level, is declared
but never required, or whose `Implies` contradicts the product order.

## 9. Declare action frames and idempotence

`TouchedResources` is the action's **frame** — the set of resources it may
change. Fluent `Modifies`, `CreatesLinked`, and `EmitsEvent` add their
resources by construction; `Touches(ActionResource)` declares a write the
ontology cannot infer, such as an external system. Descriptor-first actions
set `TouchedResources` directly:

```csharp
using Strategos.Ontology.Descriptors;

obj.Action("Settle")
    .Requires(p => p.Status == PositionStatus.Active)
    .Modifies(p => p.Status)                        // Property("Status")
    .EmitsEvent<PositionSettled>()                  // Event("PositionSettled")
    .Touches(ActionResource.External("ledger"))     // an effect outside the graph
    .Idempotent();

// Descriptor-first equivalent
new ActionDescriptor(subject, "Settle", "Settle a position")
{
    TouchedResources =
    [
        ActionResource.Property("Status"),
        ActionResource.Event("PositionSettled"),
        ActionResource.External("ledger"),
    ],
    Idempotent = true,
};
```

`ActionResource` has four kinds: `Property`, `Link`, `Event`, and `External`.
The frame is immutable once the descriptor is built.

The frame is what makes composition sound: a predicate whose resources are
disjoint from an action's frame has the same truth value before and after it
(non-interference), so requirements on untouched state survive. Three
diagnostics guard it:

- `AONT215` — a mutating postcondition names a resource absent from the frame.
  Add it to the frame if the implementation may write it; otherwise correct the
  effect declaration.
- `AONT216` — an authored compensator disagrees with the mechanically derived
  inverse (requirement, guarantee, frame, or authority). Reported at the
  analyzer tier as `NotConfigurable` and refused again at graph freeze, so
  disabling analyzers does not admit it. Section 19 covers the compensation
  contract.
- Graph freeze also rejects a guarantee about a resource outside the frame
  unless it already follows from the hard requirements (`AONT221`).

`Idempotent()` (descriptor `Idempotent`) declares that repeating the action
has the same externally observable effect. `ReadOnly()` implies it by
construction; a descriptor-first action that sets `IsReadOnly` without
`Idempotent` is `AONT213`. The MCP surface derives each per-action tool's
`ToolAnnotations.IdempotentHint` from the flag, and
`ActionSemanticSummary` exposes `RequiredAuthority` and `TouchedResources` to
agents so they can plan against effects before invoking.

## 10. Author contracts in TypeSpec with the action decorators

`Strategos.Contracts` 0.9.0 adds `extern dec` decorators for ontology
operations, and 0.10.0 makes the requirement decorators typed:

| Decorator | Emits |
|---|---|
| `@objectKind(domain, objectType, kind)` | The action's ontology subject and object kind. |
| `@authority(name)` | `RequiredAuthority`. |
| `@relation(name, ...linkPath)` | Sugar for a hard `relation-holds` requirement. |
| `@requires(predicate, strength?, description?)` | A typed hard or soft `ActionPredicateV1` requirement. |
| `@ensures(predicate, description?)` | A typed post-state guarantee. |
| `@clients(...names)` | `AllowedClients`. |
| `@confirm(required)` | `RequiresConfirmation`. |
| `@readOnly` / `@idempotent` | `IsReadOnly` / `Idempotent`. |

The decorators emit language-neutral `x-strategos-*` JSON Schema metadata
(`x-strategos-requires-v1`, `x-strategos-ensures-v1`, and so on). The C#
codegen extension — an internal `ISchemaEmissionExtension` seam in
`Strategos.Contracts.Codegen`, not a consumer surface — emits immutable
descriptors with `DescriptorSource.HandAuthoredContract`. Unknown predicate
discriminators are rejected rather than read as `Custom` or `True`.

The 0.12 decorator surface does not author frames. A TypeSpec `@ensures` fact
must already follow from a hard `@requires` fact, or graph freeze rejects it
as a guarantee about untouched state. Author state-changing actions on the CLR
descriptor or fluent surface until a versioned contract frame exists. Section
20 shows the full decorator syntax.

## 11. Give every action an ontology subject

`ActionDescriptor` now requires `ActionSubject(DomainName, ObjectTypeName)`.
Use stable ontology names rather than `typeof(T)`, assembly-qualified names, or
other CLR identity:

```csharp
// before
var action = new ActionDescriptor("Activate", "Activate a position");

// 3.0
var subject = new ActionSubject("Trading", "Position");
var action = new ActionDescriptor(subject, "Activate", "Activate a position");
```

Actions created through `obj.Action(...)` receive the containing domain and
object descriptor names automatically. Descriptor-first and polyglot actions
must supply the subject directly. Graph freeze rejects a subject that does not
match the containing object with `AONT221`.

The same identity is now required when constructing an `ObjectSet<T>`
directly. The descriptor-name-only constructor could not identify a domain and
has been removed; use `IOntologyQuery.GetObjectSet<T>(name)` when possible, or
pass an explicit subject:

```csharp
var positions = new ObjectSet<Position>(
    new ActionSubject("Trading", "Position"),
    provider,
    dispatcher,
    eventStreamProvider);
```

## 12. Replace legacy precondition initializers

`ActionPrecondition.Expression`, `Kind`, `LinkName`, and legacy relation
fields are no longer writable inputs. `PreconditionKind` is removed. Construct
the precondition from an `ActionPredicate`; `Expression` remains a read-only
canonical display projection:

```csharp
// before
new ActionPrecondition
{
    Expression = "Status == Active",
    Description = "The position is active.",
    Kind = PreconditionKind.PropertyPredicate,
    Strength = ConstraintStrength.Hard,
};

// 3.0
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

## 13. Separate guarantees from effects

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

## 14. Use typed ActionFacts

Runtime precondition APIs no longer accept
`IReadOnlyDictionary<string, object?>`. Build immutable, typed facts:

```csharp
// before
var knownProperties = new Dictionary<string, object?>
{
    ["Status"] = PositionStatus.Active,
    ["Note"] = null,
};

// 3.0
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

## 15. Handle tri-state discovery

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

## 16. Register runtime resolvers and enforce authoritative facts

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

## 17. Migrate explicit composition

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
must have the same `ActionSubject` in 3.0. Use
`ActionCalculus.Identity(subject)` for the distinct empty operand; do not model
identity as an ordinary true/true action.

## 18. Bind workflow implementations by typed identity

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

If code initialized descriptors directly, replace the string property:

```csharp
// before
new ActionDescriptor(subject, "Publish", "Publish a position")
{
    BindingType = ActionBindingType.Workflow,
    BoundWorkflowName = "publish-position",
};

// 3.0
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
`EscalateTo` approval routing also remain outside the proved 3.0 subset because
those routes are not yet represented by the closed proof graph.
No binding proof or runtime enforcement is added to an unbound workflow. The
topology lowering fixes shipped with this release still apply to every workflow.

The 3.0 proof reads one action catalog: the source declarations of the current
compilation, the workflow JSON imported as `AdditionalFiles`, and the action
contracts that referenced assemblies carry in their exported proof catalogs. It
does not inspect arbitrary ontology or workflow declarations inside referenced
binaries, execute `IOntologySource`, or repeat the proof at runtime.
Source-visible `DomainOntology.Define` bodies form the declaration catalog
regardless of which domains a particular host later registers. A direct
`ActionDescriptor` construction is cataloged only when it is inline in the
`ObjectTypeDescriptor.Actions` collection passed through
`ObjectTypeFromDescriptor` from such a body.

A catalog carries action contracts and never workflow models, so the **workflow**
must be lowered in the compilation being built. A binding whose workflow is absent
here is deferred to the compilation that lowers it, and a leaf application declares
itself the last compilation with
`<StrategosProofRequireLocalBindings>true</StrategosProofRequireLocalBindings>`,
which turns a deferral into an error.

The proof runs inside the `LevelUp.Strategos.Generators` source generator, and so
does the export. A project that references `LevelUp.Strategos.Ontology` but not the
generator package has nothing to export its contracts, so its `BoundToWorkflow(...)`
binding can never be discharged by anyone; that is refused with `AONT222`. Add the
generator package to every project that declares a workflow-bound action. The
generator emits nothing else for a project with no workflows, so the cost is a
development-time package reference.

The generator catalogs a binding only when the whole fluent chain from
`obj.Action(...)` to `.BoundToWorkflow(...)` is one expression inside
`DomainOntology.Define`. A `Define` body factored into helper methods, a
binding applied to a builder held in a local, or a chain routed through an
extension method fails closed as `AGWF042`. Inline the chain before upgrading.
The same closure rule applies to the workflow side: a bound workflow's
`Definition` must be one direct `Workflow<TState>.Create(...)...Finally<TStep>()`
chain. A `Definition` that delegates to a helper method still generates the
same saga it did before the binding proof existed, but it cannot be proved and
reports `AGWF042` while bound.

Every proof diagnostic carries the `NotConfigurable` tag — `AGWF039` (bound
workflow not found), `AGWF040` (action reference invalid), `AGWF041` (refuted),
`AGWF042` (unprovable), `AGWF043` (emission collision), `AGWF044` (invalid
inverse), `AGWF045` (underivable rollback scope), `AGWF046` (contract not
exportable), `AGWF047` (referenced catalog unreadable) and `AGWF048` (duplicate
identity across catalogs). `<NoWarn>`, `#pragma warning disable`, and
`.editorconfig` severity entries do not suppress or downgrade any of them.

`AGWF039` and `AGWF040` were configurable in 3.0.0-rc.1, because a cross-assembly
layout had no other exit and the alternative was deleting the binding. Exporting
the contract is that exit, so the exemption is withdrawn. **Delete any
`<NoWarn>AGWF039</NoWarn>` or `<NoWarn>AGWF040</NoWarn>` from your project files**:
the entry is now inert, and it was very likely silencing exactly the layout this
release proves. An internal failure of the proof itself
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

## 19. Replace authored rollback lists with typed inverse actions

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

In 3.0, typed derived compensation requires `SagaDocument` persistence.
`EventSourced` workflows own their `ApplyEvent` implementation, and that method
may legally ignore an unfamiliar generated rollback-completed event. Strategos
cannot use method presence as proof that the inverse `UpdatedState` will be
folded identically in the live saga and during Marten replay, so the source
generator reports `AGWF045` instead of emitting a rollback-safety claim.

There is no in-place migration from event-sourced persistence to
`SagaDocument` persistence. The two substrates have different identity and
different replay semantics, and this release ships no procedure, tool, or
supported query for converting a live event stream into a saga document. An
event-sourced workflow has two options in 3.0: keep it on legacy untyped
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

### Saga transitions are revision-guarded

Generated sagas now implement `JasperFx.IRevisioned`, so Wolverine persists each
saga transition with Marten's revision-guarded `UpdateRevision` instead of a
plain, last-write-wins `Update`: two deliveries that concurrently transition the
same saga no longer both commit, and the loser is retried three times against
the winner's revision before it dead-letters. No schema change is required —
Marten's `RevisionColumnInt32` mapping tolerates the `bigint mt_version` column
an existing saga table already has — but a host that previously saw silent
last-write-wins now sees `JasperFx.ConcurrencyException` retries in its logs and,
where a transition genuinely cannot be re-applied, a `wolverine_dead_letters` row.

### Execution identity and the compensation deadline

- Use `StepContext.ExecutionId` as your idempotency key. A step that produces external effects should key those effects on it: Wolverine's inbox is at-least-once, and every redelivery of one dispatch carries the same `ExecutionId`.
- `StepContext.CorrelationId` is for tracing only. Its textual shape is not part of the supported contract; do not parse it to recover an execution or rollback identity, which is what consumers had to do before `ExecutionId` existed.
- If you constructed a `StepContext` by hand (tests, custom hosts), it now requires `ExecutionId`, and `IsCompensation` is no longer settable — set `RollbackId` and `IsCompensation` follows.
- `Compensate<T>(TimeSpan timeout)` and `Compensate<T>(WorkflowActionReference, TimeSpan timeout)` bound one execution of the inverse step (distinct from `WithTimeout`, which bounds the forward step). A non-positive value is `AGWF021` in the DSL and `ArgumentOutOfRangeException` on `CompensationConfiguration`; omitted, the generated runtime applies a 300-second inverse deadline.

See [Mechanically derived compensation](/reference/action-calculus/#mechanically-derived-compensation)
for the runtime and proof contract.

## 20. Upgrade TypeSpec and workflow wire metadata

This section is for a consumer that *authors* TypeSpec or workflow JSON. A
consumer that only deserializes it needs section 1.

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
diagnostic enum; all consumers must upgrade before producers emit them. The
0.12.0 schema also states, as a JSON Schema `if`/`then` conditional, that a
present `inverseAction` requires `requiredOnFailure = true`, and it rejects an
empty or whitespace-only `compensationStepType` — a document that previously
slipped through and produced a saga with no compensation is now a build error.

## 21. Invalidate graph-version caches once

The canonical graph hash now includes action subjects, normalized typed
requirements, guarantees, custom evaluator keys/arguments/read sets, and the
expanded action contract metadata. Descriptions and display expressions remain
excluded.

Every existing action-bearing graph receives a different
`OntologyGraph.Version` after this upgrade even when its apparent business
meaning is unchanged. This is intentional. Treat the first 3.0 deployment as
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

Contracts and packages:

- Upgrade every Contracts consumer to 0.12.0 *before* any producer restores
  3.0.0-rc.1; move a direct Contracts pin and the core package pin in one
  commit.
- Restore the 3.0.0-rc.1 package set together. Coming from 2.9.1, read the
  2.10.0 CHANGELOG section first.
- Note that `Strategos.Ontology.MCP.Hosting` carries the `ModelContextProtocol`
  2.2.0 SDK.

Workflows (correctness core):

- Drain or terminate fork, branch, and multi-step approval-chain instances that
  are in flight on 2.10.0; they do not recover under the new routing.
- Run the `Phase` storage query against every saga table. Ordinal storage
  (Newtonsoft default) is a data migration; name storage is not.
- Update tests and tooling that assert on `ValidTransitions` or
  `IsValidTransition` for fork, branch, loop-only, and `OnFailure`-only
  workflows.
- Rename branch-case steps that share a name (`AGWF003` now fails the build).
- Resolve `AGWF035`, `AGWF037`, and `AGWF038`; review the re-aimed `AGWF022`.
- Switch code that names a fork path's generated completed event, where the
  step type is reused on another path, to the path-qualified
  `{PathId}_{PhaseName}Completed` name.
- Add a `DescriptorSource.HandAuthoredContract` arm to any exhaustive switch.

Principals, authority, and frames:

- Construct every `ActionContext` with an `ActionPrincipal`; pass the
  principal as the first argument to `ObjectSet<T>.ApplyAsync` and to the
  async `IOntologyQuery` discovery overloads.
- Pass `ActionContext.ActionDescriptor` from the frozen graph, never a copy.
- MCP hosts: confirm the transport authenticates callers and supplies
  `strategos:principal_type` (plus `NameIdentifier`/`sub` and any
  `strategos:authority` claims), or register an `IActionPrincipalResolver`.
- Register `IActionRelationResolver` / `IActionFactResolver` as needed; every
  `RequiresRelation` declaration is now enforced at dispatch and evaluated
  during principal-aware discovery.
- Declare authority axes and literals for any action that uses
  `RequiresAuthority`; every literal must be positioned on every axis and be
  required by at least one action (`AONT214`). Populate
  `ActionPrincipal.GrantedAuthorities` from your identity source.
- Declare frames: keep `Modifies` / `CreatesLinked` / `EmitsEvent` accurate
  and add `Touches(ActionResource.External(...))` for writes the ontology
  cannot see (`AONT215`). Mark repeat-safe actions `Idempotent()` (`AONT213`
  for read-only actions).
- If you author TypeSpec ontology contracts, adopt the 0.9.0 decorators and
  the 0.10.0 typed `@requires` / `@ensures`.

Typed action contracts, bindings, and compensation:

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
  for the typed `Compensate<T>(WorkflowActionReference)` overload, the
  `Compensate<T>(TimeSpan)` overloads, `Performs(...)`, `Join<TStep>(configure)`,
  and the approval-chain `Then<TStep>(configure)`; remove repeated compensation
  calls that previously relied on last-write-wins.
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
- Expect `JasperFx.ConcurrencyException` retries in saga logs; treat a
  `wolverine_dead_letters` row for a saga transition as an incident.
- Invalidate caches keyed by the pre-3.0 graph hash.
- Run the full solution and documentation builds before deployment.

For the formal contract and exact proof rules, see
[Typed action calculus](/reference/action-calculus/).
