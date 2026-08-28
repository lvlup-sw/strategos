# compat-requires-obsolete-warning-break — Requires stays callable and becomes a warning-break for TreatWarningsAsErrors

| | |
|---|---|
| **Claim** | `IActionBuilder<T>.Requires` remains on the public surface and keeps writing `ActionDescriptor.Preconditions`. Marking it `[Obsolete]` must not remove or rename the method, and it is a source warning-break for consumer projects that elevate CS0618. |
| **Scope** | Published fluent API `IActionBuilder<T>.Requires` / `ActionBuilderOfT.Requires`, `PublicAPI.Unshipped.txt:109`, packaged `Strategos.Ontology/README.md`, and every existing `Object<T>` author. |
| **Consequence** | A consumer with `TreatWarningsAsErrors` fails the build on an unchanged call site. A consumer who follows the obsolete message has no fluent successor and must leave the CLR-generic surface. Runtime behavior of the method is unchanged. |
| **Proof rung** | Compiler and type system |
| **Proof artifact** | The method remaining on the interface (`IActionBuilderOfT.cs:39-40`) and implementation (`ActionBuilderOfT.cs:77-90`). `IActionBuilderTests.Requires_IsObsolete_PointingAtActionDescriptorPreconditions` asserts the attribute, not that a consumer call still compiles under warnings-as-errors. |
| **Why not cheaper** | Obsolete is not generated. The compiler is exactly the layer that emits CS0618 and that still binds the call. A test that reflects the attribute (rung 4) re-states what the compiler already knows. |
| **Failure signal** | CS0618 at consumer compile. In this repository the signal is suppressed (`compat-cs0618-nowarn-unscoped`). Packaged README still demos `.Requires` with no obsolete note (`src/Strategos.Ontology/README.md:34`). |
| **Rollback** | Remove the `[Obsolete]` attributes. Does not reverse any consumer who already migrated off the method (there is no fluent successor to migrate *to*). |
| **Lenses** | 5. Exposure And Compatibility (diff form) |
| **Confidence** | high. |

**Compatibility class:** deprecation without fluent successor; warning-level source break presented as no-behavior-change.

**Impact**

- No removed or renamed method. PublicAPI still lists `IActionBuilder<T>.Requires(...)` (`PublicAPI.Unshipped.txt:109`) with no Obsolete marker (RS0016/RS0017 track declare/remove only).
- No default change of the method’s behavior. Implementation still appends a hard `ActionPrecondition` (`ActionBuilderOfT.cs:83-89`).
- No serialization or persistence change.
- Docs on pages this diff edited (`docs/.../guide/ontology/index.md:65-66`) name the obsolete and also keep the `.Requires` sample (`:46`). Platform architecture still says “Calling `.Requires()` multiple times adds AND-combined conditions” (`platform-architecture.md:1319`).

**Reverse dependency closure:**

1. Interface + implementation in `Strategos.Ontology`.
2. Existing `Object<T>` fluent authors (in-repo samples and every out-of-repo domain ontology).
3. Analyzer / query path that reads `ActionDescriptor.Preconditions` (historically populated by `.Requires`).
4. PublicAPI analyzers — will not fail if Obsolete is dropped or added; they already have the method.
5. Test/benchmark compile surface — see `compat-cs0618-nowarn-unscoped`.

**Reverses?** Yes, by removing the attribute. The method never left. Consumer source that already deleted `.Requires` calls has nowhere to go on the fluent surface.

**Open questions:**

- Do published consumer projects compile with `TreatWarningsAsErrors`? If yes, this is a hard source break at the next Ontology package upgrade. If no, it is a warning they can ignore.
- Is the packaged README omission intentional so the sample still builds without `#pragma`? That would mean the shipping sample teaches the obsolete API.

**What is expensive to find again**

`PublicAPI.Shipped.txt` is empty. The method has been on Unshipped through published product versions. Obsolete is therefore a change to a surface that is already in the wild, tracked as if it were not yet shipped.
