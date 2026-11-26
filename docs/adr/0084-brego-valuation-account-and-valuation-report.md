---
id: ADR-0084
status: accepted
date: 2026-10-26
supersedes: []
superseded_by: []
related_capabilities: [EXT-07, EXT-13]
related_frd: [FRD-24, FRD-16, FRD-10, FRD-05]
tags: [brego, valuation, key-vault, custody]
---

# ADR-0084: Brego valuation account and Valuation Report

## Status

Accepted on 26 October 2026. The operator asked for Brego (`ValuationSource.Brego`)
to be connected as a guide valuation source in `Pegasus.Web`, signing in with
Collision Engineers' dedicated Brego valuation account, populating the card's
Retail and Trade figures with Brego's `retail.high` and `trade.high`, filling
an empty Case VIN, and filing the **Brego Valuation Report** PDF on the Case in
the background.
[FRD-24](../frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)
owns what staff see. This record owns how the valuation and its Valuation Report
are fetched and where the account lives.

## Context

The Case page's Brego card already exists in the Valuation section guide grid,
and `FetchGuideValuation`, `GuideValuationReportScheduler`,
`FileGuideValuationReport`, and `IFillGlassVin` already serve Glass's
([ADR-0060](0060-glass-valuation-account-and-valuation-report.md)) and Cazana
([ADR-0079](0079-cazana-companion-account-and-companion-report.md)).

Brego offers both a direct `/v1` REST API (`https://api.brego.io/v1`) and the
tenant platform web application (`https://{slug}.brego.ai/platform`, backed by
`/platform/api/brego/...` and `/platform/api/pdf/{organizationId}`;
[notes](../external-component-documents/brego/brego.md)):
1. Resolves the account e-mail at `POST https://www.brego.ai/login/api/getAuthorizer`
   to the Cognito User Pool ID (`authorizer`) and Cognito App Client ID
   (`authorizerId`). Until November 2026 this answer also named the tenant
   `organizationId` and `slug`; Brego removed them, and from 19 November 2026
   both are read from the `organizationId` and `slug` claims of the ID token
   that step 2 issues. An ID token that names neither is `unreadable` at
   `stage=cognito_refresh`. The slug is also sent as `client` when a new
   `brego-session` is created.
2. Renews the account's 24-hour tokens at AWS Cognito
   (`https://cognito-idp.{region}.amazonaws.com/`) with
   `GetTokensFromRefreshToken`, from the refresh token stored when the account
   was linked (23 November 2026, [R1231](../rulings.md); see "The account is
   linked by its e-mailed sign-in link" below). Until then it signed in with a
   password through `USER_SRP_AUTH`; Brego now signs the account in by link
   only, and the password flow is removed. The ID token's `cognito:username`
   claim names the user the session cookies are keyed by. When an active 1-year `brego-session` cookie is already
   held in memory or in `authentication-ring/brego-session.json`, the fresh
   24-hour Cognito tokens are paired directly with that `brego-session` cookie
   without calling `device-approval/challenge` or `POST /login/api/brego/v1/sessions`
   (avoiding Brego's email OTP challenge on container recycles and daily token
   refreshes). Only when no `brego-session` cookie is present or when Brego
   answers `401 Unauthorized` does the client verify browser device approval at
   `POST /login/api/brego/users/me/device-approval/challenge` using a
   pre-approved `deviceId`, exchange the Cognito tokens at
   `POST /login/api/brego/v1/sessions` for a new 1-year `brego-session` cookie,
   and persist that cookie to `authentication-ring/brego-session.json`.
3. Looks up the vehicle by VRM at
   `GET /platform/api/brego/vehicles/vrm/{vrm}?countryCode=gb&expand=sources&expand=metadata`
   for `derivativeId`, `vin`, `manufacturerUrl`, `rangeUrl`, `year`, `color`,
   `registrationDate`, `previousOwners`, and the national
   `averageMileageMonthly` metadata.
4. Queries point-in-time valuations at
   `GET /platform/api/brego/vehicles/{vin|vrm}/{id}/valuations` for the target
   valuation date and whole-mile odometer reading, answering `items[0].retail`
   and `items[0].trade` (`low`, `average`, `high`).
5. Renders the official multi-page **Brego Valuation Report** PDF synchronously
   via `GET /platform/api/brego/vehicles/derivativeId/{derivativeId}/option-configuration?countryCode=gb`
   (selecting default factory options effective on the vehicle's registration
   date) and `POST /platform/api/pdf/{organizationId}` with
   `featuredMonthNav: "high"` and `showPdfReportImages: true`. When a vehicle's
   historical dealer advert image URL returns HTTP 404 during Brego's Headless
   Chrome render, the resulting Skia/PDF contains a 14×16 broken-image
   placeholder XObject on Pages 1 and 2; Brego serves studio stock images for
   any derivative at `GET https://assets.brego.io/vehicle/images/stock/gb/{derivativeId}/?year={year}&color={color}&view={front|side}`.

## Decision

**Connect Brego through the platform BFF session flow.** `BregoGuideValuationProvider`
signs in with Collision Engineers' dedicated Brego valuation account, reuses the
active `BregoSession` across calls while valid (refreshing once on `401`),
looks up the Case's accepted registration, queries a point-in-time valuation at
its accepted mileage in whole miles, and answers `items[0].retail.high` as the
card's Retail and `items[0].trade.high` as the card's Trade.

**The account is deployment configuration.** The Web host reads
`Brego:ValuationAccount:Email`, held in Key Vault as `brego-valuation-email`
and delivered to the Web App as a versioned Key Vault reference
(`BREGO_VALUATION_EMAIL_SECRET_URI`) read through its own secret-scoped grant
to the Web identity. It is a Production required setting, read on each
valuation rather than when the host or a Case page is built. From 23 November
2026 ([R1231](../rulings.md)) no host reads a Brego password; the Web App's
`Brego__ValuationAccount__Password` setting is still deployed until a later
release removes it, because the release before this one stops at startup
without it. Neither the refresh token nor the Cognito tokens or session
cookies ever reach a URL or log. Amended 26 November 2026 (R1288): the
account is no longer deployment configuration. Its e-mail address and its
stored sign-in (the refresh token and the `brego-session` cookie) are held in
the database, protected with the Web host's key ring, and the account is
replaced by an Administrator
([ADR-0091](0091-integration-logins-in-the-database.md)); a renewed refresh
token or a new cookie is written back there, not to
`authentication-ring/brego-session.json`. The setting and that file are read
once by that release's import and removed in the next. A session held in
memory is reused only while the account is the one it was signed in for.

**The account is linked by its e-mailed sign-in link** (23 November 2026,
[R1231](../rulings.md)). Brego's login page signs an account in by Cognito
custom authentication: one exchange asks for the link (`ANSWER` `request`),
and a later, separate exchange redeems the token in the link's fragment
(`#magic-link=`). The second needs nothing from the first, so the link can be
redeemed on another machine than asked for it; a live test on 23 November
proved that, and that `GetTokensFromRefreshToken` then renews the tokens.
`scripts/Connect-BregoAccount.ps1` does both exchanges from a workstation with
a person at the account's mailbox, has the host's device approved, mints the
`brego-session`, and writes `authentication-ring/brego-session.json` as
`{ "email", "bregoSession", "refreshToken" }`. A host with no stored refresh
token answers the valuation unavailable at `stage=sign_in` without calling
Brego; one whose refresh token Cognito refuses does so at
`stage=cognito_refresh`, and the account is linked again
([runbook](../runbook.md#linking-the-brego-valuation-account)). A host reads
the store before its own memory at each sign-in, so a link made while it runs
is used without a restart; from 26 November 2026 (R1288) the store is the
account's row in the database. There is no screen for the link yet.

**The month is a date.** Brego values on a day: the current London month is
valued today, an earlier month on its last day, and a later month today and
answered as the current month, matching Cazana ([ADR-0079](0079-cazana-companion-account-and-companion-report.md)).

**File the Brego Valuation Report in the background with broken-image repair.**
Once the figures have answered, the quote's `BregoValuationReport` (identity
`brego-valuation:{reportId}`) runs on the Web host's provider work queue: it
loads the derivative's factory option configuration, calls
`POST /platform/api/pdf/{organizationId}` with `featuredMonthNav: "high"` and
`showPdfReportImages: true`, repairs any 14×16 broken listing image placeholders
on Pages 1 and 2 in place with Brego's `view=front` and `view=side` studio
stock images (`BregoPdfImageRepair`), and files the PDF on the Case's Documents
as `Brego valuation {registration} {yyyy-MM}.pdf` (`ValuationEvidence`,
`Generated`) once `FileGuideValuationReport` has verified the `%PDF-` header and
that the extracted text names the Case registration. If stock image fetching
fails during repair, the unmodified PDF from Brego is returned so report filing
still succeeds.

**Fill an empty VIN.** When the lookup names an unmasked 17-character VIN, the
quote carries it (`GlassVinFillPolicy.Carried`) and `FetchGuideValuation` fills
the work's VIN only when the work holds none, through `IFillGlassVin`.

**Failure codes and not-valued answer.** A 404 at `lookup` or `valuation`
answers `NoVehicleData` (`brego.valuation.not_found`). Every other failure
answers the card's "unavailable" sentence and logs `brego.valuation.rejected`,
`forbidden`, `throttled`, `provider_failed`, `unreadable`, `device_approval`,
`transport` or `mileage_unit`, with
`stage=<mileage|authorizer|cognito_init|cognito_verify|device_approval|session|lookup|valuation>`.
The `configuration` failure (a missing or unresolved account setting) names the
setting's key instead.

## Consequences

- Glass's, Brego and Cazana are the connected guide sources in `Pegasus.Web`.
- Creating `brego-valuation-email` and `brego-valuation-password` in Key Vault
  with secret-scoped grants to the Web identity and setting the two azd
  variables are release steps.
- Linking the account is a person's step, at a workstation, with the account's
  mailbox open: once, and again whenever Brego ends the sign-in. Until it is
  linked every Brego valuation is unavailable (23 November 2026,
  [R1231](../rulings.md)).
- Production and the hosted test instance (`Features:LiveGuideValuation`,
  [R709](../rulings.md)) compose the Brego provider; any other host does not.
  The test instance keeps its `brego-session` cookie in its own Azurite
  `authentication-ring/brego-session.json`, seeded with production's cookie, so
  it never signs in through device approval ([R933](../rulings.md), 11 November 2026).
  From 26 November 2026 (R1288) it keeps them in its own database, imported
  once from that file, and a production copy keeps the instance's own
  ([ADR-0091](0091-integration-logins-in-the-database.md)).

## Links

- [FRD-24 — Valuation sources](../frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)
- [ADR-0060 — Glass's valuation](0060-glass-valuation-account-and-valuation-report.md)
- [ADR-0079 — Cazana Companion valuation](0079-cazana-companion-account-and-companion-report.md)
- [Brego notes](../external-component-documents/brego/brego.md)
