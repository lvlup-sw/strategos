# claim-icons-null-when-unset — OntologyToolDescriptor.Icons stays null when unset

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 30, 31, 54, 124, 125, 146, 148, 152
Confidence: high for the null-when-unset invariant; medium that discovery wiring is the subject the tests hit

## Ledger

| | |
|---|---|
| **Claim** | Optional `Icons` on `OntologyToolDescriptor` stays null when the source supplies none. Discovery does not invent a placeholder icon. INV-3 stops flagging the icon gap and does not treat a missing placeholder as a failure. |
| **Scope** | `OntologyToolDescriptor.Icons`, `ToolIcon`, factory mapping onto `ProtocolTool.Icons`, INV-3 / deterministic-checks 3.5. |
| **Consequence** | A placeholder icon is worse than an absent one (claims 148, 152). Clients render a fake icon. The inverse — documenting the omission — is what INV-3 forbade (claim 30). |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | Factory/discovery tests that a descriptor with no source icons yields `ProtocolTool.Icons == null` for every registered tool, including traverse. Record-ctor tests that leave `Icons` null are the wrong subject for "discovery does not invent." |
| **Why not cheaper** | An optional reference type defaults to null (rung 2) and does not prove discovery never assigns a placeholder. Structural greps for the identifier `Icons` (check 3.5) do not assert null-when-unset. |
| **Failure signal** | Nothing unless a client shows a placeholder. INV-3 is a checklist. |
| **Rollback** | Revert the property. Additive public API if already shipped. |
| **Lenses** | 6 Claim Derivation (claims 54 / 30 / 125). Survey lenses 1, 4, 5. |

**Open questions:**

- Survey: `Icons` null path is reached; non-null path is unreached (`Discover` never sets). Is a non-null production assignment supposed to exist in this wave?
- Factory tests filter traverse out of the discovery-icons assertion (P28). Is traverse's null-when-unset unasserted?

## Evidence

Highest-stakes CHANGELOG (`CHANGELOG.md:189`). Plan T4 (claims 30–31). Factory/descriptor comments (claims 124–125). INV-3 (claims 146, 148) and deterministic-checks 3.5 (claim 152).

Existing-proof P28–P29: discovery-derived icons null on a test graph; two-arg ctor leaves `Icons` null. P29 constructs the record in the test and does not run discovery. P32 check 3.5 does not assert null-when-unset.

Claim 55's "instead of flagging the icon gap" is the INV-3 swap recorded on `claim-mcp-resulttype-complete`.
