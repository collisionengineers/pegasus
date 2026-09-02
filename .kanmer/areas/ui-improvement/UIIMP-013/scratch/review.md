---
kind: review-attestation
pr: "644"
head_sha: "35667cb176baf31eceaa3eefa77ddb7ec3111ac8"
verdict: needs-changes
reviewer: "codex-review-agent-uiimp-013"
independent: true
plan_hash: "a0d5938e370f1a96"
ticket_updated: "2026-09-02T14:53:07.044Z"
board_sha: "bfcd88520df76064b54a03d1f9227f306f9c5f90"
expected_reviewers:
  - "codex-review-agent-uiimp-013"
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Report the actual 35-minute snapshot-step timeout in the failure diagnostic."
    disposition: open
---

# Independent review — UIIMP-013

## Scope reviewed

Reviewed PR #644 at exact head
`35667cb176baf31eceaa3eefa77ddb7ec3111ac8` against merge base
`0f0e90ae44ffda7339ca2a460310deeb98121afa`, the complete ticket
packet, EPIC-011 context, `docs/engineering.md`, `docs/design/README.md`,
the affected test support, all PR checks, comments, reviews, and review
threads. The expected reviewer set is settled by this attestation. There were
no GitHub review threads or comments at gather time.

## Changes and acceptance checks

The implementation preserves the original 415-test selection as a disjoint
119-browser/296-non-browser partition, retains the shared once-wiped capture
directory, keeps the browser two-thread cap, lets non-browser capture inherit
the four-thread project cap, and reuses the build after the first phase. The
three exact-SHA performance runs passed at 22:42, 21:32, and 20:50. Final-head
repository check 33641477638 passed every reported job, including Test UI.

The local canonical integration run remains inconclusive because LocalDB is
unavailable. The unchanged stale-file and orphan assertions rely on retained
negative-injection evidence from UIIMP-005. These are recorded residual test
limits, not additional review findings.

## Findings

### F-001 — open major

`.github/workflows/ci.yml:273` says the snapshot step may have exceeded a
40-minute step budget, but the same diff sets that step's timeout to 35 minutes
at line 268. A timeout therefore emits the diagnostic specifically at 35
minutes while reporting a false limit. Update the diagnostic to name the
actual 35-minute step budget (or avoid duplicating the number).

## Verdict

Needs changes. F-001 violates the plan's requirement for honest incomplete-run
failure language. No other correctness, security, performance, or meaningful
maintainability defect was found.

## Residual risk

Hosted Windows timing remains variable; the final safety run took 25:04, while
the measured 35-minute step budget retained headroom. No implementation file
was modified by this review.
