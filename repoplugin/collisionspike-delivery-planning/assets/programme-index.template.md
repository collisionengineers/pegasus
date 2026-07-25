# <Programme name>

## Finish line

<One observable release or delivery outcome. Link to authoritative requirements rather than copying them.>

## Authority and boundaries

- <Source-of-truth link and conflict rule>
- <Explicit exclusions>
- <External-system, cost, data and approval boundaries>

## Stable invariants

- <Application/domain invariant>
- <Data/transaction invariant>
- <Caller/ownership invariant>

## Delivery order

| Order | Area or task | Requires | Real or intended caller | Unlocks |
|---|---|---|---|---|
| 1 | [<area>](<relative-link>) | <dependency> | <caller and current evidence> | <downstream outcome> |

## Ownership and merge hotspots

| Boundary | Single owner | Consumers | Coordination rule |
|---|---|---|---|
| <policy/data/composition boundary> | <owner> | <callers> | <no-concurrent-edit or merge rule> |

## Approval boundaries

| Action | Exact scope required | Approval/evidence required |
|---|---|---|
| <external mutation or release> | <account, region, folder, mailbox, corpus, target> | <decision and proof> |

## Evidence language

Use `Planned`, `Implemented`, `Called`, `Locally verified`, `Deployed`, `Live verified`, and `Accepted` literally. Record evidence in the owning task, not in this index.

## Integrated acceptance journey

<Link the ordered task outcomes that prove the end-to-end workflow and state what remains outside that proof.>

## Plan maintenance

<State when authority, code, dependency and evidence changes require reconciliation. Exclude mutable workspace facts and status roll-ups.>
