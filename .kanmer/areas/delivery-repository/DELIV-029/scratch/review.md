---
kind: review-attestation
pr: "578"
head_sha: "cb2ab070ca1e3a0dd27723e5944989e2a37bc565"
verdict: pass
reviewer: "claude-fable-5 independent reviewer (not the DELIV-029 author)"
independent: true
plan_hash: "4924c20fb0007b3f"
ticket_updated: "2026-08-27T20:00:51.449Z"
findings:
  - id: R1
    severity: minor
    summary: "docs/open-decisions.md stale-threshold row rewritten (\"three missed ApprovedInboxPollSchedule recovery ticks at 0 */5\") while MAIL-022, which owns exactly that edit, is still in backlog — scope overlap with another ticket."
    disposition: accepted-risk
    reason: "The DELIV-029 plan (step 10) explicitly conditioned this one-line correction on MAIL-022 not having shipped, and the new text matches MAIL-022's stated required outcome verbatim in substance. Merging it does not change behaviour. MAIL-022 must be closed as superseded by DELIV-029 at closeout rather than worked again; flagged for the verifier/closeout."
  - id: R2
    severity: note
    summary: "First CI run 33111115968: `changes` job cancelled after 5m03s in actions/checkout (stale merge ref). One close/reopen performed by the reviewer; rerun 33111566782 green on the same head."
    disposition: fixed
---

# Review — DELIV-029 / PR #578 (release 35 record)

Docs-only PR, head `cb2ab070ca1e3a0dd27723e5944989e2a37bc565`, base `dev`.
Files: `docs/operations.md`, `docs/current-architecture.md`,
`docs/open-decisions.md`. No code, no infra.

## Truthfulness against the proof and live estate

Every value in the diff matches `proof/proof.md`: release SHA
`3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f`; manifest SHA-256
`CA81E6F7D9A1A63C9CC8460614E728B601E206919CB6653E7CB5A681D9EF10CF`; image
`sha256:694c562f9b686877b73e30015a65d35b52c05e5a4b0c455219388c157a0892c8`;
revision `pegasus-prod-web-252ow37gij--3a1a017c8dea`; migration
`20260827100901_ReactivateBoundApprovedMailboxes` data-only, zero rows;
census 526/359; seven functions, `0 */5 * * * *`; smoke line incl.
`Inbox intake liveness smoke passed` with the same poll/expiry timestamps;
AppDependencies 223 records / 14 min, stated as an observation, not a
controlled comparison. `64.7 MB` sourced from the MAIL-020 plan.

Read-only Azure re-check by the reviewer (2026-08-27): `az containerapp show`
→ image `…@sha256:694c562f…`, latest revision `--3a1a017c8dea`, traffic 100%
latestRevision; `az monitor app-insights component billing show` → cap 0.5;
`az monitor log-analytics workspace show` → `dailyQuotaGb 0.5`,
`RespectQuota`, next reset 2026-08-28T03:00Z. `origin/main` = `origin/dev` =
`3a1a017c…`.

## Scope

Matches plan step 10 exactly: release-35 row + paragraph, monitoring/cost
bullet and the MAIL-020 "Release 34 telemetry" paragraph updated to live
state, current-architecture date line and telemetry diagnosis replaced with
component-cap facts + Worker SQL filter, open-decisions cap decision added.
Only unauthorised-scope candidate is R1 (above). No new features, no
operator-facing copy, nothing touching `operator-notes.md`.

## Markdown convention

H1 line 1, blank line before every heading, compact `| --- |`-style
delimiters consistent with the surrounding tables, prose wrapped ~78 cols.
`documentation` CI job green.

## CI

Run 33111115968: `changes` CANCELLED (checkout hang); one close/reopen;
run 33111566782 on head `cb2ab070`: changes, documentation,
local-development-scripts, reference-data all SUCCESS; code jobs SKIPPED by
path filter as expected for a docs-only change.

## Residual risk

None beyond what the docs themselves state as unproved (PLAT-034 working-day
cap, INTK-044 live path, Mailboxes page screenshot).
