# contracts-schema-diff-fails-closed — Contracts schema compatibility diff fails closed

| | |
|---|---|
| **Claim** | Every #167 schema change is compared with the immediately preceding Contracts release, and missing/unreadable baselines or narrowing of nested constraints, references, discriminators, union arms, or item schemas is a blocking result rather than success. |
| **Scope** | Contracts 0.11.0 JSON Schema compatibility policy, `.github/workflows/contracts-schema-diff.yml`, and `scripts/contracts-schema-diff.mjs`. |
| **Consequence** | A minor Contracts package can silently narrow `ActionReferenceV1` or another regenerated contract and break older producers/consumers despite a green PR gate. |
| **Proof rung** | Rung 3 — deterministic structural analysis. |
| **Proof artifact** | A fail-closed schema comparator bound to the latest stable published Contracts nupkg and its digest, with exhaustive structural rules or a vetted compatibility engine and mutation fixtures for each narrowing class and for absent/unreadable baselines. |
| **Why not cheaper** | Rung 1 regeneration proves current outputs match current inputs, not compatibility with a prior release. Rung 2 compilation cannot see JSON-wire evolution. |
| **Failure signal** | A missing/unreadable published baseline exits indeterminate and blocks; a classified narrowing or invalid version progression fails. Without that gate, strict downstream deserializers or validators fail later. |
| **Rollback** | Restore an additive schema, publish a versioned V2/major contract, or have consumers pin the previous Contracts version. Regenerating the same narrowed schema does not reverse the break. |
| **Lenses** | False-green shapes: skipped-as-pass, wrong subject, cannot-fail. |

**Confidence:** High.

**Current proof posture:** Indeterminate. The workflow downloads the latest stable published `LevelUp.Strategos.Contracts` nupkg, records its SHA-256, rejects missing/unreadable/empty baselines with exit 2, and compares recursive constraints including `minLength`, `pattern`, `$ref`, discriminator/`const`, union arms, items, and unknown validation keywords with version policy. The exact-current `98fabb4` comparison against published 0.4.0 passed with recorded digest; four breaking changes were permitted by the pre-1.0 minor-version policy. Protected PR execution and review remain pending.

**Open questions:**

- None. The gate's stated additive-minor policy requires these cases independently of the exact #167 release date.

### Investigation Log

#### Does the compatibility gate compare the correct prior subject and reject indeterminate input?

- Read: all of `.github/workflows/contracts-schema-diff.yml`, `scripts/contracts-schema-diff.mjs`, the recursive C# classifier tests, and the current `ActionReferenceV1` schema assertions.
- Historical finding: product-tag selection, successful skip branches, swallowed directory failures, and a shallow classifier left precisely the new constraint keywords uncovered.
- Current finding: immutable published-package selection, digest output, fail-closed baseline handling, recursive classification, version rules, and Node/C# mutation parity are present. The exact local 0.4.0 comparison is recorded in `../final-evidence.md`.
- Not found: exact-current or protected PR execution bound to `98fabb4`.
- Conclusion: the candidate gate is structurally and locally supported, but its active verdict remains `Indeterminate` until protected execution.
