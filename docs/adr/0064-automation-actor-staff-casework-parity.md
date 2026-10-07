---
id: ADR-0064
status: accepted
date: 2026-10-07
supersedes: [ADR-0021, ADR-0031]
superseded_by: []
related_capabilities: [MCP-01, MCP-02, MCP-06]
related_frd: [frd-10, frd-14, frd-24, frd-25, frd-27]
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
  decision 2: the structural absence of a finding tool. The absence of a
  report-approval tool and of any tool that sends outward stands until the
  later deliveries below.
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
3. **Findings.** `pegasus_assessment_update` records the professional
   findings staff record: outcome, roadworthiness, the unroadworthy reason,
   salvage category and value, and the Retail, Trade and Engineer's values.
   `pegasus_valuation_save` records guide cards and adopts the valuation
   calculation. Derived fields, Case-owned facts and lookup facts stay
   refused, as for staff.
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
   lease does.

## Consequences

- The Automation Actor's values are the Case's values, shown with their
  source tag. No per-field review returns.
- A new automation estimate needs a staff **Use repair spec** before a
  report uses it. An automation edit of the Current estimate marks a
  generated report stale, as a staff edit does.
- The inventory grows from 36 to 42 tools.
- This is the first of four deliveries. Later ones add Case lifecycle
  tools, report and document tools, queue and job tools, and outward sending
  under a new `automation.send` scope. Until each lands, those acts stay with
  staff.

## Links

- [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md)
- [FRD-14](../frd/frd-14-record-edit-leases.md)
- [FRD-24](../frd/frd-24-engineer-findings-damage-valuation-and-settlement.md)
- [FRD-25](../frd/frd-25-repair-estimates-imports-and-glasss-sessions.md)
- [FRD-27](../frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md)
