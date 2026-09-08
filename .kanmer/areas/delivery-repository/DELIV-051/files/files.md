# Files — DELIV-051

## Where the change lands

| Path | Why |
| --- | --- |
| `AGENTS.md` | Consolidate and correct the owner-approved guide; preserve canonical generated block. |
| `docs/engineering.md` | Remove expired exceptions and obsolete claims; align Markdown preamble exception. |
| `docs/runbook.md` | Align workflow ownership wording and references. |
| `docs/index.md` | Clarify navigation to Kanmer lifecycle plus Pegasus supplement. |
| `.agents/skills/kanmer-*/**` | Reconcile shipped Kanmer skill content and assets. |
| `.grok/skills/kanmer-*/**` | Reconcile the second tracked mirror. |
| `.agents/skills/.kanmer-skills-version` | Record distribution provenance. |
| `.grok/skills/.kanmer-skills-version` | Record distribution provenance. |

## Context files

| Path | Meaning |
| --- | --- |
| `docs/operator-notes.md` | Protected business truth; do not edit. |
| `docs/design/README.md` | UI economy authority; preserve its existing constraints. |
| `.github/workflows/ci.yml` | Existing documentation checks and no dev push receipt. |
| `.agents/skills/pegasus-release/SKILL.md` | Exact-SHA release route and authorization remain unchanged. |

No application or CI edits. Shared checkout has pre-existing changes to AGENTS managed block, .gitignore and .opencode skills: preserve them, use isolated worktree. No active implementing ticket found owning this amendment scope.
