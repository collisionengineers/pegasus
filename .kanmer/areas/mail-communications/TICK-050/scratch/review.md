## Independent review — 2026-08-20 — PR #480 at `75c9f3a0576b73c722c03b6e1a71b39205711602`

### Changes

- `src/Pegasus.Core/Intake/RetainedMail.cs` adds one nullable concrete `RetainedMailSuggestedMove` and derives it on the existing authorized exact-message read from the landed `FolderRecommendation.CanMove`, suppressing an unresolved `Uncertain` move.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml` labels the optional advisory and continues to submit the existing MAIL-07 reason dialog/POST with the existing server-derived classification, policy, mailbox and operation-key fields.
- The two existing test files cover eligible, unavailable/current-location, writer-unavailable, Uncertain recovery, terminal-failure retry, one rendered advisory and no view-time move.
- `docs/capabilities.md` and `docs/current-architecture.md` record only local source/test behavior and retain the no-deployment/no-live-writer boundary.

### Comments and disposition

- **Non-blocking — implementation scope/correctness:** No finding. The six-file PIR exactly matches the diff. The change is a pure zero-or-one read projection; it introduces no Infrastructure/store/schema/transaction/provider/registry/framework/MCP/Automation scope and does not duplicate MAIL-07 authorization or execution rules. Disposition: no change required.
- **Blocking gate — replacement CI is not green:** Run 32401331139 passed changes, documentation, reference-data, local-development-scripts, unit, browser, SQL shards 2 and 3, coverage, with Infrastructure correctly skipped. SQL shard 1 did not pass. Attempt 1 hit an unrelated SQL execution timeout in `CustodyOutboxIntegrationTests.ExportIsRefusedForACaseThatIsNotInReview` and was later cancelled while terminating. The rerun completed 266/267 but hit an unrelated post-login SQL connection timeout in `CaseWorkflowPersistenceTests.ExactLeaseClaimReplayRecoversOpaqueTokenWithoutExtendingExpiry`. Neither failure touches any of the six changed MAIL-08 files. Disposition: no PR Review ticket filed because there is no identified TICK-050 implementation defect; nevertheless repository workflow requires green CI, so no merge or stage move is permitted.

### Verdict

**Needs changes / held at Review solely on the green-CI gate.** The independent code, governing-doc, PIR, test-shape and simplicity review passes at the exact head, but CI is not green after the replacement run and one rerun. PR #480 remains open and TICK-050 remains in Review.

## Final independent review disposition — 2026-08-20

The exact PR head remains `75c9f3a0576b73c722c03b6e1a71b39205711602`. The third isolated SQL shard-1 attempt passed all 267 assigned tests, and the aggregate SQL coverage check passed. The complete required check set is now green: changes, documentation, local-development-scripts, reference-data, unit, browser, SQL shards 1–3 and SQL coverage; Infrastructure is correctly skipped because this PR changes no infrastructure path.

**Final verdict: PASS.** This supersedes the earlier CI-only hold. The independent implementation/governing-doc/PIR/simplicity review had no findings, and the repository green-CI gate is now satisfied. Merge to `dev` and move [[TICK-050]] exactly one stage to Verifying; do not verify, close out, or promote `dev` to `main` in this review.
