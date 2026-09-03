# Files — KANMER-011

## Where the change lands

| Path | Why |
|---|---|
| `.opencode/skills/kanmer-setup/SKILL.md` | line 169 links `../../../../docs/manual/greenfield.md`, which resolves only inside the Kanmer monorepo; `scripts/Test-DocumentationLinks.ps1` reports it and the `documentation` job has been red on every PR since `c5c7a874`. |
| `.agents/skills/kanmer-setup/SKILL.md` | the same copied skill under the Codex/agents tree carries the same link (committed); fixed identically so the two copies do not drift. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `scripts/Test-DocumentationLinks.ps1` | the rule: every relative Markdown link in tracked files must resolve; scheme links are ignored. |
| Kanmer `plugins/kanmer/skills/kanmer-setup/SKILL.md` (CORE-139, PR collisionengineers/kanmer#314) | the upstream fix uses the same wording, so the next `kanmer-setup` reconcile produces no diff here. |

## Ripple effects

- None in application code; `Get-CiChangeFlags.ps1` classifies `.md` changes as non-build, so CI runs the light lanes only.

## Out of scope

- The AGENTS.md managed block (duplicated content, dangling `Native`): reconciled by `kanmer-setup` after Kanmer 0.4.1 ships.
- Whether the copied skill trees should be committed at all (Kanmer GUI-149 gitignores them on the next Connect).
