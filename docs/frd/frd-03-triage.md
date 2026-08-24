# FRD-03: Triage

## Boundary with Unidentified

Triage remains a separate pre-Case workflow. A missing registration or route that is
specifically a Triage request follows the Triage states and does not receive a U
reference merely because it is awaiting information. Material that is not accepted
as Triage, or a terminal unreadable/ambiguous source outside that workflow, enters
Unidentified with its canonical reason.
> Owner capabilities: TRI · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## Triage

### Normal workflow and completion evidence

Triage begins when the exact accepted route policy classifies a provider request as an assessment request or an authorised staff member manually classifies safely retained, attributable material as Triage. Manual classification records the source, available route evidence, actor, time, reason, and policy version; it neither invents Principal identity nor creates a Case. Material whose route or category remains unaccepted stays `Needs sorting` and never becomes Triage or a Case by fallback. A Triage request stays separate pre-Case work: without a VRM it remains `Needs sorting`; with a VRM it opens as `Open`, may move to `Awaiting information`, records an accepted finding as `Finding recorded`, and reaches `Completed` only after the required response evidence is confirmed. An acknowledgement, request for information, Draft, queue action, or other correspondence may be retained but is not itself a finding or completion evidence.

Automatic creation from intake follows exactly that rule and adds nothing to it.
When the accepted route classification records a received message as a Triage request,
processing does not treat it as an instruction: it is pre-case work, no case is
allocated from it, and the accepted route classification decision is itself the accepted
Triage-match evidence — the same route policy the paragraph above names, with its policy
key and version stamped on the record. A known vehicle registration opens the Triage as
`Open`; no known registration registers the material as Unidentified with its canonical
reason and opens no Triage, until a registration is known. A message whose classification
is the recorded Ambiguous outcome is neither: it opens no Triage and reaches staff.

Triage records have the states `Open`, `Awaiting information`, `Finding recorded`, `Completed`, and `Cancelled`.

A recorded finding has two independently optional dimensions:

- Roadworthiness: `Roadworthy` or `Unroadworthy`;
- Assessment: `Repairable` or `Total loss`.

At least one dimension is required. A later correction creates a reasoned superseding finding; it never overwrites history. A pre-send correction replaces the current finding with a reason. A post-send correction creates a superseding finding, returns the Triage to `Finding recorded`, and requires a new response.

Every `Completed` Triage has one exact reply-chain Sent item from an approved mailbox. Subject, VRM, a manual “sent” assertion, a Draft, a queue result, an acknowledgement, or an unrelated Sent item is not completion evidence. `Cancelled` is the only terminal Triage outcome without a finding and reply; `Completed` and `Cancelled` close only that Triage workflow and never make its finding definitive for a later Case.

Triage may have an optional assignee but no due date or chase schedule. It may link to at most one current case; a case may have many Triages. The [staff role access matrix](frd-04-parties-accounts-and-access.md#staff-role-access-matrix) permits every staff role to reasonedly unlink or relink; the exact prior/current Case identities, actor, time, reason, and evidence remain in permanent history.

Cancellation and reopen require reasons. Reopen always returns to `Open` and never erases the prior finding, reply, actor, or chronology.
