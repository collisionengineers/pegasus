# Post-implementation report — INTK-017

PR: https://github.com/collisionengineers/pegasus/pull/443 (base `dev`, branch `task/intk-017-extraction-coverage`, commit da86502f). Worktree `../pegasus-worktrees/intk-017`.

**Dependency:** builds on [[ENG-004]] PR #437 (branch fast-forwarded onto its commit from `origin/dev`). Merge #437 first; this PR's diff then reduces to the single INTK-017 commit. If dev moves, merge `origin/dev` into this branch before its merge.

## What changed

- `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs`
  - `ExtractFields` collects candidates with their fragment rank; multiple distinct values now resolve deterministically via `ResolveConflictingCandidates`: candidates satisfying the definition's new optional `IsValidTyped` beat unparsable ones; then the earliest fragment (document order — instruction material precedes appended reports/MOT tables) wins when unambiguous; distinct values inside one fragment remain a genuine conflict (null + `HasConflict=true`). Resolved fields keep all candidates listed with `HasConflict=false` (satisfies `CaseDataSnapshotFactory.AddSuggestion`'s winner-in-candidates contract).
  - A labelled value is truncated where the next known field label followed by an explicit `:`/`-` begins (`TruncateAtFollowingFieldLabel`); a mid-line label token now matches only with that explicit separator (bare tokens still need line start / column separator, preserving ENG-004's rule).
  - Sole-registration fallback for `Vehicle registration` (same in-engine zero-candidate convention as the `Instruction date` default): when no label matched, the document's only current-format-VRM-shaped value (`[A-Z]{2}[0-9]{2} ?[A-Z]{3}`, uppercase, word-bounded) is suggested with fragment provenance; more than one distinct VRM → withheld (fail-closed).
  - New `IsCurrentFormatRegistration` beside the other typed helpers.
- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` — registration label synonyms longest-first (`Vehicle Registration`, `Registration Number`, `Registration No`, `Vehicle Reg No`, `Vehicle Reg`, `Registration`, `Reg No`, `VRM`); `IsValidTyped` wiring for registration (current-format shape), mileage (`ParseMileage`), and the three date fields (`ParseDate`).
- `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs` — 10 new tests (8 red-first + 2 regression pins).

## Evidence

- Red first: 8 new fixtures failed before implementation; the 2 pins (identical-values-not-conflict, same-fragment date conflict) passed throughout.
- Green: policy class 20/20; full `Pegasus.Core.Tests` 706/706; focused integration `InstructionDraftWebTests` 5/5 (1 m 15 s, LocalDB); `dotnet build ./Pegasus.slnx -c Release` 0 warnings, 0 errors.
- Pinned behaviours preserved: overlong-candidate retention, `"Claim Number | X"` trim, next-line fallback, invalid-mileage raw suggestion, same-fragment date conflict, all ENG-004 MOT-exclusion fixtures.

## Target field set delivered (plan's exact list)

claimant_name, claim_number, vehicle_registration, vehicle_make, vehicle_model, vehicle_mileage (+ vehicle_mileage_unit), accident_circumstances, incident_date, instruction_date, inspection_date, inspection_address (+ derived inspection_mode). Excluded with reason (in plan): contact_name/contact_email_address/contact_phone_number/vat_status — no extraction pathway (`InstructionDraft` is a persisted schema) and no verified evidence the QDOS form carries them; follow-up ticket if the operator wants them.

## Ticket verification checklist state

- Deterministic rules covered by fixtures using realistic text shapes: DONE (no AI, no config, no fabricated domain data; shapes derive from the existing fixture corpus and the recorded production provenance labels).
- Wrong-value suggestions do not regress: DONE (ENG-004 fixtures green on this branch).
- Re-running extraction over the real QDOS26002 document: NOT possible pre-merge (production action; real PDF inaccessible to this task) — belongs to verify stage after release.

## Deliberately left out

- Hard scoping of label search to "the instruction fragment only" — the instruction document is not deterministically identifiable from provenance labels; ordered preference achieves the effect while keeping later-document values visible as reviewable candidates.
- Contact/VAT extraction (above). No schema, UI, or migration changes anywhere in this diff.
