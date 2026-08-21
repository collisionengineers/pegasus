# Proof — ENG-007 (verified on deployed release 16, 2026-08-21)

Type: visual + command-log. Deployment evidence bundle: [[DELIV-015]] proof.

- Deployed at release 16 (`4111ad29`, PR #495 squash `d21741fc`).
- Live production verification on case QDOS26006 (V2MTM): after the automatic vehicle lookup produced its observation (MERCEDES-BENZ, 2018, 1950 cc, DIESEL), the Assessment vehicle form prefilled **Make MERCEDES-BENZ, Year 2018, Engine size 1950, Fuel DIESEL** through the shared Case projection — the page constructor no longer takes `IVehicleEvidenceQueries` (one query fewer per GET) and precedence is saved → confirmed → extracted fact → lookup observation. Mileage stayed empty because the observation carried none (correct, no invented value).
- Case page itself shows Make/Model "Not recorded" (the observation is unaccepted external evidence and does not displace instruction evidence) — exactly the FRD-06 boundary the fix preserves.
- CI green on the PR after dev update; independent review (claude-code) recorded in [[DELIV-015]] research.
