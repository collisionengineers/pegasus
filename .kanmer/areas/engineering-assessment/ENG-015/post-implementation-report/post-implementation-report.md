# Post-implementation report

Branch `task/eng-015-eva-field-values`, stacked on [[ENG-014]]
(`task/eng-014-drop-manifest-indent-json`, PR #527) and targeting it. It must
merge after #527.

## What each lettered change became

| | Where | What |
| --- | --- | --- |
| a | `EvaHandoffStore.BuildEvidence` | `Reference` now reads `caseData.Claim.Number` through the same `FromCaseField` the twelve neighbouring fields use; the hand-built case-reference value is gone. |
| b | `CaseEvaMapping` | New `NormalizeInspectionAddress`, hooked into the existing `NormalizedValue` name switch beside `VRM`. Six lines always; commas split; surplus body joins line five; the image-based literal becomes EVA's own `Image-based Assessment`. `MapOfflineReplay` uses it too, so an operator download and a hand-off cannot differ. |
| c | `QdosInstructionExtractionPolicy` + `CaseDataPolicy` | `WithLabelledDamageArea` runs after `ExtractFields`, as `DeriveVehicleFields` does, and appends `\n\nDamage Area: <text>` — or emits it alone when the letter has no prose. `CaseDataPolicy.Paragraphs` lets that blank line survive being saved. |
| d | `EvaHandoffStore` | One `MileageUnit` helper owns the `Miles`/`Km` vocabulary for both the confirmed-vehicle and case-field branches. |
| e | `EvaHandoffStore` | `VehicleModel` now owns make+model for both sources through a shared `Compose`; the fallback no longer reads the model alone. |
| f | `QdosInstructionExtractionPolicy` | Bare `Date` added last in the label list, with `AcceptsValue` and `IsValidTyped`, plus its own `GuardedPrefixes`. |
| g | `InstructionFieldExtraction` | One new `FieldDefinition` member, `PrefersLatestFragment`, set on `Inspection date` only; `ResolveConflictingCandidates` selects `Max(FragmentRank)` when set. The `:210-215` docstring no longer claims earliest-wins is unconditional. |

## Three route corrections

The changes stand as decided; three of the ticket's stated *mechanisms* did not
survive contact with the code. Detail is in the plan; in short:

1. `CaseEvaMapping.ImageBasedAssessment` is a **gate**, compared `Ordinal`
   against the value intake stores, not the EVA-facing literal. Retyping it
   would have failed the hand-off closed for every image-based case. A separate
   export constant was added instead.
2. (c) needed a `CaseDataPolicy` exemption as well as an extraction change —
   both the field engine and the case normalizer collapsed every whitespace
   run, so a blank line could not reach the case at all.
3. (f)'s bare `Date` matches the **suffix** form too (`Accident Date:`), whose
   value is a valid date and so passes `AcceptsValue`. `GuardedPrefixes`
   handles it; the QDOS projection now unions per-field guards with the
   third-party guard instead of overwriting them.

## One defect introduced and fixed

(a) silently renamed the download, because the archive is named from the
bundle's `Reference` field and `SafeFileComponent` truncates at the last path
separator: `AKH//47743/1` would have produced `EVA-1.zip` for every such case.
`CreateOfflineReplay` now takes the naming reference, and both production
callers pass the Pegasus case reference. Caught by the existing
`BundleRevisionProxyAndDownloadCommandAreAtomicReplaySafeAndIntegrityChecked`,
and pinned by a new contract test.

This is the only place I touched `EvaBundleSchema.cs` — an added optional
parameter for naming, not packaging or serializer options.

## Where the samples beat the research

The research doc quotes the original as canonicalising postcodes to
`OUTWARD INWARD`. `Final Format Example 02.json` carries `CH490DJ`, unspaced.
The samples are the JSON EVA is known to accept, so **no postcode re-spacing is
applied** and the exported line is byte-identical to the sample. Flagged rather
than decided silently.

## Tests

New:

- bare `Date:` is the instruction date while `Date of Accident:` stays the
  incident date — the (f) regression guard the ticket required;
- `Accident Date:`/`Inspection Date:` do not become the instruction date;
- the appended report's inspection date wins over the instruction's;
- the earliest fragment still wins for every other field, proving (g) is scoped;
- a letter with only a damage area;
- the six-line address: the image-based literal, a real address with its
  postcode on line six, surplus lines joining line five, and no postcode;
- `VAT Status` blank for QDOS, pinned;
- suggested lookup `Mileage` reaching an export, pinned;
- the archive named by the case, not by the provider's reference;
- circumstances keeping their blank line while other fields still collapse.

Changed, because they asserted the old values for exactly what changed: the
two circumstances tests, and the boundary contract's inspection address.

## Results

- `dotnet build --configuration Release` — succeeded, 0 warnings, 0 errors.
- `Pegasus.Core.Tests` — 951 passed, 0 failed.
- `Pegasus.ArchitectureTests` — 99 passed, 0 failed.
- `Pegasus.IntegrationTests` — recorded in the ticket scratch and the PR checks.
- Line endings byte-audited after the `sed` edits: all touched files uniformly
  CRLF, no LF-only lines, no whole-file rewrites.

## Not done, deliberately

- **`docs/frd/frd-07` is not edited.** It fixes the 13-key order, not per-field
  value shapes, so nothing in it became false; and
  `task/docs-013-strike-eva-manifest` is editing that file concurrently. If
  review judges the six-line address shape to be FRD-worthy behaviour, it
  belongs there rather than in a conflicting edit here.
- **`Intake/Details.cshtml` renders circumstances in a `<dd>`**, so HTML
  collapses the new blank line visually. The stored value and the export are
  correct, and the case edit form is a `<textarea>` that round-trips it
  faithfully. Changing the display is a design decision I did not take.
- **(c) carries the ticket's flagged assumption** — blank-line separator, label
  exactly `Damage Area: `. Repeated in the PR body so the operator can correct
  it cheaply; it is a one-line change if wrong.

## PR and CI

**PR #534** — https://github.com/collisionengineers/pegasus/pull/534
`task/eng-015-eva-field-values` → `task/eng-014-drop-manifest-indent-json`.
Stacked on #527 and must merge after it. Not merged: this ticket stops at review.

CI green on every check:

| Check | Result |
| --- | --- |
| unit | pass (3m10s) |
| sql-integration (1) | pass (14m5s) |
| sql-integration (2) | pass (8m48s) |
| sql-integration (3) | pass (10m40s) |
| sql-integration-coverage | pass (12s) |
| browser | pass (7m33s) |
| changes / documentation / local-development-scripts / reference-data | pass |
| infrastructure | skipped (not triggered by this diff) |

No stale merge-ref checkout hang; every job's `actions/checkout` completed.

Three commits: the implementation, the simplification pass, and the one export
contract assertion that (a) makes stale.
