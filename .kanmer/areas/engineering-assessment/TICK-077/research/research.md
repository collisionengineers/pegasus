# Research — TICK-077 (EXT-04) Direct EVA API integration

## Activation

EXT-04 is allocated `Later / 0.7.0` and `docs/open-decisions.md:229` holds its
activation decision open with the recommended default "make no EVA call". The
operator directed activation on 2026-08-27, against the **EVA test environment
only**; live credentials are deferred to a blocked follow-up ticket, actioned
at their direct request only. That direction supersedes the recommended
default, and the open decision must be resolved in the same PR rather than left
contradicting the code.

## Premises verified by read-only check

| Premise | How checked | Result |
| --- | --- | --- |
| No EVA network client exists today | Read `src/Pegasus.Core/Eva/*`, `src/Pegasus.Infrastructure/Eva/*` | Confirmed. `LocalEvaHandoffProxy` is 38 lines, local-only, and its receipt asserts `ClaimsExternalDelivery: false`. |
| The 13-field mapping and image policy are reusable | Read `CaseEvaMapping.cs`, `EvaBundleSchema.cs` | Confirmed. `MapForOperatorExport` and `EvaHandoffPolicy.SelectEligibleImages` are pure Core and independent of the ZIP writer. |
| The export path already gates on Review and loads image bytes | Read `EvaHandoffStore.cs` | Confirmed. Review gate at line 72, re-checked under `UPDLOCK, HOLDLOCK` at 138-155; `LoadEligibleImagesAsync` at 271-375 is the exact query needed. |
| `EvaFirstHandoffProxies` cannot record an API delivery | Read `EvaHandoffModelConfiguration.cs:14-25` | Confirmed. `CK_EvaFirstHandoffProxies_NoDeliveryClaim` pins `ClaimsExternalDelivery = 0`. A real delivery needs its own table. |
| There is a durable outbox to hang automatic submission on | Read `ExternalWorkProcessing.cs` | Confirmed. Five kinds, lease/backoff/poison, operator retry surface. |
| There is a per-principal setting precedent | Read `PegasusDbContext.cs:404-440,1037-1053`, ADR-0018 | Confirmed. `Principals.InspectionMode` + check constraint + Core policy + migration seed. ADR-0018 is a scoped exception to ADR-0008 and deferred the post-creation edit. |
| There is no hook on entering Review | Grepped `CaseLifecycleState.Review` across `src/` | Confirmed. Three writers: `EfCaseDataStore`, `EfCaseWorkflowStore`, `EfQueuedCustodyProcessor`. No event bus, no MediatR. |
| A client-credentials adapter pattern exists to copy | Read `DvlaDvsaProductionAdapter.cs` | Confirmed. Options record with validating factory + host allow-list; `SemaphoreSlim` token cache with expiry margin; failure taxonomy with `Retryable`. |
| Secret wiring has exactly one route | Traced `.azure/pegasus-prod/.env` → `main.parameters.json` → `platform.bicep` → `Program.cs` | Confirmed, three hops, no other consumer. |

## The EVA contract

Read from `collisionsuite/active/connectors/evaconnector` — a TypeScript MCP
server against the EVA "Sentry" API, with its official reference PDF
(`docs/official_eva_reference/evaapidocs.pdf`, 99 pages, authoritative) and
recorded live traffic under `tests/testsubmits/`.

- One base URL, `https://sentry.evasoftware.co.uk/api/`, for test and live. The
  environment is the credential pair. The key swapover changes no URL.
- `POST /Connect/token`, form-urlencoded, `Client_Id` / `Client_Secret`
  (PascalCase, no `grant_type`). Not OAuth2. **`expires_in` is minutes.**
- `POST /Instruction/Inspection` is the case-submission endpoint. Images are
  inline base64 in `Files: [{ Name, Extension, Data }]` on the same request.
- Live responses are camelCase (`statusCode`/`message`/`id`) while the docs
  specify PascalCase. A 400 envelope can arrive inside an HTTP 200. A 500 can
  arrive as `text/plain`.
- Two identifiers return: the envelope `id` and a File Reference embedded in
  the message string.
- **No idempotency mechanism.** Fixtures show the same `ExternalRef` and claim
  number submitted repeatedly, each creating a new File Reference. Pegasus owns
  de-duplication.
- No published rate limits; the reference advises ≤5 concurrency, 50–200 ms
  spacing, backoff on 5xx, retry-once on 401.

### Assumed, not verified

- **That a live submission now succeeds with images.** The connector carries an
  8-case A/B reproduction pack (`tests/testsubmits/1706/`) in which every
  submission with a file returned HTTP 500 and every identical file-less
  payload returned 200. The operator states EVA has resolved this (2026-08-27).
  Not re-verified here: posting to `/Instruction/Inspection` is an external
  write and needs explicit approval for the exact target. One live test-
  environment submission is required before the ticket can claim delivery.
- The exact `RequestFrom` contact code for the test credentials
  (`COLLENGAPI` in the recorded traffic).

## Governing documents that contradict the change

Amended, not merely added to:

- **FRD-07** states the export "has no separate EVA activation or
  mapping-acceptance switch", and `docs/operations.md:462` records that an
  `EvaMappingAcceptance` check was deliberately removed. A per-principal toggle
  re-introduces an activation gate. FRD-07 also gains the once-per-case
  limitation.
- **`docs/boundaries.md:19`** excludes "network adapter or replacement
  workflow" outright.
- **`docs/open-decisions.md:229`** recommends making no EVA call.
- **`docs/capabilities.md`** EXT-04 moves off `Later / 0.7.0`.

FRD-07 also imposes the one hard behavioural requirement: *"External success,
rejection, partial or unknown outcomes must remain distinct."*

## Scope decisions taken with the operator (2026-08-27)

1. Two independent per-principal toggles, Manual and Automatic.
2. Toggles on the existing Principal page; PLAT-028 carries them through its
   redesign.
3. **Submission only, once per case.** EVA's update endpoints
   (`Claim/Update`, `Claim/LocationUpdate`, `Claim/AuthorityStatusUpdate`) are
   unsuitable for this use case. Later case changes are not reflected in EVA.
4. Automatic submits on reaching Review.
5. Images inline as base64 on the instruction.
6. One **Send to EVA** button on the case, opening a confirmation page offering
   the API submission or the existing export download.

## Related board items

- [[TICK-022]] EXT-03 — the shipped manual export this sits beside.
- [[TICK-015]] CASE-21 — the once-per-case first-send proxy.
- [[PLAT-028]] — redesigning the Principals surface; carries the toggles.
- [[TICK-078]], [[TICK-079]], [[TICK-080]] — replacing EVA's own functions,
  out of scope here.
