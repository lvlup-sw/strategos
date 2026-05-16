---
title: Validation
sidebar:
  order: 6
---

`ontology_validate` is the MCP tool external agents call before they act. It takes a `DesignIntent` — a description of what the agent plans to read or change — and returns a `ValidationVerdict` summarizing hard constraint failures, soft warnings, the blast radius of the proposed change, and any pattern violations. Agents that consult the verdict before dispatch get structured pushback instead of after-the-fact rejection.

## The verdict shape

`ValidationVerdict` carries five fields:

```csharp
public sealed record ValidationVerdict(
    bool Passed,
    IReadOnlyList<ConstraintEvaluation> HardViolations,
    IReadOnlyList<ConstraintEvaluation> SoftWarnings,
    BlastRadius BlastRadius,
    IReadOnlyList<PatternViolation> PatternViolations,
    CoverageReport? Coverage);
```

`Passed` is `true` when `HardViolations` is empty and every entry in `PatternViolations` has `Severity == Warning`. Any pattern violation at `Error` severity fails the verdict regardless of constraint state.

`HardViolations` and `SoftWarnings` are buckets of `ConstraintEvaluation` records. Each evaluation names a precondition, an `IsSatisfied` flag, and an optional `FailureReason`. Hard and soft are split by the constraint's `ConstraintStrength`: hard preconditions block the dispatch; soft ones surface as advisory warnings the agent can choose to address.

`Coverage` is populated only when an `IOntologyCoverageProvider` is registered in DI; otherwise it is `null`. Tightening coverage to fail-closed is deferred to a follow-up.

## Blast radius

`BlastRadius` captures the downstream reach of the proposed change:

```csharp
public sealed record BlastRadius(
    IReadOnlyList<OntologyNodeRef> DirectlyAffected,
    IReadOnlyList<OntologyNodeRef> TransitivelyAffected,
    IReadOnlyList<CrossDomainHop> CrossDomainHops,
    BlastRadiusScope Scope);
```

`DirectlyAffected` is the seed set the caller passed in via `DesignIntent.AffectedNodes`. `TransitivelyAffected` is the set the BFS expansion reaches through derivation chains, postconditions, and link traversal. `CrossDomainHops` records each traversal that crosses a domain boundary so consumers can spot inter-domain coupling without re-walking the graph.

`Scope` classifies the reach:

- `Local` — one object type in one domain.
- `Domain` — multiple object types, one domain.
- `CrossDomain` — at least one cross-domain hop.
- `Global` — cross-domain hops touch four or more distinct domains.

Expansion is bounded by `BlastRadiusOptions.MaxExpansionDegree` (default 16). The result is deterministic: the same graph plus the same seed set yields the same lists, ordered by `(DomainName, NodeName)`.

`IOntologyQuery.EstimateBlastRadius` is the primitive — `OntologyValidateTool` calls it under the hood, but you can call it directly when you want the reach without the full verdict.

## Pattern violations

`IOntologyQuery.DetectPatternViolations` returns a list of `PatternViolation` records. The 2.5.0 v1 set covers four cases:

| Pattern | Severity | What fires it |
|---|---|---|
| `Computed.Write` | Error | A `ProposedAction` writes to a property whose `IsComputed == true`. |
| `Link.MissingExtensionPoint` | Error | A `ProposedAction` creates a link whose source object type lacks a matching `ExtensionPoint` on the target. |
| `Action.PreconditionPropertyMissing` | Error | An action's precondition reads a property name not present on `AcceptsType`. |
| `Lifecycle.UnreachableInitial` | Warning | An object type declares a `Lifecycle.Initial` state but no incoming transition can produce it. |

Each violation names its pattern, a human-readable description, the subject `OntologyNodeRef`, and a `ViolationSeverity` of `Warning` or `Error`.

## A worked example

Suppose an agent proposes to call `CancelOrder` on a `trading.Order` whose lifecycle requires the order to be `Pending`:

```csharp
var intent = new DesignIntent(
    AffectedNodes: [new OntologyNodeRef("trading", "Order", Key: "ORD-7421")],
    Actions:
    [
        new ProposedAction(
            ActionName: "CancelOrder",
            Subject: new OntologyNodeRef("trading", "Order", "ORD-7421"),
            Arguments: new Dictionary<string, object?> { ["reason"] = "duplicate" }),
    ],
    KnownProperties: new Dictionary<string, object?> { ["Status"] = "Filled" });

var verdict = validateTool.Validate(intent);
```

The dispatcher resolves `CancelOrder`'s descriptor, sees its precondition (`Status == "Pending"`), evaluates it against the supplied `KnownProperties`, and finds the constraint unsatisfied. The verdict comes back with `Passed = false`, one entry in `HardViolations` naming the precondition, a `BlastRadius` scoped `Local` (one type, one domain), and an empty `PatternViolations` list. The agent reads `FailureReason` and reports back rather than calling the action.

If `KnownProperties` is null or omits the referenced property, the evaluator returns satisfied — optimistic 2.5.0 behavior. Link-existence preconditions remain deterministic and still fail closed when the link is absent. Populate `KnownProperties` when you need deterministic verdicts on property predicates.
