# Open questions — CASE-029

## Resolved

- [x] Which vehicle fields carry a lookup chip? **Operator answer 2026-09-03:
  a separate feature ticket owns the record extension; CASE-029 ships chips
  for make, model and mileage only.** The new ticket is [[CASE-043]] "Extend
  the case vehicle record with the DVLA/MOT fields, populated from the
  instruction first and DVLA/DVSA on intake" (EPIC-012 + EPIC-011, refs
  frd-06 / frd-02 / frd-05), blocked by CASE-029 because it reuses the
  DVLA/MOT lookup port this ticket adds.

  The operator's standing rule for those fields: populate from the supplied
  instruction or data by extraction first, then call DVLA and DVSA
  automatically on intake to fill the rest. That is CASE-043's scope, not
  CASE-029's.

  CASE-029 therefore keeps its plan's narrow answer: the lookup runs and its
  results appear as chips only for the fields `CaseVehicleData` already owns
  (make, model, mileage). No Core contract change, no field-name allow-list
  change, no migration in this ticket.

## Parked (explicitly deferred)
