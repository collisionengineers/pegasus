---
id: ADR-0075
status: accepted
date: 2026-10-16
supersedes: [ADR-0064]
superseded_by: []
related_capabilities: [CASE-27, MCP-01]
related_frd: [frd-10, frd-14]
tags: [mcp, automation, edit-lease]
---

# ADR-0075: Staff may take over an Automation lease

## Status

Accepted on 16 October 2026 by operator decision. On 15 October 2026 the
operator ruled "Allow Take over of an Automation lease (Core change under
FRD-14 and FRD-10)"; on 16 October 2026, that the history line reads "Edit
lease taken over from AI.", that only staff take over an Automation lease,
and that this record is written.

This record replaces, in part:

- [ADR-0064](0064-automation-actor-staff-casework-parity.md), decision 7:
  its second sentence, "A staff member still cannot take over an Automation
  lease; it lapses within five minutes." The rest of decision 7 stands, and
  the body of ADR-0064 stays as filed.

[FRD-14](../frd/frd-14-record-edit-leases.md#take-over) owns the Take over
rule and
[FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md#edit-leases) the
Automation lease tools.

## Context

A Case lease the Automation Actor held could not be taken over, so a lease
left behind by a session that ended without releasing it held the Case until
it lapsed. Stopping the automation client already releases every lease
Automation holds.

## Decision

`CaseEditAuthority.CanTakeOver` lets a member of staff with the casework
right take over a Case lease the Automation Actor holds, by the same Take
over. The Automation Actor still takes over only a lease a member of staff
holds. The takeover history line names the previous holder as AI, never by
the Actor's grant or client id; `CaseEditAuthorityHolder.AutomationName`
holds that one word for the holder chip and both takeover history writers.

## Consequences

- The Case ribbon offers Take over on a Case the Automation Actor holds.
- The Automation Actor's next heartbeat, renew, write or release under the
  token it held is refused.
- Otherwise an Automation lease still lapses within five minutes, or ends
  when an Administrator stops the automation client.
- ADR-0064 lists this record in `superseded_by`, and its index row names the
  replaced sentence.

## Links

- [FRD-14 — Take over](../frd/frd-14-record-edit-leases.md#take-over)
- [FRD-10 — Edit leases](../frd/frd-10-mcp-automation-and-actor-boundary.md#edit-leases)
- [ADR-0064](0064-automation-actor-staff-casework-parity.md)
