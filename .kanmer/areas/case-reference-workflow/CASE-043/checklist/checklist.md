# Checklist — CASE-043

- [ ] Step 1: extend the Core vehicle contract, validations, allow-list and
      projections for the ten fields; do not expand `CaseEditableData` until
      open question 3 is answered.
- [ ] Step 1b: retire `vehicle.year`, `vehicle.vin`, `vehicle.engine_cc` and
      `vehicle.fuel` from `AssessmentVocabulary` into `AssessmentCaseOwnedData`,
      repointing the report projection and the MCP tool projection.
- [ ] Step 2: extend `InstructionDraft`, the QDOS extraction definitions
      (`IsRequired: false`) and the provider-declared instruction contract, and
      map present values as extraction-backed facts.
- [ ] Step 3: extend `AddLookupSuggestionsAsync` to fuel, engine capacity,
      manufacture year and MOT expiry as attributed lookup suggestions; add the
      Core MOT-expiry selection policy with its abstention cases.
- [ ] Step 4: one additive migration with the instruction-draft columns, the
      replaced CaseData constraint, disposal of the four assessment rows before
      the narrowed assessment constraint, generated metadata and Worker-role
      grant evidence.
- [ ] Step 5: reconcile `docs/frd/frd-06-vehicle-and-engineering-evidence.md`
      with D49, including its Excluded clause on automatic external calls.
- [ ] Step 6: round-trip, single-owner, both-instruction-path, lookup
      provenance, save-retention and migration tests.
- [ ] Prove a save of an unrelated case field retains all ten values.
- [ ] Run `dotnet restore ./Pegasus.slnx --locked-mode`.
- [ ] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [ ] Run `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"`.
- [ ] Run `./scripts/Test-MigrationGrants.ps1`.
- [ ] Simplification pass run over the branch diff; findings and dispositions
      recorded in the plan.
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: CASE-043
