# Research — Rewrite AGENTS.md

Root plan: `docs/temp-plans/retire-now-rewrite-agents.md`. Coordinates with
[[SIMPLI-004]] (claim mechanism) and [[SIMPLI-006]] (index self-containment).

## Structure of current AGENTS.md (151 lines)
- Lines 1–12: `kanmer:instructions` **managed block** ("edits inside will be
  overwritten"). Leave intact.
- 14–34 "Planning process": names `NOW.md` as the multi-agent queue.
- 36–68 "Safety rails" · 70–96 "Product invariants" — keep (still current).
- 98–150 "Repository task workflow": NOW.md is the claim mechanism (commit a
  claim line, push to dev, bump the NOW.md date). This is what changes.

## Invariants (must not break)
- **`CLAUDE.md` is a git symlink to `AGENTS.md`** (mode 120000). Editing
  AGENTS.md updates CLAUDE.md; keep the symlink — ADR-0023 mandates it.
- Keep the filename `AGENTS.md`: 7 integration tests walk up to it as the
  repo-root marker (`VrmRecognitionCorpusEvaluationTests`, `QdosEmailCohortTests`,
  `ProviderDomainReferenceIntegrationTests`, `Browser/OperatorJourneyTests`,
  `MultiFormatIntakeWebTests`, `MultiFormatGenuineCorpusWebTests`,
  `IntakeWebTestSupport`).
- Keep the `#repository-task-workflow` anchor — `docs/index.md:18`,
  `docs/runbook.md:7,1091` link to it.
- `.codex/skills/kanmer-setup/SKILL.md` installs its block at the top of
  AGENTS.md — the managed block must remain the first content.

## Note
`simplify.md` explicitly says **do not add another "simplicity contract"** —
those rules already exist in AGENTS.md/engineering.md. This is a workflow
rewrite, not a new ruleset.
