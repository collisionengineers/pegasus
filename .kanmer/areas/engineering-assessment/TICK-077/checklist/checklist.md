# Checklist — TICK-077 (EXT-04)

## Core

- [x] `EvaApiContracts.cs` — outcome enum, result, payload, two ports
- [x] `CaseEvaApiMapping.cs` — reuses `CaseEvaMapping`, renames to EVA fields
- [x] `EvaSubmissionPolicy.cs` — toggles, required right per trigger,
      once-per-case, outcome table
- [x] `ExternalWorkKinds.SubmitCaseToEva` + arm + `EvaSubmissionRetryPolicy`
- [x] `ProcessQueuedEvaSubmission` + `ReconcileAutomaticEvaSubmissions`

## Infrastructure

- [x] Extract the shared eligible-image reader out of `EvaHandoffStore`
- [x] Extract the shared case-evidence reader as well (same reason)
- [x] `EvaApiOptions` with host allow-list and lazy resolution
- [x] `EvaApiTransport` — minutes-based token cache, 401 retry-once,
      case-insensitive envelope, `text/plain` tolerance
- [x] `EvaSubmissionStore` — Review gate, replay, dedupe, action history
- [x] `EvaSubmissions` entity + model configuration + filtered unique index
- [x] `EfEvaSubmissionWorkStore`, `EfAutomaticEvaSubmissionStore`,
      `EfEvaSubmissionQueries`, `EfEvaSubmissionModeStore`
- [x] Migration: table + the two `Principals` columns
- [x] Migration: Web and Worker role grants (same diff, with its Designer so
      EF actually discovers it)

## Per-principal toggles

- [x] `PrincipalEntity` columns and Core policy
- [x] Administration summary, create, replace, and the new edit operation
- [x] Razor controls on the Principal pages
- [x] ADR-0034 for the database-authored setting

## Web

- [x] `Details.cshtml` — Export button becomes Send to EVA
- [x] `Pages/Cases/Eva/Send.cshtml(.cs)` — confirmation page, two routes
- [x] API submission handler mirroring `Export.cshtml.cs`

## Composition

- [x] Infrastructure DI registration
- [x] Worker DI + the reconciliation sweep on the existing timer
- [x] `Eva:*` config in both composition roots, in the fail-fast list
- [x] `main.parameters.json` + `platform.bicep` secret wiring (bicep compiles)

## Tests

- [x] `EvaApiMappingTests` — field rename, Agent, note lines, address split
- [x] `EvaSubmissionPolicyTests` — four outcomes distinct, retry rule, toggles
- [x] `EvaApiTransportTests` — recorded-traffic fixtures incl. the `text/plain`
      500, the 400-inside-200 rejections, and the minutes-based token
- [x] `OrganizationAdministrationTests` — the in-place settings update
- [ ] `EvaSubmissionTests` — end-to-end through the store against LocalDB:
      Review gate, operation-key replay, and the once-per-case unique index
      actually refusing a second success. **Not yet written.** The rule is
      currently proved by policy unit tests and a database constraint that no
      test exercises, which is the weakest point in this diff.
- [x] Architecture tests green (100/100)

## Documents

- [x] FRD-07 amended — route, toggle, once-per-case limit, four outcomes
- [x] `capabilities.md`, `boundaries.md`, `open-decisions.md` (resolved)
- [x] `current-architecture.md`, `operations.md`, `runbook.md`
- [x] ADR-0034 + index row

## Delivery

- [x] Simplification pass run and recorded in the plan with dispositions
- [x] `dotnet restore --locked-mode` / `build Release`
- [x] Core 1041/1041 and Architecture 100/100 green
- [ ] Integration suite green (re-running after the eleven fixes)
- [x] [[ENG-019]] live-key follow-up created, blocked and linked
- [x] [[ENG-020]] inspection-date/mileage follow-up created and linked
- [ ] PR opened against `dev`
- [ ] One approved live test-environment submission captured as evidence
      (external write — needs the operator's explicit go-ahead)
