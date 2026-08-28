### inv3-resulttype-icons-grep-substring — INV-3 3.4/3.5 must not pass on a mention

| | |
|---|---|
| **Claim** | INV-3 Check 3.4 must fail when any Hosting `CallToolResult` construction omits `ResultType`. Check 3.5 must fail when `OntologyToolDescriptor.Icons` is absent as a property, not when the identifier is missing from the file as a string. A comment, an unused identifier, or a second construction in a file that already mentions the token must not satisfy either check. |
| **Scope** | S3. Checks added on `324768f` in `.agents/skills/strategos-design-invariants/references/deterministic-checks.md` 3.4 (`112-124`) and 3.5 (`126-133`). The INV-3 spec acceptance questions this wave added. Not a CI job. |
| **Consequence** | An audit that "ran INV-3" reports pass while a new `new CallToolResult { ... }` in `OntologyServerToolFactory` ships the pre-2026-07-28 shape. Check 3.5 reports pass while `Icons` exists only in a comment. The HIGH severity in the INV-3 spec (emitting `CallToolResult` without `resultType`) is the class these greps are written to catch. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | A syntax-aware check over Hosting constructions (object initializers and `new()` inferred as `CallToolResult`) that requires a `ResultType` assignment on each construction, plus a member-existence check for `OntologyToolDescriptor.Icons`. File-level `grep -L` is not this artifact. |
| **Why not cheaper** | `CallToolResult.ResultType` is optional on the SDK type; omitting it still compiles against 2.2.0. Generation does not emit these constructions. A component test of today's two sites does not lock a third site. |
| **Failure signal** | Nothing. These recipes are not a pipeline job. A miss is a human checklist that printed empty and a protocol response without `resultType`. |
| **Rollback** | Revert the two check sections. No runtime reversal. The Hosting constructions this wave added are a separate change. |
| **Lenses** | False-Green Shapes |

**Open questions:**

- Is any agent or CI job actually executing Check 3.4/3.5, or are they documentation that a reviewer might treat as a gate? If they never run, the skip-and-pass is the whole suite staying green; if they run in an audit workflow outside this repo, a comment-satisfying pass is a reported INV-3 success.
- Does `grep -rlE 'new CallToolResult|CallToolResult'` include files that only *consume* a `CallToolResult` (parameters, properties) and therefore demand a `ResultType` mention in files that never construct one? If yes, 3.4 is noisy in the other direction and a construction-only file that omits `ResultType` is still the miss.

## What led here

This wave added Check 3.4 and 3.5. The recipes are file-level `grep -L`. Competing explanation: the Hosting factory today assigns `ResultType` on both constructions (`OntologyServerToolFactory.cs:384-386`, `410-412`), so a file-level mention is enough. Discriminating detail: 3.4's inner grep is `CallToolResult` or `new CallToolResult`, then `-L ResultType` on those files. A third construction in the same file that omits the assignment still leaves `ResultType` in the file. A comment `// ResultType required` satisfies. Check 3.5 is `grep -L Icons` on a single file: any occurrence of the four characters, including a comment that says the property must stay null.

No `.github` workflow references these check headings. They are not a job.

## Code read (revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`)

- `deterministic-checks.md:112-124` — Check 3.4 recipe and "Expected: empty."
- `deterministic-checks.md:126-133` — Check 3.5 recipe; prose says "must stay null when unset" but the command does not assert null.
- `INV-3-mcp-first-class-latest-spec.md:11-12, 23, 36-37` — acceptance questions and repo-grounded pointers this wave added. Those pointers are file paths, not proofs.
- `OntologyServerToolFactory.cs:384-386`, `410-412` — today's two constructions do assign `ResultType`. That does not make the grep sound.

## Kill probe

Add `return new CallToolResult { StructuredContent = structured };` next to `MapTraversalResult`. Check 3.4 still prints empty because the file already contains `ResultType`. Delete the `Icons` property and leave `// Icons stay null when unset`. Check 3.5 still prints empty.

## Failure scenario

A new Hosting tool maps an error path with `new CallToolResult { IsError = true, ... }` and no `ResultType`. INV-3 audit is "run the deterministic checks." 3.4 is empty. The client sees the pre-2026-07-28 shape. The spec's HIGH line names that omission.

## Open questions (full stakes)

### Do these checks run anywhere?

`rg` over `.github`, `.agents`, and `scripts` found the headings only in `deterministic-checks.md` and a pointer from the skill README. If they never run, the false-green is "absence of a gate treated as INV-3 covered" (skip-and-pass). If an out-of-repo audit runner executes the recipes and stores a pass, the substring hole is a recorded success. The obligation's failure signal and whether this is a skipped check vs a weak check both change with that answer.

### Does 3.4's file set include non-constructors?

`grep -rlE 'new CallToolResult|CallToolResult'` matches every mention. A file that only reads `result.ResultType` is in the set and will have the token. That does not create a false pass on a construction-only miss; it creates noise. The false pass is the construction that shares a file with an existing mention. Confirming whether Hosting has files that construct without the identifier in the same file tells us whether today's tree is one shared file (`OntologyServerToolFactory.cs`) and therefore whether a second construction is the entire hole.
