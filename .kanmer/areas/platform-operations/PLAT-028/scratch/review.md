---
kind: review-attestation
pr: "680"
head_sha: "539aa4684d6dba1964c8fa594d2d2a0e3e3489b6"
verdict: needs-changes
reviewer: "/root/intake_audit"
independent: true
plan_hash: "287d65eb554e90ed"
ticket_updated: "2026-09-07T21:19:45.171Z"
board_sha: "2c8e04298c4bfea9223f8026633d59713f7ab223"
expected_reviewers: ["/root/intake_audit"]
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Retired Organizations flow still has two known browser-test callers in the final verification lane."
    disposition: open
---

# Consolidated independent review — PLAT-028

Round 0, complete PR head above. This separately assigned reviewer authored
none of the implementation. Root assigned only this reviewer; findings are
settled on this exact head and posted publicly on PR #680:
https://github.com/collisionengineers/pegasus/pull/680#issuecomment-5575798047.
PR base dev and head branch PLAT-028-principal-customer agree with the ticket.

## Scope and accepted implementation

Read ticket and all packet documents, plan/checklist/report, EPIC-008 and
superseding EPIC-014 context, FRD-04/FRD-09, relevant design changes, changed
application contracts/callers, focused test changes and route/catalogue inventory.

Customer creation uses the existing Core normalizer and one serializable EF
transaction for name/code, backing identity, lineage, Principal, operation
receipt and permanent history. Duplicate failure leaves no orphan; exact and
concurrent replay are covered. The list is bounded to 25 Principal rows, not
an unbounded nested organisation/principal projection. Code replacement keeps
the backing identity and lineage, copies all six default-location fields,
and preserves the accepted Case principal/reference. Real directory
functionality is untouched.

Settings calls existing credential Core commands with Principal ID, expected
credential version, operation key and reason. Administrator authorization is
on the routed page and Core boundaries. Issue/reset secrets stay in the
immediate no-store response model, never TempData, subsequent GET or replay.
Tests exercise real issue, replay, pause/resume, stale version, reset that
invalidates the old key, revocation and durable history. Independent settings
forms retain their reason/key validation and manual-only EVA behavior.
QDOS metadata consumes the current Core route owner. Generic activation is
separately assigned to TICK-035, not hidden scope in this PR.

Obsolete organization APIs/registrations/pages are removed, not retained as
compatibility wrappers. No package, schema, migration or runtime is added.
No other blocker/major was found in this consolidated review.

## F-001 — reconcile missed known callers

One root-cause class: the removed organization workflow was not reconciled
with two existing browser consumers.

- tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs:32 retains
  /Administration/Organizations in AuthenticatedRouteList; its theory asserts
  HTTP 200, while this PR correctly retires that route as 404.
- tests/Pegasus.IntegrationTests/Browser/QdosAllocationRecoveryBrowserTests.cs:
  92–101 still navigates the retired route and uses Organization name,
  Work Provider and Create organization before creating the recovery Principal.
- .github/workflows/ci.yml:238 and docs/runbook.md:328 include both in the
  Category=Browser lane. This is a deterministic known-consumer failure,
  not a request for additional infrastructure.
- The report's zero remaining source/test retired-route references claim
  is false; read-only git grep found both.

One remedy: update these two existing callers to the approved flat Principal
workflow and correct the report. Remove only the retired route from the
accessibility inventory; Principals and Create remain covered. Replace the
recovery test's parent creation with the real Name/Code Principal create page,
retaining authorization, keyboard, retry, immutable destination and exact
replay assertions. Root may run the affected browser cases only. Do not run
whole CI just to reconfirm the obvious dead route. Same ticket/PR/claim.

## Evidence and limits

Root supplied locked restore/Release build PASS (0 warnings/errors), 19/19
Core and 25/25 focused integration PASS, five scoped rendered captures,
snapshot update/verify and catalogue PASS. Reviewer ran read-only code/Git/
GitHub checks only, no build/test/capture. Passing filters omit these browser
callers and cannot prove them.

Rendered form contracts/semantics were inspected. Root's browser file URL
attempt was blocked; visual layout at supported widths/zoom is not claimed.
Do not relabel static HTML as visual proof.

GitHub protection API returned 404 Branch not protected; effective dev rules
returned an empty list. Required check set and statusCheckRollup are empty,
not a green CI run. The automated security-review status comment is
informational and contains no finding; it is not an expected reviewer or
gate. There were no review threads or requested-change reviews at the final
gather. Board local/remote SHA matched above with ahead=0.

## Decision and next step

Needs changes for F-001. Return only Review to Implementing, retaining the
same claim, branch, worktree and PR. Hand off to kanmer-execute for one
bounded remediation batch, then delta review of F-001 and changed direct
callers. Do not merge or write post-merge proof.
