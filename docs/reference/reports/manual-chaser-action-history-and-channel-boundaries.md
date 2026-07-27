# Accepted finding: manual chaser action history and channel boundaries

**Operator decision:** Accepted in narrowed form on 2026-07-24.

**Legacy source dealt with:** ADR-0003 (`../dealt-with/accepted/0003-channel-aware-chasers-whatsapp-constraint.md`).

This report records the accepted finding from the predecessor ADR. It does not adopt that ADR wholesale, prove implementation, or authorise an outbound email, WhatsApp, Box, Audatex, or other external operation.

## Accepted finding

A chaser is tracked case work for obtaining missing information, images, or documents. In the `0.1.0-alpha.1`:

- the application prepares a message that staff can copy for manual use;
- preparing or copying a message must not record it as sent;
- the application provides an explicit staff action that records the acting staff member, timestamp, selected channel, outcome, and an optional chaser note;
- a staff-confirmed `sent` outcome is a staff assertion recorded in permanent action history, not proof that the external service delivered or the recipient received the message; and
- viewing or copying a chaser, or creating a Box File Request, must not create false completion or an external-delivery claim.

The exact persistence schema, outcome vocabulary, and whether the intended recipient or complete prepared message should be retained are not settled by this finding. Those choices must be justified by the real caller, operational need, privacy and retention requirements rather than copied from the predecessor.

## Current `Next`/`unallocated` position

### Operator truth and requirements

- The [WhatsApp operator note](../../operator-notes/systems-and-integrations/whatsapp.md) says WhatsApp is primarily used to chase garages for images.
- The [questionnaire](../../history/product/project-discovery-questionnaire.md#7-communications-and-tasks) already requires recurring seven-day reminders while material is missing, manual copy-and-paste messages for email or WhatsApp, and an optional Box File Request. Automated sending is outside the `0.1.0-alpha.1`.
- WhatsApp remains a manual staff channel. Its channel history remains external, while staff add relevant received material to the application or Box.
- `Held` pauses chasers while keeping due dates visible. The first chase is due at the same Europe/London local clock time seven calendar days after entering `Not ready`. Entering `Held` preserves the remaining interval; release back to `Not ready` resumes it, while release to `Review` ends the missing-information chase.

### Plan and implementation

The [lifecycle and work-management plan](../../history/plans/remainder-delivery/casework/lifecycle-and-work-management.md#surface-due-work-and-manual-chasers) already plans one Core due/chaser policy, reminder schedule/history, a copyable Web action, and a later Worker caller. It explicitly says copying sends nothing and does not prove delivered communication.

The plan does not yet define the manual chase action's channel, staff-confirmed outcome, actor/time action-history entry, or optional note. It also states that neither intended caller exists today. Current source contains only static `Not ready` and `Held` dashboard labels; there is no implemented case workspace, chaser policy, reminder scheduler, or mutable manual-chase caller.

## Differences from the legacy ADR

| Legacy proposal | Accepted `Next`/`unallocated` treatment |
| --- | --- |
| Chasers are assisted, tracked requests | Accepted, within the current seven-day manual-chaser requirement. |
| Record channel | Accepted for the explicit manual chase action. |
| Record staff disposition and timestamps | Accepted as actor, timestamp, and staff-confirmed outcome in permanent action history. |
| Record the complete draft and recipient | Not accepted as mandatory fields. Their need, privacy, and retention consequences remain to be justified. |
| Distinguish prepared from sent | Accepted, narrowed to prepared/copied versus staff-confirmed sent versus externally delivered. Only the first two are `0.1.0-alpha.1` application facts. |
| Allow free-text Notes alongside structured chasers | Accepted only as an optional note on the manual chase action, not as approval of a generic predecessor Notes feature. |
| Email may be sent through the approved mail path | Staff may manually paste and send through Outlook. Application-driven sending remains deferred and is not authorised by this finding. |
| WhatsApp chasers are prepared for manual staff sending | Already aligned with `Next`/`unallocated`. No WhatsApp API, sender, ingestion, scraping, or delivery-status integration is introduced. |
| Audatex sources are await-only | Not adopted. Estimating and valuation integration is deferred, and current authority defines no Audatex-specific no-chase rule. |

## Real caller and evidence still required

The intended staff case-workspace action must call the single Core `CaseWork` chaser owner. The planned Worker may schedule from the settled cadence only after that policy has caller-backed implementation evidence; it must not record a manual send or call an outbound channel.

Implementation evidence must eventually show:

- preparing, viewing, or copying a chaser records no sent outcome and performs no email, WhatsApp, or Box operation;
- an authorised staff action records one actor, case, channel, timestamp, outcome, and optional note through the real Web-to-Core caller;
- replay or double submission does not create contradictory duplicate chase actions;
- `staff-confirmed sent` is displayed separately from any future external delivery status;
- unauthorised, stale, closed, or otherwise invalid case actions fail visibly without changing history;
- `Held`, received material, and terminal outcomes suppress future reminders according to the separately settled cadence policy; and
- permanent action history remains available without writing message content into telemetry.

No implementation or end-to-end behaviour is proved by this documentation decision.

## Deferred-capability impact

Automated outbound email, WhatsApp ingestion/automation, broader mailbox management, and external delivery receipts remain deferred. A channel-neutral manual action identity, actor/time action history, and explicit separation between staff assertion and external delivery preserve a future adapter boundary.

Any later automated sender needs a separately approved communication contract covering external message identity, recipient and consent handling, content retention, retries, provider responses, delivery semantics, permissions, and failure recovery. This finding adds no dormant sender, WhatsApp client, mailbox permission, queue, endpoint, credential, or release gate.
