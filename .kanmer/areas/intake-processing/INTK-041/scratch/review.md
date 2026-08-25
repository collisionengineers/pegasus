# Independent review — PR #547

Reviewer did not implement INTK-041.

## Changes

- `docs/prd/pegasus-product.md` adds the operator-facing ten-second p95 intake outcome, truthful transient-state requirement, durable recovery outcome, and seven-day idle Function cost ceiling.
- `docs/frd/frd-02-intake-and-source-identity.md` adds the shared post-commit publication rule, one-minute missed-publication recovery, stage timing vocabulary, and truthful status behavior for email and manual uploads.
- `docs/frd/frd-08-email-mailbox-and-background-processing.md` defines the Web callback validation boundary, identifier-only wake messages, Worker-owned cursor/delta processing, subscription maintenance and lifecycle recovery, five-minute fallback, and neutral unresolved-sender projection.
- `docs/adr/0032-near-real-time-durable-intake-triggering.md` records immediate durable publication plus Graph wake-up, slow recovery, Worker ownership, and scale-to-zero as the architecture choice.
- `docs/adr/0002-dotnet-modular-monolith-on-azure.md` adds ADR-0032 to frontmatter and status prose as a partial replacement of polling/timer-first triggering.
- `docs/adr/README.md` adds ADR-0032 and annotates ADR-0002's polling/trigger mechanism as partially superseded.
- `docs/capabilities.md` registers INT-33 and updates the planned and alpha allocation counts.

The post-implementation report names all seven changed files and accurately describes the intended diff. The PR contains no runtime implementation, Azure change, mailbox mutation, or overlap with INTK-040.

## Comments

- **Blocking:** The architecture relationship is internally inconsistent. ADR-0032 says it partially replaces only polling/timer-first clauses while remaining `status: accepted`, but its frontmatter says `supersedes: [ADR-0002]`; ADR-0002 reciprocally says `superseded_by: [ADR-0032]` while itself remaining accepted. These whole-ADR fields overstate the declared scope. The repository's established clause-level precedent, ADR-0030, leaves `supersedes: []` and records the limited replacement in status/body prose. The current diff therefore does not meet its own plan/report claim of a precise partial supersession or AGENTS.md's machine-readable ADR relationship rule.
- No non-blocking comments.

## Disposition

- Blocking architecture-metadata finding: **filed-as-ticket** [[PR-062]], which blocks [[INTK-041]].
- No changes were applied during review; `open-questions` contains no unresolved question.

## Checks

- Read INTK-041, all pipeline documents, both group contexts, all four governing refs, and PR #547's complete diff.
- Plan versus diff: every planned change is present; no unplanned file is present.
- Authority: PRD owns outcome/quality; FRD-02 owns shared intake behavior; FRD-08 owns mailbox behavior; ADR-0032 owns the mechanism; capabilities remains a registry. The partial-supersession metadata exception above is blocking.
- Simplicity: docs-only pass is honestly recorded; the diff reuses the existing Core/Worker/Web/SQL/queue boundaries and creates no parallel business implementation.
- Capability census independently checked: 232 capability rows = 203 planned + 29 Not planned; alpha target count changed from 131 to 132.
- Local `Test-TestMarkdownPlacement.ps1` passed.
- Local `Test-DocumentationLinks.ps1` passed (200 files).
- `git diff --check origin/dev...HEAD` passed.
- Live CI at review time: changes, reference-data, and local-development-scripts passed; documentation remained pending; build/infrastructure lanes were path-skipped as expected for docs-only.

## Verdict

**Needs changes.** PR #547 is not merged and INTK-041 remains in Review. Resolve [[PR-062]], update the report if its exact metadata description changes, obtain green documentation CI, then rerun independent review.

## CI addendum

Live PR checks subsequently completed: `changes`, `documentation`, `local-development-scripts`, and `reference-data` all passed; build and infrastructure lanes were skipped by the docs-only path classifier. The needs-changes verdict remains solely because [[PR-062]] is unresolved.

# Independent re-review — PR #547

Reviewer did not implement INTK-041 or [[PR-062]].

## Changes since the first review

- [[PR-062]] is Done and its correction is present in PR #547 at head `800cdc7c421d28ceff526b38dc2876b8999d284d`.
- `docs/adr/0002-dotnet-modular-monolith-on-azure.md` now has `superseded_by: []`.
- `docs/adr/0032-near-real-time-durable-intake-triggering.md` now has `supersedes: []`.
- Both ADRs remain accepted, while ADR status prose and the ADR index retain the exact polling/timer-first partial replacement. This matches the ADR-0030 clause-level precedent and resolves the only prior blocker.
- The remaining seven-file documentation result and post-implementation report are unchanged and still agree.

## Comments and disposition

- Prior blocking comment: **fixed-in-PR** by [[PR-062]] / PR #549, merged into this branch at `800cdc7c`.
- New blocking comments: none.
- New non-blocking comments: none.
- `open-questions` still contains no unresolved question.

## Checks

- [[PR-062]] status: Done; its outcome records merge into the INTK-041 branch.
- PR #547: open, mergeable, target `dev`, head `800cdc7c421d28ceff526b38dc2876b8999d284d`.
- Exact metadata correction inspected from the branch and the two-commit correction diff.
- Live CI for the corrected head: `changes`, `documentation`, `local-development-scripts`, and `reference-data` passed; application and infrastructure lanes were path-skipped for the docs-only diff.
- Previous independent checks remain applicable: Markdown placement passed, 200 documentation links resolved, capability census was 203 planned plus 29 Not planned with alpha count 132, and `git diff --check` passed.
- Report versus diff, governing authority, scope, and docs-only simplicity pass remain accurate.

## Verdict

**Pass.** The sole blocking review finding is resolved, the corrected head is green and mergeable, and the ticket is ready to merge into `dev` and move one stage to Verifying.
