# Open questions — CASE-043

All three resolved by the controller on 2026-09-03 from rules already
recorded; the operator may veto any of them in review.

- [x] Should all ten CASE-043 fields be required for instruction/case
      completeness? **No — they are ordinary optional Case fields.** The
      product invariant fails closed only when processing, limits or principal
      identity are incomplete or ambiguous; a vehicle's colour, body,
      transmission or tax expiry is none of those, and no recorded decision
      asks for a new completeness gate. Making them required would block case
      creation on data the instruction often does not carry. A field absent
      from both the instruction and the lookup stays absent and is drawn
      absent, not disabled and not invented.

- [x] Must the automatic DVLA/DVSA lookup populate every listed field?
      **No — it populates only what the approved adapter actually returns.**
      "Dependencies are approvals": no new package, endpoint or provider may
      be added for this ticket. Fields the approved adapter cannot obtain
      (VIN, body, transmission, and any other it does not return) are filled
      by instruction extraction or stay absent. Never synthesise or guess a
      value to fill a field. The plan must state, per field, which of the two
      sources can supply it, verified against the adapter's real response
      shape rather than assumed.

- [x] Does CASE-043 also deliver the staff-editable path for the ten fields?
      **Yes.** "Done means wired": a record with no production caller and no
      operator surface is not delivered, and the epic's feature outcome
      requires every field the mockup shows to have a Core owner *and* a
      production caller. CASE-043 therefore expands `CaseEditableData` and
      every production save caller, taking the capacity-one
      `Pages/Cases/Shared/*` lock — it is already serial in wave 4, so the
      lock is available to it. Note `EfCaseDataStore.SetConfirmed` deletes a
      confirmed value when its parameter is null: the save path must
      distinguish "not submitted" from "cleared" for the new fields, and a
      test must prove a value survives an unrelated save.

      Coordination: no lane may remove an existing Engineer edit route for a
      field before CASE-043 provides the replacement. If [[ENG-035]] retires
      those four fields from the Assessment vocabulary in wave 1, its own PR
      keeps them editable until CASE-043 lands; this is recorded on ENG-035's
      plan for its reviewer.
