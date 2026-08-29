## 2026-08-28 implementation notes

- AGENTS.md block reconciled by hand (the plugin install has no `scripts/agents-block.mjs`); the bundled block body hash now matches. Repo-authored text that lived inside the markers was relocated verbatim below the end marker.
- `.grok/skills`: 34 tracked files overwritten from the 0.3.3 bundle. Not-shipped assets left in place (brief-*.md, approval-contract.md, agents-template.md, group-context.md, kanmer-auto/assets).
- `.claude/skills` drift and missing stamp cannot be fixed through the PR: `/.claude/` is gitignored and exists only in the main checkout (`kanmer-setup`, `kanmer-standup`, `kanmer-workflow` folders). Operator: reconnect the project in the Kanmer app, or copy `…/Kanmer/resources/plugins/kanmer/skills/kanmer-setup/SKILL.md` over `.claude/skills/kanmer-setup/SKILL.md`.
- `update_item TICK-222 area: delivery-repository` still fails with Windows EPERM on renaming `.kanmer/areas/_none/TICK-222` (process lock). Not moved by hand.
- For the operator: CASE-024 and MAIL-017 record worktree paths under `C:/Users/Alex/…`, which do not exist on this machine; their taken records are stale here.
- AGENTS.md Pegasus half duplicates the "Operator-facing explanation is a defect" bullet verbatim under Simplicity rails — out of scope, left for a follow-up.

## 2026-08-29 verification (proof written against merged `dev` b92cb9a7)

Proof written; ticket **held in Verifying, not moved to Done**.

- Proven clean: AGENTS.md managed block byte-matches bundled 0.3.3
  (sha256 `7b6a306b…`, 2446 bytes) and `get_status` no longer lists
  `agents-block`; `.grok/skills` is content-complete (33/33 bundled files
  identical after CR-strip, 0 missing) and no longer flagged.
- Verification item 1 FAILS: live `get_status.repo.upToDate` is `false` —
  `skills` = `behind` (`.claude/skills/kanmer-setup`), `skills-stamp` =
  `unstamped`. These are exactly the two artefacts the ticket's *Why* named.
  Out of PR reach (`/.claude/` is gitignored, 0 tracked files); needs an
  operator reconnect. No ticket owns it.
- Verification item 2 FAILS: TICK-222 is still `area: ''` in
  `.kanmer/areas/_none/TICK-222`. Its `updated` (2026-08-26T14:34:46Z)
  predates KANMER-006 being taken (2026-08-28T08:11:49Z), so no `update_item`
  from this ticket ever landed. Not retried here — this pass is read-only
  apart from the proof document.
