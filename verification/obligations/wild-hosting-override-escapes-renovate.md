# wild-hosting-override-escapes-renovate

Closing the Renovate path (#181) does not manage the MCP pin that INV-3's new `resultType` deny now requires. Hosting's `VersionOverride` is a second pin authority. Nobody states that.

## What led here

Residue claims two independent leftovers closed: Renovate now resolves the org dotnet preset (`CHANGELOG.md:184-185`; `renovate.json:3-6`), and Hosting pins MCP 2.2.0 so every constructed `CallToolResult` can set `resultType` (`CHANGELOG.md:187-190`).

CPM still pins `ModelContextProtocol` at 1.3.0 (`Directory.Packages.props:52`). Hosting carves out `VersionOverride="2.2.0"` (`Strategos.Ontology.MCP.Hosting.csproj:18-20`); Hosting tests repeat the override. The 1.3.0 SDK has no `CallToolResult.ResultType`. INV-3 Check 3.4 now greps that every Hosting `CallToolResult` construction mentions `ResultType` (`deterministic-checks.md:112-124`). That check is unsatisfiable on the CPM pin.

Renovate's `extends` list is the control that is supposed to keep NuGet pins current. It reads `Directory.Packages.props`. It does not author `VersionOverride` on a single csproj. A successful org-preset apply updates 1.3.0 in CPM and leaves Hosting on 2.2.0. A failed preset apply (the #181 class: a path that 404s and looks present) leaves both pins as they are; INV-3 still passes because the override is local.

The two leftovers share one dependency and one deny-list. The wave treats them as file-disjoint.

## Failure scenario

Three concrete splits, one class:

1. Renovate updates CPM `ModelContextProtocol` to a current 2.x. Hosting stays on `VersionOverride` 2.2.0. The override wins. Hosting lags the org preset the Residue item claims is now in force.
2. Someone removes the override after seeing CPM at 2.x, or after a Renovate PR that only touched `Directory.Packages.props`. If CPM is still 1.3.0, Hosting stops compiling `ResultType` and INV-3 Check 3.4 becomes unsatisfiable — or the assignments are deleted to compile, and the deny-list is grepped green on a file that no longer constructs the field.
3. The new `local>lvlup-sw/lvlup-claude:...` path 404s the same way the old path did. Residue reports #181 closed. The Hosting override continues to be the only reason `resultType` exists. The two controls still look independent.

## Code paths read (rev `324768f`)

- `renovate.json:3-6`
- `src/Directory.Packages.props:51-52`
- `src/Strategos.Ontology.MCP.Hosting/Strategos.Ontology.MCP.Hosting.csproj:18-20`
- `src/Strategos.Ontology.MCP.Hosting/OntologyServerToolFactory.cs:50-57`, `:384-412`
- `.agents/skills/strategos-design-invariants/references/deterministic-checks.md:112-124`
- `.agents/skills/strategos-design-invariants/references/INV-3-mcp-first-class-latest-spec.md:11`, `:23`, `:36`
- `CHANGELOG.md:184-190`

## Why not cheaper

- **Rung 1.** CPM `PackageVersion` and Hosting `VersionOverride` are two XML strings for one package. Nothing generates one from the other. Situational: a single pin source would move this to rung 1.
- **Rung 2.** MSBuild will compile whichever version the override selects. It will not require that INV-3's deny and the override stay coupled, or that CPM equal the override.
- **Rung 3 is the cheapest sound rung.** A structural check that either CPM `ModelContextProtocol` is `>= 2.2.0` (so the override is not a second authority) or that INV-3 Check 3.4 and the Hosting override are listed as one control. Renovate resolving the org preset is a different subject and does not prove this.

## What is expensive to find again

The files sit in different leftover tracks (T3 vs T4). Each track's tests can pass. The coupling is that INV-3's new deny is only expressible on the override, and the override is invisible to the Renovate control the wave just "fixed."

## Open questions

- Does Renovate resolve `local>lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json` after the `exarchos` rename? A 404 leaves #181 in the inert-control class. It does not remove this obligation: the override would remain the only live MCP pin either way.
- Does the org preset already special-case `VersionOverride`, or ignore csproj overrides? If it ignores them, closing #181 cannot keep Hosting current by construction.
