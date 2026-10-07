---
id: ADR-0060
status: accepted
date: 2026-10-01
supersedes: []
superseded_by: []
related_capabilities: [EXT-07, EXT-13]
related_frd: [FRD-24, FRD-16]
tags: [glass, valuation, key-vault, custody, background-work]
---

# ADR-0060: Glass's valuation through a Key Vault-held account, with its report filed on the Case

## Status

Accepted on 1 October 2026. The operator asked for Glass's to be the first
guide valuation source that Pegasus fetches itself, signing in with a Glass's
account held in Key Vault, and for each valuation's PDF report to be stored in
Box. [FRD-24](../frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)
owns what staff see. This record owns how the valuation is fetched, where its
account lives and how its report is kept.

## Context

The Case page's guide cards already had a Get valuation button and a Core port
for a guide provider, but no provider was connected, so every card said the
source was unavailable. Pegasus's Glass's client already performed most of a
Market Value Assessor valuation for the repair estimate launch, and discarded
the figures.

The repair estimate signs in with each Engineer's own Glass's account,
protected per staff account in SQL ([ADR-0043](0043-per-engineer-vendor-credential-protection.md)).
A valuation belongs to the Case, not to whoever pressed the button, and
Collision Engineers holds one Glass's account for valuations.

The portal's own valuation, captured on 1 October 2026, signs in, looks the
registration up, reads the candidate and its valuation page, and then — the
account being in insurance mode — saves the vehicle to the account's stock
list. A stocked vehicle can then be printed as a PDF valuation report.

## Decision

**The valuation account is deployment configuration.** Get valuation signs in
with the Glass's valuation account: `Glass:ValuationAccount:Username` and
`Glass:ValuationAccount:Password`, held in Key Vault as
`glass-valuation-username` and `glass-valuation-password` and handed to the
Web App only as versioned Key Vault references, each read through its own
secret-scoped grant. Both are Production required settings. The account is
read on each valuation, never when the host or a Case page is built. The
repair estimate keeps each Engineer's own account; ADR-0043 is unchanged.

**The valuation follows the portal.** One Get valuation signs in, values the
Case's accepted registration and its accepted mileage in whole miles for the
card's month (the current month in London when none is entered), and answers
Retail Transacted as the card's retail and Glass's Trade as its trade. It then
saves the vehicle to the account's stock list in that month, as the portal does
after every valuation (operator, 1 October 2026). The figures are answered even
if the stock save fails; that valuation then has no report. The stocked
vehicle's details page names the VIN Glass's looked up from the registration,
and that VIN fills the Case's VIN only where the Case holds none (operator,
7 October 2026; [FRD-06](../frd/frd-06-vehicle-and-engineering-evidence.md)).
A VIN that cannot be read leaves the figures and the report as they are.

**Every failure is the card's existing notice.** Whatever stops a valuation —
the account, the registration, the provider or the network — the card answers
the approved "unavailable" sentence and the host log names the stage by its
failure code. No new operator copy is added (operator, 1 October 2026). The
one exception is Glass's answering that it does not value a vehicle of that
age (`glass.valuation.vehicle_age`, matched on "due to the age" in its
message). Nothing is broken, so the card shows the operator's own sentence as
an info notice instead (2 October 2026).

**The report is filed after the answer.** Each valuation's stocked vehicle is
printed with the account's "Vehicle Valuation Report – Glass's Values Only"
template and the PDF is retained on the Case through the existing Case artifact
custody, so it reaches Box like the repair estimate's own artifacts. The work
runs on the Web host's provider work queue
([ADR-0058](0058-glass-provider-work-in-the-web-host.md)) after the figures
have been answered, over the same signed-in session, which lives in that
process. It is keyed by the provider's own stock id, never by the Case form's
operation key, and it takes no Case version or edit lease, so the Engineer's
unsaved edit and its Save are untouched.

## Consequences

- Every press saves one vehicle to the Glass's stock list and files one PDF on
  the Case, named for the source, registration and month. That is what was
  asked for.
- Filing a document marks the Case's current report Stale, as any filed
  document does.
- The filed report must name the Case registration in its own text (operator,
  5 October 2026; issue 1032). A report that names another registration, or
  none, is not filed, and the host log carries only `registration=different`
  or `registration=absent`. Two presses' reports on the shared account can
  therefore share a file name at Glass's without effect: a wrong-vehicle file
  is refused, and no per-account serialisation is built.
- A host restart before the queued report work runs loses that valuation's
  report; the figures and the stock-list entry stand.
- The secrets and their grants are release steps: Bicep carries the references,
  the repository forbids a vault-wide grant, and no CI check proves a grant.
  The pre-provision check refuses an empty or malformed secret URI.
- The other four guide sources stay unconnected until each has its own
  adapter.

## Links

- [FRD-24 — Valuation sources](../frd/frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)
- [FRD-16 — Valuation](../frd/frd-16-case-record-workspace.md)
- [ADR-0043 — Per-Engineer vendor credential protection](0043-per-engineer-vendor-credential-protection.md)
- [ADR-0058 — Glass's provider work runs in the background of the Web host](0058-glass-provider-work-in-the-web-host.md)
