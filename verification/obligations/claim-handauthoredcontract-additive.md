# claim-handauthoredcontract-additive — HandAuthoredContract appended as 2 without moving 0 or 1

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 32, 56, 76, 128
Confidence: high

## Ledger

| | |
|---|---|
| **Claim** | `DescriptorSource.HandAuthoredContract` is appended as `2`. `HandAuthored` stays `0` and `Ingested` stays `1`. Numeric values remain part of the public contract. |
| **Scope** | `DescriptorSource` public enum; PublicAPI unshipped listing; consumers that persist or switch on ordinals. |
| **Consequence** | Moving `Ingested` from 1 is a breaking published-API change. Stored ordinals mis-decode. AONT205 retarget keyed on `Ingested` would then hit the wrong members. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | Ordinal lock: `(int)HandAuthored == 0`, `Ingested == 1`, `HandAuthoredContract == 2`. PublicAPI membership of the new member. |
| **Why not cheaper** | C# permits any explicit ordinal. The compiler accepts a reordering that keeps the names. Generation does not own this enum. Existing P34 restates the authored values; that is the available lock. |
| **Failure signal** | Consumer decode errors after upgrade. In-repo, nothing pages. |
| **Rollback** | Revert the member. Value `2` if already published is a compatibility event (stage 0 S4). |
| **Lenses** | 6 Claim Derivation (claims 32 / 56 / 128). |

**Open questions:**

- None about the ordinals as authored. Producer assignment is `claim-handauthoredcontract-ingest-assignment`.

## Evidence

Plan T5 (claim 32), CHANGELOG (`CHANGELOG.md:192–193`, claim 56), commit `662f0d1` subject (claim 76), `DescriptorSource.cs:9–10` (claim 128). Existing-proof P34/P39 lock ordinals and PublicAPI membership. They do not prove AONT205 retarget (sibling obligation).
