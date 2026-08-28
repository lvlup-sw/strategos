# recur-mcp-pin-bound

Open class **R4**. Guard candidate **G-R4**. Shares the `quality-gates` job with G-R3. Distinct class: pin / deny-list / emitted shape drift, not “any inert control.”

## What led here

INV-3: latest non-draft MCP revision, never the LCD subset. #166 re-pinned docs to 2026-07-28 and left the annotation shape unchanged — that is how #176 (`CallToolResult.resultType`) and #177 (`Tool.icons`, present on both 2025-11-25 and 2026-07-28) were missed. #178: deny-list omitted a superseded revision; 12 sites across 3 files. Check 3.3 failed as a guard once (zero hits on a stale tree).

This diff: Hosting pin 2.2.0; every constructed `CallToolResult` in the factory sets `ResultType`; optional `Icons`, null when unset; Checks 3.4 / 3.5 added as greps. Checklist stops flagging the icon gap.

## Surfaces at 324768f

- `deterministic-checks.md` Check 3.3 — denied revision strings, expected zero hits. Check 3.4 — `grep -L ResultType` over files that mention `CallToolResult`. Check 3.5 — `grep -L Icons` on `OntologyToolDescriptor.cs`.
- `OntologyServerToolFactory` constructions assign `ResultType` (existing-proof P33).
- Hosting **tests** `VersionOverride=2.2.0`. CPM still lists `ModelContextProtocol` 1.3.0. No test asserts the **production** csproj pin (P30).
- `TraversalToolHostingTests.AssertResultTypeComplete` — traverse wire JSON contains `"resultType":"complete"`. Query cousin asserts the object property, not the wire (P26/P27).
- Checks 3.1–3.5 are not a CI job (see `recur-inert-control-resolves` investigation).

## Failure

A client speaking 2026-07-28 receives a pre-revision `CallToolResult` (no `resultType`) or a tool descriptor that invents placeholder icons. Docs claim the new revision. Who observes it: an MCP client or an INV-3 auditor; in-repo CI stays green.

## Expensive to find again

- File-level `grep -L ResultType` passes when a comment or unused identifier mentions the token. A new `new CallToolResult` in that file can omit the assignment.
- Tests compiling against 2.2.0 while production drops the override is the wrong-subject shape (P30).
- A deny-list with no failing fixture can be emptied and still “pass” (zero hits).

## Open questions (with stakes)

- Is `ErrorResult` with `resultType: complete` protocol-legal? If not, G-R4’s assignment scan would lock a protocol lie. Stakes: the factory error path already sets `ResultType` (P33). A later spec “no” requires a policy exception with a cite, not a silent skip.

### Investigation Log

#### Can Check 3.4 pass while a construction omits ResultType?

- Read: `deterministic-checks.md` Check 3.4; existing-proof P32.
- Found: file-level `grep -L`. Any mention of `ResultType` in the file satisfies.
- Conclusion: Check 3.4 as written is not a sound mechanism. G-R4 specifies an assignment scan. Do not implement Check 3.4 verbatim as the guard.
