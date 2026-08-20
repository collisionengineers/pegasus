# Plan — ENG-004 MOT-row pollution of make/model suggestions

## Root cause (verified in code + production)

`InstructionFieldEngine.FindCandidates` matches a label token (e.g. `Make`) anywhere on a line after any single whitespace and takes everything to end-of-line as the value. PdfPig flattens table rows into single lines, so a brake-test row in an appended bodyshop report yields `vehicle_make = "AUDI NSF : Footbrake : SATISFACTORY"` (production case QDOS26002, CaseDataFields suggestion rows, SourceLabel "attachment 7: Bodyshopreport706254-V1.pdf, page 1").

## Steps

1. **Red-first fixtures** in `QdosInstructionExtractionPolicyTests` (reuses the existing `Readable(params IntakeContentFragment[])` helper and policy-level test convention):
   - flattened brake-table lines (`Make AUDI NSF : Footbrake : SATISFACTORY`, `Model A4 OSR : Handbrake : SATISFACTORY`) as a `PdfContent` fragment alone → no make/model suggestion, never the MOT row;
   - the same table alongside a genuine instruction fragment (`Vehicle Make: Audi`, `Vehicle Model: A4`) → the instruction values win with no conflict;
   - segment-boundary truncation (`Vehicle Make: Audi | further column`) → `Audi`.
2. **Engine fix** in `InstructionFieldExtraction.cs` (all deterministic, no new abstractions — extends the existing `FieldDefinition` record and static engine):
   - *Label position:* the label must sit at a plausible label position — line start, or after a clear separator (`|`, `;`, tab, or a run of 2+ spaces) — replacing the `(?:^|\s)` any-whitespace prefix.
   - *Segment boundary:* after the existing leading-trim (which existing tests pin for `"Claim Number | X"`), truncate the value at the first column boundary: tab, `|`, 2+ consecutive spaces, or a whitespace-preceded `:` (the spaced-colon shape of flattened table cells; genuine label colons are attached to the label and already consumed by the label regex).
   - *Per-field validator hook:* `FieldDefinition` gains optional `AcceptsValue` predicate applied to a discovered candidate; a rejected candidate is dropped (never suggested). Sits beside the existing `NormalizeRegistration`/`ParseMileage`/`ParseDate` typed-value helpers, but at candidate level because the production defect is the *suggestion* row itself.
   - *Make/model validator:* `IsPlausibleVehicleMakeModel` — rejects wheel-position tokens (NSF/OSF/NSR/OSR), MOT test-result vocabulary (SATISFACTORY/ADVISORY/DANGEROUS/FOOTBRAKE/HANDBRAKE/PASS/FAIL/MOT, word-bounded), and any character outside a conservative make/model charset (letters incl. accents, digits, space, `- . ' & / ( ) +`) — which excludes `:`/`|` tabular residue. Deliberately no length cap: `ProcessIntakeTests.OverlongStringsAndInvalidRegistrationRemainFullCandidatesButTypedValuesAreNull` pins that overlong values stay visible as candidates while `TypedString(…, 100)` nulls the typed draft value.
3. **Wire** the validator onto the `Vehicle make` / `Vehicle model` `FieldDefinition` rows in `QdosInstructionExtractionPolicy`.
4. **Verify:** `dotnet build -c Release` (zero warnings) + focused `dotnet test` on `Pegasus.Core.Tests` filters `QdosInstructionExtractionPolicyTests|ProcessIntakeTests|InstructionDraftCompletenessTests|QdosCaseMatchPolicyTests`.

## Deliberately out of scope (owned by [[INTK-017]])

Section awareness / restricting label search to instruction-document fragments, conflict resolution (identical values, validated-beats-unvalidated), the missing VRM row, and single-space multi-field line segmentation. ENG-004 only guarantees an MOT/brake-table row can never be offered as make/model.

## Behaviour preserved

All currently-passing extraction fixtures keep their exact behaviour (verified by running the classes above): one-field-per-line labels at line start, leading-separator trim, next-line fallback, overlong-candidate retention, conflict on genuinely different values.

## Verification note on the ticket's first checkbox

Re-extracting QDOS26002's real document is a production action (the real PDF is not available to this task); the fixture reproduces the exact flattened line shape recorded in the production CaseDataFields suggestion rows. Prod re-extraction belongs to verify/closeout after merge.
