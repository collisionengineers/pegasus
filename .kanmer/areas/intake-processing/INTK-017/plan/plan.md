# Plan — INTK-017 deterministic extraction coverage

## Dependency

Builds on [[ENG-004]] (PR #437, branch `task/eng-004-mot-row-pollution`, not yet merged). `task/intk-017-extraction-coverage` was created from `origin/dev` and fast-forwarded onto the ENG-004 commit; the orchestrator merges ENG-004 first, then this branch is rebased/merged with `origin/dev` before its own merge. Until then this PR's diff shows the ENG-004 commit too.

## Target field set (the ticket's "list the exact fields")

Extraction-fed case details, all deterministic, all via the existing 11 QDOS `FieldDefinition` rows and `CaseDataSnapshotFactory` (no schema change): **claimant_name, claim_number, vehicle_registration, vehicle_make, vehicle_model, vehicle_mileage (+ vehicle_mileage_unit), accident_circumstances, incident_date, instruction_date, inspection_date, inspection_address (+ derived inspection_mode)**. `work_provider_code` (mail-route fact) and `inspection_deadline` (case acceptance) already populate by other paths. **Excluded with reason:** `contact_name`, `contact_email_address`, `contact_phone_number`, `vat_status` — no extraction pathway exists (`InstructionDraft` is a persisted entity; adding them is a schema migration) and there is no verified evidence the QDOS instruction form carries labelled contact/VAT fields; they remain operator-entered. If the operator wants them extracted, that is a follow-up ticket with the real form as evidence.

## Why fields come back empty today (verified)

Extraction runs over every fragment (email body + all attachment pages). Any label that matches two distinct values anywhere — instruction form plus an appended report — nulls the field as conflicting. And the VRM never matched a label at all in QDOS26002.

## Steps (each names the code it reuses)

1. **Red-first fixtures** in `QdosInstructionExtractionPolicyTests` (reuses the existing `Readable(params IntakeContentFragment[])` helper and the multi-fragment convention from ENG-004's `InstructionFieldsWinOverAppendedMotTableWithoutConflict`):
   - same value repeated across fragments → suggested, no conflict (regression pin — already passes via `DistinctBy`);
   - differing values across fragments → earliest fragment wins, `HasConflict=false`, all candidates retained;
   - differing values where only one parses (mileage `unknown` vs `42,000`) → the parsing candidate wins regardless of order;
   - same-fragment distinct dates → still conflict (pins the existing `ProcessIntakeTests` behaviour at policy level);
   - no registration label but one distinct current-format VRM in the document → vehicle registration suggested; two distinct VRMs → still absent (fail-closed);
   - registration label synonyms (`Registration No`, `Reg No`, `Vehicle Reg`);
   - flattened multi-field single line (`Vehicle Make: Audi Vehicle Model: A4 Avant`) → both fields correct.
2. **Engine — conflict resolution** in `ExtractFields` (extends the existing candidate array with fragment rank; reuses `DistinctBy` dedupe):
   - identical values are already not a conflict (kept);
   - when >1 distinct value: first narrow to candidates whose value satisfies the definition's new optional `IsValidTyped` predicate when at least one does (validated beats unvalidated — reuses `ParseMileage`/`ParseDate` and a strict current-format VRM check beside them); if exactly one survives, it wins;
   - otherwise prefer the earliest fragment (document order = the reader's emission order, instruction material before appended reports): if the earliest contributing fragment has exactly one distinct value, it wins with `HasConflict=false` and all candidates listed (satisfies `AddSuggestion`'s winner-in-candidates contract);
   - same-fragment distinct values remain a genuine conflict (null + `HasConflict=true`), preserving the pinned date test.
   Full scoping of label search to only "the instruction fragment" was considered and rejected: the instruction document is not deterministically identifiable from provenance labels; ordered preference is deterministic and keeps later-document values visible as operator-reviewable candidates.
3. **Engine — value stops at a following field label**: truncate a labelled value at the earliest occurrence of another known definition label followed by `:`/`-` (reuses the `definitions` list already passed to `FindCandidates`, same shape as the existing `StartsWithKnownFieldLabel` next-line guard). Fixes single-space flattened multi-field lines.
4. **Engine — sole-VRM fallback** for `Vehicle registration` when no labelled candidate exists (follows the existing in-engine zero-candidate special case for `Instruction date`): scan all fragments for word-bounded current-format VRMs (`[A-Z]{2}[0-9]{2} ?[A-Z]{3}`, uppercase only); suggest only when exactly one distinct VRM appears in the whole document, else stay absent (fail-closed before case allocation, per product invariants). Candidate carries the fragment's source/label provenance; `NormalizeRegistration` (existing) still types it for the draft.
5. **Policy** (`QdosInstructionExtractionPolicy`): add registration label synonyms longest-first (`Vehicle Registration`, `Registration Number`, `Registration No`, `Vehicle Reg No`, `Vehicle Reg`, `Registration`, `Reg No`, `VRM`); wire `IsValidTyped` for registration (current-format check), mileage (`ParseMileage`), and the three date fields (`ParseDate`). No new labels for other fields — unverified spellings would be fabricated evidence; shape-based rules carry the coverage.
6. **Verify**: `dotnet build ./Pegasus.slnx -c Release` (0 warnings); focused `dotnet test tests/Pegasus.Core.Tests` (full Core suite is fast); focused integration filter `InstructionDraftWebTests` if the local environment allows.

## Acceptance conditions

- All new fixtures green; every currently-passing fixture unchanged.
- A document shaped like QDOS26002 (labelled instruction fragment + appended report/MOT fragments) yields suggestions for the target set instead of conflict-nulls, and a sole-VRM document yields vehicle_registration.
- No non-deterministic input (no AI, no config, no clock beyond the existing instruction-date default).

## Verification note on the ticket's first checkbox

Re-running extraction over the real QDOS26002 document is a production action after merge (verify stage); fixtures use realistic text shapes derived from the existing fixture corpus and the recorded production provenance labels, per the no-fabricated-domain-data rule.
