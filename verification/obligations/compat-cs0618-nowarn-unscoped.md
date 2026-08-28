# compat-cs0618-nowarn-unscoped — Test/benchmark NoWarn adds CS0618 for every obsolete API, not only Requires

| | |
|---|---|
| **Claim** | The in-repo test and benchmark compile surface must not hide CS0618 for APIs other than `IActionBuilder<T>.Requires`, and it must not be the proof that existing Requires call sites still compile. |
| **Scope** | `src/Directory.Build.targets` `NoWarn` when `IsTestProject` or the project name contains `Benchmarks`. Applies to every test and benchmark under `src/`. |
| **Consequence** | A newly obsoleted API in any in-repo test compiles clean. The deprecation this revision introduces is invisible on the only in-repo consumers. A later obsolete (unrelated to Requires) inherits the same silence. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | A check that `CS0618` is not in the global test `NoWarn`, or that it is granted only to files that call `Requires`. Today the token is appended on one line (`Directory.Build.targets:5`). |
| **Why not cheaper** | MSBuild warning sets are not a type-system fact and are not generated from the Obsolete attribute. A component test that calls `Requires` under this `NoWarn` cannot fail when the suppression is too broad. |
| **Failure signal** | Nothing. The warning is the signal, and it is disabled. |
| **Rollback** | Revert the `CS0618` token. Test projects that call `Requires` then warn (or fail under warnings-as-errors). That is the intended consumer signal. |
| **Lenses** | 5. Exposure And Compatibility (diff form) |
| **Confidence** | high. |

**Compatibility class:** changed default of a compile warning set; control that looks like a scoped Requires exception and is global.

**Impact**

- Additive `NoWarn` token. No public API change. No serialization change.
- Comment at `Directory.Build.targets:4` names Requires / #115. The condition at `:3` is every test and every `*Benchmarks*` project.
- Commit `d01a78f` claims “Test projects suppress CS0618 so existing Requires call sites still exercise the Preconditions lowering.” The suppression is what lets those call sites stay; it is not a proof they compile as consumers will.

**Reverse dependency closure:**

1. All `IsTestProject` and benchmark projects under `src/`.
2. Every `[Obsolete]` member those projects call, including future ones.
3. The Requires obsolete obligation — this file is the in-repo reason that obligation has no failure signal here.

**Reverses?** Yes, one-token revert. No published package carries this `NoWarn`.

**Open questions:**

- Do any test projects already set `TreatWarningsAsErrors`? If yes, dropping the token is a red build until `#pragma warning disable CS0618` is added at Requires sites. If no, revert only restores the warning text.

**What is expensive to find again**

The token sits at the end of a long existing `NoWarn` list. A later reader who greps `CS0618` in product projects will find nothing and assume consumers see the warning. They do. Tests do not.
