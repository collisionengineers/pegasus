# Open questions

## Parked (explicitly deferred)

- **Damage-area extent.** **Resolved 2026-08-24 — option 3.** The description is
  now read as a **block** running from the label to the next block, not a single
  line. This was the load-bearing part: the letters wrap the description
  mid-sentence across physical rows (the retained `QDOS_NX14AXY` output carries
  `...rear wheel arch is\ndamaged.`), and reading one row cut the sentence in
  half. `ap.QDOS26015` never exposed it because its description fits on one row.

  `Pre-existing Damage` **stays a stop marker** and does not travel inside
  `Accident Circumstances` — a deliberate divergence from the original, whose
  `"Damage Area || TP Vehicle"` rule carries it and whose retained sample shows
  it. Operator decision, taken against the three options and their exact output
  for both letter shapes. Rationale: pre-existing damage is a separate field.

  Blast radius confirmed nil: `DamageAreaStopRegex` is used only by
  `DamageArea`; INTK-025's circumstances rule has its own inline regex at `:363`.
  My earlier caution that the two were coupled was wrong.

- Mileage from the MOT lookup diverges from the original. **Resolved: intentional**,
  operator-confirmed. Pinned by a test.
- Postcode re-spacing. **Resolved**: the original canonicalises to
  `OUTWARD INWARD` only when `force_postcode_for_inspection_address` is set —
  true for 3 of 29 providers, **false for QDOS, SBL and AX**. Matching the
  samples (`CH490DJ`) is correct; the research doc over-generalised.
- The `Damage Area: ` label and blank-line separator are operator-specified.

## Review findings recorded, not fixed on this branch

Non-blocking. None changes exported values for the current corpus.

- **Missing-field marker never cleared.** `WithLabelledDamageArea` fills
  `Accident circumstances` but adds no name to the `missingFields` filter, so for
  the very letter shape (c) exists to fix, the intake receipt lists the field as
  missing while the draft carries a value. Its own docstring cites
  `DeriveVehicleFields` as the convention it follows.
- **Over-limit combined value nulls the field.** `TypedString` returns null above
  its limit rather than truncating. Now more reachable than when first raised:
  the block form makes a long value likelier than the single-line form did.
- **The synthetic candidate hard-codes `PdfContent`.** `DamageArea` returns a
  bare string, discarding the fragment, so provenance can claim a PDF when the
  source was an email body.
- **An unparseable mileage unit passes through verbatim** — the case editor is
  free text and this repo's own label for kilometres is `km`, exporting as `km`.
- **A postcode-only address misses line six** — the `parts.Length > 1` guard.
- **(e) has no test for the branch it changed.** The new assertion is fed by the
  confirmed vehicle record, which already composed make+model.
- **Both pinning tests bite one layer below their intent** — they pin
  `CaseEvaMapping`; a future "fix" would be made upstream in `BuildEvidence`.
- **`MappingVersion` stays 1.** Acceptance is configuration-driven, so bumping
  fail-closes the hand-off everywhere until each config is updated. An
  operator/deployment decision spanning ENG-014 and ENG-015 — its own ticket.
- **The six-line address shape has no FRD home.** [[DOCS-013]] scoped only the
  `Reference` semantics.
- **`CreateOfflineReplay`'s naming parameter should become required** once
  ENG-014 lands, so a third caller cannot silently reproduce the `EVA-1.zip`
  collision this branch fixed.
