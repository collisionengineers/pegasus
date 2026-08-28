## 2026-08-28 implementation notes

- AGENTS.md block reconciled by hand (the plugin install has no `scripts/agents-block.mjs`); the bundled block body hash now matches. Repo-authored text that lived inside the markers was relocated verbatim below the end marker.
- `.grok/skills`: 34 tracked files overwritten from the 0.3.3 bundle. Not-shipped assets left in place (brief-*.md, approval-contract.md, agents-template.md, group-context.md, kanmer-auto/assets).
- `.claude/skills` drift and missing stamp cannot be fixed through the PR: `/.claude/` is gitignored and exists only in the main checkout (`kanmer-setup`, `kanmer-standup`, `kanmer-workflow` folders). Operator: reconnect the project in the Kanmer app, or copy `…/Kanmer/resources/plugins/kanmer/skills/kanmer-setup/SKILL.md` over `.claude/skills/kanmer-setup/SKILL.md`.
- `update_item TICK-222 area: delivery-repository` still fails with Windows EPERM on renaming `.kanmer/areas/_none/TICK-222` (process lock). Not moved by hand.
- For the operator: CASE-024 and MAIL-017 record worktree paths under `C:/Users/Alex/…`, which do not exist on this machine; their taken records are stale here.
- AGENTS.md Pegasus half duplicates the "Operator-facing explanation is a defect" bullet verbatim under Simplicity rails — out of scope, left for a follow-up.
