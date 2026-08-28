# claim-requires-still-compiles — Requires is obsolete; existing Object<T> authoring still compiles

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 38, 39, 58, 71, 72, 73, 134, 135, 136, 143
Confidence: high

## Ledger

| | |
|---|---|
| **Claim** | `IActionBuilder<T>.Requires` is `[Obsolete]` pointing at `ActionDescriptor.Preconditions`. There is no fluent successor. The method remains so existing `Object<T>` authoring still compiles. This is a tracked public-API change (RS0016/RS0017). Test projects suppress CS0618 so existing call sites still exercise Preconditions lowering. |
| **Scope** | `IActionBuilder<T>.Requires` / implementation; PublicAPI; `Directory.Build.targets` CS0618 `NoWarn` on tests/benchmarks; ontology guide/reference docs. |
| **Consequence** | Removing the method or marking it `[Obsolete(..., error: true)]` breaks existing `Object<T>` authors. Inventing a fluent successor contradicts claims 38, 72, 134, 136. Consumers with warnings-as-errors see CS0618 (stage 0 S5). |
| **Proof rung** | Compiler and type system |
| **Proof artifact** | Method still present on the public interface; `ObsoleteAttribute` without `error: true`; existing `Object<T>` call sites still compile. PublicAPI still lists `Requires`. RS0016/RS0017 on the ontology assembly — `builder-api-stability` currently does not include `IActionBuilder<T>`. |
| **Why not cheaper** | This *is* a compiler claim (presence + warning vs error). Generation does not apply. |
| **Failure signal** | Consumer compile break. In-repo tests cannot fail on CS0618 because `Directory.Build.targets` NoWarns it on every test/benchmark project (claim 73 / existing-proof P42). |
| **Rollback** | Remove the `[Obsolete]` attribute. No runtime behavior change is claimed. |
| **Lenses** | 6 Claim Derivation (claims 58 / 38 / 72 / 134). Survey lenses 1, 3, 4. |

**Open questions:**

- Packaged ontology README still demos `.Requires` with no obsolete note (survey backbone §8). Docs in this wave (`guide/ontology/index.md:66`, claim 136) do note it. Which published page is the consumer surface?
- `RequiresSoft` / `RequiresLink` are not obsolete (P40). Is that intentional omission or a hole in "prefer Preconditions"?

## Evidence

Highest-stakes CHANGELOG (`CHANGELOG.md:196–197`). Plan T6 (claims 38–39). Commit `d01a78f` (claims 71–73). Interface remarks and obsolete message (claims 134–135). Guide/reference (claims 136, 143).

Survey: `[Obsolete]` only; still callable; `Directory.Build.targets` adds CS0618 to `NoWarn` for all tests/benchmarks. Existing-proof P40 reflects the attribute. P41 NSubstitute tests pass by construction and do not touch `ActionBuilder`. P43 PublicAPI still ships the method. `builder-api-stability` builds only `Strategos.csproj` workflow builders.
