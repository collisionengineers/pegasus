# Checklist

- [x] Confirm INTK-042/INTK-040 are merged and take a fresh worktree. Both are
  merged (INTK-042 PR #553, INTK-040 PR #548; both `verifying`). The recorded
  worktree and branch were reused rather than replaced, per EPIC-011 D17 — the
  branch was brought up to `origin/dev` `b92cb9a7` by merge `4033e881`.
- [x] Project truthful retry/due facts in the single Core queued-status contract.
  `RetryScheduled` maps to `Processing`; `RetryDueAtUtc` carries the due time and
  is set for no other state.
- [x] Reuse the authoritative current-Case association precedence. Delivered by
  *removing* the copy rather than adding one: `QueuedIntakeStatus.CaseId` had no
  reader, and `UploadOutcomeQueries` already resolves the Case from
  `IntakeReceipt.CurrentCaseId`, Core's one owner of the rule.
- [x] Add bounded due-aware visible-tab refresh while preserving group status.
  `Presentation/UploadStatusRefresh` is the one owner of the cadence; both pages
  read it through the injected `TimeProvider`.
- [x] Remove Upload Status lede narration. Both `<p class="lede">` paragraphs are
  gone; the duplicate fact they carried is now a `notice`, not narration.
- [x] Add retry, visibility, association precedence, group, and no-lede tests.
  Retry, association precedence and no-lede are covered by new assertions; the
  group derivation is covered by the existing
  `UploadConfirmationWebTests` group-refresh assertion, which now exercises the
  new code. **Visibility is not covered by an automated test** — see Parked.
- [ ] Update FRD/design authority text.
- [x] Run Release validation and simplification lenses. Release build clean;
  199 focused integration tests pass; the simplification pass is recorded in the
  plan under 2026-08-29.
- [x] Report, commit, push, open PR to `dev`, and move to Review.

## Parked (explicitly deferred)

- [ ] Update FRD/design authority text. **Parked: the premise no longer holds.**
  The files document expected to replace an obsolete "four-state / fixed
  two-second Upload Status" row in `docs/design/README.md`; UIIMP-006's wave-0
  rewrite already removed it, and the rewritten Contracts table specifies no
  refresh cadence for any page. `frd-02` already states both required
  behaviours — "A large, retrying, or legitimately incomplete item remains
  Received or Processing" (§ durable receipt) and, in the Upload confirmation
  surface decision table, that a receipt with `CurrentCaseId` set shows "a link
  to open it". Writing a new cadence row would be unrequested authority text in
  another lane's file. Checked by reading both documents on the merged branch,
  not assumed.
- [ ] An executed test for the hidden-tab behaviour. **Parked: cannot be run in
  this lane.** It needs a real browser (`document.hidden`, `visibilitychange`),
  so it belongs in `tests/Pegasus.IntegrationTests/Browser/`, and this lane is
  instructed not to run the Browser category. Adding a test that is never
  executed here would be worse than naming the gap. The single-timer scheduler
  in `wwwroot/js/site.js` is reviewed code, not proven behaviour; INTK-047 owns
  these pages next and runs with the browser suite.
