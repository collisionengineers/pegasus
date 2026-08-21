# Research — ENG-009: Cazana valuation workbench action

## Question

How should the assessment workbench request a Cazana valuation without exposing vehicle data, creating a browser-to-provider path, or taking ownership of valuation evidence?

## Findings

- `ENG-009` is a UI ticket and is blocked by `ENG-008`, whose scope owns the worker/function contract, configuration boundary, response handling, and Cazana request.
- `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` already has a valuation section with static Cazana retail/trade placeholders; its page model supplies existing antiforgery, operation-key, role, redirect, and `TempData` patterns.
- The supplied `reference/cazana-api-spec.json` defines `GET /valuation/1.0`: it accepts `vrm`, `mileage`, and `date`; VIN, condition, confidence, and stocking-depreciation are optional and excluded by ticket scope. It returns market evidence, not an Engineer-selected value.
- The Cazana API key can be sent as a Bearer token, avoiding a secret in a URL. Azure inventory shows no Cazana environment setting or secret in the current Web Container App.
- `CaseDataProjection` contains the case vehicle registration, mileage/unit, and incident date. Cazana data selection must read these server-side; the browser should only submit the selected case identity.
- `docs/capabilities.md` records EXT-07, EXT-10, EXT-13, and UI-15 as Later/1.0.0. `docs/boundaries.md` requires provider contract, credentials, recovery, caller, and acceptance evidence before activation.
- User decisions: ENG-009 remains the UI bridge; it returns status only; a deferred provider-activation ticket is required; kilometre case data normalises to miles in INTK-026, with no legacy conversion.

## Implications

- ENG-009 must not introduce a Cazana adapter, secret, worker, result persistence, or guide-figure display.
- ENG-008 must first expose the Core command that accepts a case ID, actor, and operation identity; it alone validates case evidence and maps incident date to the provider request.
- `PLAT-022` and `INTK-026` now block ENG-008. The user-facing action cannot be implemented or shipped until ENG-008 lands with those dependencies satisfied.

## Open questions

- None for ENG-009. Provider activation and mileage normalisation own their separate accepted requirements.
