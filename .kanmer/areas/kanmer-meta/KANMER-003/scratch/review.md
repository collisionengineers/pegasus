## Independent review — 2026-08-17

### Changes checked
- `AGENTS.md`: refreshes the Kanmer-managed operating block, but also adds a pointer outside the managed markers.
- `.agents/skills/**` and `.grok/skills/**`: synchronize the Kanmer-owned files with the installed packaged 0.3.3 bundle and remove retired `kanmer-import/SKILL.md` and `kanmer-research/assets/impact-template.md`.
- No application/runtime files are changed. `CLAUDE.md` is ignored and is not part of PR #382.

### Comments and disposition
- **Blocking:** `AGENTS.md` begins with `Follow the repository-wide agent instructions in [AGENTS.md](AGENTS.md).` This is a self-reference above the managed marker. The packaged setup skill requires the managed block to begin the file and assigns that pointer only to an existing `CLAUDE.md`. **Disposition:** filed as [[PR-002]], which blocks [[KANMER-003]].
- **Non-blocking:** `.agents/skills` includes the pre-existing non-Kanmer `grill-me` skill and both provider trees include their version stamp. These extras are outside the bundled Kanmer subtree comparison and should remain preserved. **Disposition:** won't-do because deleting unrelated provider content would exceed scope.

### Evidence
- PR #382 targets `dev`, is open and mergeable; repository checks `changes`, `documentation`, and `reference-data` are successful, with code-test jobs appropriately skipped for this docs/skills-only diff.
- SHA-256 comparison: all 33 bundled files exist and match byte-for-byte in each of `.agents/skills` and `.grok/skills`; no bundled file is missing or different.
- Retired Kanmer files are absent in both trees.
- `git diff --check origin/dev...HEAD` passes.
- The Kanmer-managed text between the markers matches the packaged block; repository-specific Pegasus instructions below the closing marker remain present. The file-level contract still fails because of the extra first line.
- No plan, post-implementation report, or open-questions document exists for this custom-profile setup ticket; the ticket intentionally declares no gates.

### Requirements coverage
- Ticket/setup requirements missed anything? **Yes:** the setup contract explicitly requires the managed block to be the first content in `AGENTS.md`.
- Implementation missed anything? **Yes:** remove the self-referential pointer from `AGENTS.md`. The skill synchronization and retired-file cleanup otherwise meet the stated requirement. The ignored local `CLAUDE.md` pointer is correctly worded but is not delivered by this PR; final setup reporting must be precise about that local-only state.

### Verdict
**NEEDS CHANGES.** Do not merge PR #382 until [[PR-002]] is resolved and the corrected head is re-reviewed.

## Independent re-review — 2026-08-17

### Remediation
- Commit `7af202c7` removes the stray self-referential line. `AGENTS.md` now begins at byte zero with the Kanmer managed-start marker.
- [[PR-002]] is fixed in the PR, unlinked as a blocker, and archived resolved.
- `CLAUDE.md` is a tracked symlink to `AGENTS.md`; that symlink is already the provider pointer. Adding pointer prose through it would necessarily modify `AGENTS.md`, which caused the original defect.

### Re-check evidence
- Managed block extracted from `AGENTS.md` exactly equals the literal Markdown block in packaged Kanmer 0.3.3 `kanmer-setup/SKILL.md` after newline normalization.
- Pegasus repository-specific content after the closing marker exactly matches `origin/dev`.
- All 33 bundled files are present and SHA-256-identical in each of `.agents/skills` and `.grok/skills`; zero missing and zero different.
- Retired `kanmer-import/SKILL.md` and `kanmer-research/assets/impact-template.md` are absent from both provider trees.
- Pre-existing non-Kanmer `.agents/skills/grill-me/**` and provider version stamps remain preserved.
- `git diff --check origin/dev...HEAD` passes; the worktree is clean.
- PR #382 head is `7af202c7`, mergeable, and its refreshed `changes`, `documentation`, and `reference-data` checks pass; code-test jobs are skipped for the docs/skills-only change.

### Requirements coverage
- Ticket/setup requirements missed anything? **No remaining gap in the PR scope.** Migration/ingestion/final live status are setup-run reporting concerns rather than omitted committed files.
- Implementation missed anything? **No.** The managed block, repo-specific preservation, provider bundle synchronization, retired-file removal, and symlink behavior now match the packaged setup contract.

### Verdict
**PASS.** PR #382 is ready for the owning agent's merge decision. This reviewer did not merge it, per delegation.
