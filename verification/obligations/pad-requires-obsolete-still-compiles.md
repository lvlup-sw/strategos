# pad-requires-obsolete-still-compiles

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 38, 39, 58, 71–73, 134–136, 143

| | |
|---|---|
| **Claim** | `IActionBuilder<T>.Requires` is obsolete, points at `ActionDescriptor.Preconditions`, has no fluent successor, and stays so existing `Object<T>` authoring still compiles. |
| **Scope** | Interface + implementation; `Directory.Build.targets` CS0618; packaged ontology README. |
| **Consequence** | Removing the method would break existing `Object<T>` authors. Inventing a fluent successor would contradict T6. Treating Obsolete as a behavior change would be a lie — the body still lowers to `Preconditions`. |
| **Proof rung** | Compiler and type system |
| **Proof artifact** | `[Obsolete]` on interface and implementation; method body unchanged (`ActionBuilderOfT.cs:77-90`). Test projects add CS0618 to `NoWarn` so existing call sites still compile under the repo’s warning policy. |
| **Why not cheaper** | Obsolete is a compiler feature. Generation is not involved. |
| **Failure signal** | CS0618 in consumer builds that treat warnings as errors. The method still compiles when warnings are warnings. |
| **Rollback** | Remove the attribute. No runtime behavior to reverse. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High for compile-and-lower. |

**Open questions:**

- None on “the method remains and still lowers.” PublicAPI tracking of Obsolete is a different surface (survey L3); this lens did not re-read those files.

## Discriminating detail

```39:40:src/Strategos.Ontology/Builder/IActionBuilderOfT.cs
    [Obsolete("Use ActionDescriptor.Preconditions to declare action preconditions. There is no fluent successor.")]
    IActionBuilder<T> Requires(Expression<Func<T, bool>> predicate);
```

Implementation (`ActionBuilderOfT.cs:77-90`) still appends an `ActionPrecondition` and returns `this`. `RequiresSoft` / `RequiresLink` are not marked obsolete and are not new successors for the hard predicate.

`Directory.Build.targets:3-5` adds CS0618 to `NoWarn` for every test and benchmark project, matching commit `d01a78f` / inventory 73.

Guide docs (`docs/src/content/docs/guide/ontology/index.md:65-66`) add a caution. The **packaged** ontology README (`src/Strategos.Ontology/README.md:33-34`) still demos `.Requires` with no obsolete note. That is delivery past the “still compiles” claim into a shipped sample that hides the attribute. See also inventory 136 (guide) vs the package README.

Inventory 39 (RS0016/RS0017 tracked public-API change): not re-validated file-by-file in this lens; PublicAPI.Unshipped is the expected artifact.

## Disposition

- Inventory 38, 58, 71, 72, 134, 135, 143: **supported** — obsolete, no new fluent successor, method remains, still lowers.
- Inventory 73: **supported** in `Directory.Build.targets`.
- Packaged README demo without a note: **implemented past the description** (a shipping sample still presents the obsolete API as the happy path).
