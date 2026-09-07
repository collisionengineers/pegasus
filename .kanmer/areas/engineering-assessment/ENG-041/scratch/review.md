---
kind: review-attestation
pr: "683"
head_sha: "1ac8bc428e0432b510b745342fdadd849d726878"
verdict: needs-changes
reviewer: "/root/principal_delivery_audit"
independent: true
plan_hash: "9d6183d25a42280e"
ticket_updated: "2026-09-07T22:12:53.773Z"
board_sha: "b24762f6e37e4c4df5069c57d883abbba19073c3"
expected_reviewers:
  - "/root/principal_delivery_audit"
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Resumed fresh provider writes bypass the existing Case edit-authority guard."
    disposition: open
  - id: F-002
    severity: major
    summary: "Bounded provider response failure loses unknown-write semantics and releases the external account."
    disposition: open
---

# ENG-041 independent consolidated review

Reviewed exact head 1ac8bc428e0432b510b745342fdadd849d726878 against
plan 9d6183d25a42280e, report 0b29da1c105a2198, research/files/checklist/questions,
EPIC-014 context and FRD-06. Author is /root/pack_reconcile; reviewer authored
none of this diff. Root assigned this sole independent reviewer. Its exact-head
public review is https://github.com/collisionengineers/pegasus/pull/683#pullrequestreview-5135577047.
No inline threads exist; the bot completion summary reports no finding and is
informational, not an expected reviewer or gate.

## F-001 — restore Case authority before resumed fresh provider work

At src/Pegasus.Infrastructure/Glass/GlassRepairEstimateGateway.cs:369-371,
Prepared/known-vehicle sessions call ContinueLaunchAsync without invoking
IGlassRepairEstimateCaseAuthority.RequireEditAuthorityAsync. The resume
request's supplied Case version and lease are ignored on this branch. Unlike
LaunchAsync:115, this can perform create-new-vehicle/start-ere(0) after the
original lease expired or another editor gained authority. The live Case POST
does not close this gap: Details.GuardEstimateEditAsync:2229 checks a nonblank
lease and read access, not the CaseMutationGuard-backed authority.

Reuse the existing gateway authority port before every resumed fresh write,
validating the presented actor, version and live lease; retain regained
authority for the later import. This is one missing-boundary root cause, not
separate Web/Core implementations. Add focused Prepared and known-vehicle
negative tests for missing/expired/foreign lease and stale version with no
further provider writes, and valid authority recovery. Preserve existing
owner identity and known-ID-only reopening semantics.

## F-002 — keep bounded response failures uncertain after a provider write

GlassMvaClient.TextAsync:697 does not pass its outcomeUnknown argument through
the existing ReadAsync boundary. ReadAsync:767 throws GlassMvaStageException
with the default false when a successful create/start response exceeds the
size limit. ContinueLaunchAsync's stage-exception handler and SettleAsync:897
then record Failed rather than Unknown, clearing ActiveAccountKey in the SQL
store even though the provider may already have created the estimate. A later
launch can duplicate an unrecorded external calculation.

Preserve the write-uncertainty classification across the existing bounded HTTP
read/stage exception taxonomy; do not weaken response bounds or classify a
definite pre-write refusal as uncertain. This is one classification-loss root
cause. A parameterized scripted create/start oversized-response regression
should reconstruct the gateway, assert Unknown/account occupied, and assert
no repeated create/start even after local expiry.

## Checked and retained evidence

All 18 changed paths read: 16 implementation/document/test files plus 2 fresh
Case snapshot changes. Read production gateway/session-store/editor/estimate
store paths, operation hash guard and permanent result identity, actor/lease
guards, Core and SQL/Web assertions. Current dev was fetched at
522e67f270ab4d6086d9fba04095988db3598888; author worktree is clean at the
exact head. Execution-packet regeneration correctly refuses Review resumption;
review used its recorded plan/files/research/execute evidence, without moving
or retaking the ticket.

The K1/K2/K3/K2 replay now returns the durable result identity in its current
state after hash matching, without reapplying K2. Submitted version/line IDs
remain stable; hidden evidence and amendment stamping are moved behind replay.
No additional finding was identified in that path.

Root evidence reused: locked restore; corrected Release build 0 warnings/errors
16.26s; Core56 PASS; integration153 PASS/1 FAIL, then failed case plus true
default capture2 PASS after the injected-clock fixture correction. Snapshot
update/verify2 PASS each and catalogue62 routes/69 prototypes/0 broken.
Compiler, clock-test and missing-capture failures remain in the report, not
erased. No reviewer build/test, provider/cloud operation or source edit ran.

The Razor review inspected native labelled controls, antiforgery routing,
owner-only CAS closure and safe notices; no new library, script or CSS.
Manual supported-width/200%-zoom inspection is unavailable and not claimed.

## Check policy and handoff

Live dev protection returned 404 not-protected; branch rules [], exact-head
check runs [], status contexts [] (aggregate pending without a context).
No missing required check is identified. Root-authorized skip-ci does not
discharge the final integrated release CI/packaging obligation. No deployment
or merged-SHA proof is claimed.

Verdict needs-changes for the two open majors above. Root confirmed both are
in scope. Return the same PR683, branch/worktree/claim to Implementing, amend
the bounded plan/checklist and address both in one remediation batch. Do not
merge. Next is kanmer-execute, then independent delta review.
