# pad-renovate-resolve-unasserted

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 25, 51, 52, 78, 79

| | |
|---|---|
| **Claim** | Renovate resolves the organisation's dotnet preset (#181). |
| **Scope** | `renovate.json` second `extends` entry; Renovate GitHub App (outside this repository). |
| **Consequence** | The #181 class: a control that looks present and is inert. If the slug or path still 404s, org pin policy never applies and the bot reports a healthy config. |
| **Proof rung** | Production-path integration tests |
| **Proof artifact** | None. No test, no CI job, no recorded Renovate run binds this revision to a resolved preset. |
| **Why not cheaper** | The path token is a string. Types cannot resolve a GitHub `local>` preset. A structural check could prove the *path shape*; it cannot prove the bot fetched the file. That is an external process (rung 5), and even that is unobserved. |
| **Failure signal** | Nothing in this repository. A 404 from the Renovate app is the real signal and is not captured here. |
| **Rollback** | Revert the one-line path. A Renovate run that already applied the corrected preset is benign. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High that nothing in-repo supports “resolves.” |

**Open questions:**

- Does Renovate resolve `local>lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json` after the `exarchos` rename? Survey reports the file exists on the renamed repo. This lens did not re-fetch the remote. Stakes: if the slug 404s, CHANGELOG 51 is false; if it resolves, the path fix is the whole delivery and the claim holds *after* a bot run nobody recorded.

## Competing explanation

The path token was the whole defect. Pointing it at `tools/...` is the fix. “Resolves” is then a restatement of the path edit.

## Discriminating detail

```1:7:renovate.json
{
  "$schema": "https://docs.renovatebot.com/renovate-schema.json",
  "extends": [
    "local>lvlup-sw/.github:renovate.json",
    "local>lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json"
  ]
}
```

CHANGELOG (`:184`): “Renovate resolves the organisation's dotnet preset (#181).” That is a claim about an external process. The diff is a path token. Inventory 25 / 52 / 79 (the old path 404’d because the file lives under `tools/`): **supported as the stated cause and the edit.** Inventory 51 / 78 (“resolves”): **acceptance criterion with no assertion.**

This is a symptom-shaped fix of the inert-control class (recurrence R3): the path is corrected; nothing proves the bot applies the preset.

## Disposition

Inventory 51: **nothing supports the resolution claim.** Inventory 25, 52, 79: path-cause and path-edit are in tree.
