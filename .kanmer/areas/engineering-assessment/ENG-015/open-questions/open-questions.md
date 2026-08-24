# Open questions

## Parked (explicitly deferred)

- Mileage from the MOT lookup diverges from the original extractor. **Resolved
  2026-08-24: intentional**, operator-confirmed. Pinned by a test.
- Postcode re-spacing. **Resolved 2026-08-24**: the original only canonicalises
  to `OUTWARD INWARD` when `force_postcode_for_inspection_address` is set, which
  is true for 3 of 29 providers and **false for QDOS, SBL and AX**. Matching the
  samples (`CH490DJ`, unspaced) is correct; the research doc over-generalised by
  reading the function without checking when it is reached.
- The `Damage Area: ` label and blank-line separator are operator-specified and
  flagged for cheap correction. Not a question, an assumption.

## To resolve

- [ ] **How much of the damage-area block should `Accident Circumstances` carry?**

  Found by reading the corpus after review, not by the review. The current
  implementation (`QdosInstructionExtractionPolicy.DamageArea`) takes **one
  line**: the inline remainder of the `Damage Area` row, or else the first
  non-empty line beneath it.

  The real `ap.QDOS26015` letter reads:

  ```
  Damage Area – Front: Moderate Grill damage, dents, scratches.

  Pre-existing Damage:
  ```

  So we emit `Damage Area: – Front: Moderate Grill damage, dents, scratches.`

  **The known-good output for `QDOS_NX14AXY` runs to four lines and includes the
  pre-existing-damage answer:**

  ```
  – Nearside: Moderate, Rear Nearside: Moderate: Nearside rear wheel arch is
  damaged. Nearside door is damaged. Nearside rear wheel is buckeld.
  Pre-existing Damage:
  No.
  ```

  That follows from the original's rule — `"accident_circumstances": {"method":
  "two_labels", "config": "Damage Area || TP Vehicle"}` — which captures
  everything between the two labels, wrapped lines and the pre-existing-damage
  block included, stopping only at the third-party rows.

  Ours stops at the first line, and `DamageAreaStopRegex` additionally lists
  `pre-existing damage` as a stop marker — so even the multi-line branch would
  cut before the block the original keeps.

  **Two readings, materially different output:**
  1. *Match the original.* Capture to the `TP Vehicle` boundary: multi-line, and
     `Pre-existing Damage` stops being a stop marker for this field. This is what
     "match the original json and the way that works" implies, and the sample is
     unambiguous.
  2. *Damage area only.* Keep one line; treat pre-existing damage as a separate
     concern the operator did not ask to carry.

  Reading 1 is the literal instruction and the evidence supports it. Not applied
  unilaterally because it materially changes exported content and interacts with
  the `pre-existing damage` stop marker that INTK-025's existing circumstances
  rule also uses (`:369`) — changing it there would be out of scope.

## Review findings recorded, not fixed on this branch

Non-blocking, from the ENG-015 review. Each is real; none changes exported values
for the current corpus.

- **Missing-field marker never cleared.** `WithLabelledDamageArea` fills
  `Accident circumstances` but contributes no name to the `missingFields` filter
  that `DeriveVehicleFields` uses, so for the very letter shape (c) exists to fix
  the intake receipt lists the field as missing while the draft carries a value.
  Its own docstring cites `DeriveVehicleFields` as the convention it follows.
- **Over-limit combined value nulls the field.** `TypedString` returns null above
  its limit rather than truncating, so appending `\n\nDamage Area: …` to prose
  near 2000 characters drops circumstances entirely from the draft.
- **The synthetic candidate hard-codes `PdfContent`.** `DamageArea` returns a
  bare string, discarding the fragment, so provenance can claim a PDF when the
  source was an email body.
- **An unparseable mileage unit passes through verbatim.** The case editor is
  free text and this repo's own operator label for kilometres is `km`, which
  would export as `km` rather than `Km`.
- **A postcode-only address misses line six** — the `parts.Length > 1` guard.
- **(e) has no test for the branch it changed.** The new assertion is fed by the
  confirmed vehicle record, which already composed make+model; the case-field
  fallback that (e) actually changed is unevidenced.
- **Both pinning tests bite one layer below their intent.** They pin
  `CaseEvaMapping`; the realistic future "fix" would be made upstream in
  `BuildEvidence`, where neither would fail.
- **`MappingVersion` stays 1.** Acceptance is configuration-driven, so bumping
  fail-closes the hand-off in every environment until each config is updated. An
  operator/deployment decision spanning ENG-014 and ENG-015 — worth its own
  ticket, not a silent bump or a silent pass.
- **The six-line address shape has no FRD home.** [[DOCS-013]] scoped only the
  `Reference` semantics. Required behaviour of a capability belongs in FRD-07.
- **`CreateOfflineReplay`'s naming parameter should become required** once
  ENG-014 lands — a third caller forgetting it silently reproduces the
  `EVA-1.zip` collision this branch fixed.
