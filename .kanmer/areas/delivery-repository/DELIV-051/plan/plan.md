# Plan — DELIV-051

> SUPERSEDED FOR EXECUTION by the operator's expanded scope on 2026-09-08.
> The ticket body and new checklist define the current requested outcome.
> The previous plan below is retained as historical evidence, not a completed
> full-repository audit or authority to restore removed local Kanmer skills.
> Research, file mapping and the implementation plan must be refreshed for
> AGENTS.md, docs/**, CONTEXT.md, README.md and operator skill/removal changes.
> New documentation is allowed as required. Current-head regression validation
> is required; the prior build cancellation is not a waiver for the expanded work.

## Objective
Implement the full owner-approved amendment from the conversation, including condensed project principles and both local Kanmer mirrors.
## Starting state
AGENTS has 552 lines with duplicate managed conventions/conduct and a conflicting handwritten pipeline. Board integrates dev and releases main. DELIV-003 PR 399 and DELIV-046 PR 660 are merged, so exceptions expired. Latest GitHub stable release v0.4.2 published 2026-09-05; all 39 Codex plugin skill files match the running 0.4.2-4920 bundle byte-for-byte. Local .agents/.grok mirrors differ in seven files each. Existing shared-checkout modifications remain untouched.
## Governing docs
Repository governance is owned by AGENTS and docs/index, not a new ADR. No PRD/FRD/ADR behavior change or new governing document is required by the fix profile. User approved the complete amendment and explicitly requested implementation.
## Required changes
Keep one canonical managed block; remove duplicate conventions/conduct. Condense Pegasus instructions while retaining Commands, Architecture map, Conventions, Gotchas, Verification, documentation model/ADR/new-placement anchors, simplicity, product invariants and repository workflow. Add condensed development/compatibility principles. Consolidate every safety/product rule without weakening it. Replace manual pipeline with board-configured integration, independent reviewer merge, unchanged release authorization, simplification-pass reference, no maintenance pushes or age-only cleanup. Remove expired exceptions from AGENTS/engineering. Reconcile local Kanmer mirrors and stamps from verified bundle, never hand-edit generic skills. Keep managed block matching shipped writer. Record verification-contract mismatch separately.
## Expected files
- `AGENTS.md`
- `docs/engineering.md`
- `docs/runbook.md`
- `docs/index.md`
- `.agents/skills/kanmer-*/**`
- `.grok/skills/kanmer-*/**`
- `.agents/skills/.kanmer-skills-version`
- `.grok/skills/.kanmer-skills-version`
## Do not modify
- `docs/operator-notes.md`
- `src/**`
- `infra/**`
- `.github/**`
- `corpus/**`
- `.opencode/**`
## Constraints
Preserve shared work, protected business meaning, release commands, current board policy and all active recorded workspaces. Existing canonical generated preamble is exempt from first-line H1 formatting. No packages, application changes, deployment, branch-protection/configuration change, or new repository Markdown. No self-review/merge. Use an independent reviewer for the final audit as explicitly required by approved plan.
## Ordered steps
1. Record old-rule disposition inventory before editing; reconcile skill sources and canonical block in isolated workspace.
2. Rewrite user-owned guide and align three linked canonical documents.
3. Audit inventory, managed-block and 39-file mirror parity, links, scope and docs checks.
4. Run required solution rails serially after checking no competing verifier; record every exit.
5. Obtain independent amendment audit, disposition findings, write implementation report and open draft PR to dev; record Review and hand off without merging.
## Acceptance checks
One managed block/conduct; no duplicate workflow or UI bullet; no obsolete paths, main proof requirement or expired allowance. Unique rules retained with owners. Both skill mirrors/stamps match distribution. No application or release mechanics change. All required checks recorded truthfully.
## Commands
From ticket worktree on Windows PowerShell 7:
- git diff --check
- pwsh -NoProfile -File ./scripts/Test-TestMarkdownPlacement.ps1
- pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1
- pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1
- dotnet restore ./Pegasus.slnx --locked-mode
- dotnet build ./Pegasus.slnx --configuration Release --no-restore
- dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
## Failure and deviation rules
Keep failed exits and report unrelated blockers without absorbing their scope. No weakening assertions or fabricating passes. Source-root get_status remains behind until integration: validate ticket worktree content separately and state this limitation. Fresh Codex host reload cannot be claimed from a running session.
## Simplification pass
n/a — docs-only; distributed skill text is copied unchanged.
## Stop condition
Open PR and ticket in Review for independent kanmer-review. No merge, post-merge proof, release, cleanup of the retained implementation workspace, or next-ticket execution.
