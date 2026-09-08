# Files — DELIV-053

## Where the change lands

| Path | Why |
| --- | --- |
| `.gitignore` | Remove only the ignore entry for .codex/config.toml. |
| `.codex/config.toml` | Track existing Kanmer configuration plus global subagent settings. |
| `.codex/agents/pegasus-scout.toml` | Luna medium read-only bounded inventory. |
| `.codex/agents/pegasus-investigator.toml` | Terra high read-only causal and requirements analysis. |
| `.codex/agents/pegasus-implementer.toml` | Terra high scoped writes, no tests/builds/merge/deploy. |
| `.codex/agents/pegasus-reviewer.toml` | Sol high independent read-only review. |
| `.codex/agents/pegasus-verifier.toml` | Sol medium sole authorized verification executor; no product fixes. |
| `AGENTS.md` | Unmanaged routing and explicit host-wide all-test/build exclusivity. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `docs/index.md` | Documentation authority and current existing owners. |
| `docs/engineering.md` | Effect-scoped verification and evidence requirements. |
| `.agents/skills/pegasus-release/SKILL.md` | Release commands belong to primary after explicit approval and host-slot handoff. |

## Ripple effects

Codex project discovery/configuration is the real consumer. Preserve existing Kanmer launch settings. No application API, schema, package, runtime artifact or Razor snapshot changes.

## Out of scope

Application/source/test files; principal-document moves; cloud/merge permissions; user-level Codex settings; local Kanmer copies; release procedure repairs and workspace plan edits are separate work.
