# Plan — truthful queued upload status

## Chosen approach

Extend the one Core queued-status projection so `RetryScheduled` remains truthfully `Processing` and carries its due time. Use that due time for a bounded refresh delay and suspend shared reloads while the document is hidden. Resolve the current Case with the exact manual-association precedence already owned by `IntakeReceipt.CurrentCaseId`, including inactive association suppressing fallback. Remove Upload Status lede narration.

Reuse existing work state/due fields, status query, Core association rule, `data-auto-refresh` helper, Playwright convention, and upload/recovery fixtures. Add no state table, migration, endpoint, worker, or dispatcher.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: implement truthful Processing for retry/large work and current destination while preserving the single public state vocabulary.
- `docs/design/README.md`: align Upload Status with no explanatory copy and replace the obsolete fixed two-second behaviour.
- ADR-0032/INTK-042 govern scheduling; this ticket only projects durable facts.

## Ordered steps

1. Wait for INTK-042 and INTK-040 overlap; start from refreshed `origin/dev`.
2. Extend `QueuedIntakeStatus` and query projection with retry/due facts; map RetryScheduled to Processing.
3. Extract/reuse one current-Case resolution expression matching `IntakeReceipt.CurrentCaseId`: active manual association wins, inactive suppresses, absent falls back to accepted link.
4. Derive a bounded due-aware refresh interval for moving work; keep grouped status compatible.
5. Update shared script to schedule/reload only while visible and resume safely on visibility change.
6. Remove Upload Status lede narration; retain concise status values and actions.
7. Add Core/EF/Web/browser tests for retry state/delay, hidden tab, visible resume, active/inactive association precedence, allocation fallback, group caller, and no lede.
8. Update FRD/design text, run Release relevant/full validation and simplification lenses, then report/commit/push/open PR.

## Proof

Tests prove RetryScheduled never appears Received, refresh is bounded/due-aware and paused while hidden, current Case follows exact precedence, grouped refresh still works, and no lede is rendered. Merged-dev verification repeats focused tests; deployed UI/latency proof remains DELIV-021.

## Risks and mitigations

- **Third association rule:** reuse/extract the current Core owner and test inactive precedence.
- **Refresh loop/duplicate timer:** one visibility-aware scheduler with cleanup.
- **State vocabulary drift:** Core maps persistence to public states; UI does not invent another enum.
- **Overlap:** wait for blocking/claimed work, then fresh base.
