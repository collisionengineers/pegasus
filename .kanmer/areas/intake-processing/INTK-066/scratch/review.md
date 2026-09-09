---
kind: review-attestation
pr: "712"
head_sha: "e8bc3fcb47b2b47e405c806d17314cccefc71e26"
verdict: needs-changes
reviewer: "/root/review_712_resumed"
independent: true
plan_hash: "61139e57cd296a85"
ticket_updated: "2026-09-08T23:36:56.847Z"
board_sha: "f61919697a91d2f0cfcc3d1ba63cbca1b118e144"
expected_reviewers:
  - "/root/review_712_resumed"
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Group attachment can commit a ready subset while the submission is incomplete."
    disposition: open
  - id: F-002
    severity: minor
    summary: "Allocation summaries omit the new manual-upload acceptance exception."
    disposition: open
  - id: F-003
    severity: note
    summary: "Glass launch precondition is masked by the callback helper; failed CI cause remains unproven."
    disposition: deferred-to-ticket
    ticket: CASE-047
---

# Independent consolidated review — INTK-066 / PR712

## Immutable inputs and independence

Reviewed exact full head e8bc3fcb47b2b47e405c806d17314cccefc71e26 against
dev. The implementation worktree and branch were read-only and remained clean.
This is a distinct reviewer agent role, not the author. The interrupted prior
reviewer /root/review_712 was replaced by the controller with
/root/review_712_resumed; that sole expected reviewer has now settled by posting
the consolidated findings on the exact head:

[Public review and dispositions](https://github.com/collisionengineers/pegasus/pull/712#issuecomment-5593982352).

Inputs: live ticket/body/refs, research, files, plan@61139e57cd296a85,
checklist@49fdc7cbdf50861d, report@3e9bda57cf697fb8, current execution/diagnosis
record, current gates, current PR changed-path census and affected diff/callers,
checks, comments and GraphQL review threads. No feature group is assigned.
Open-questions document is absent; live questions-resolved gates pass.
Review round is absent/default 0 and remediation budget absent/default 1.
The board was already pushed (ahead 0, behind 0) at the recorded board SHA.
No board ref was pushed or switched by this reviewer.

The current operator revision supersedes browser/snapshot/capture requirements.
Razor review skill was used for server-owned rules, form/confirmation states,
progressive enhancement, input retention, keyboard semantics and evidence
limits; it does not confer visual approval.

## Scope, implementation and acceptance

The production route uses Core's new destination policy/read port, registered
through Infrastructure and called by UploadOutcomeQueries/UploadCaseDecision.
Manual automatic association/allocation and image recovery are withheld; the
existing staff link, lease, allocation and reconciliation owners are reused.
No schema, migration, new package or second allocator is introduced.
Cases/Index awaiting-image attachment and Mail search consumers are updated.
Cases/Create retains operation/version chaining for acceptance and replay.
The source/lock/installer changes retain runtime PDF Playwright dependencies
while removing the expressly retired browser/capture systems and percentage
gate. Their historical failures are not reclassified as passes.

Single/group happy-path confirmation, route membership, retained source and
selected replay behavior have HTTP/Core evidence. The planned complete-group
fail-closed acceptance is not met (F-001). The documentation is not yet wholly
coherent (F-002). Current regression acceptance also remains failed (F-003),
even though its implementation owner is outside this ticket.

## F-001 — major, open: incomplete group is only gated in presentation

Location: src/Pegasus.Web/Pages/UploadGroupStatus.cshtml.cs:202, :339, :374;
src/Pegasus.Web/Presentation/UploadCaseDecision.cs:447.

OnPostAttachGroupAsync loads current state but never checks readiness before
calling AttachGroupAsync. LoadAsync sets OpenGroupDecision false while the group
is refreshing, yet populates OpenMemberReceiptIds and receipt versions from
only members already exposing an attachable outcome. TryGetPostedRoster requires
only that subset, not all declared members or their readiness.
ExpectedMemberCount is not checked. AttachGroupAsync iterates only the supplied
roster and the existing mutation validates each receipt, not group completeness.

An authenticated, antiforgery-valid POST with a current Case version/reason,
operation id and the ready sibling's receipt version therefore reaches the
normal link mutation while another sibling is still processing or missing from
the actionable roster. A failed sibling can likewise be omitted. This is
static control-flow evidence, not a claimed runtime reproduction.
FRD-02:430-438 says an incomplete group changes nothing further and
missing/failed members are reported rather than silently counted as success.

WorkingMemberWithAnOpenSiblingWithholdsTheGroupDecision at
tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs:526 only examines
GET markup with substituted outcomes; it never posts or asserts the mutation
boundary. Hiding the form does not enforce the contract.

One root-cause class, one remedy: replace the actionable-subset readiness
assumption with authoritative complete/readied submission validation before
any group attachment, preserving valid identical-decision completed-member
replays and honest already-committed partial progress. Add actual POST tests
for processing/missing/failed siblings with no further association/history
changes. Do not introduce a batch framework or a separate remedy per example.

## F-002 — minor, open: affected allocation summaries still contradict FRD-02

CONTEXT.md:44 says an Audit definitive instruction creates Case/PO without
confirmation. docs/frd/frd-01-case-identity-and-lifecycle.md:14 states allocation
occurs when processing gates pass and a valid instruction does not stay pre-Case.
Neither qualifies the new ManualUpload exception now specified in FRD-02.

Qualify these affected summaries and link to the current manual acceptance
contract. This is bounded documentation reconciliation, not an instruction to
restore automatic manual behavior or redesign Audit. The separately
route-qualified Audit paragraph in FRD-01:16 is not raised as another defect.

## F-003 — note, deferred to [[CASE-047]]; CI acceptance remains unresolved

[Failed SQL shard 2](https://github.com/collisionengineers/pegasus/actions/runs/34291422038/job/102278695840)
reports KeyNotFoundException in
GlassRepairEstimateGatewayTests.ADifferentCallbackQueryForTheSameSessionIsRefusedAndChangesNothing.
CompleteAsync at line1996 indexes correlations before invoking Gateway.CompleteAsync.
Harness.LaunchAsync populates the dictionary only when the returned session is
Active (1979-1984); the failing test never asserts that launch precondition.
The callback-conflict assertion was not reached. Existing CI logs/TRX do not
retain the actual returned launch State/FailureCode, so the underlying launch
cause is not recoverable from that run.

The controller's retained instrumented diagnosis at this unchanged source head:
ten fresh actual-harness launches all Active; instrumented exact test passed
209 ms; whole GlassRepairEstimateGatewayTests class passed 78/78, zero skips
or failures, 56.3298 seconds, with the disputed test passing 169 ms.
Three deliberately induced own-process CPU-contention probes also stayed
Active. No RegexMatchTimeoutException was observed. The 100 ms regex timeout
hypothesis is unproven. Local runtime 10.0.10 differs from CI 10.0.12.

The diagnostic hook's initial CA1050 build failure and accidental uninstrumented
181 ms test pass remain separately retained in scratch/execution; neither is
instrumented proof. Corrected artifact-only hook build and later runs are
identified there. Source remained clean and both host records are IDLE.

CASE-047 is the existing Glass engineering owner, read live in Verifying.
Defer investigation/diagnostic hardening there: safely assert/report the launch
precondition, then obtain instrumented CI evidence if this recurs. This does
not attribute the defect to this PR, waive the failed acceptance check, label
the failure flaky, or authorize deleting the test. No separate ticket created.

## Checks, comments and residual risk

At final gather, all ten repository-check jobs are complete: SQL shard2 FAIL;
changes, documentation, local-development-scripts, reference-data,
infrastructure, unit, SQL shards1/3 and sql-integration-coverage PASS.
Effective dev branch rules returned no required status contexts. That GitHub
configuration does not override the plan's required regression acceptance.

GraphQL has zero review threads. The bot comment
IC_kwDOThBrk88AAAABTWSj-g is informational security-review completion without
reported findings; it is not an expected reviewer or independent approval.
The public consolidated review comment IC_kwDOThBrk88AAAABTW1lkA records the
three dispositions above. No inline thread requires resolution.

No browser/visual, screen-reader, rendered-PDF appearance, deployment or
production acceptance is claimed. No implementation edits, tests/builds by
this reviewer, merge, release, or post-merge proof occurred.

## Decision and handoff

Needs changes. F-001 is the in-scope major basis for one sanctioned
Review -> Implementing return on the same INTK-066 branch/worktree/PR.
F-002 remains in the same bounded remediation batch. F-003 does not expand the
implementation packet; the failed project acceptance remains explicit.
Stop for kanmer-execute after the return; do not implement or merge here.
