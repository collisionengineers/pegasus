# Plan — TICK-060: Return the provider's resulting Case/PO or fail

## Approach

Add one Core-owned query over existing durable intake state. It accepts the authenticated Principal and that Principal's opaque API-01 receipt—not an arbitrary Case/PO. It returns generic unfinished, success only with an actual active Case link, or terminal failure when work completes without creating/linking a Case. It creates no result store and returns no files or Case detail.

## Governing docs

- Modify FRD-09 to state the three outcomes, actual Case-link authority, indistinguishable unknown/foreign absence, and strict identifier-only response.
- ADR-0004 remains accepted. No new ADR is required.
- Report/file delivery remains a separate contract and is not absorbed here.

## Steps

1. Integrate API-01 receipt identity and the TICK-058 authentication boundary.
2. Add one Core query/result vocabulary: unfinished; linked Case/PO success; terminal failure.
3. Implement one no-tracking Azure SQL projection joining Principal ownership, staged/completed work, processed receipt, and actual active Case link. A `case_created` decision alone is insufficient.
4. Add the result route to the existing Web Container App using the shared provider wire contract. Return only immutable Case/PO on success; bounded failure otherwise; unknown/random/foreign receipts remain indistinguishable.
5. Omit files, reports, source material, general Case fields, internal state names, attempts, exception details, listing, and search.
6. Add application-level per-credential throttling consistent with API-01 and disclosure-safe Application Insights outcome/latency telemetry.
7. Add Core/SQL/Web/architecture tests, refresh current-state docs after deployment, and run simplification plus locked verification.

## Azure decision

Reuse the existing Container App and Azure SQL. Add no result database/table, queue, blob container, webhook service, APIM instance, or delivery channel.

## Verification

Seed unfinished, linked, completed-without-link, technical-failure, unknown, random, foreign, revoked, and disabled-route cases. Assert only an actual active Case link succeeds and every response contains neither files nor general Case information.

## Deferred

Exact wire status/error mappings follow TICK-058's approved contract. Webhooks, list/search, report delivery, and gateway services require separate evidence.
