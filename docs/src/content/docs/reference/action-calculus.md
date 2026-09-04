---
title: Action calculus contracts
description: Principal, relation, authority, retry-safety, and resource-frame semantics for ontology actions.
---

An ontology action is an immutable contract. Its preconditions say what must be
true, its postconditions say what it changes, `RequiredAuthority` says what the
caller must be granted, and `TouchedResources` bounds the part of the world the
action may affect.

## Authority is a product order

Authority uses its own vocabulary; it is not the orchestration `Capability`
flags enum. A domain declares independent axes from weakest to strongest and
positions every named authority on every axis:

```csharp
builder.AuthorityAxis("access", "read", "write", "owner");
builder.AuthorityAxis("sensitivity", "public", "internal", "restricted");

builder.Authority("internal.writer")
    .At("access", "write")
    .At("sensitivity", "internal")
    .Implies("public.reader");
```

A grant satisfies a requirement only when it is at least as strong on every
axis. Sequential composition computes its authority requirement as the
pointwise join of the component requirements.

## Frames and non-interference

`TouchedResources` is the action's frame. Fluent `Modifies`, `CreatesLinked`,
and `EmitsEvent` declarations add their corresponding resource to the frame by
construction. Descriptor-first actions are checked at graph construction;
literal descriptor initializers are also checked by the Roslyn analyzer.
Postcondition mutation outside the frame is `AONT215` and fails the build.

The contract callers may rely on is:

> For every predicate whose resources are disjoint from `TouchedResources`,
> the predicate has the same truth value before and after the action.

This makes local reasoning possible. The frame of `A ; B` is computed as the
union of both frames. Two actions are candidates for parallel execution only
when their frames are disjoint.

## Compensation

An action may name its compensating action with `CompensatedBy`. The
compensating action must declare the same frame, because an inverse restores
exactly the resources the forward action touched. `AONT216` rejects a missing
or frame-incompatible compensator. For a completed prefix `A ; B`,
`ActionCalculus.DeriveRollbackPlan` mechanically produces `B⁻¹ ; A⁻¹`; an
authored rollback order can be compared with that derived plan. The Roslyn
analyzer compares fluent compensation frames at compilation, and graph freeze
provides the descriptor-first backstop.

## Retry safety and relation authorization

`ReadOnly()` implies `Idempotent()` by construction, and descriptor-first input
is checked by `AONT213`. `RelationHolds` preconditions are evaluated at the
dispatcher boundary against the authenticated `ActionPrincipal`; failures deny
dispatch before the inner action handler runs.

## Contract-authored operations

`Strategos.Contracts` supplies TypeSpec `extern dec` decorators backed by a
JavaScript implementation. A contract operation can declare its owning object,
required authority, relation path, visible clients, confirmation posture, and
retry semantics:

```typespec
@objectKind("Trading", "Position", "entity")
@authority("position.reader")
@relation("owner", "Portfolio")
@clients("mcp", "web")
@confirm(false)
@readOnly
@idempotent
op inspectPosition(input: InspectPositionRequest): InspectPositionResult;
```

The decorators write `x-strategos-*` JSON Schema metadata. The contracts
code-generation pipeline consumes those extensions and emits
`ContractOntologyCatalog.ObjectTypes`, with every object marked
`DescriptorSource.HandAuthoredContract`. Compose the descriptors through
`IOntologyBuilder.ObjectTypeFromDescriptor` alongside the domain's authority
lattice. Contract metadata is deontic—what an action permits and requires—and
never contains or selects a runtime implementation.
