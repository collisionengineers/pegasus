---
id: ADR-0079
status: accepted
date: 2026-10-21
supersedes: [ADR-0066]
superseded_by: []
related_capabilities: [EXT-07, EXT-13]
related_frd: [FRD-24, FRD-16, FRD-10]
tags: [cazana, valuation, key-vault, custody]
---

# ADR-0079: Cazana Companion account and Companion Report

## Status

Accepted on 21 October 2026. Supersedes
[ADR-0066](0066-cazana-valuation-through-a-key-vault-held-api-key.md). The
operator asked for Cazana's Get valuation to sign in to Cazana Companion, file
the **Companion Report** PDF on the Case in the background and fill an empty
VIN from Companion's lookup (21 October 2026; issue #1478).
[FRD-24](../frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)
owns what staff see. This record owns how the valuation and its Companion
Report are fetched and where the account lives.

## Context

Under [ADR-0066](0066-cazana-valuation-through-a-key-vault-held-api-key.md),
Get valuation called `GET /valuation/1.0` with the API key `Cazana:ApiKey`.
That endpoint answers retail and trade figures, masks the VIN, and offers no
PDF report; `POST /trade/auth` (Embedded Companion SSO) is disallowed on the
account (`static: 0`).

Cazana Companion (`https://trade.cazana.com`, backed by
`https://api.cazana.com`; [notes](../external-component-documents/cazana/cazana-companion-and-api-notes.md)):
1. Exchanges the portal account's e-mail and password at
   `POST /guest/onboard/branch` (with `X-ENVIRONMENT: production`,
   `X-CLIENT: cazana`, and the portal's `X-CZN` header) for an onboarding
   token and the user's branches, then confirms the active branch at
   `POST /guest/onboard/confirm` for a Bearer JWT.
2. Creates one metered lookup at `POST /app/companion` with `vrm`, `date`
   (`yyyy-MM-dd`), `mileage` and `options.forceEstimate: false`, answering
   `uuid`.
3. Reads `GET /app/companion/{uuid}` (unmetered) for `response.valuation`
   (`retail`, `remarket` as the trade value, and the unmasked 17-character
   `vin`), `specs` and `listing`.
4. Builds the **Companion Report** client-side from nine Mustache templates in
   `chunk-027a409e.57eb8fb5.js` (`companion-report`, `header`,
   `market-overview`, `vehicle-information-card`, `vehicle-overview-card`,
   `vehicle-specification`, `vehicle-timeline`,
   `vehicle-advert-full-details` and `recent-listing`), populated from the
   lookup, `GET /app/companion/{uuid}/timeline`,
   `POST /app/listings/similar` (using `valuation.cid`) and
   `GET /cz/image/2.0?vrm=…`, then posts the Base64-encoded UTF-8 HTML to
   `POST /app/pdf/create` to receive the PDF bytes.

The background report filing pipeline (`GuideValuationReportScheduler` and
`FileGuideValuationReport`) and the empty-VIN fill (`IFillGlassVin`) already
exist for Glass's ([ADR-0060](0060-glass-valuation-account-and-valuation-report.md))
and are source-agnostic.

## Decision

**Cut over to the Companion account.** Get valuation signs in with
Collision Engineers' Cazana Companion account and runs one metered
`POST /app/companion` lookup per press. Retail (`valuation.retail`) and Trade
(`valuation.remarket`) come from that lookup; `Cazana:ApiKey` and its wiring
are removed (operator, 21 October 2026).

**The account is deployment configuration.** The Web host reads
`Cazana:CompanionAccount:Email` and `Cazana:CompanionAccount:Password`, held
in Key Vault as `cazana-companion-email` and `cazana-companion-password` and
delivered to the Web App as versioned Key Vault references
(`CAZANA_COMPANION_EMAIL_SECRET_URI` and
`CAZANA_COMPANION_PASSWORD_SECRET_URI`), each read through its own
secret-scoped grant to the Web identity. Both are Production required
settings, read on each valuation rather than when the host or a Case page is
built. Neither the credentials nor the JWT ever reaches a URL or log.

**The month is a date.** Cazana values on a day: the current London month is
valued today, an earlier month on its last day, and a later month today and
answered as the current month (operator, 9 October 2026).

**File the Companion Report in the background, matching Companion.** Once the
figures have answered, the quote's `CazanaCompanionReport` (identity
`cazana-companion:{uuid}`, holding the signed-in session and lookup response)
runs on the Web host's provider work queue: it reads the timeline, comparable
listings and stock image, renders Cazana's own nine Mustache templates (copied
verbatim from `chunk-027a409e.57eb8fb5.js` as embedded resources) with
Companion's own data mapping, calls `POST /app/pdf/create`, and files the PDF
on the Case's Documents as `Cazana valuation {registration} {yyyy-MM}.pdf`
once `FileGuideValuationReport` has verified the `%PDF-` header and that the
extracted text names the Case registration. Private value is omitted
(`privateValuation: 0` on the account), and Companion's own quirks (such as
reading `valuation.ocn.onTheRoadPrice` for Original cost new) are kept as
Companion prints them (operator, 21 October 2026). A report failure is logged
and leaves the card's figures standing.

**Fill an empty VIN.** When the lookup names an unmasked 17-character VIN, the
quote carries it and `FetchGuideValuation` fills the work's VIN only when the
work holds none, through `IFillGlassVin` (operator, 21 October 2026).

**Failure codes and not-valued answer.** A 404 at `lookup` or `read` answers
`NoVehicleData` (`cazana.valuation.not_found`). Every other failure answers
the card's "unavailable" sentence and logs `cazana.valuation.rejected`,
`forbidden`, `throttled`, `provider_failed`, `unreadable`, `branch` (no active
branch on the account), `transport` or `mileage_unit`, with
`stage=<sign_in|branch|confirm|lookup|read|mileage>`. The `configuration`
failure (a missing or unresolved account setting) logs no stage; it names the
setting's key instead.

## Consequences

- Each press is one metered Companion lookup; the figures, the VIN and the
  Companion Report all come from that one `uuid`.
- Creating `cazana-companion-email` and `cazana-companion-password` in Key
  Vault with secret-scoped grants to the Web identity, setting the two azd
  variables, and retiring `cazana-api-key` after cutover are release steps.
- Hosts that do not run the Production profile compose no Cazana provider.

## Links

- [FRD-24 — Valuation sources](../frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)
- [ADR-0060 — Glass's valuation](0060-glass-valuation-account-and-valuation-report.md)
- [ADR-0066 — Superseded API-key route](0066-cazana-valuation-through-a-key-vault-held-api-key.md)
- [Cazana Companion and API notes](../external-component-documents/cazana/cazana-companion-and-api-notes.md)
