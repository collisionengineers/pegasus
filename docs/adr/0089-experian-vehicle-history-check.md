---
id: ADR-0089
status: accepted
date: 2026-11-06
supersedes: []
superseded_by: []
related_capabilities: [EXT-20, EXT-07, EXT-13]
related_frd: [FRD-06, FRD-16, FRD-10, FRD-24]
tags: [experian, vehicle-history, cap, valuation, key-vault, custody]
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

Amended 19 November 2026 (R1201 to R1205, R1207): the same API also values
CAP for the Case's CAP guide card, with the Experian account that holds CAP
valuations; see "CAP valuation is Experian's second act".
[FRD-24](../frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)
owns what staff see.

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
`dateremoved` (R743). An answer in which message 100 refuses ConditionRecords is unavailable, not
clean, and the log names Experian's message ids (R747; 18 November 2026,
after UAT answered message 100, "Permission to view ConditionRecords was
denied"). Experian omits the block for a vehicle with no records, so an answer
without the block and without that refusal is clean (amended 20 November
2026, R1217, after UAT answered SW15CWX, SC15ZXZ and MT73GGO with the vehicle
alone and OV08ESN with its record). Message 13 ("Plate Change") marks a plate
change on record, which the wording reports without asking for the
PlateChanges block (R1217). A record's category is its `losstype`, else its
`status`, beside its `theftindicator` and the calendar date of its `lossdate`
(R1218; cause, insurer and the claim number, which Experian sends as
"Redacted", are not used). Message 20 or `vehicle.status.imported` "NonEU"
is an import from outside the EU, with `summary.dateoffirstreginuk` and
`yearofmanufacture` (else `dateofmanufacture`), and message 29 is no DVLA
registration (R1218). Messages' `guidance` ("See condition details", "See the
additional DVLA data") points at other parts of Experian's answer and is never
written. Core's `VehicleHistoryCheckNarrative` turns the answer into the
approved wording (R742, R1218).

**Record through the assessment save.** `RunVehicleHistoryCheck` requires
casework, refuses a registration other than the current work's accepted one,
asks the provider, and writes the wording into `narrative.history_check`
through `SaveAssessment`. It is the same edit lease, version, operation key,
history and report staleness as a staff edit, under the actor who pressed.
The answer replaces the text (R744). A vehicle Experian does not hold
(its own 404 body with an `errors` object) writes nothing (R748). No schema
or data migration is involved: the field already exists.

**Sign in once per token.** `ExperianClient`, the one way either act calls
Experian, holds its account's bearer token in process
until a minute before `expires_in` and renews it once when Experian answers
`401`. The token request is a JSON body (`username`, `password`,
`client_id`, `client_secret`) with a `Grant_type: password` header, as
Experian's gateway takes it. If Experian's onboarding material names another
shape, that one method and its test change. Each call is bounded at 30 s.

**The accounts are deployment configuration.** The Web host reads
`Experian:BaseUri`, `Experian:TokenUri` and, for each of Experian's two
accounts (amended 20 November 2026, R1215), `ClientId`, `ClientSecret`,
`Username` and `Password` under `Experian:HistoryAccount` (condition records,
this check) and `Experian:CapAccount` (CAP valuations). The
[configuration reference](../engineering/configuration.md#experian-accounts)
names each account's users.
- The account values are held in Key Vault as `experian-history-client-id`,
  `-client-secret`, `-username` and `-password`, and the same four under
  `experian-cap-`. Each reaches the Web App as a versioned Key Vault
  reference (`EXPERIAN_HISTORY_*_SECRET_URI`, `EXPERIAN_CAP_*_SECRET_URI`)
  through its own secret-scoped grant.
- The two addresses are plain settings (`EXPERIAN_BASE_URI`,
  `EXPERIAN_TOKEN_URI`), shared by both accounts, and must be HTTPS on an
  `experian.com` host.
- All ten are Production required settings (R749). They are read on each
  check, never at startup, so an unresolved reference makes the check
  unavailable rather than stopping the host.
- Neither the credentials, the token nor the registration reaches a URL or
  log. The registration travels in the body.
- Amended 26 November 2026 (R1288): the two accounts are no longer deployment
  configuration. Each is held in the database, protected with the Web host's
  key ring, and replaced by an Administrator
  ([ADR-0091](0091-integration-logins-in-the-database.md)); a kept token is
  used only while the account is the one it was issued to. The eight account
  settings were read once by that release's import and are removed in the
  release after it, so nothing references their Key Vault secrets (#2177,
  #2163); the two addresses stay settings.

**One answer for every failure.** A refusal, a gateway 404, a malformed
answer, the network or a missing setting all answer the approved
"unavailable" sentence (R747). Each is logged as `experian.history.rejected`,
`forbidden`, `throttled`, `provider_failed`, `unreadable`, `transport` or
`configuration`, with `stage=token|vehicle`. Experian's own not-found answer
is logged as `experian.history.not_found`.

**CAP valuation is Experian's second act** (19 November 2026, R1201).
Experian issues its permissions per account, and has set up two (R1207):
`merceraapi` (production) and `merceraapiuat` (UAT) hold CAP valuations, and
a second account (`pattersonaapi`, UAT `pattersonaapiuat`) holds condition
records. CAP signs in with `Experian:CapAccount:*` and the history check with
`Experian:HistoryAccount:*` (R1215). Each act is composed with a client and an
account of its own (`AddExperianCapValuation`, `AddExperianVehicleHistoryCheck`),
each keeping its own token, and neither reads the other's account.
`ExperianCapValuationProvider` is the CAP guide source's provider behind
`FetchGuideValuation`, as Glass's, Cazana and Brego have theirs. It sends a
Custom Search, `{"vrm":…,"currentmileage":…,"data":{"cap":{"valuation":true}}}`,
with the Case's accepted mileage in whole miles, and reads
`vehicle.valuations.cap.valuation`: `retail` is the card's retail and `clean`
its trade (R1202). The request carries no valuation date, so every answer is
the current London month and every ask is the current month's ask (R1203).
The quote carries no report and no VIN (R1205). Experian's own not-found
answer is the card's "holds no data for its registration" notice (R1204). An
answer naming the vehicle without the block or without both figures is CAP
not valuing it (R1236): Experian's message 108 is a mileage too high, 109 one
too low, and 107 or anything else is CAP declining it, each the card's info
notice and logged as `experian.cap.not_valued` with its reason; a permission
refusal (message 100 without 107) is not. That refusal, an answer naming no
vehicle, a refused call, the network or a missing setting is the card's
unavailable notice, logged as `experian.cap.rejected`, `forbidden`,
`throttled`, `provider_failed`, `unreadable`, `transport`, `configuration` or
`mileage_unit`. Every log line carries Experian's message ids where it sent any. It needs no
setting, secret or grant beyond the ten above, and no migration: `Cap` has
been a stored valuation source since the baseline.

## Consequences

- The Vehicle history area offers Run Experian check where Experian is
  composed and the text is editable (R746). The Automation Actor has the same
  act as `vehicle_history_check` on `pegasus_case_action`.
- Hosts that do not run the Production profile compose no Experian unless
  `Features:LiveExperian` is set on a DevelopmentOffline host (the hosted
  test instance, against the sandbox). Without it they say "Experian is not
  connected", and their CAP card says CAP valuation is unavailable.
- The CAP card offers Get valuation wherever Experian is composed, and the
  Automation Actor has it as `pegasus_valuation_get` with source `Cap`.
- The release that first ships this creates the four secrets with
  secret-scoped grants to the Web identity and sets the six azd variables.
  The production base and token addresses come from Experian with the
  credentials. Amended 26 November 2026 (R1288): only the two address
  variables remain; the logins are entered on Administration > Integrations
  ([ADR-0091](0091-integration-logins-in-the-database.md)).

## Links

- [FRD-06 — Vehicle data and MOT enrichment](../frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment)
- [ADR-0064 — Automation Actor staff casework parity](0064-automation-actor-staff-casework-parity.md)
- [ADR-0084 — Brego valuation account](0084-brego-valuation-account-and-valuation-report.md)
- [Experian Velocity API schema](../external-component-documents/experian/experian-velocity-api-schema.yaml)
