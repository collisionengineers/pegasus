# Accepted finding: EVA API preference and focused `0.1.0-alpha.1` JSON handoff

**Operator decision:** Accepted in narrowed form on 2026-07-24. The EVA API is the preferred integration route, but it is currently unavailable; focused `0.1.0-alpha.1` therefore exports structured JSON and stored images for manual import into EVA.

**Legacy sources dealt with:** `docs/reference/dealt-with/accepted/0005-eva-handoff/docs/adr/0005-eva-api-full-scope-test-environment.md` and its `docs/reference/dealt-with/accepted/0005-eva-handoff/README.md` bundle. Those historical targets are absent from this worktree and are retained as path labels, not live links.

This decision establishes product direction and the present release boundary. It does not select the predecessor Sentry routes or twelve-field schema as the `0.1.0-alpha.1` contract, authorise an EVA call, prove vendor availability, or establish a current implementation.

## Accepted finding

- Direct EVA API integration is the preferred route once EVA makes a usable integration available.
- The API is not available for focused `0.1.0-alpha.1`, so focused `0.1.0-alpha.1` provides an operator-approved structured JSON extract together with the stored images for staff to transfer into EVA manually.
- EVA remains authoritative for Engineer assignment, estimating, valuation, and report generation until an approved replacement slice exists.
- The focused `0.1.0-alpha.1` export is the current primary handoff. It may become the explicit fallback and recovery route after a later API integration is accepted and activated.

## Future EVA API activation boundary

A future direct integration must remain unavailable until all of the following are established:

- current vendor confirmation and usable test credentials for the intended EVA environment;
- correct routing for every required Collision Engineers principal code rather than a single-principal limitation;
- an accepted, versioned request/response contract and field mapping;
- parity against the operator-approved focused-`0.1.0-alpha.1` JSON-and-image handoff for the same case;
- deterministic image selection and ordering, idempotency, retry and duplicate-submission behavior;
- typed validation, authentication, authorization, error, unknown and recovery outcomes;
- separate test and production credentials held in the approved secret store; and
- an authenticated, version/lease-guarded caller that records actor, case revision and outcome without allowing an Infrastructure adapter to decide workflow.

Any vendor test or production call remains an external data mutation requiring exact target, data, credential and approval scope. A later direct EVA integration requires its own current Pegasus ADR and approval.

## Difference from the predecessor material

Legacy ADR-0005 correctly preserves manual handoff and requires vendor testing, multi-principal support and parity. Current Pegasus differs in these ways:

- The predecessor describes the API as an already selected Sentry path. Current Pegasus prefers the EVA API outcome but does not yet select a usable endpoint, route, payload or authentication contract.
- The predecessor treats schema-validated JSON as an API fallback. For focused `0.1.0-alpha.1`, JSON plus images is the primary handoff because the API is unavailable.
- The legacy `contracts/eva-payload.schema.json` and `docs/architecture/eva-field-model.md` paths are review evidence only. The current export plan still requires operator approval of the versioned mapping, image selection, readiness/release gate and error/recovery procedure.
- The old `docs/architecture/eva-sentry-api.md` path, routes, base64-image format, request digest, credentials and predecessor service are not adopted merely by accepting the preferred API direction.
- The predecessor `docs/tickets/done/TKT-126-eva-export-zip/TKT-126-eva-export-zip.md` and `docs/tickets/now/TKT-216-eva-sentry-route-body-contract/TKT-216-eva-sentry-route-body-contract.md` paths demonstrate old workflow and contract concerns but do not prove a Pegasus caller or accepted payload.

## Current architecture, plan and evidence state

The settled [questionnaire](../../history/product/project-discovery-questionnaire.md) and [remaining requirements](../../product/qdos-alpha-gap.md) now record the API as preferred once available and the focused-`0.1.0-alpha.1` JSON/image handoff as the current route. The [EVA delivery plan](../../history/plans/remainder-delivery/integrations/vehicle-data-and-eva-export.md) keeps field mapping, image selection, readiness and recovery behind operator approval.

The accepted [.NET modular-monolith architecture](../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md) provides an EVA export port in Infrastructure while Core owns case data, review and release policy. A future API changes the adapter, not the business-policy owner.

The current evidence state remains **Planned**. There is no current EVA adapter, export serializer, accepted mapping, credential, registered caller or proven API/test-environment integration. The legacy files do not change that state.

## Deferred-capability impact

Focused `0.1.0-alpha.1` must preserve stable case, principal, reference, field-provenance and image identities so a later EVA API adapter can use the same confirmed business data. It must not add dormant Sentry clients, credentials, routes, queues, feature flags or background jobs.

Direct EVA API use, bidirectional report/status exchange and eventual EVA replacement remain later slices. Availability from the vendor, an accepted contract, representative parity evidence, security/licence approval, a real caller and exact external-call authority activate the direct-integration work.
