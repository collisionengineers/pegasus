# Post-implementation report — CASE-031

## Status

Root attempt 1 has one failing intake fixture setup; correction investigation
is active. No overall PASS, PR, integration or deployment claim. No author
build/test/live call ran.

## Starting state and scope

Exact base: dev56566371a5b80ef59c4f98e377c8e8ff6469b5f7.
Branch: CASE-031-eva-claimant-address. Worktree: .worktrees/case-031.
Ready packet, clean exact HEAD, worktree root, common Git directory and
claim census were checked before the isolated take. Root approved the ten-file
map and verified TICK-085 does not claim FRD-07.

Plan7c58da505ab83829 contains only the current files-version pin refresh
after root's approved plan d1e74db86c2c6a50/files56e85ba675b83b94. No scope
expansion or new schema, dependency, UI or automatic submission.

## Implementation and production caller

- EvaApiContracts.cs: required typed ClaimantAddress.
- CaseEvaApiMapping.cs: separate canonical claimant-address argument carried
  unchanged; API MappingVersion2. No EvaReplayFields/ZIP mapping change.
- EvaSubmissionPolicy.cs: AcceptedClaimantAddress uses CaseField.Current and
  IsAccepted. Confirmed wins over Fact; invalid current data never falls back.
  Missing/suggestion-only, whitespace-only, over40 and any Unicode control/
  format character return no valid value. Normal punctuation is preserved.
- EvaSubmissionStore.cs: the existing manual ExecuteAsync returns known
  operation replay before the new guard, then validates before
  LoadEligibleImagesAsync and SubmitInstructionAsync. Invalid new operations
  return the existing blocking-result shape without persistence or workflow
  writes.
- EvaApiTransport.cs: existing EvaInstructionSerializer writes exact ClmAdd.
- FRD-07: only the direct-API paragraph gains the claimant prerequisite and
  exact refusal/replay behavior, expressly not a Case-readiness or ZIP gate.

## Focused tests written, not yet run

- EvaApiMappingTests: existing caller updates plus exact value/whitespace
  preservation, independent inspection address and API version2.
- EvaSubmissionPolicyTests: accepted Fact, Confirmed precedence, suggestion/
  missing refusal, invalid Confirmed with good old Fact, ordinary punctuation,
  control and BMP/supplementary Unicode format characters, 40/41 boundary.
- EvaApiTransportTests: existing serialized request asserts exact ClmAdd and
  distinct inspection-location address.
- CustodyOutboxIntegrationTests: extended the existing
  EvaRoutesTransitionFirstSendAtomicallyAndResendWithoutStateChange method,
  retaining every prior assertion. Its real SQL/store caller checks eight
  invalid address states, zero image reads/transport calls/submission/history/
  state/version/lease mutations, accepted Fact first send, known replay after
  invalidation, and Confirmed-over-Fact explicit re-send.
  The existing recording transport now retains its payloads. One small private
  IDocumentContentStore read counter delegates real eligible-image reads to
  the existing store; it is not application infrastructure.
  Supplied vendor address and existing fixture address are the positive field
  evidence; malformed/length/punctuation variants are structural probes, not
  newly fabricated instructions or live vendor acceptance evidence.

## Verification attempts

No restore/build/test attempt by this author. Root is the sole heavy owner.

Read-only checks on 2026-09-08:
- git diff --check: exit0, no whitespace errors.
- Complete changed-path census: exactly the approved ten files.
- Source caller census: one production Map call (EvaSubmissionStore), known
  two mapping test files; no unhandled direct payload constructor.
- ZIP mapping/schema/fixtures, intake/Case-data models, Worker, UI and generated
  snapshots have no diff.

Editing attempts included rejected apply_patch contexts (a comment-separated
contract member, then UTF-8 BOM first lines); atomic refusal left source
unchanged until corrected. These were not build or test failures.
Two read-only path searches used nonexistent filenames/globs and returned
exit1; corrected source discovery supplied the actual files. No missing source
was assumed to be absent behavior.



### Root attempt 1 — 2026-09-08, session 3667

Locked restore PASS for all seven projects (maximum reported 1.48s).
Solution Release build PASS, 127.88s, zero warnings/errors. Core command PASS:
57/57, 127ms. Integration command FAIL, exit1: 12 PASS/1 FAIL, 35s.
The failing existing EvaRoutesTransitionFirstSendAtomicallyAndResendWithoutStateChange
stopped at line1412: expected CaseCreated, actual NeedsSorting, before address
probes. No downstream store/ZIP/address runtime claim is made for this attempt.

Artifacts retained unmodified under artifacts/verification:
- case-031-core.trx: F0859E4685461865D0376F0562F33F2F3E146345E4F187A9B3F60133B0D9DC2A.
- case-031-integration.trx: E2AC1683BA4DDFEA610B7F808148CC89AE8886DB19FDB6555E5D88AE21CCEF94.

Author read the TRX counters/failure and computed both SHA256 values. Root
supplied actual command exits/build durations. Original failure is retained.

## Commands for root

In .worktrees/case-031, PowerShell7, no live providers required:

    dotnet restore ./Pegasus.slnx --locked-mode
    dotnet build ./Pegasus.slnx --configuration Release --no-restore
    dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~EvaApiMappingTests|FullyQualifiedName~EvaSubmissionPolicyTests|FullyQualifiedName~EvaBundleContractTests"
    dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~EvaApiTransportTests|FullyQualifiedName~CustodyOutboxIntegrationTests.EvaRoutesTransitionFirstSendAtomicallyAndResendWithoutStateChange"

No extra method/filter, browser, snapshot, corpus or capacity run is needed.
Record actual root command exits and retained artifacts here before publication.

## Governing docs and simplicity

FRD-01/02: existing Case identity, field authority and provenance remain.
FRD-07: approved API prerequisite; ZIP and all existing manual outcomes/re-send
remain. ADR-0038: no automatic submission. One Core validation owner, existing
mapping/serializer/store; no new application abstraction or dependency.
Reuse, simplicity, efficiency and layer-direction review found no additional
required source change. The only new test boundary is a local read counter
needed to prove no external image access.

## Process deviations

The first execution orientation read get_item/get_doc_gates before requesting
the ready packet, contrary to skill ordering; no Git/ticket mutation preceded
the ready packet. The governing-doc skill was read after the initial authorized
FRD edit; its model/template and existing ref were then checked, with no
additional document scope. Both deviations are retained rather than hidden.

## Remaining boundary

Root focused verification, then report/checklist update and authorized
commit/push/PR for independent review. No self-review/merge. Post-merge proof
must bind the actual integration SHA and reuse or rerun appropriate exact-source
checks honestly. No deployment has occurred.
