---
id: ADR-0063
status: accepted
date: 2026-10-06
supersedes: []
superseded_by: []
related_capabilities: [EXT-06, ENG-01]
related_frd: [FRD-25]
tags: [glass, repair-spec, session]
---

# ADR-0063: A Glass's estimate belongs to its repair spec, by its stock vehicle

## Status

Accepted on 6 October 2026. The operator ruled that the same repair spec is
resumed, that its return updates it in place, that there is no Resume button,
and that any staff member holding the Case edit may reopen it.
[FRD-25](../frd/frd-25-repair-estimates-imports-and-glasss-sessions.md#glasss-launch-and-return)
owns what staff see. This record owns where the estimate's identity is kept
and how a reopen and its return find it.

## Context

Pegasus could reopen a Glass's estimate only while the Pegasus session that
launched it was live. The estimate's identity, its stock vehicle and estimate
id, was kept on that session alone. Once the session ended (Save & Exit,
failed, expired or closed), the next launch made a new stock vehicle and a
new estimate, and every return landed as another repair spec. The account's
stock list collected one vehicle per launch.

The operator's captures of 6 October 2026 show how the portal resumes. It
opens the stock vehicle record and posts `start-ere` with `ere_id` 0, exactly
as for a first start, and the provider answers the estimate that vehicle
already holds. Stock record 33645233 answered estimate 1961565 on six starts
across two separate sign-ins, with its lines and totals intact. The browser
never sends an estimate id. Several stock records can share one registration,
so the registration does not identify the estimate; the stock record does.

Glass's keeps no reference back to Pegasus on the record: its claim-number and
free-text reference fields are empty for a vehicle Pegasus made.

## Decision

The stock vehicle is the estimate's durable identity, and it is recorded on
the repair spec.

- `CaseRepairSpecifications` carries the Glass's stock vehicle id, the
  estimate id that vehicle last answered, and the type number, placeholder
  flag, registration and mileage the vehicle was proved against. They are set
  together by the return that lands the spec, in the spec's own transaction,
  or not at all.
- A launch names the repair spec on the screen. When that spec carries a
  vehicle, the launch is a new session seeded with it. The session proves the
  vehicle and starts with `ere_id` 0, as the existing reopen does, and makes
  no vehicle. Any other spec starts a new estimate.
- A session is no longer the identity. It is one staff member's one visit to
  the provider: its credential, cookies, callback and import authority. A new
  one is made for each reopen, under the pressing staff member's own account.
- A return is matched to its spec by the stock vehicle. A live spec of the
  current work that stands on the vehicle is updated in place through the one
  import; otherwise the return makes a spec and records the vehicle on it.
- A reopen ends a colleague's session still live on the same stock vehicle.
  The Case edit lease decides who is working, so the session store ends the
  other session and frees its account rather than refusing.
- A session reopening a spec's estimate makes nothing at the provider, so
  whatever stops it settles `Failed`, except an answer naming another
  estimate, which stays `Unknown`.
- A first launch whose `start-ere` answer was lost keeps its recorded vehicle
  and asks the start again on it, since a start on a vehicle answers the
  estimate the vehicle holds.

The spec's shared read model is unchanged. The identity is written through
the save request and read by the Glass's Case authority, so report snapshots,
history and other readers of a spec do not carry it.

## Consequences

- One stock vehicle and one repair spec per estimate, however many times it
  is opened. A second Glass's spec on a Case comes from launching on a spec
  that belongs to no estimate.
- Reopening no longer depends on a session's eight-hour lifetime, on the
  credential generation it was launched with, or on which staff member
  launched it.
- A different staff member reopens under a different Glass's login. Whether
  every login can open another's stock record is not shown by the captures
  and is to be confirmed live.
- An estimate reset with the portal's own Reset Repair Estimate cannot be
  reopened: the vehicle then shows no estimate, or answers another one.
- A reopen of a placeholder vehicle after the Engineer has identified the
  real one stays unproven
  ([ADR-0062](0062-glass-placeholder-vehicle-for-unknown-plates.md)).
- Specs made before this record carry no vehicle and start a new estimate.
- The repair profile remains one deployment setting. Changing it makes
  existing estimates fail their profile proof.
