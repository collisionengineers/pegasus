# Checklist — CASE-026

- [x] Header "Search" + freshness (RefreshFields carry filters/page/selected) + Create Case primary.
- [x] Advanced grid fields: Case/PO or image reference, Registration, Claimant, Claim/provider reference, Principal, State (enum select, All states), Engineer, Received from, Received to, Origin; Search dark + Clear.
- [x] Old parameters (`case`, `receivedDate`, `instructionDate`, `kind`) still bound, applied, pager-preserved.
- [x] Vehicle images section renders image-initiated rows with named lifecycle states.
- [x] Results table columns: Case/PO + provider ref, Vehicle + make/model, Claimant, Principal, Type, State (D3 chip incl. "Closed · <outcome>"), Due.
- [x] Rows selectable: `tr[data-select-href]` + `data-select-id` + per-row `<template>`; `aria-selected` initial; pane body is `data-row-list`.
- [x] Selected Case pane: eyebrow type, h2, chip, Accident circumstances, fact grid Provider ref/Engineer/Due/Next action, Outstanding (n), Open Case, Copy Case/PO (hidden until script, outside the swapped region — plan P7).
- [x] `?selected=` renders server-side; default first row; NotFound when absent from the page.
- [x] Empty vs unavailable states distinct, settled sentences kept; 503 on failure.
- [x] No new CSS/JS file; no inline styles/scripts; labels via OperatorLabels; no explanatory copy.
- [x] Core/Infra build green (Release, 0 warnings); tests updated (build-compiled only — orchestrator runs the wave loop).
- [x] Simplification pass recorded in plan/plan.md (2026-08-28).

Orchestrator-side items still open (not runnable in this lane): browser
walk at 1580/1100/760, snapshot regen, test loop, `/Cases?query=` 301
re-check against the live app.
