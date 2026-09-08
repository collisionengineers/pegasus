# Plan — CASE-031: send the canonical claimant address to EVA

## Objective

Complete the existing manual EVA API caller's required ClmAdd field using the
accepted Case claimant address. No new intake, Case-data or ZIP capability.

## Starting state

Read-only research base: origin/dev
cc441645b0a62a806e34367ad75e9eaff4df8b11.
Evidence: research/research.md@084f62058746126a and
files/files.md@623d466dfb5fd003.

The address already extracts, persists with provenance and supports normal
Case editing. Only the API payload/map/serializer and local submission guard
are missing. Old plan 85d53ae384521aa8 is superseded, not another implementation
track. CASE-031 is Preparing and untaken. No tests ran during preparation.
Before later take, root must sequence FRD-07 ownership with TICK-085 and select
fresh accepted dev, preserving TICK-035's canonical provenance changes.

## Governing docs

- FRD-01 — meets: use existing immutable Case identity and canonical field
  projection; no new allocation, completeness, edit or handoff behavior.
- FRD-02 — meets: consume accepted provenance-bearing claimant evidence,
  not suggestions or another party/address role.
- FRD-07 — modifies its direct API paragraph only under the current user
  remediation authority and root's bounded instruction: identify ClmAdd's
  required accepted claimant value, max 40 and local no-call refusal. Keep
  the thirteen-key ZIP, manual outcomes/re-send and retained attempt rules.
  Root sequences this file against TICK-085's separate import work.
- ADR-0038 — meets: manual API only; known operation replay stays idempotent.
  No Worker route, unattended submission or retry framework is introduced.

No new ADR or compatibility layer is warranted.

## Required changes

Use CaseField.Current and CaseDataValue.IsAccepted, owned by Core Case data,
inside the existing EvaSubmissionPolicy. Accept Confirmed before Fact; a
Suggestion alone is not accepted. An invalid selected value does not fall back
to an older Fact. Unresolved extraction conflict has no accepted canonical
value; do not invent a second conflict model.

Reject absent/whitespace-only values, any control or Unicode format character,
and length over 40. Preserve valid text exactly, including normal commas,
hyphens and apostrophes. No truncation, multiline flattening, postal inference,
punctuation blacklist or inspection-address substitution.

Pass the validated string separately into CaseEvaApiMapping.Map and the typed
EvaInstructionPayload. Emit exactly ClmAdd from the existing serializer.
Advance CaseEvaApiMapping.MappingVersion from 1 to 2; leave the ZIP mapping and
EvaReplayFields unchanged. Keep one validation owner and the existing
SubmitCaseToEvaResult blocking-reason shape, not a new wrapper/service.

In EvaSubmissionStore.ExecuteAsync, retain all existing authorization,
Principal mode, Engineer and replay checks. After a known replay has been
returned, and before LoadEligibleImagesAsync/SubmitInstructionAsync, validate
the loaded Case claimant field. Return a named blocking reason without
submission/history/state/version/lease mutation when invalid. A known replay
must still return its previously recorded outcome if the current address is
now unusable; it never rereads images or resubmits.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | src/Pegasus.Core/Eva/EvaApiContracts.cs | Typed claimant address. |
| Modify | src/Pegasus.Core/Eva/CaseEvaApiMapping.cs | Exact API-only mapping and version. |
| Modify | src/Pegasus.Core/Eva/EvaSubmissionPolicy.cs | Accepted address decision/reason. |
| Modify | src/Pegasus.Infrastructure/Eva/EvaApiTransport.cs | Exact ClmAdd serializer member. |
| Modify | src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs | Guard and production mapping caller. |
| Modify | tests/Pegasus.Core.Tests/Qdos/EvaApiMappingTests.cs | Known callers, exact value and role separation. |
| Modify | tests/Pegasus.Core.Tests/Qdos/EvaSubmissionPolicyTests.cs | Accepted status, precedence and malformed boundaries. |
| Modify | tests/Pegasus.IntegrationTests/EvaApiTransportTests.cs | Known payload helper and serialized JSON. |
| Modify | tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs | Actual SQL/store caller and no-external-work/replay evidence. |
| Modify | docs/frd/frd-07-eva-and-external-engineering-handoff.md | Root-sequenced API requirement only. |

No generated artifacts change.

## Do not modify

- src/Pegasus.Core/Eva/CaseEvaMapping.cs
- src/Pegasus.Core/Eva/EvaBundleSchema.cs
- tests/Pegasus.Core.Tests/Qdos/EvaBundleContractTests.cs
- src/Pegasus.Core/Cases/CaseDataContracts.cs
- src/Pegasus.Core/Cases/CaseDataOperations.cs
- src/Pegasus.Core/Intake/**
- src/Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs
- src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs
- src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs
- src/Pegasus.Infrastructure/Persistence/Migrations/**
- src/Pegasus.Web/**
- src/Pegasus.Worker/**
- docs/design/test-ui/**
- docs/operator-notes.md
- corpus/**

## Constraints

Use existing libraries and fixtures; no schema, runtime dependency, UI,
framework or new application port. Preserve every current manual outcome,
re-send/version/lease assertion. Supplied vendor address evidence may seed the
new isolated field in the existing fixture; malformed/length variants are
structural tests, not claims of genuine new instructions. No live provider or
email calls, credentials, InstEmail, Principal activation or deployment work.

## Ordered steps

1. Extend the three existing Core API files with the typed address, separate
   mapping input/version and single accepted-address policy. Update existing
   mapping/policy tests and all known Map callers in the declared test files.
2. Wire the store guard after known replay and before image retrieval or
   transport, and emit ClmAdd from EvaInstructionSerializer. Extend the
   existing CustodyOutbox EVA fixture and its recording transport; use the
   existing IDocumentContentStore boundary to count/refuse image reads in
   invalid/replay probes. Preserve actual SQL history/state/lease assertions.
3. Amend only FRD-07's direct-API claimant prerequisite after root's ownership
   clearance. Review the complete bounded diff for duplicated policy, unknown
   callers, fabricated substitutions and accidental ZIP/UI/schema changes.
4. Freeze source for root's focused commands. Record every actual exit/result,
   including failures, in the post-implementation report; leave implementation
   ready for independent review, not self-merged or self-verified.

## Acceptance checks

- Fact passes; Confirmed wins over a differing Fact; suggestion-only/missing/
  unresolved evidence fails. An invalid Confirmed value never selects old Fact.
- Valid canonical address equals payload ClaimantAddress and wire ClmAdd
  exactly, while inspection location remains independently mapped.
- Exactly 40 characters pass; over 40, whitespace-only, embedded control and
  format characters fail locally. Ordinary address punctuation passes.
- Actual EvaSubmissionStore invalid/replay paths perform zero document-content
  reads and zero new transport calls. Invalid new operation creates no
  EvaSubmission or Case action-history entry and changes no state/version/lease.
- The existing real store fixture still proves successful manual first send,
  explicit re-send, distinct undelivered outcomes and post-delivery conflict
  replay. Known replay returns the original outcome after an address becomes
  invalid, without another attempt.
- API mapping version is 2; existing ZIP schema/keys/order/bytes and mapping
  identity/version are unchanged. No fresh EVA acceptance claim is made.

## Commands

Execution is not authorized by this preparation. Once root assigns execution,
root alone runs heavy commands in the exact ticket worktree (PowerShell 7).
Author supplies the frozen source and reads exit-coded evidence.

    dotnet restore ./Pegasus.slnx --locked-mode
    dotnet build ./Pegasus.slnx --configuration Release --no-restore
    dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~EvaApiMappingTests|FullyQualifiedName~EvaSubmissionPolicyTests|FullyQualifiedName~EvaBundleContractTests"
    dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~EvaApiTransportTests|FullyQualifiedName~CustodyOutboxIntegrationTests.EvaRoutesTransitionFirstSendAtomicallyAndResendWithoutStateChange"

Prefer extending that existing actual-caller method; if root agrees to split
new probes into named methods in the same file, record their exact filter
before verification. No browser/corpus/capacity run is needed for this API-only
diff. Root owns scheduling the repository's final non-Corpus rail/CI; this
ticket must not launch a competing repeated full run or claim one passed.

## Failure and deviation rules

Stop on a failing check and retain it. Additional consumers, shared-file
ownership, governing conflict, schema or broader validation needs require a
recorded scope amendment/root decision before edits. Do not change fixtures
or intended behavior just to make a test pass.

## Stop condition

This preparation ends with research/files/plan/checklist/body read back in
Preparing, untaken, for root's bounded review and ownership sequence. Later
assigned execution stops at independently reviewable source plus actual
root-run evidence; no self-review, merge, deployment or next ticket.
