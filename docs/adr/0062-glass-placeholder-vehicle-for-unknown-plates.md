---
id: ADR-0062
status: accepted
date: 2026-10-02
supersedes: []
superseded_by: []
related_capabilities: [EXT-06]
related_frd: [FRD-25]
tags: [glass, lookup, placeholder]
---

# ADR-0062: A plate Glass's does not know launches its estimate on a placeholder vehicle

## Status

Accepted on 2 October 2026. The operator chose this route after reviewing the
captures for a.QDOS26047 (issue 996).
[FRD-25](../frd/frd-25-repair-estimates-imports-and-glasss-sessions.md#glasss-launch-and-return)
owns what staff see. This record owns how the lookup decides, what the launch
does instead of stopping, and how such a session's return is checked.

## Context

Glass's Market Value Assessor finds a vehicle by its plate through a VRM data
supplier. For a plate the supplier does not know, the portal's plate search
answers `vrm_lookup` 0 and a `natcode` that is the JSON `false`, and the
candidate list that follows is empty. The portal's own page calls this "The
vehicle details have not been found" and offers its manual search tree or an
unqualified vehicle; it never repeats the search.

Pegasus read the boolean as the text "False", took the type number as present,
read the empty candidate list three times and settled every such launch and
valuation at `glass.candidates.refused`. Its own rule of repeating a plate
search once after 250 ms, and the `glass.lookup.unavailable` code that
followed it, came from the spike and had no counterpart in the portal.

The portal's estimate button starts an estimate only from an open stock
vehicle. The captures of 2 October 2026 show that an unqualified
"placeholder" vehicle — the portal's "Add Unqualified Vehicle" form with every
control at its default — is given a stock id and a generic type number, that
the estimate starts from it, and that the estimator's own "Alternative
vehicle selection" then lets the Engineer identify the real vehicle by make,
model and year. No Save & Exit from such an estimate was captured.

## Decision

**A lookup without a type number is "not found", once.** The stock search is
the lookup, as before; a stocked plate is searched afresh, as before. The type
number is read only when it is a non-empty string. A negative `vrm_lookup`, or
no type number, is `glass.lookup.notfound` after that one search, with no
candidate read and no retry. An answer whose `vrm_lookup` cannot be read is a
bad request, never "not found". `glass.lookup.unavailable` is removed. Get
valuation on such a plate answers the card's existing notice, with the code
and `natcode=absent` in the host log; a placeholder has no value.

**The launch goes on with a placeholder.** When the lookup is not found, the
launch inserts an unqualified vehicle with the captured form, its model text
the Case registration, one per launch and never reused, so each estimate
keeps one vehicle and one export. A lost or unreadable insert answer, or one
naming no usable id, is an unknown outcome and is never retried; the portal's
own validation refusal is a known one. The placeholder is proved by its id,
an empty registration, a numeric type number — the one the launch recorded,
on every later Resume — and the repair profile, and is then selected and
started as any vehicle. The host log says the launch is on a placeholder,
with the lookup's numbers and flags. Nothing is added to the Case page.

**The return rule is defensive until read against a live return.** A
placeholder session's export may name no plate and no mileage, or the Case's
own; a plate or a non-zero mileage of another vehicle is refused as before.
The type number it names is recorded on the session beside the placeholder's
own, which stays the one the stock entry is re-proved by, and is logged.
Everything else — the relay, the single export, custody, the empty-estimate
refusal and the import — is unchanged.

## Consequences

- Four codes are added, one per placeholder stage and reason:
  `glass.placeholder.request`, `glass.placeholder.refused`,
  `glass.placeholder.id` and `glass.placeholder.identity`.
- Placeholder entries accumulate in each Engineer's stock list, one per launch
  on an unknown plate, named by the Case registration.
- The placeholder's fixed year, month and plate letter are the captured
  values; whether Glass's prices or validates against them is unknown.
- If the estimator writes the Engineer's choice back to the stock entry, a
  later Resume will refuse the entry as no longer the placeholder and the
  session will wait as `Unknown`. That fails safe and is to be read against
  the first live return, as is the whole return rule.
- No scripted test proves what a placeholder export carries; the first live
  Save & Exit on such a plate is that evidence.

## Links

- [FRD-25 — Glass's launch and return](../frd/frd-25-repair-estimates-imports-and-glasss-sessions.md#glasss-launch-and-return)
- [ADR-0058 — Glass's provider work runs in the background of the Web host](0058-glass-provider-work-in-the-web-host.md)
- [ADR-0060 — Glass's valuation through a Key Vault-held account](0060-glass-valuation-account-and-valuation-report.md)
