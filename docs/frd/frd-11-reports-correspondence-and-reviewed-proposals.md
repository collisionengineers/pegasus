# FRD-11: Reports, correspondence, and reviewed proposals
> Owner capabilities: RPT, AI · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design.md

## Reports, correspondence, and reviewed proposals

Reports are produced from accepted case facts and source-labelled evidence
through the approved renderer boundary. Renderer source workspaces remain
independent source imports until an accepted integration contract and real
application caller exist.

### Report correction, finality, and post-report work

**Accepted report boundary:** an issued report has an immutable artifact/version identity and hash. A
correction or addendum creates a new reasoned version and retains every earlier
artifact, accepted fact, actor, time, and source; it never silently overwrites
the issued report. A closed case must be reasonedly reopened before its report
or evidence is revised.

The report-sent business event is the exact approved-mailbox Sent-item evidence
specified in [FRD-08 § Outbound correspondence evidence](frd-08-email-mailbox-and-background-processing.md#outbound-correspondence-evidence)
and remains final if Outlook later moves or deletes the item.
Outlook `sentDateTime` remains the business time; discovery and link times are
not substitutes. Report sent enters post-report work rather than closing the
case. A Box report PDF, file upload, generated artifact, draft, queue result, or
staff assertion alone proves neither sending nor external receipt.

Post-report queries, disputes, amendment requests, and replies remain
case-owned correspondence with source/reply-chain identity and permanent
history. Collision Engineers' Engineer responds to them, but the exact
CASE-23 states, transitions, correction/reopen interaction, due/chaser
interaction, and closure rules remain `Next`/unallocated and unresolved; no
mailbox adapter may invent them or create a new case/reference. See [external
data, submission, and report
contracts](../open-decisions.md#external-data-submission-and-report-contracts).

Requirements:

- deterministic template and payload versioning;
- preserved document/source provenance;
- authorised human review and approval of report facts and content before
  issue, without inventing a separate case-lifecycle pre-send review gate;
- immutable issued artifact identity and hash;
- correction/addendum rather than silent overwrite;
- exact delivery evidence where the workflow requires it;
- accessible staff presentation of status, validation, and failure without
  implying an unproved external delivery.

### Targeted sending and reviewed AI proposals

An allocated targeted report-send transaction is idempotent and records
approved destinations, immutable artifact/version, Box filing, exact send
evidence, completion outcome, and partial-failure recovery. A correction does
not silently alter an issued fee note or invoice; later financial impact uses
its own versioned, authorised contract. Staff-selected AI Assessor and
Engineer-reviewed query proposals remain proposals until the authorised human
accepts or rejects them through Core.

The vendor-neutral `Send to AI` work transport (AI-09; reworded by ADR-0021
under the operator's 2026-08-03 direct-write decision) hands a scoped worker
a pointer to one case — never case content — and the worker returns its work
as ordinary Automation Actor writes through the same Core commands, edit
lease, operation-key replay, and version guards as a staff save, attributed
and permanently recorded with the same rigor as any human action. Values the
automation records are unconfirmed working data reviewed by the engineer the
case is manually assigned to. Confirming a professional finding is
staff-Engineer-only, and report approval and outward dispatch remain human
acts, so no model, skill, prompt, or external source ever issues an accepted
case, engineering, economic, legal, or report outcome.

Durable Send to AI work has stable request, hand-off, reply, and disposition
identities. Stale work cannot overwrite a newer case/evidence version;
duplicate, expired, or cancelled requests are idempotent or inert outcomes of
the tracking record that never mutate accepted data; no AI caller confirms,
approves, or sends autonomously.

Signatures embedded in governed renderer documents are provenance-sensitive
document assets, not Web decorative imagery.
