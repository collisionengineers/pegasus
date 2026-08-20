# Post-implementation report — MAIL-005

Branch task/mail-005-inbox-case-links (fda7c1af + dev merge). Root cause found by code read + live read-only SQL (recorded in the ticket body): the automatic allocation route records its created case on the succeeded `IntakeAllocationAttempts` row and never writes `CaseIntakeLinks`, which was the projection's only case source.

- `EfRetainedMailboxMessageStore` summary mapping: `CaseId`/`CaseReference` resolve link-first, else from the loaded allocation state (set only on a succeeded attempt) — no new query. "Ready for case allocation" is now unreachable for an allocated receipt; failed attempts still read "Case not created".
- Inbox outcome cell: `.outcome-row` aligns chip + case link on one line.
- The three live zero-attempt rows (Aug 13–14, pre-release-14) are stranded data, not a live code path — the durable-intake reconciliation + CASE-005 closed that window; the T9 wipe removes them.

Tests: new `ASucceededAllocationAttemptResolvesTheCaseWithoutALinkRow` (receipt with succeeded attempt, no link → "Case created" + reference); RetainedMailPersistenceTests 27/27; Release build 0/0.

Deviation: subagents barred — self-reviewed.

## Verification hand-off
Post-deploy: allocated inbox messages show "Case created" with their reference link; the read-only join query returns no allocated-but-label-stale rows.
