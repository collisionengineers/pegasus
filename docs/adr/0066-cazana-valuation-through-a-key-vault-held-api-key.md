---
id: ADR-0066
status: accepted
date: 2026-10-09
supersedes: []
superseded_by: []
related_capabilities: [EXT-07, EXT-13]
related_frd: [FRD-24, FRD-16, FRD-10]
tags: [cazana, valuation, key-vault, custody]
---

# ADR-0066: Cazana valuation through a Key Vault-held API key

## Status

Accepted on 9 October 2026. The operator asked for Cazana to be connected as
a further guide valuation source, using the Cazana API key held in Infisical,
entered into Key Vault. [FRD-24](../frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)
owns what staff see. This record owns how the valuation is fetched and where
its key lives.

## Context

The Case page's Cazana card already existed with hand entry and the
"unavailable" notice, because no Cazana provider was connected. The guide
provider port, the card's Get valuation, the Case Save and
`pegasus_valuation_get` already served Glass's
([ADR-0060](0060-glass-valuation-account-and-valuation-report.md)): a source
is connected exactly when a provider for it is composed.

Cazana's public API ([supplied specification](../external-component-documents/cazana/cazana-api-spec.json))
answers `GET /valuation/1.0` for a VRM or VIN with retail, trade and dealer-type
values. It takes an optional mileage (estimated when absent, at most 500,000)
and an optional valuation date (today when absent). The key may be sent as a
`key` query parameter or as a Bearer token. A probe on 9 October 2026 showed
the supplied key is a Production key: `https://api.cazana.com` answered a
request without a VRM with 400 `vrm or vin is required`, and the UAT endpoint
answered 403.

## Decision

**The key is deployment configuration.** Get valuation sends the Cazana key
`Cazana:ApiKey`, held in Key Vault as `cazana-api-key` and handed to the Web
App only as a versioned Key Vault reference, read through its own
secret-scoped grant to the Web identity. It is a Production required setting.
The key is read on each valuation, never when the host or a Case page is
built, and it travels only in the `Authorization: Bearer` header, never in the
URL, because request URLs reach host logs and dependency telemetry.

**One request per press.** Get valuation asks
`https://api.cazana.com/valuation/1.0` for the Case's accepted registration
(upper case, without spaces) at its accepted mileage in whole miles. It
answers Cazana's `retail` as the card's retail and its `trade` as the card's
trade. Cazana values on a day, so the card's month becomes a date (operator,
9 October 2026): the current London month is valued today (no date is sent),
an earlier month is valued on its last day, and a later month is valued today
and answered as the current month. Nothing is retried or cached.

**Cazana offers no report and its valuation names no VIN.** Nothing is filed
on the Case and the Case VIN is untouched.

**Every failure is the card's existing notice, but one.** Whatever stops a
valuation — the key, the request, Cazana's refusal, throttling, a server
error, an unreadable answer or the network — the card answers the approved
"unavailable" sentence and the host log names the stage by its failure code
(`cazana.valuation.rejected`, `forbidden`, `throttled`, `provider_failed`,
`unreadable`, `transport`, `configuration` or `mileage_unit`, with Cazana's
own error words and never the key). When Cazana answers 404, it holds no data
for the registration and nothing is broken, so the card shows the operator's
sentence as an info notice instead (`cazana.valuation.not_found`; 9 October
2026). The provider port's not-valued answer carries its reason, so Glass's
vehicle-age answer, Glass's plate search without a type number (10 October
2026) and this one share one route to the card and to `pegasus_valuation_get`
(outcome `NoVehicleData`, worded with the source's name).

## Consequences

- Each press is one Cazana lookup against the account. A figure fetched and a
  figure typed are the same record once the Case Save records the card.
- The secret and its grant are release steps: Bicep carries the reference,
  the repository forbids a vault-wide grant, and the pre-provision check
  refuses an empty or malformed `CAZANA_API_KEY_SECRET_URI`.
- Hosts that do not run the Production profile compose no Cazana provider, so
  the card keeps its notice and hand entry there.
- Brego, Super CAP and CAP stay unconnected until each has its own adapter.

## Links

- [FRD-24 — Valuation sources](../frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)
- [ADR-0060 — Glass's valuation](0060-glass-valuation-account-and-valuation-report.md)
- [ADR-0064 — Automation Actor staff casework parity](0064-automation-actor-staff-casework-parity.md)
