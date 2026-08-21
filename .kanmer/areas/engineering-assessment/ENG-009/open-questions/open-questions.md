# Open questions — ENG-009

## Resolved

- [x] ENG-009 remains the UI bridge; ENG-008 owns the backend contract and Cazana HTTP call.
- [x] The UI reports initiation status only; it does not show or persist returned figures.
- [x] Provider activation is deferred to [[PLAT-022]] and requires exact operator consent.
- [x] Kilometre normalisation is owned by [[INTK-026]]; use (1 km = 0.6213711922 miles), nearest whole mile with midpoint values away from zero, and no legacy conversion.

## Parked (explicitly deferred)

- Cazana credential targets, sandbox/live approval, recovery, and deployment evidence — [[PLAT-022]] owns these before any activation.
- Cazana evidence persistence, Engineer acceptance/adjustment, and revaluation history — EXT-10/EXT-13 via the governing FRD.
