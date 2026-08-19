# Proof — DOCS-002

Verified on merged `dev` at merge commit `4d1bff3db4ed16692e7646ea07e7f4491365defd` (PR [#413](https://github.com/collisionengineers/pegasus/pull/413), merged 2026-08-19T09:19:55Z).

## Evidence

- `git diff --check HEAD^ HEAD` — passed with no whitespace errors.
- `git show --stat --oneline HEAD` — confirmed the merge contains exactly:
  - `docs/adr/0028-run-integrated-renderer-in-web-container-app.md`
  - `docs/adr/README.md`
  - 85 insertions across two documentation files; no code, IaC, deployment-state, or reference-evidence changes.
- `pwsh -NoProfile -File scripts/Test-DocumentationLinks.ps1` — passed: all relative Markdown links resolve across 224 files.
- `pwsh -NoProfile -File scripts/Test-MarkdownPlacement.ps1 -Base HEAD^ -Head HEAD` — passed.
- Direct inspection confirmed ADR-0028 frontmatter uses stable id `ADR-0028`, `status: accepted`, date `2026-08-19`, no supersession, capabilities `EXT-08`, `RPT-01`, `RPT-02`, FRD-11, and architecture/renderer/container-hosting tags.
- `rg -n "0028|integrated report renderer" docs/adr/README.md` — confirmed the accepted index row at line 41.
- Kanmer refs now attach ADR-0028 to DOCS-002, TICK-215, SIMPLI-014, and PLAT-007; DOCS-002 and TICK-215 no longer carry `docs_todo`.
- The ticket checklist is 3/3 complete.

## Result

ADR-0028 and its index entry are present and valid on merged `dev`. The technical decision creates no new runtime or deployment unit and unblocks TICK-215 planning. No Azure write or `main` update was performed.
