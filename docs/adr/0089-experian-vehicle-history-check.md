---
id: ADR-0089
status: accepted
date: 2026-11-06
supersedes: []
superseded_by: []
related_capabilities: [EXT-20]
related_frd: [FRD-06, FRD-16, FRD-10]
tags: [experian, vehicle-history, key-vault, custody]
---

# ADR-0089: Experian vehicle history check

## Status

Accepted on 6 November 2026. The operator asked for the Experian seam to be
connected: a **Run Experian check** button that reveals a vehicle's previous
total losses, recorded as the Vehicle history text the report prints
(rulings R741 to R749).
[FRD-06](../frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment)
owns what staff see and what the text says. This record owns how Experian is
called and where its account lives.

## Context

Experian has been the one named disabled seam since 28 August 2026 (design
README, "Absent versus disabled"). The legacy process checked Experian for
adverse history "such as being a previous total loss" and filled a field
"showing either no adverse history, or the results it found"
(`reference/reports/collision_engineers_admin_overview.md`). That field is
`narrative.history_check`, which the report already prints verbatim.

Experian's Velocity API
([schema](../external-component-documents/experian/experian-velocity-api-schema.yaml))
answers `POST /v2/vehicle/` with a vehicle and, on request, its provenance
blocks. It authenticates with an OAuth2 password grant: client credentials and
user credentials exchanged for a bearer token. The schema names only the
sandbox host, `sandbox-uk-api.experian.com`.

## Decision

**Ask for the condition block alone.** `ExperianVehicleHistoryProvider` sends
a Custom Search, `{"vrm":…,"data":{"condition":true}}`, with no template
(R741). It reads `vehicle.provenance.condition[]` and drops each record with a
`dateremoved` (R743). A record's category is its `losstype`, else its
`status`. Its cause is the schema's `causeofdamage` code in the schema's own
word, and its loss date is the calendar date Experian wrote. Its insurer is
`insurername` and its claim is `claimnumber`. Core's
`VehicleHistoryCheckNarrative` turns the records into the approved wording
(R742).

**Record through the assessment save.** `RunVehicleHistoryCheck` requires
casework, refuses a registration other than the current work's accepted one,
asks the provider, and writes the wording into `narrative.history_check`
through `SaveAssessment`. It is the same edit lease, version, operation key,
history and report staleness as a staff edit, under the actor who pressed.
The answer replaces the text (R744). A vehicle Experian does not hold
(its own 404 body with an `errors` object) writes nothing (R748). No schema
or data migration is involved: the field already exists.

**Sign in once per token.** The provider holds the bearer token in process
until a minute before `expires_in` and renews it once when Experian answers
`401`. The token request is a JSON body (`username`, `password`,
`client_id`, `client_secret`) with a `Grant_type: password` header, as
Experian's gateway takes it. If Experian's onboarding material names another
shape, that one method and its test change. Each call is bounded at 30 s.

**The account is deployment configuration.** The Web host reads
`Experian:BaseUri`, `Experian:TokenUri` and `Experian:Account:ClientId`,
`ClientSecret`, `Username` and `Password`.
- The four account values are held in Key Vault as `experian-client-id`,
  `experian-client-secret`, `experian-username` and `experian-password`.
  Each reaches the Web App as a versioned Key Vault reference
  (`EXPERIAN_*_SECRET_URI`) through its own secret-scoped grant.
- The two addresses are plain settings (`EXPERIAN_BASE_URI`,
  `EXPERIAN_TOKEN_URI`) and must be HTTPS on an `experian.com` host.
- All six are Production required settings (R749). They are read on each
  check, never at startup, so an unresolved reference makes the check
  unavailable rather than stopping the host.
- Neither the credentials, the token nor the registration reaches a URL or
  log. The registration travels in the body.

**One answer for every failure.** A refusal, a gateway 404, a malformed
answer, the network or a missing setting all answer the approved
"unavailable" sentence (R747). Each is logged as `experian.history.rejected`,
`forbidden`, `throttled`, `provider_failed`, `unreadable`, `transport` or
`configuration`, with `stage=token|vehicle`. Experian's own not-found answer
is logged as `experian.history.not_found`.

## Consequences

- The Vehicle history area offers Run Experian check where Experian is
  composed and the text is editable (R746). The Automation Actor has the same
  act as `vehicle_history_check` on `pegasus_case_action`.
- Hosts that do not run the Production profile compose no Experian unless
  `Features:LiveExperian` is set on a DevelopmentOffline host (the hosted
  test instance, against the sandbox). Without it they say "Experian is not
  connected".
- The release that first ships this creates the four secrets with
  secret-scoped grants to the Web identity and sets the six azd variables.
  The production base and token addresses come from Experian with the
  credentials.

## Links

- [FRD-06 — Vehicle data and MOT enrichment](../frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment)
- [ADR-0064 — Automation Actor staff casework parity](0064-automation-actor-staff-casework-parity.md)
- [ADR-0084 — Brego valuation account](0084-brego-valuation-account-and-valuation-report.md)
- [Experian Velocity API schema](../external-component-documents/experian/experian-velocity-api-schema.yaml)
