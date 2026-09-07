---
kind: review-attestation
pr: "680"
head_sha: "d2bf633ec8ddc5b08b4052554d1d4e79f3930682"
verdict: pass
reviewer: "/root/intake_audit"
independent: true
plan_hash: "b7ea993913c84058"
ticket_updated: "2026-09-07T21:46:29.393Z"
board_sha: "05fe78801d845e97e7e94688beceb6f112d87ad5"
expected_reviewers: ["/root/intake_audit"]
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Retired Organizations flow retained two known browser callers."
    disposition: fixed
---

# Independent delta review — PLAT-028

PASS at exact current head above. This reviewer authored none of the source.
The only assigned expected reviewer is /root/intake_audit and its findings are
settled/published on this exact head. Same ticket, worktree, branch and PR680.

## Delta boundary

review_round=1. Previous consolidated attestation 0f92badd048442ee at
539aa4684d6dba1964c8fa594d2d2a0e3e3489b6 was needs-changes for F-001 only.
This review is limited to that finding, the two changed browser files,
their direct Principal create route and corrected packet/report evidence.
No unrestricted re-audit of unchanged application source was performed.

The plan's remediation addendum matches the authorized correction.
Post-implementation-report 6cdb9dc68164e418 explicitly corrects its prior
zero remaining consumer claim and preserves the original review/verification.

## F-001 fixed

AccessibilityTests removes only the retired Organizations 200-route row.
Principals and Create accessibility cases remain. QdosAllocationRecovery
BrowserTests now navigates /Administration/Principals/Create and submits
Name and Principal code through the real existing PageModel/Core/EF path.
It no longer creates a parent organization. All roleless denial, required
reason, keyboard retry, safe display, immutable destination and exact replay
assertions are unchanged.

git diff 539aa468..d2bf633 shows exactly these two files; git diff --check
exited0. Full src/tests/catalogue grep now finds only the two intentional
absence/404 assertions in OrganizationAdministrationWebTests, not any positive
consumer of the retired Organizations route.

## Verification evidence

Root's exact frozen delta build passed, exit0, 0 warnings/errors, 64.96sec.
Root's focused filter passed 3/3 cases, exit0, 54sec:

(FullyQualifiedName~AccessibilityTests.RealAuthenticatedRouteHasNoAxeViolationsAndNoInlineStyleAttribute&DisplayName~Administration/Principals)|FullyQualifiedName~QdosAllocationRecoveryBrowserTests.FailedAllocationShowsSafeRecoveryWithoutRawIdentifiers

This is actual browser evidence for Principals/Create accessibility and the
real allocation recovery/replay journey; it supplements the previous 19/19
Core and 25/25 integration PASS plus scoped captures and catalogue checks.
Reviewer did not run a build/test/capture. No new source or snapshot change
beyond the two browser consumers exists in this delta.

## Carried consolidated conclusions and residual limits

The previous full review found no other production correctness/security
blocker: atomic flat customer creation/replay, same-customer code replacement
and all six default-location fields, immutable existing case/reference,
Administrator-only Settings and existing show-once no-store credentials were
coherent. Existing organization CRUD was removed while real directory
functionality remained intact. No new package/schema/runtime was added.

Root's prior file-URL visual inspection was blocked. Real browser checks
now exercise the corrected callers, but full human visual layout review at
all supported widths/zoom is still unclaimed. Static HTML is not visual
proof. No deployment, live credential/test-customer creation or full v1
completion is claimed. Generic routes remain TICK-035's separate ownership.

## GitHub/board gather

Exact head d2bf633ec8ddc5b08b4052554d1d4e79f3930682, PR OPEN to dev on
PLAT-028-principal-customer. Reviews and reviewThreads are empty.
The prior informational automated security summary is completed on old
539aa468 and contains no finding; it is not an expected reviewer/gate.
The prior public F-001 comment is dispositioned fixed by this exact-head
public delta comment. No review thread needs resolution.

Dev branch protection was previously 404 Branch not protected; freshly read
effective branch rules are empty. statusCheckRollup is empty, not a green
CI run. No absent required check is being called PASS. Board local/remote
SHA matched the recorded board_sha with ahead=0. Skipped CI is not proof.

## Handoff

Independent PASS, F-001 fixed and no open findings. Root explicitly retains
merge ownership: re-gather current head, plan/ticket timestamp, checks,
threads, branch protection and pushed board immediately before authorized
merge. Any substantive new thread or moved evidence invalidates this record.
After confirmed merge, move only Review to Verifying and use kanmer-verify
at the exact GitHub merge SHA. Do not treat this review as Done/proof.
