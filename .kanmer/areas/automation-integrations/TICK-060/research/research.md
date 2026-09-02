# Research — TICK-060: provider Case/PO result lookup

## Question

How should a provider retrieve the Case/PO produced by its own API submission
without exposing internal processing detail or creating a general lookup
surface?

## Verified findings

- API-01 already owns the authenticated route
  `GET /api/provider/v1/submissions/{submissionId}`, the
  `IGetProviderSubmissionResult` Core port, and its production caller in
  `ProviderApiEndpoints`. API-03 extends that seam; it does not add another
  route, store, or Core query.
- `GetProviderSubmissionResult` already scopes a loaded submission to the
  authenticated credential's Principal before reading queued-intake and receipt
  state.
- `EfQueuedIntakeStatusQueries` and `IIntakeReceiptQueries` already provide
  the durable completion and active Case-link facts. A decision such as
  `case_created` is not Case-existence authority.
- The existing public response leaks internal status, decision, failure, time,
  and provider-reference details. No provider has called this surface, so the
  replacement needs no compatibility or deprecation path.
- API-01's authentication already permits a paused credential to read previous
  results and rejects a revoked or invalid credential.
- The existing Web application, Azure SQL state, rate limiter, and telemetry are
  sufficient. No result table, queue, blob container, webhook, API Management
  instance, dependency, or deployment unit is required.

## Settled public contract

- Unknown, random, or another Principal's submission: indistinguishable
  `404 Not Found`.
- Owned but unfinished submission: empty `202 Accepted`.
- Completed submission with an actual active Case link:
  `200 OK` with only `caseReference`.
- Failed work, or completed work without an active Case link:
  generic `422 Unprocessable Entity`.
- A paused credential may read; a revoked or invalid credential receives
  `401 Unauthorized`.

No public response includes processing states, decisions, failure codes,
provider references, timestamps, files, reports, Case detail, retry hints, or
search/listing capabilities.

## Implications

Replace the detailed public result record with a three-outcome Core result and
map it at the existing endpoint. Keep ownership policy in Core. If a small
persistence signature change can scope the initial submission read by Principal,
make that change in the existing store rather than adding a projection.

## Facts and assumptions

The route, ports, callers, persistence queries, feature state, and absence of
provider use were checked against merged `main`, FRD-09, capabilities, and the
current operations record. No unverified external-service or production-data
assumption is required for implementation.
