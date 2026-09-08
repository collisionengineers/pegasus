# Files — CASE-031

## Where the change lands

| Path | Responsibility and risk |
| --- | --- |
| src/Pegasus.Core/Eva/EvaApiContracts.cs | Add typed ClaimantAddress to EvaInstructionPayload; update its known constructors only. |
| src/Pegasus.Core/Eva/CaseEvaApiMapping.cs | Separate canonical address argument and exact payload mapping; API MappingVersion 1 to 2 only. |
| src/Pegasus.Core/Eva/EvaSubmissionPolicy.cs | One Core-owned accepted-address selection/validation and blocking reason; preserve other submission decisions. |
| src/Pegasus.Infrastructure/Eva/EvaApiTransport.cs | Existing EvaInstructionSerializer emits exact ClmAdd; no postal policy or normalization here. |
| src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs | After known replay, guard before external image read/submission and pass accepted address; no outcome/workflow redesign. |
| tests/Pegasus.Core.Tests/Qdos/EvaApiMappingTests.cs | Update current Map callers and prove exact address, role separation and API version. |
| tests/Pegasus.Core.Tests/Qdos/EvaSubmissionPolicyTests.cs | Accepted Fact/Confirmed precedence, unaccepted/missing and value boundaries. |
| tests/Pegasus.IntegrationTests/EvaApiTransportTests.cs | Update Payload helper and assert ClmAdd in real serialized request without HTTP calls. |
| tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs | Extend existing real store/SQL fixture and recording boundary fakes for guard, exact payload, no calls/mutations and known replay. |
| docs/frd/frd-07-eva-and-external-engineering-handoff.md | Root-sequenced direct-API claimant-address prerequisite only; preserve TICK-085 estimate-import section. |

## Context files

| Path | Constraint |
| --- | --- |
| AGENTS.md | Existing Core policy owner, exact claim scope, immutable sources and one heavy verifier. |
| docs/frd/frd-01-case-identity-and-lifecycle.md | No new Case mutation, completeness gate or handoff semantics. |
| docs/frd/frd-02-intake-and-source-identity.md | Preserve source provenance and unresolved ambiguity; no extraction reinterpretation. |
| docs/adr/0038-manual-only-eva-api-submission.md | No automatic EVA path, retry worker or second setting. |
| docs/json-extraction-parity/eva-api-docs.md | Supplied ClmAdd required/max 40 vendor contract. |
| src/Pegasus.Core/Cases/CaseDataContracts.cs | CaseField.Current and CaseDataValue.IsAccepted are the existing precedence/status owner. |
| src/Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs | Existing conflict/provenance promotion; preserve TICK-035 correction. |
| src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs | Already persisted/projected claimant field and guarded correction. |
| src/Pegasus.Infrastructure/Persistence/EvaCaseImageReader.cs | Existing ReadVersionsAsync boundary whose invocation must be avoided for invalid address. |
| src/Pegasus.Core/Eva/CaseEvaMapping.cs | Fixed 13-field ZIP mapping; not API claimant validation. |
| src/Pegasus.Core/Eva/EvaBundleSchema.cs | Deterministic ZIP shape remains unchanged. |
| tests/Pegasus.Core.Tests/Qdos/EvaBundleContractTests.cs | Run existing byte/order tests unchanged as bounded regression evidence. |

## Ripple effects

The Map-call census at cc441645 finds one production caller in
EvaSubmissionStore and two test files above; no other direct
EvaInstructionPayload constructor was found. Recheck the census on the
eventual accepted base and update only these known callers. No generated
artifacts, schema/grants, UI snapshots or deployment files change.

The existing large CustodyOutbox EVA fixture has three store constructions;
populate accepted claimant data for its unchanged successful-send probes,
retain every existing outcome/version/lease assertion, and add no new
fabricated mail/image/instruction. Reuse supplied vendor address evidence for
the isolated new field and label malformed/length probes as structural tests.

## Out of scope

Intake/extractor/Case-data schema and fields; UI and MCP; Case readiness,
assignment or handoff behavior; ZIP/export types/mapping/fixtures; new address
parser/geocoder; inspection/repairer/third-party substitution; InstEmail,
credentials, Principal activation, delivery configuration, live EVA requests
and automatic submission. Root checked the actual write maps: TICK-085 does
not claim FRD-07; its context reference is not a write owner. CASE-031 may
change only the API paragraph while preserving the separate import section.


## Root-authorized fixture correction after attempt 1

The existing EVA caller fixture's literal invalid PDF contains no attached
work-type tell. The now-required classifier correctly withholds allocation;
this is stale test preparation, not a non-QDOS profile-guard regression.
Preserve CaseCreated and all downstream assertions, without a policy override.

Use existing QdosCorpus.Root and QdosMappingCustodyFact conventions for
qdosmapping/(EREF10) RTA on 14_08_2026  Mr Paul Larcombe (Our Ref AMA_47857_1, Vehicle PG18 BTY).eml.
Pin original SHA256
3063FF9ECB31878F582FB439047D999A41A7C6FE5B978CFBEE5C7E7F277553B4.
Keep its headers/body/33742_1_LtrtoEngineerIn.pdf unchanged; append only the
same two existing image fixture bytes in memory for the bounded export probe.
Explicitly call that input derived, not an untouched original. Existing
GenuineQdosCorpus loader cannot address this supplied qdosmapping directory;
reuse QdosCorpus resolution and the existing explicit hash-check convention,
not another loader. Local acceptance must execute, never count a skip as PASS.

The inspected letter supplies ENGINEER NOTIFICATION (REPORT + AUDIT REPORT),
so accept InspectionAndAudit and assert its actual AMA/47857/1 reference in
exported JSON. Replace no substantive lifecycle/outcome/ZIP/image assertion.
Improve setup failure output with receipt reason, route and classification.
Only CustodyOutboxIntegrationTests.cs changes in this correction; no production
intake source, parser, schema, image generation or immutable source write.
Root reruns only the failed actual-caller method after a compiler check.
