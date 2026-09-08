# Research — CASE-031: remaining EVA claimant-address submission

## Question

What remains to send the canonical claimant address as EVA ClmAdd, without
redoing completed intake/Case work or altering the operator ZIP?

## Current findings — 2026-09-08

Read-only source base: origin/dev at
cc441645b0a62a806e34367ad75e9eaff4df8b11. No build, test or provider call ran.

- Claimant address already exists in InstructionDraft
  (src/Pegasus.Core/Intake/IntakeContracts.cs:583), in QDOS and other
  supported extraction definitions, and in receipt persistence
  (EfIntakeReceiptStore.cs:586,735,1003). CaseDataSnapshotFactory.cs:238
  promotes it through the existing provenance guard; its candidate conflict
  guard refuses unresolved evidence. TICK-035's separately reviewed canonical
  provenance correction must be preserved, not replaced here.
- The canonical Case value is CaseClaimantData.Address. CaseDataContracts.cs:
  53,62,85 owns accepted status and precedence: Current is Confirmed, then
  Fact, then Suggestion; only Fact and Confirmed are accepted. EfCaseDataStore
  projects, saves and replays the address at lines360,583,660. The normal
  Case-data surface and guarded edits already carry it. No new field, schema,
  extraction, UI or staff-confirmation path is required.
- CaseData has no independent Conflict status. An unresolved extraction
  conflict must not become a Fact; missing/unaccepted projected evidence is
  blocked at this boundary. A deliberate Confirmed value superseding a Fact
  is an accepted correction, not a conflict. Do not invent a second conflict
  classifier or fall back from an invalid current value to an older Fact.
- EvaInstructionPayload (EvaApiContracts.cs:65) has no claimant address.
  CaseEvaApiMapping.Map accepts only the 13-field EvaReplayFields plus Case,
  Principal, settings and files. Its API mapping version is1. Its sole
  production caller is EvaSubmissionStore.cs:140; known test consumers are
  EvaApiMappingTests and EvaApiTransportTests. EvaInstructionSerializer
  (EvaApiTransport.cs:386) emits InsName but no ClmAdd.
- EvaSubmissionStore.ExecuteAsync already loads canonical Case data and
  returns known operation replay before mapping/images/transport. After that,
  it maps the unchanged export fields, calls EvaCaseImageReader (whose
  ReadVersionsAsync reaches the document-content boundary), then submits and
  records the attempt. The address guard belongs after known replay and
  before LoadEligibleImagesAsync and SubmitInstructionAsync. Invalid input
  returns an existing blocking result, with no submission/history/state
  mutation. Keep replay, actor/mode/Engineer checks and outcome recording.
- EvaSubmissionPolicy is the current Core owner for API submission decisions.
  Extend it to select/validate the existing accepted claimant field. Do not
  place business validation in the serializer or add a service/result layer.
- docs/json-extraction-parity/eva-api-docs.md defines ClmAdd as required,
  max40, and supplies the address example22 Park Avenue. It does not make
  inspection-location address a claimant address. Preserve the accepted
  claimant string exactly; no shortening, line flattening or substitution.
- Reject absent, unaccepted/suggestion-only, whitespace-only, control/format
  containing and over40 values. Ordinary commas, hyphens and apostrophes
  inside an address are valid. The old punctuation-only failure probes do
  not establish a punctuation blacklist or a general postal-validation rule.
- ADR-0038 removes automatic submission. FRD-07 permits explicit manual send
  and deliberate re-send, preserves four outcomes and known-operation replay,
  and distinguishes API transport prerequisites from the fixed13-key ZIP.
  The current ClmAdd requirement needs a narrow API-only clarification there;
  it must not add a Case-readiness or ZIP-export gate. TICK-085 shares FRD-07
  for a separate estimate-import section; root sequences ownership.
- Existing actual-caller fixture
  CustodyOutboxIntegrationTests.EvaRoutesTransitionFirstSendAtomicallyAndResendWithoutStateChange
  constructs EvaSubmissionStore at lines1466,1844,1914 and already proves
  manual/re-send/replay/version-conflict/undelivered outcomes. Reuse its SQL
  fixture and recording transport. A bounded document-content read counter
  at the existing IDocumentContentStore seam can prove zero image reads;
  no new application port or test framework is needed.
- Core EvaApiMappingTests/EvaSubmissionPolicyTests and Integration
  EvaApiTransportTests already prove typed mapping, decisions and exact wire
  JSON. Existing EvaBundleContractTests prove deterministic ZIP keys/order/
  bytes and remain unchanged. No declared external research sources exist.

## Historical disposition

The earlier research806d0a70e38ca4fd, files4f0d1451d44a4929 and
plan85d53ae384521aa8 remain historical board versions. Their promises to add
extraction, draft columns, Case fields and UI are obsolete because those paths
already exist. Their automatic-submission and once-per-case-only language is
superseded by ADR-0038 and the current explicit re-send contract.

The prior research records controlled vendor probes on2026-08-28:
null/empty/whitespace returned400; punctuation-only and invisible-character
placeholders returned500. These are retained historical observations, not
fresh calls, live acceptance proof, or authority to ban normal punctuation.

## Implications

Five production files, existing focused tests and one bounded FRD-07
clarification are sufficient. Pass accepted claimant address separately to the
API mapping; increment only its API mapping version. Leave EvaReplayFields,
CaseEvaMapping, ZIP schema/fixtures/bytes and all intake/Case-data models
unchanged. No dependency, migration, UI capture or live EVA call is needed.

## Open questions

No unresolved product choice. Root must sequence the shared FRD-07 ownership
before execution; CASE-031 remains Preparing and untaken.
