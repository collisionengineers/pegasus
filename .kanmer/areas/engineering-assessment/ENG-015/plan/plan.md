# Plan

Stacked on [[ENG-014]] (PR #527): branched from `task/eng-014-drop-manifest-indent-json`,
PR targets that branch, merges after it.

## Three corrections to the ticket's mechanics, found by reading the code

The lettered changes stand. Three of the *mechanisms* named in the ticket do not
survive contact with the code, so the plan records the route actually taken.

1. **`CaseEvaMapping.ImageBasedAssessment` is not "the EVA-facing literal".**
   `EvaAddressResolution.IsResolved` (`CaseEvaMapping.cs:40`) and
   `ResolveInspection` (`EvaHandoffStore.cs:942`) both compare it `Ordinal`
   against the value the *case* stores, which comes from
   `Ext18InspectionAddressPolicy`. Retyping it to `Image-based Assessment`
   would make every image-based case fail the resolution gate — a fail-closed
   hand-off for the exact cases the ticket is fixing. So the constant keeps its
   value as the gate, and a **separate** export literal is added and applied
   during address normalization.
2. **(c) cannot be done at extraction alone.** `InstructionFieldEngine`
   collapses every whitespace run (`InstructionFieldExtraction.cs:189`), and
   `CaseDataPolicy.Text` (`CaseDataOperations.cs:215-217`) collapses it again on
   save. A blank line cannot reach the case today. (c) therefore needs a named
   exemption for `AccidentCircumstances` as well as the extraction change. The
   AX sample's own `Accident Circumstances` contains `\n\n`, so multi-line is
   part of the target contract, not an invention.
3. **(f)'s bare `Date` also matches the *suffix* form.** The ticket names
   `Date of Accident:` (prefix). The engine's second regex
   (`:165`) matches ` Date:` mid-line, so `Accident Date:`, `Incident Date:`
   and `Inspection Date:` also yield an instruction-date candidate — and
   `AcceptsValue` does not filter those, because their values *are* valid
   dates. `GuardedPrefixes` is the existing mechanism for "this label is really
   another row"; it is extended rather than a new one invented.

## Steps

Each step names the existing code it reuses.

1. **(a) `Reference`.** In `EvaHandoffStore.BuildEvidence`, replace the
   hand-built `new(caseData.Identity.Reference, …)` with the existing
   `FromCaseField(caseData.Claim.Number, static value => value, includeSuggestions)`
   — the same helper the twelve surrounding fields already use.
2. **(d) `Mileage Unit`.** Same method: retarget the two existing formatters to
   `Miles`/`Km`. The confirmed-vehicle branch already switches on
   `VehicleMileageUnit`; the case-field branch's `ToLowerInvariant()` becomes
   the same mapping so the two branches cannot drift.
3. **(e) `Vehicle Model`.** The confirmed branch already composes make+model via
   the existing `VehicleModel(…)`/`Combine` pair. Reuse `Combine` for the
   case-field fallback so both branches produce one shape.
4. **(b) `Inspection Address`.** Add `ImageBasedAssessmentExportValue` next to
   the existing `ImageBasedAssessment` constant, and one
   `NormalizeInspectionAddress` helper. Hook it into the existing
   `NormalizedValue` name switch — which already special-cases `VRM`, so this
   is that convention, not a new seam. `MapOfflineReplay` calls it too, so an
   operator download and a hand-off keep the same shape.
   - commas → line breaks, then split; last line is line 6 when it is
     postcode-shaped, otherwise line 6 is blank; surplus body lines join into
     line 5 with spaces; always exactly 6 lines.
   - **No postcode re-spacing.** The research quotes the original as
     canonicalising to `OUTWARD INWARD`, but the known-good sample
     `Final Format Example 02.json` carries `CH490DJ` with no space. The sample
     is the JSON EVA is known to accept, and the operator's standard is "match
     the original json", so the samples win over the quoted algorithm. Flagged
     in the PR body.
5. **(f) `Instruction Date`.** Add `Date` to the label list with the ticket's
   `AcceptsValue`/`IsValidTyped` pair, and add the suffix-form words to the
   definition's `GuardedPrefixes`. `FieldDefinitions` currently *overwrites*
   every definition's `GuardedPrefixes` with `ThirdPartyRowPrefixes`; change
   that projection to union so a per-definition guard survives.
6. **(g) `Inspection date` precedence.** Add one member to `FieldDefinition`
   alongside the five it already carries, set it on `Inspection date`, and
   select `Max(FragmentRank)` instead of `Min` in
   `ResolveConflictingCandidates` when set. Rewrite the `:210-215` docstring so
   earliest-wins is stated as the default rather than as unconditional.
7. **(c) `Accident Circumstances`.** A post-`ExtractFields` step in the QDOS
   policy, mirroring the existing `DeriveVehicleFields` — the established
   convention for "adjust a field after the neutral engine has run". It reads
   the damage-area line from the raw fragments and appends
   `\n\nDamage Area: <text>`, or emits that alone when there is no prose. Plus
   the `CaseDataPolicy` exemption from step 2's note above.

## Tests

- (f) regression guard: a letter carrying both `Date of Accident:` and a bare
  `Date:` — incident date still correct, instruction date reads the bare row.
- (f) suffix guard: `Accident Date:` does not become the instruction date.
- (g): two fragments both carrying an inspection date — the later wins.
- (b): the image-based literal, and a real comma-separated address landing its
  postcode on line 6.
- Pinning tests, per the ticket: `VAT Status` stays blank for QDOS, and
  `Mileage` keeps the lookup value.

## Governing docs

`docs/frd/frd-07` specifies the 13-key *order*, not per-field value shapes, so
nothing in it becomes false. It is also being edited concurrently by
`task/docs-013-strike-eva-manifest`. I am not editing it here; if review judges
the 6-line address shape to be FRD-worthy behaviour it should land in that
ticket rather than conflict with it. Called out in the PR body.
