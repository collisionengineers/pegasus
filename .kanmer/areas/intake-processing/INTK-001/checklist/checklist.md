# Checklist

- [x] Confirm INTK-042/INTK-040 are merged and take a fresh worktree. Both are
  merged (INTK-042 PR #553, INTK-040 PR #548; both `verifying`). The recorded
  worktree and branch were reused rather than replaced, per EPIC-011 D17 - the
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
  read it through the injected `TimeProvider`. Hiding cancels the one timer and
  returning visible reloads immediately.
- [x] Remove Upload Status lede narration. Both `<p class="lede">` paragraphs are
  gone; the duplicate fact is the labeled value `Duplicate / Already received`.
- [x] Add retry, visibility, association precedence, group, and no-lede tests.
  `UploadStatusRefreshBrowserTests` runs the real shared script in Chromium and
  proves a hidden page does not reload and a visible return reloads immediately.
  The focused test failed against the old scheduler and passes after remediation.
- [x] Run Release validation and simplification lenses. The original branch had
  199 focused integration tests pass. Verifier remediation rebuilt Release with
  zero warnings/errors, then passed 6 focused non-Browser cases and 1 focused
  Browser case; the dispositions are recorded in the plan.
- [x] Report, commit, push, open PR to `dev`, and move to Review. PR #620 remains
  open; remediation commits `ce3c0cfe` and `6ff999b2` are ready to push.

## Parked (explicitly deferred)

- [ ] Update FRD/design authority text. **Parked: the premise no longer holds.**
  The files document expected to replace an obsolete "four-state / fixed
  two-second Upload Status" row in `docs/design/README.md`; UIIMP-006's wave-0
  rewrite already removed it, and the rewritten Contracts table specifies no
  refresh cadence for any page. `frd-02` already states both required
  behaviours - "A large, retrying, or legitimately incomplete item remains
  Received or Processing" (� durable receipt) and, in the Upload confirmation
  surface decision table, that a receipt with `CurrentCaseId` set shows "a link
  to open it". Writing a new cadence row would be unrequested authority text in
  another lane's file. Checked by reading both documents on the merged branch,
  not assumed.
