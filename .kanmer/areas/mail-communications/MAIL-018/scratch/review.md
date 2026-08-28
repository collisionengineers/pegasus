---
kind: review-attestation
pr: "577"
head_sha: "47ebad543b342193049126979067243a7072c7a9"
verdict: pass
reviewer: "claude-fable-5 independent reviewer (session 2026-08-27, not the implementer)"
independent: true
plan_hash: "5e8d25091b6f0ba1"
ticket_updated: "2026-08-27T18:07:02.709Z"
findings:
  - id: R1
    severity: note
    summary: "SubscriptionStatusFor uses SingleOrDefault over Subscriptions; correct only because the store upserts one row per ApprovedMailboxId (research premise, EF SaveAsync keyed on that id). Identical shape to adjacent PollStatusFor."
    disposition: accepted-risk
    reason: "One row per mailbox is the store's own invariant; a dictionary would diverge from the established page shape (plan simplification item 4)."
  - id: R2
    severity: note
    summary: "First CI run: documentation job hung in actions/checkout@v7 and was cancelled at 10m00s (steps never ran). One close/reopen performed; rerun 33103349280 all green, documentation 27s."
    disposition: accepted-risk
    reason: "Known stale merge-ref checkout hang, unrelated to the diff; single permitted retry recorded here."
  - id: R3
    severity: note
    summary: "scripts/Update-TestUiSnapshots.ps1 -Verify was not run on the branch; 49 unrelated regenerations deferred."
    disposition: deferred-to-ticket
    ticket: MAIL-023
---

# MAIL-018 review — PR #577 at 47ebad54

## Changes reviewed

- `src/Pegasus.Core/Identity/ApprovedMailboxSubscriptions.cs`: `ListAsync` added to the existing `IApprovedMailboxSubscriptionStore`; no parallel query port; no business policy added to Core.
- `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxSubscriptionStore.cs`: `ListAsync` (AsNoTracking, ordered, existing `Map`).
- `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml(.cs)`: injects the store, loads `Subscriptions`, `SubscriptionStatusFor` modelled on `PollStatusFor`; two new columns `Activated` and `Subscription`.
- Tests: fake store in `GraphMailWebhookTests` gains `ListAsync`; `ApprovedMailboxAdministrationWebTests` gains `ThePageShowsActivationAndSubscriptionHealthPerMailbox` (activation time, `Missed. Expires 02 Sep 2026 10:05. Last failure: Graph subscription renew failed.`, identifiers hidden) and the no-identifiers test asserts `Not activated` / `None.`.
- Snapshot `docs/design/test-ui/pages/administration-mailboxes--default.html`: diff is exactly two `<th>` and two `<td>` per row.

## Acceptance checks

- Port extension on the existing store: yes; implementations are the EF store and one test fake, both updated.
- Grant: `scripts/Invoke-AzureDatabaseBootstrap.ps1:334` already expects `pegasus_web_runtime_role` SELECT on `ApprovedMailboxSubscriptions`; no grant or migration change required.
- Design authority (`docs/design/README.md#no-explanatory-copy-and-page-economy`): labels and values only; the Subscription value follows the existing `PollStatusFor` value-sentence convention (`Last completed X. Next due Y.`) and renders the failure code via `OperatorLabels.Humanise` rather than prose, so it reads as a value.
- Conventions reused: `OperatorLabels.OfficeTime(DateTimeOffset?, absent)`, `OperatorLabels.Humanise`.
- FRD-08 "Failure is visible per mailbox" (line 329): satisfied by the Subscription column.
- Plan vs implementation: all six steps delivered; checklist 9/9; simplification pass 1 applied, 5 not applied with reasons; post-implementation report states the controller serial 987/988 with the unrelated `QdosMappingExtractionTests` regex timeout and the 7/7 class rerun honestly.
- EPIC-010 context: no new features, no explanatory copy, file lane respected.

## CI

- Run 33101827545 (before close/reopen): every check green except `documentation` (checkout hang, cancelled at 10m).
- Run 33103349280 (after the one close/reopen): changes, documentation, local-development-scripts, reference-data, unit, browser, sql-integration (1)(2)(3), sql-integration-coverage all pass; infrastructure skipped (no infra diff). No required checks are declared on the branch by branch protection; all workflow checks are recorded as evidence.
- No review comments or threads on the PR.

## Residual risk

Test UI snapshots on dev fail `-Verify` independently of this PR (MAIL-023).
