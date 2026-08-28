# requires-obsolete-still-mutates-preconditions

Lens: **3. Representable Invalid States**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Claim

Hard property-predicate preconditions must not remain writable by an `[Obsolete]` fluent that the public remarks say has no successor, while three sibling fluents stay current writers of the same `ActionDescriptor.Preconditions` list. One field, several unrestricted writers.

## What led here

#115’s mechanical task is `[Obsolete]` on `Requires` pointing at `ActionDescriptor.Preconditions`. The method still appends to `_preconditions`. `RequiresSoft` / `RequiresLink` / `RequiresLinkSoft` are not obsolete and write the same list. `ActionDescriptor.Preconditions` is also public `init`. Survey backbone item 8; wildcard W6.

## Code at this revision

- `src/Strategos.Ontology/Builder/IActionBuilderOfT.cs:35-46` — `Requires` is `[Obsolete("Use ActionDescriptor.Preconditions … There is no fluent successor.")]`. `RequiresSoft`, `RequiresLink`, `RequiresLinkSoft` have no attribute.
- `src/Strategos.Ontology/Builder/ActionBuilderOfT.cs:77-132` — `Requires` still `_preconditions.Add(...)` with `ConstraintStrength.Hard` (`:78-90`). The three siblings add Soft / LinkExists entries to the same list (`:93-131`).
- `src/Strategos.Ontology/Builder/ActionBuilderOfT.cs:172-184` — `Build()` assigns `Preconditions = _preconditions.ToList().AsReadOnly()`.
- `src/Strategos.Ontology/Descriptors/ActionDescriptor.cs:25-33` — `IsReadOnly` and `Preconditions` are independent `init` properties. Remarks say `Requires` is obsolete and has no fluent successor. Nothing stops `new ActionDescriptor(...) { Preconditions = … }` *and* a later builder write, or `IsReadOnly = true` with write `Postconditions` (documented must-not at `:19-23`, still representable).
- `src/Directory.Build.targets` — `CS0618` is in `NoWarn` for test/benchmark projects, so in-repo calls to `Requires` do not warn.

The obsolete attribute is a compile-time signal. It does not make the write unrepresentable. The named successor is an `init` list on a record, not a fluent, so CLR-generic `Object<T>` authors who need Hard predicates still have only the obsolete method or a post-hoc descriptor edit.

## Failure scenario

An author follows the obsolete message and tries to set `ActionDescriptor.Preconditions` from the fluent chain — there is no method. They keep calling `Requires`. With warnings-as-errors off (or in this repo’s tests, suppressed), the descriptor is valid and dispatch still evaluates the list (`OntologyQueryService` reads `Preconditions`). Soft and link constraints stay first-class fluent; Hard property predicates look deprecated but work. Two authoring worlds for one field. A later “remove the obsolete method” change silently drops Hard fluent predicates while Soft/Link remain.

`IsReadOnly: true` plus write `Postconditions` is a second pair on the same record: `DispatchReadOnlyAsync` keys on the boolean (`IActionDispatcher.cs:25`) and does not inspect postconditions.

## Why not cheaper

Rung 1: no generated builder.

Rung 2: either remove the write (method throws / is excluded from the interface implementation) or obsolete the whole fluent precondition family and make `Preconditions` a required construction path that the builder cannot also mutate. A single writer. `[Obsolete]` plus a live body is a loosened deprecation.

Rung 4: an NSubstitute test that the method still exists can pass by construction (survey lens 5). It does not close dual-write.

## Failure signal

CS0618 where not suppressed. In this repository’s tests, nothing. Runtime dispatch succeeds. “Nothing” for in-repo; a consumer with treat-warnings-as-errors sees a warning and a working write.

## Rollback

Revert the `[Obsolete]` attribute. Does not reverse descriptors already built via `Requires`. Removing the method body later is a breaking API change (RS0016/RS0017); the attribute claims that change has not happened.

## Open questions

- Is leaving `RequiresSoft` / `RequiresLink` / `RequiresLinkSoft` current an intentional split (only Hard property predicates move to the descriptor) or an incomplete obsolete? If intentional, the obligation narrows to “Hard predicates have two writers.” If incomplete, the whole fluent precondition surface is the unrestricted assignment.
- Can a consumer set `ActionDescriptor.Preconditions` on a descriptor that the fluent `Object<T>` path then overwrites in `Build()`? If the fluent path always replaces the list, descriptor-first init on a builder-produced descriptor is dead. Stakes: the named successor may be unreachable on the path the obsolete message points authors toward.

## What is expensive to find again

The obsolete message names `ActionDescriptor.Preconditions` as the replacement. That property is `init` on a record constructed at the end of a fluent `Build()`. Readers conclude the write moved. It duplicated.
