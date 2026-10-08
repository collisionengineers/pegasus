---
id: ADR-0064
status: accepted
date: 2026-10-07
supersedes: [ADR-0021, ADR-0031]
superseded_by: [ADR-0067]
related_capabilities: [MCP-01, MCP-02, MCP-06]
related_frd: [frd-06, frd-10, frd-11, frd-14, frd-21, frd-24, frd-25, frd-27]
tags: [mcp, automation, ai, assessment, estimate]
---

# ADR-0064: The Automation Actor has staff casework parity

## Status

Accepted on 7 October 2026. The operator ruled that the Automation Actor may
do anything a member of staff can do in casework, with two exceptions: it
never puts an estimate in use, and it never runs a Glass's session.
Administration rights stay with staff Administrators.

This record replaces, in part:

- [ADR-0031](0031-automation-actor-contract-without-eva-export-tools.md):
  the rule that automation never records a professional finding (its Current
  applicability) and its consequence that automation cannot confirm
  findings. Its EVA Export decision, kill switch and attribution stand.
- [ADR-0021](0021-automation-actor-direct-write-assessment-contract.md),
  decision 2: the structural absence of a finding tool and, from the second
  delivery, of a report-approval tool; from the fourth, of any tool that
  sends outward.
- The job-cited AI-draft estimate model in
  [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md#ai-job-and-estimate-tools):
  `pegasus_estimate_save` no longer needs an AI job. The
  [ADR-0035](0035-ai-job-ledger.md) ledger is unchanged.

[ADR-0059](0059-native-mcp-file-content-and-consolidated-tool-inventory.md)'s
tool count is history. FRD-10 owns the current inventory.

## Context

The Automation Actor holds `PerformCasework` and nothing else, the right
every casework staff role holds. Its tools nevertheless refused what staff
record every day: the outcome, roadworthiness, salvage and the Retail, Trade
and Engineer's values were professional findings for staff alone, and a new
estimate had to cite an Estimate job taken by the same client. A job is how
an external agent picks up work, not proof of authority. The generic Case
detail tool wrote through its own save path, `ISaveCase`, which cleared any
fact the call did not name.

## Decision

1. **Casework parity.** An Automation tool may perform any casework act a
   staff member with `PerformCasework` can, through the same Core command,
   lease, version and operation-key guards, and with its own attributed
   history. The `Manage*` rights stay with staff Administrators; automation
   has no Administration, configuration, credential, release or deletion
   authority ([ADR-0011](0011-restrict-mcp-to-automation-actor.md)).
2. **Two exceptions.** Automation never puts an estimate in use (**Use repair
   spec**, making an estimate Current). It never runs a Glass's session,
   since a session signs in with the staff member's own Glass's account.
   Get valuation is not a session: it uses Pegasus's own valuation account
   ([ADR-0060](0060-glass-valuation-account-and-valuation-report.md)), and
   `pegasus_valuation_get` presses it and records the card (operator,
   8 October 2026).
3. **Findings.** `pegasus_assessment_update` records the professional
   findings staff record: outcome, roadworthiness, the unroadworthy reason,
   salvage category and value, and the Engineer's Value.
   `pegasus_valuation_save` records guide cards and adopts the valuation
   calculation, which writes the report's Retail and Trade from its basis
   card: since 8 October 2026 they are the chosen card's figures, typed by
   no one (FRD-24). Derived fields, guide-card figures, Case-owned facts and
   lookup facts stay refused, as for staff.
4. **System fills defer to a deliberate value.** A system fill (vehicle
   lookup, original-report extraction, Glass's VIN, Principal default fee)
   lands only on
   a value nobody recorded on purpose. A value staff or the Automation Actor
   recorded is never overwritten; a value another fill recorded takes the
   newer reading.
5. **One Case save.** `pegasus_case_update_details` calls the staff Case save
   (`ISaveCaseWorkspace`). An omitted value is unchanged, an empty string
   clears, and a value not named never changes. `ISaveCase` is removed.
6. **Estimates.** `pegasus_estimate_save` creates an estimate that lands as
   an AI-draft `Draft`, or edits any live estimate in place, the Current one
   included. Citing an AI job is optional. `pegasus_estimate_act` performs
   the Repair Spec acts other than Use repair spec.
7. **Leases.** `pegasus_edit_begin` may take over a lease a staff member
   holds, recorded as a takeover. A staff member still cannot take over an
   Automation lease; it lapses within five minutes. An Automation lease that
   lapsed with nobody claiming since carries on with its token, as a staff
   lease does. A write under an Automation lease keeps the lease, so one
   `pegasus_edit_begin` token carries every write until `pegasus_edit_end`
   (operator, 8 October 2026); a staff write still ends its lease.
8. **Lifecycle, reports and documents.** The operator approved the Case
   lifecycle and the reports and documents tiers, report approval included
   (7 October 2026). `pegasus_case_action` takes the Case's Actions-menu
   lifecycle acts; `pegasus_report_action` generates the report and its
   companion documents, records report approval and links or unlinks
   report-Sent evidence; `pegasus_document_action` tags images, puts them in
   or out of the report, marks the original report, removes documents and
   retries failed custody. Each calls the staff command. Report approval is by
   staff or the Automation actor; it is not a send. A recorded approval,
   archive or evidence link reads back as the Automation actor.
9. **Sending.** The operator approved outward sending (7 October 2026) under
   its own scope, `automation.send`, so a grant can hold casework without
   sending. `pegasus_report_send` sends a generated report to the Principal
   through the staff report send, and `pegasus_mail_send` sends new mail, a
   reply, reply all or forward of a retained message, and the Triage reply
   through the staff mail send, from the same approved mailboxes. Report
   delivery and the staff mail send admit the Automation actor where they
   required a signed-in member of staff; the system worker and a Principal
   still never send. A send records its sender's kind, so the operation, its
   history, the Case's Notes line, a chase it records and a post-report query
   it completes read back as the Automation actor. Its current-sender check is
   the client registration's kill switch, read at each step a staff send
   re-reads the staff account. EVA submission is not part of this delivery:
   it is being removed elsewhere.

## Consequences

- The Automation Actor's values are the Case's values, shown with their
  source tag. No per-field review returns.
- A new automation estimate needs a staff **Use repair spec** before a
  report uses it. An automation edit of the Current estimate marks a
  generated report stale, as a staff edit does.
- The first delivery grows the inventory from 36 to 42 tools; the second
  adds `pegasus_case_action`, `pegasus_directory_search`,
  `pegasus_report_list`, `pegasus_report_action` and
  `pegasus_document_action`, making 47; the queue and job delivery adds
  eight more, making 55; the report preparation delivery adds four, making
  59; sending adds `pegasus_report_send` and `pegasus_mail_send`, making 61.
- This is the first of four deliveries, all now delivered: the Case
  lifecycle, report and document tools; the queue and job tools; and outward
  sending under `automation.send`.
- `StaffMailSendOperations` gains an `ActorKind` column (Staff or
  Automation; every earlier send is Staff), so no send by the Automation
  Actor reads back as a member of staff. An engineer's report count counts
  staff sends only.
- Queue and job tools: delivered (operator, 7 October 2026;
  [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md#queue-and-intake-tools)).
  The Actor creates any AI job kind, cancels and confirms jobs, assigns,
  unassigns and notes a Triage, closes and reopens Unidentified items,
  creates a Case directly, accepts a received item and retries its
  allocation, moves and dismisses mail (ADR-0067, 8 October 2026: dismiss and restore only; the move is removed), and dismisses Work Centre records.
- Inspection address, report wording and image preparation: delivered
  (operator, 7 October 2026). `pegasus_intake_action`
  `resolve_inspection_address` settles a received item's inspection address
  as **Create case** does. A settled address records its settler's actor
  kind and identity rather than a staff identifier, so one the Actor settled
  reads as the Actor's; migration `20261007181000_InspectionAddressSettlerKind`
  rewrote each earlier settlement as a member of staff's.
  `pegasus_report_wording_get` and `pegasus_report_wording_save` read and
  change the report's wording blocks, and `pegasus_image_preparation_get`
  and `pegasus_image_prepare` read and change each image's crop, rotation,
  order and page of its own, both through the Case save
  ([FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md#report-wording-and-images)).

## Links

- [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md)
- [FRD-14](../frd/frd-14-record-edit-leases.md)
- [FRD-24](../frd/frd-24-engineer-findings-damage-valuation-and-settlement.md)
- [FRD-25](../frd/frd-25-repair-estimates-imports-and-glasss-sessions.md)
- [FRD-27](../frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md)
- [FRD-21](../frd/frd-21-outbound-correspondence-and-sent-evidence.md)
