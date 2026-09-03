# Open questions — CASE-029

## Open

- [ ] Which vehicle fields carry a lookup chip? The mockup
  (`21-case-sections.js`, `04-fixtures.js`) offers chips for colour, fuel,
  engine capacity, first registration, tax expiry, MOT expiry, transmission
  and manufacture year in addition to make, model and mileage. The case
  record owns only registration, make, model, mileage and mileage unit
  (`Core/Cases/CaseDataContracts.cs` `CaseVehicleData`), and the persisted
  field-name allow-list is enforced by a database check constraint
  (`Infrastructure/Persistence/CaseDataEntities.cs` `CaseDataFieldNames`), so
  the extra fields have no Core owner and cannot be persisted as suggestions
  today. EPIC-012's feature outcome says every field the mockup shows has a
  Core owner, but no wave-3 lane owns `CaseVehicleData`. Does CASE-029 extend
  the case vehicle record (Core contract, field-name allow-list, migration,
  completeness implications) as part of this ticket, or does a separate
  ticket own that extension while CASE-029 ships chips for make, model and
  mileage only? Raised by the 2026-09-03 cross-model plan review; the plan
  currently assumes the narrow answer and marks the extension out of scope.

## Parked (explicitly deferred)
