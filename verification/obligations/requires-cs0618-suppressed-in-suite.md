### requires-cs0618-suppressed-in-suite — Requires deprecation must remain observable as CS0618

| | |
|---|---|
| **Claim** | A clean in-repo test compile must not be evidence that `IActionBuilder<T>.Requires` produces CS0618. Tests that still call `Requires` to exercise Preconditions lowering must not compile with CS0618 suppressed if they are the cited proof that authors see the warning. |
| **Scope** | S5. `src/Directory.Build.targets:3-5` (this wave adds `CS0618` to `NoWarn` for every test and benchmark project). New test `Requires_IsObsolete_PointingAtActionDescriptorPreconditions` in `IActionBuilderTests.cs:65-76`. Lowering tests that still call `.Requires(...)`. |
| **Consequence** | The only new proof this wave added is reflection on `ObsoleteAttribute`. The suite that still calls `Requires` compiles clean because the warning is suppressed. Removing the attribute and deleting or skipping the reflection test leaves the lowering tests green. Authors with `TreatWarningsAsErrors` are the ones who would see CS0618; the in-repo suite cannot. |
| **Proof rung** | Compiler and type system |
| **Proof artifact** | A compile of a non-test subject (sample, fixture project, or `NoWarn`-free consumer) that calls `Requires` and fails on CS0618. The reflection test proves the attribute exists; it is not this artifact. |
| **Why not cheaper** | Generation does not emit the obsolete call. The attribute on the interface is a type fact; consumer-visible warning-as-error is a compiler setting on the *caller's* project. This wave's `NoWarn` removes that setting from the only in-repo callers. |
| **Failure signal** | Nothing in this suite. CS0618 in a consumer build is the channel, and this repo's tests are excluded from it. |
| **Rollback** | Remove `CS0618` from `Directory.Build.targets` and the `[Obsolete]` attributes. Existing `Object<T>` authoring still compiles either way; the warning is the only consumer-visible change. |
| **Lenses** | False-Green Shapes |

**Open questions:**

- Do any in-repo non-test projects call `Requires` without the new `NoWarn`? If yes, they are the missing compiler proof. If no, the reflection test is the only in-repo signal and CS0618 is unobserved in this repository.
- The pre-existing `IActionBuilder_*_ReturnsSelf` NSubstitute tests in the same file: are they cited anywhere as coverage for this wave? They cannot fail for `Requires` or for fluent-return on `ActionBuilder`. This lens did not promote them as their own obligation because they were not added on `324768f` and they do not claim the obsolete contract.

## What led here

This wave marks `Requires` obsolete and adds `CS0618` to test `NoWarn` so lowering tests still compile. Competing explanation: the new reflection test is the intended proof, and `NoWarn` is only so existing tests can keep calling the method. Discriminating detail: a green lowering test after `NoWarn` is indistinguishable from "the method is not obsolete." The reflection test can fail (remove the attribute). The compile of every other in-repo caller cannot.

NSubstitute tests in `IActionBuilderTests.cs:9-62` (unchanged): `Substitute.For<IActionBuilder>()` then `.Returns(substitute)` then assert equal. Kill probe: no edit to `ActionBuilder` / `ActionBuilderOfT` turns them red. Examined; not promoted as a separate slug.

## Code read (revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`)

- `src/Directory.Build.targets:3-5` — comment: "CS0618: IActionBuilder<T>.Requires is obsolete (#115); tests still exercise its Preconditions lowering."
- `src/Strategos.Ontology/Builder/IActionBuilderOfT.cs:39-40` — `[Obsolete("... no fluent successor.")]`.
- `src/Strategos.Ontology/Builder/ActionBuilderOfT.cs:77-78` — same attribute on the implementation.
- `src/Strategos.Ontology.Tests/Builder/IActionBuilderTests.cs:65-76` — reflection: attribute present; message `Contains` `Preconditions` and `"no fluent successor"`.
- `git diff 4d060f4...324768f` on `IActionBuilderTests.cs` adds only that reflection test.

## Kill probe

- Remove `[Obsolete]` from the interface; leave `Directory.Build.targets` as is. Reflection test red; every lowering test still green.
- Keep `[Obsolete]`; the lowering tests that call `Requires` stay green because of `NoWarn`. They cannot go red for a missing warning.
- Delete `Description` from `ActionBuilder`. The NSubstitute `IActionBuilder_Description_ReturnsSelf` test stays green.

## Failure scenario

A follow-up removes `[Obsolete]` from the interface as "cleanup" and deletes the one reflection test as "the attribute is obvious from the docs." Lowering tests still compile. CI is green. Consumers no longer see CS0618. The CHANGELOG line that the method is obsolete remains.

## Open questions (full stakes)

### Do any in-repo non-test projects call `Requires`?

If a sample or production authoring path still calls it without `NoWarn`, CS0618 is observable there and this obligation's "nothing in this suite" failure signal is too strong — the channel exists, it is just not the test projects. If every remaining caller is under the new `NoWarn`, the warning is a consumer-only event with no in-repo compile that can fail.

### Are the NSubstitute tests cited as this wave's coverage?

If a review or the plan points at `IActionBuilderTests` as a whole, the class is a mix of cannot-fail mocks and one reflection test. That would raise the standing of a separate cannot-fail obligation. If they are leftover fluent-return decoration and nobody cites them, leaving them unpromoted is correct.
