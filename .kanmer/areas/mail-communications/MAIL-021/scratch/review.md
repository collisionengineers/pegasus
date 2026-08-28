---
kind: review-attestation
pr: "575"
head_sha: "267b45a0ca0ef564b1fc7a44b929143ea39a1e88"
verdict: pass
reviewer: "claude-fable-5 independent reviewer (kanmer-review, 2026-08-27)"
independent: true
plan_hash: "820028ff8200ff07"
ticket_updated: "2026-08-27T17:06:44.166Z"
findings:
  - id: RF-1
    severity: minor
    summary: "docs/open-decisions.md:314 still says 'fifteen missed one-minute ticks', contradicting the corrected StaleAfter remark that cites it (Codex review thread)."
    disposition: deferred-to-ticket
    ticket: MAIL-022
---

# MAIL-021 review — PR #575

## Change reviewed

Head `267b45a0ca0ef564b1fc7a44b929143ea39a1e88`, one file,
`src/Pegasus.Core/Intake/RetainedMail.cs`: six XML-doc lines in the
`StaleAfter` `<remarks>`. `TimeSpan.FromMinutes(15)` and all code untouched.
Matches the ticket brief, plan, and post-implementation report exactly.

## Facts checked against the code (read-only)

- `InboxRecoveryFunction` (`src/Pegasus.Worker/MailboxFunctions.cs:10-23`)
  binds `[TimerTrigger("%ApprovedInboxPollSchedule%")]`.
- `ApprovedInboxPollSchedule` = `0 */5 * * * *` in
  `infra/modules/platform.bicep:540` (asserted by
  `scripts/Test-AzureDeploymentPlan.ps1:193`) and
  `src/Pegasus.Worker/local.settings.example.json`.
- Graph change-notification wakes exist (`src/Pegasus.Web/GraphMailWebhook.cs`,
  `UnifiedWorkFunction` in `src/Pegasus.Worker/IntakeFunctions.cs:37`);
  EPIC-010 context records the webhook path as the live primary ingest.
- 15 min / 5 min = three missed recovery ticks. Correct.
- No explanatory or speculative claims; PROVISIONAL / open-decisions sentence
  retained.

## Plan and report

- Plan checklist fully ticked; simplification pass dated with an honest
  "no findings" for a comment-only diff.
- Post-implementation report states the test evidence truthfully: local
  suite 985/987 under workstation contention, the two
  `LocalDbTemplateDatabaseTests` failures green 14/14 on one targeted
  re-run, and explicitly points the reviewer to CI as the clean signal.
  Controller scratch corroborates the attempt history.

## Checks

`dev` has no branch protection, so the required gate is the
`repository-check` workflow: changes, documentation,
local-development-scripts, reference-data, unit, sql-integration (1..3),
browser, sql-integration-coverage all SUCCESS; `infrastructure` SKIPPED by
path filter (no infra change). No human review requested. Re-gathered
immediately before the merge decision: head, checks unchanged; MERGEABLE.

## Findings and dispositions

- RF-1 (minor, Codex thread on `RetainedMail.cs`): `docs/open-decisions.md`
  line 314 still reads "fifteen missed one-minute ticks". Verified true.
  The ticket brief bounds MAIL-021 to the code comment, and conduct rule 1
  routes "while I'm here" edits to a follow-up, so the finding is
  **deferred to [[MAIL-022]]** (created, EPIC-010, linked both ways). The
  Codex thread carries the disposition reply and is resolved. Not a
  blocker: the PR removes a wrong statement and introduces none.

## Residual risk

None for behaviour. A reader of open-decisions sees the stale rationale
until MAIL-022 lands.
