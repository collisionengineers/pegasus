# Plan — CASE-031: Extract, retain, display and submit claimant addresses

## Approach

Extend the existing claimant field through the established intake-draft,
provenance-bearing Case-data, and guarded Save Case paths. At the EVA boundary,
read that canonical Case claimant address in EvaSubmissionStore, validate the
vendor-required value locally, pass it separately into the API mapping, and
serialize ClmAdd. This is smaller and safer than changing the shared offline
export model: the ZIP remains exactly as it is, while manual and automatic API
submission both receive the same local gate and wire field.

Alternatives rejected:

- Reusing inspection address is incorrect domain data.
- Adding a claimant-address table/service duplicates the existing Case field
  model.
- Adding claimant address to EvaReplayFields would pull the ZIP contract into
  scope.
- Truncating or substituting a placeholder would fabricate a different address.
- Broad postal-address inference would exceed the supported provider grammar.

## Governing docs

- **FRD-01 — meets:** steps 2–4 extend snapshotted claimant Case data and use
  the existing lease, version, provenance, history and completeness-invalidation
  rules. No new mutation authority is introduced.
- **FRD-02 — meets:** steps 1–2 preserve original evidence, explicit source
  candidates and ambiguity. Claimant address does not become an allocation
  gate, and no address is inferred from a different party or role.
- **FRD-07 — meets:** steps 5–6 complete the existing direct EVA API request
  against the published required field while preserving once-per-case,
  fail-closed and distinct outcome behavior. The focused manual handoff ZIP is
  deliberately unchanged.

No ADR is needed: the plan extends established models and adapters without a
new architectural boundary. No governing document modification is planned.

## Steps

1. **Add conservative intake extraction.**
   - Extend QdosInstructionExtractionPolicy's one field-definition list with a
     claimant-address field using only labels evidenced by repository-provided
     QDOS instructions; if no additional alias is evidenced, support the exact
     Claimant Address label only.
   - Map the unambiguous bounded value into InstructionDraft.
   - Add focused tests for a supported label, absence, conflicting candidates,
     third-party exclusion and whitespace normalization.
   - Reuse InstructionFieldEngine; add no parser or secondary vocabulary.

2. **Persist the optional intake draft and provenance.**
   - Add nullable ClaimantAddress to InstructionDraftEntity with the same
     bounded-text convention selected for the Case value.
   - Carry it through EfIntakeReceiptStore and EfIntakeMutationStore in every
     create, map, correction and replay path.
   - Add an EF migration, designer and model snapshot for the nullable column;
     confirm existing InstructionDrafts permissions remain sufficient.
   - Promote only unambiguous evidence into CaseDataSnapshotFactory through
     AddExtractedValue.
   - Keep InstructionDraftCompleteness unchanged so missing address does not
     block allocation.

3. **Extend the canonical Case claimant field.**
   - Add claimant_address to CaseDataFieldNames.All.
   - Extend CaseClaimantData and CaseEditableData, normalize bounded multiline
     text with existing Core helpers, and wire EfCaseDataStore save/replay/
     projection.
   - Update all production and test constructors, preferring named arguments
     where it reduces positional drift.
   - Prove accepted-source provenance, staff correction attribution, stale
     version refusal and persistence replay using existing Case tests.

4. **Display and edit the one canonical value.**
   - Extend the shared intake draft view/partial and Case creation binding.
   - Add Claimant address beside Claimant in the existing Case identity block
     and to the existing edit form; add no explanatory panel or duplicate
     read-only list.
   - Carry the field through DetailsModel proposed-value recovery and
     Assessment MCP read/save parity so both existing callers use ISaveCase.
   - Add focused page/browser assertions for extracted display, missing display
     and guarded staff correction.

5. **Map claimant address only into the EVA API.**
   - Leave EvaReplayFields, CaseEvaMapping, EvaBundleSchema and their tests and
     mapping identities unchanged.
   - Read caseData.Claimant.Address from the already-loaded projection in
     EvaSubmissionStore.
   - Extend CaseEvaApiMapping and EvaInstructionPayload with claimant address,
     advance only CaseEvaApiMapping.MappingVersion, and emit exact ClmAdd from
     EvaInstructionSerializer.
   - Add contract assertions that the API value is byte-for-byte the canonical
     Case value and the ZIP output remains unchanged.

6. **Fail closed before external work.**
   - Add one Core-owned or mapping-owned validation result for missing,
     whitespace-only, control/format-only and over-40-character claimant
     addresses.
   - In EvaSubmissionStore, return a named blocking reason before image reads,
     attempt persistence or IEvaApiTransport invocation.
   - Cover both manual and automatic callers through the shared store and prove
     invalid input results in zero HTTP requests and no delivered submission.
   - Do not issue further live EVA test requests.

7. **Verify and simplify.**
   - Run focused Core and Integration tests covering intake, Case persistence,
     browser/UI, API mapping/transport/submission and unchanged EVA bundle
     contracts.
   - Run the canonical locked restore, Release build and non-Corpus test suite.
   - Review the branch diff through reuse, simplification, efficiency and
     altitude lenses; record docs-only as not applicable only for unchanged
     documentation surfaces, and apply behavior-preserving findings.
   - Confirm no ZIP/export source, fixture, mapping identity or deterministic
     artifact changed and summarize command exit codes in the
     post-implementation report.

## Verification

Focused commands will be selected from the affected projects and include:

- Pegasus.Core.Tests filters for QDOS extraction, Case data policy, EVA API
  mapping and EvaBundleContractTests.
- Pegasus.IntegrationTests filters for intake persistence, Case data
  persistence, operator/browser behavior, EvaApiTransportTests and
  EvaSubmissionPersistenceTests.
- A transport-body assertion for exact ClmAdd.
- A no-call assertion for each invalid claimant-address class.
- An unchanged deterministic EVA bundle contract assertion.

Canonical delivery gate:

    dotnet restore ./Pegasus.slnx --locked-mode
    dotnet build ./Pegasus.slnx --configuration Release --no-restore
    dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"

Proof after merge will cite the exact merge SHA, focused and full command exit
codes, migration presence, the API JSON assertion, invalid-input no-call
assertions, Case UI evidence, and unchanged ZIP contract tests.

## Risks / open questions

- **Source-label overreach:** mitigate by accepting only labels demonstrated in
  repository-provided QDOS evidence; unsupported layouts remain unextracted.
- **Positional-record drift:** update the complete constructor census and use
  named arguments where practical.
- **Address semantics collision:** claimant, inspection, repairer and third
  party remain separate named fields and tests.
- **EVA length mismatch:** preserve the canonical value but block API submission
  above 40 characters; never truncate.
- **Accidental ZIP change:** exclude shared export types from implementation and
  run existing deterministic bundle tests unchanged.
- **Schema rollout:** nullable migration is backward-compatible; verify the
  migration and existing table-scoped permissions in the same diff.

No unresolved operator question remains. The user's scope ruling excludes the
ZIP.
