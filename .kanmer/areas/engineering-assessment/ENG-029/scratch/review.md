---
kind: review-attestation
pr: "700"
head_sha: "f86054c0e7cc73cb6245355dd21c03e58196d582"
verdict: needs-changes
reviewer: "principal_delivery_audit"
independent: true
plan_hash: "a940dcb4f0bb7d81"
ticket_updated: "2026-09-08T07:16:57.405Z"
board_sha: "804e79add1857d725bcf9158e0e2d4875604e0c6"
expected_reviewers:
  - principal_delivery_audit
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Optional assessment dictionary absorbed unrelated Case form keys."
    disposition: fixed
  - id: F-002
    severity: major
    summary: "Save authorization refusal discarded proposed Case values."
    disposition: fixed
  - id: F-003
    severity: major
    summary: "Carried unshown accepted Facts or equal Confirmed values lost their original provenance."
    disposition: fixed
  - id: F-004
    severity: major
    summary: "Two existing Case-page fixtures were not adapted to the new canonical metadata and accepted-settlement readers."
    disposition: open
  - id: F-005
    severity: note
    summary: "Manual multi-width visual acceptance remains inconclusive."
    disposition: accepted-risk
    reason: "Root-approved integration-first sequencing only; actual visual acceptance remains mandatory and unchecked in Verifying before Done."
---

# ENG-029 independent consolidated review

## Decision and immutable inputs

NEEDS CHANGES at the exact head above. No merge, deployment, Done, or
verification claim. Return the same PR and claim to Implementing for one
bounded existing-caller fixture remediation batch (F-004).

Reviewer is a distinct agent from both authors (root and pack_reconcile),
despite use of the repository's shared GitHub account. Root named this
reviewer alone; it has settled on this head through public review
[5138568230](https://github.com/collisionengineers/pegasus/pull/700#pullrequestreview-5138568230).
No other expected reviewer is absent.

Read the whole current plan, file map `9a7f6a62591c25fe`, report
`3d2573d41ecb51b5`, checklist `c61aa1209472f297`, research
`26c9ebe86c82c2d0`, execution `92a0f5185e50a669` and verification scratch
`2d73c3b407764d83`, plus governing FRD-06/11, relevant design/engineering
rules and EPIC-011/012/014 contexts. The current user and root's explicit
sequencing govern over historical workstream constraints.

Read all 27 PR files (25 source/test/governing-document files and two
generated Case snapshots), their actual callers, and corrections from
prepublication review. The author tree is clean at the full head, on
`ENG-029-case-workspace-editors` in `.worktrees/eng-029`; its Git common
directory is the source repository's `.git`. No reviewer source edits,
builds, tests or captures occurred.

Checkpoint `e0ab668a184c167c5e1fc753a92d9a4fb3120421` was normally merged
with accepted dev `96777888bfa7ee7f85d63979a4a09ae10cda7d13` to make
this head. Independently compared all 27 path blobs: 26 are identical to the
checkpoint; only CaseDataCompletenessPersistenceTests incorporates dev's
Triage import and accepted INTK-064 AcceptIntake constructor arguments.
Its dev-relative changes are only the five-added/two-removed equal-value
assertions already reviewed. No rewrites or obsolete constructor restoration.

At final read the PR is OPEN and still targets dev; its API baseRefOid remains
96777888. The actual dev ref has advanced independently through disjoint
DELIV-048 PR701 to `26ba4ed408317cccdb354dc1e115b0297f15df94`.
No merge is attempted against either base. A later decision must refresh the
base and exact head again.

Fresh live policy: dev protection returns HTTP404 "Branch not protected";
effective branch rules return an empty list. There are no GitHub-required
checks to bypass. That does not make the actual failed CI green, and root
explicitly forbids merging this head. Board tip above was pushed (0 ahead,
0 behind), branch convention matches. Review-stage gates and PR association
were freshly read; round is initially absent/default zero.

All paginated GitHub reviews, issue comments, review comments and GraphQL
review threads were gathered. There are zero review threads and zero inline
review comments, so the thread snapshot is truthfully empty. Bot issue
comment 5580579243 is completed status-only with no finding; disposition:
informational, neither an expected reviewer nor a gate. Public reviewer
COMMENT 5138568230 records the exact-head finding and all failed CI lanes.

## Scope and source conclusions

The existing global Case form now reaches ISaveCaseWorkspace rather than a
parallel section writer. Null/unsubmitted sections remain absent. Typed
inspection charges, canonical assessment paths/types and existing lease,
version, replay, actor and serializable transaction owners are reused.
Engineering writes remain post-handoff; PostReport is the only state added
to the existing AssessmentPolicy writable-state owner. Three store refusal
messages stop duplicating stale state lists.

Settlement's editable financial/logistics fields remain distinct from
Current estimate totals and RepairDays. The report and Case display share
BuildSettlement and the existing totals owner. Dead independent duration is
removed; no schema, service, package, queue or calculation copy is added.
Ordinary GET obtains the existing metadata-only ICaseReportSnapshotSource;
the real EF reader's withImageContent:false path does not read image bytes.
Preview/generation retain their explicit commands.

The report preview tests demonstrate real workspace save/reload of persisted
Case and Assessment fields through the real Preview handler and renderer seam.
Their estimate/signatory projection provider is a fixture seam. They do NOT
prove a SQL SignOffEngineerId/account-to-production-projection round trip;
the report correctly retains this limit. No image-rendering-on-GET claim is
inferred merely from a fake's success.

## Findings and dispositions

### F-001 — fixed before publication

Details.cshtml.cs:996 binds the optional dictionary explicitly with
`[FromForm(Name = "assessmentFields")]`. It no longer consumes unrelated
Case form keys. Existing ordinary-save/refusal callers and the focused
unknown-assessment-path test retain their meaningful assertions.

### F-002 — fixed before publication

CaseMutationPageModel.cs:370-375 is the actual Save authorization catch.
It clears edit authority, retains the proposed values and returns Forbid.
The new authorization-refusal Web case asserts 403/no save, retained proposal
on GET, and no retained edit token. The fix is not stranded in ClaimLease.

### F-003 — fixed before publication

EfCaseDataStore.cs:711-722, in the shared CaseDataFieldWriter.SetConfirmed,
returns only when the same accepted Confirmed-or-Fact value AND type already
exist. Suggestions are excluded. Original Fact/source attribution and an
equal Confirmed actor/time therefore survive the submitted-section round
trip; genuinely changed values and explicitly chosen suggestions still gain
staff confirmation.

The real SQL SubmittedOverviewPreservesAcceptedProvenanceAndDoesNotConfirmAnUnpostedSuggestion
test checks these distinct branches. Earlier SQL null-section coverage was
not overstated as proving this submitted-section case. The narrow
CaseDataCompleteness assertion changes preserve full original Fact equality,
no redundant Confirmed row, all history/version/lease/replay/identity and
completeness checks. They do not weaken the writer's authority or accept
suggestions implicitly.

### F-004 — OPEN: incomplete adaptation of existing reader fixtures

One underlying class, two observed existing callers. Amend the current map
before one bounded test-fixture batch; no production fallback or weakened
assertion.

1. `Browser/AssessmentReadinessSummaryBrowserTests.cs`:
   `NotReadyReportDraftControlsStateTheConditionAndTheShellRenders`
   times out at line61 in both browser and Test-UI capture. Its fixture
   registers only IAssessmentReportProjectionSource, while Details:631-640
   now obtains readiness from ICaseReportSnapshotSource. The real EF port
   sees no persisted fake Case and returns null, so the controls/reasons are
   absent. The fixture also combines a NotReady Case/projection with an
   access fake that opens ReportPreparation. Use the existing metadata port
   in the existing fake and a coherent post-handoff but report-incomplete
   input. Preserve the named conditions, disabled Generate/Preview controls,
   no actual Preview, full shell and accessibility assertions. Do not
   invent ready business facts or weaken the post-handoff gate.
2. `AssessmentEstimateImportWebTests.cs`:
   `UseEstimateRecordsTheEngineersAcceptance` fails the redirected GET
   with HTTP500 at line559. RecordingStores.SetCurrentEstimate at1583-1589
   changes only State/IsCurrent, leaving the accepted RecordedTotals null.
   New `_CaseSettlement.cshtml:65` asks ReportRepairCosts.For for the
   Current repair total; the existing Estimates.cs:280-282 correctly refuses
   accepted estimates without the recorded breakdown. The real
   EfRepairSpecificationStore.SetCurrentEstimateAsync:434-442 calculates
   once and records that breakdown. Make this existing fake reflect the
   real acceptance contract, retaining all request/lease/acceptance and
   redirected-message assertions. Do not alter ForProjection or synthesize
   another calculation owner.

The first was found in finished browser CI; the second in the last-settled
SQL shard, so both are included in this initial consolidated review.

### F-005 — bounded acceptance limitation, not waiver

Manual 1580/1100/760 editor/read-only/conflict visual review is INCONCLUSIVE:
CUA refused local file URLs. No workaround, capture or automated 1440 render
is being represented as human visual acceptance. The amended plan permits
integration first only after source/runtime acceptance, retaining this
unchecked obligation in Verifying. It cannot authorize Done.

## Local evidence, including failed attempts

Root-only Windows/PowerShell verification is retained in the versioned
report and scratch with exact commands. Independently read actual TRX
counters, timestamps and hashes; compared failed-test names so all 21
initial failures are present among the 31 corrected passing cases.

| Attempt | Actual result | Retained SHA256 |
| --- | --- | --- |
| Locked solution restore + whole Release build, root72086 | PASS; build63.93s, zero warnings/errors | Commands/exits in report and scratch |
| Core, root42335 | 100 PASS, 0 FAIL/skip; 131ms | 397195ACE12E391DB5632B37A73A0138DCBDC09AA10740BDEB1439A280AB3FE9 |
| Initial Integration, root42335 | 38 PASS, 21 FAIL, 0 skip; 1m57 | BE35CA056D98924E3A02B3A1224172B110C99D84EE04CDE9F538F3C661F7CECD |
| Corrected Integration build, root52531 | PASS54.67s; zero warnings/errors | Commands/exits retained |
| Corrected Integration, root48296 | 31 PASS, 0 FAIL/skip; 2m16 | 045402CA0DBE8570923D8AF88CEDA07FE4F5F742BA57F44664F790977D0FF65A |

TRXs remain under author artifacts/verification. Initial capture directory is
retained separately. Root's fresh canonical Case capture led to update3PASS,
verify3PASS and catalogue60 routes/67 prototypes/0 broken references.
Independently read both generated default/conflict deltas; no fabricated
snapshot/index change. These focused local passes do not erase the later CI
findings.

## Completed CI failure census

[Run34196369756](https://github.com/collisionengineers/pegasus/actions/runs/34196369756)
is bound to the exact head above, event pull_request, started06:49:19UTC,
completed failure by07:10:48UTC. It actually ran despite the skip-ci PR title
and earlier checkpoint; the final normal merge commit triggered it. No
reviewer cancellation, rerun or duplicate local suite occurred.

| Job | PASS | FAIL | Skipped | Total | Test duration |
| --- | --- | --- | --- | --- | --- |
| unit101964990528 | 1928 | 1 | 14 | 1943 | 5s |
| SQL1 101964990607 | 672 | 5 | 1 | 678 | 15m46s |
| SQL2 101964990706 | 616 | 29 | 3 | 648 | 13m38s |
| SQL3 101964990656 | 620 | 15 | 0 | 635 | 13m26s |
| browser101964990855 | 133 | 2 | 0 | 135 | 13m39s |
| Test-UI capture101964990562 | 133 | 2 | 0 | 135 | 11m |

The same two browser failures occur in browser and capture; they are not four
distinct defects. Test-UI stopped in capture, so no comparison PASS is claimed.
Changes/documentation/local-development-scripts/reference-data succeeded;
infrastructure was skipped. SQL coverage job101970106397 succeeded, but is
not proof that the three failed SQL shards passed.

Complete SQL failed-case class counts, taken from job logs:

| Shard | Class | Failed xUnit cases |
| --- | --- | --- |
| 1 | VehicleLookupBackfillTests | 3 |
| 1 | AssessmentEstimateImportWebTests | 1 |
| 1 | TestUiFocusedRenderTests | 1 |
| 2 | CustodyOutboxIntegrationTests | 19 |
| 2 | ImageViewingWebTests | 1 |
| 2 | InstructionDraftWebTests | 5 |
| 2 | MailWorkspaceWebTests | 1 |
| 2 | QdosTriageIntegrationTests | 2 |
| 2 | TriageQueuesWebTests | 1 |
| 3 | ImageIntakeWebTests | 1 |
| 3 | MailboxIntakeIntegrationTests | 1 |
| 3 | MultiFormatIntakeWebTests | 5 |
| 3 | RecoveryTests | 1 |
| 3 | SendToAiIntegrationTests | 2 |
| 3 | UploadConfirmationWebTests | 5 |

F-004 names the actual ENG-029 adaptation failures. Other failures are
preserved outside this corrective source map and explicitly handed to root
for existing-owner remediation, not silently treated as PASS:

- Unit PrincipalIdentificationCorpusTests.TrackedPegasusSourceHashesHaveNotDrifted
  at305 fails on a removed QdosCaseMatchPolicy snapshot path. A bounded
  independent census of all12 Pegasus entries finds three retired policy
  paths and one changed classification-contract hash; eight match.
  This is the accepted [[TICK-035]] policy move's stale generator/manifest
  inventory, not this PR's diff. Root assigned separate source-inventory
  follow-up preparation; do not restore obsolete policies or weaken hashes.
- UploadCaseSearchBrowserTests.CaseSearchComboboxIsKeyboardOperableAndCompletesTheAttachDecision
  at53 is the other browser/capture failure. Independent intake diagnosis
  confirms the old body-only instruction has no accepted work-type tell;
  ProcessIntake:889-896 parks it and durable Unidentified wins
  UploadOutcome:263-276. A genuine instruction would instead auto-allocate,
  also not create the assumed open decision. The legitimate existing
  unmatched-image AwaitingInstruction route is the bounded [[INTK-016]]
  owner remedy, preserving keyboard/ARIA/axe/real drain and attach evidence.
- SQL samples include explicit accepted-route/missing-work-type refusals and
  null allocation lookups in existing intake setup, plus differing source
  counts. These are not all proven to share one cause; the class census and
  exact logs remain for root's owner-based diagnosis, not one patch per test.
  SQL1 HeldLeaseConfirmation uses SendToAi's old SeedAcceptedCaseAsync and
  fails CaseCreated versus NeedsSorting. Its three VehicleLookupBackfill
  failures stop in SeedCaseWithObservationAsync:117 before migration checks.
- SendToAiIntegrationTests.InaccessibleCaseCannotPostSendToClaude fails its
  disabled-control expectation. Its FakeGetAssessmentAccess(false) gives
  NotReady; the unchanged _CaseEstimate:24 hides the action row whenever
  AssessmentIsReadOnly. Neither that access assignment nor the markup
  changed in this PR. AdministrationConnectorValuesOverrideConfiguration
  also fails, separately in its intake seed. Do not weaken a production
  access gate as part of this Case-editor fix.

The failure census is an integration blocker for the controller's converged
v1 verification, not an authority to absorb unrelated ticket scope or to
waive the full CI. This review introduces no new provider, cloud or privacy
constraint.

## Handoff

After this whole record, fresh gates authorize the sanctioned
Review-to-Implementing return on F-004. Preserve this head, public review,
all local/CI attempts, same PR/branch/worktree/claim, and the manual visual
obligation. Author must amend the bounded map/plan for the two existing
fixture callers and execute through the fresh resume packet. Root owns
focused runtime verification; the next independent review is a delta
against F-004, changed lines and their direct contracts, not a fresh
repository audit.
