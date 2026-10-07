# FRD-27: Send to AI, reviewed proposals and the AI Job List

> Owner capabilities: AI-07 to AI-11, MAIL-17, MCP-06, MCP-07 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- An AI job's result is a draft or proposal until a staff member uses or
  rejects it. What the Automation Actor records directly is the Case's value,
  as a staff member's is.
- `Send to AI` queues a named AI job for one Case or Unidentified item. The
  job names the record and gives a short instruction, never Case content. An
  external client claims it and writes back through the same Core commands as
  staff.
- The AI Job List is the only Send to AI route: one durable ledger of named
  AI jobs. External clients claim jobs; Pegasus never runs one and never
  applies a result itself.
- A targeted report send is idempotent and records exact send evidence.
- An Administrator holds the Send to AI on/off switch in Administration.

## Purpose

This document owns targeted report sending, how AI proposals are reviewed,
the AI Job List, and the Send to AI switch. How reports are
produced and corrected is in
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md). The
Automation Actor boundary and its tools are in
[FRD-10](frd-10-mcp-automation-and-actor-boundary.md). Sent evidence is in
[FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md). The
Administration workspace is in
[FRD-17](frd-17-administration-workspace.md).

## Behaviour

### Targeted sending and reviewed AI proposals

A targeted report send is idempotent. It records approved destinations, the
immutable file and version, Box filing, exact send evidence, completion
outcome and partial-failure recovery. A correction never silently alters an
issued fee note or invoice; later financial impact uses its own versioned,
authorised contract. AI Assessor and Engineer-reviewed query proposals stay
proposals until an authorised person accepts or rejects them through Core.

**Send to AI.** The vendor-neutral `Send to AI` action queues an AI job
([ADR-0035](../adr/0035-ai-job-ledger.md)); there is no separate push
hand-off to a channel. The job names one record, never its content. The
external worker writes back through the same Core commands, edit lease,
operation-key replay and version guards as a staff save
([ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md)),
attributed and recorded like any human action. What the automation records is the Case's value, attributed to it
and shown with its AI source tag; it writes only fields staff can record on
the Case, so staff can change or clear each value on its section
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary)).
It records professional findings as staff do (operator, 7 October 2026). There
is no per-field review (operator, 25 September 2026).
Report approval and sending stay human acts until later deliveries add them.
No model, skill, prompt or external source ever issues an accepted legal or
report outcome.

Durable Send to AI work has stable job and disposition identities. Stale work
cannot overwrite a newer Case or evidence version. Duplicate, expired or
cancelled jobs are inert, recorded outcomes that never change accepted data.
No AI caller approves or sends a report; automation sending is not yet
delivered.

### AI Job List

The AI Job List is one durable ledger of named AI jobs
([ADR-0035](../adr/0035-ai-job-ledger.md)) that external AI clients claim
through the Automation Actor
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#ai-job-and-estimate-tools)).
Pegasus never runs an AI job itself and never applies a result to accepted
data. Every result is a draft or proposal that a staff act confirms through
the normal action for that record. Visuals follow
[design](../design/README.md).

**Kinds.** A closed Core list. An unknown kind is refused at creation. The
five kinds are Estimate, Unidentified resolution, Query response,
Unidentified-queue pass and Market Research (`MarketResearch` in the code
and in the table below).

| Kind | Started from | Input | Result | Staff confirmation |
| --- | --- | --- | --- | --- |
| Estimate | Estimate section `Send to AI` (With Engineer or later) | Direction text and an optional target percentage of the recorded Engineer's Value, 0 to 80 %, no default; the amount is shown as derived from that value and is guidance only, never an accepted figure. Refused without an Engineer's Value | A drafted estimate saved on the Case through the estimate tools, which may cite the job; state `Draft` | An enabled human staff member uses the draft (**Use repair spec**), which makes it the Current repair spec |
| Unidentified resolution | The Unidentified record's `Send Unidentified to AI` for that item's U reference | The U reference only | A proposed destination (existing Case, new Case from an accepted instruction, Image-initiated Case, or close) and a reason | Staff confirm through the existing Unidentified resolve action; the proposal never resolves the item itself |
| Query response | A retained post-report query linked to a Case | The message reference only | Draft reply text | Offered to the composer or Case notes; never sent automatically |
| Unidentified-queue pass | An external scheduler through the Actor `create` tool; Pegasus runs no timer | The queue scope | One Unidentified-resolution proposal per item examined | As Unidentified resolution, per item |
| MarketResearch | **AI market research** in the Case record's Valuation section, while editing, for the chosen Valuation month. The section shows a "Researching · {month}" card while the job is Queued or Taken; a re-run replaces the card. The result is filed without the Case edit lease or version, so it returns while the Engineer is still editing | The Case and its valuation context. External Claude Cowork uses the Pegasus connector plus research tools outside this repository | Research files attached to the Case through the connector, with attributable evidence and optional source-labelled valuation entries | The Automation Actor marks the job Completed after attachment. No staff completion gate and no automatic adoption as the Engineer's Value |

**States.** Reviewed proposals go `Queued` → `Taken` → `Draft ready` →
`Completed`. MarketResearch goes `Queued` → `Taken` → `Completed` once its
files are retained. All kinds can also end in `Failed`, `Cancelled` or
`Expired`.

- `Queued`: created and claimable. Creation records the kind, the target
  record and *started by* (a staff username or the connector client name).
- `Taken`: claimed by a named connector client under a lease with a visible
  expiry. An expired lease returns the job to `Queued` and records the
  expired claim. A client may release a job back to `Queued` before then.
- `Draft ready`: the client has written and named its result; the job waits
  for staff.
- `Completed`: staff recorded the consumption act for a reviewed proposal,
  or completed a Query response or Unidentified-queue pass. For
  MarketResearch, the Automation Actor completes the taken job after the
  connector attaches its files; no staff act and no acceptance of a value is
  implied.
- `Failed`: the client reported failure with a reason; not re-queued
  automatically.
- `Cancelled`: staff cancelled with a reason. A taken job is cancelled at
  once and the client's next progress call is refused.
- `Expired`: never taken before its own expiry.

Every transition carries an operation key and an expected version. A stale
or duplicate transition is an inert, recorded outcome. Client transitions
are attributed to the Automation Actor and the client name; staff
transitions to the staff username. The Administrator kill switch refuses
claims and progress; queued jobs wait and taken jobs expire back to `Queued`.

**Work Centre AI jobs.** The Work Centre's AI jobs tab is the live queue
([FRD-15](frd-15-work-centre-queues-and-search.md#work-centre)). Each row
shows the kind and state, the instruction, the record, who started it and
when. The action is one of `Review estimate` (opens the Case's Repair Spec
section), `Open query` (opens the message it answers, or the Case when the
job names none) or `Review` (opens the Unidentified item) for a `Draft
ready` job, the same place the Case's Next action opens; `Complete job` for
a `Draft ready` Query response or Unidentified-queue pass; otherwise
nothing. A job that names no record page has no open action. Every row also
ends with `Dismiss`, which takes the job off the Work Centre until it next
changes state and leaves the job itself as it was
([FRD-15](frd-15-work-centre-queues-and-search.md#work-centre)).

**Send Unidentified to AI.** The Unidentified record offers this action while
the item is open. It creates one Unidentified-resolution job for that item.
The reference comes from the record, not from typed input, and the
instruction is fixed. It has the same authority as the rest of that page.
While the Administrator switch is off, the action is refused with "AI work is
not accepting new jobs."

Staff cannot cancel a job from these surfaces. An Administrator stops a
non-terminal job on Administration AI jobs.

**Administration.** Automation & AI shows the active and failed job counts
and the Stop/Start automation control. That control is the
[ADR-0026](../adr/0026-enable-automation-mcp-by-explicit-deployment-configuration.md) kill switch, so stopping
automation also stops the ledger. The Work Centre pane is the live queue;
the history of the same jobs is
[Action logs](frd-04-parties-accounts-and-access.md#permanent-action-history),
where an AI job row's Reference opens the Case or Unidentified record.

**Where the list appears.** The list appears in two places: the Work Centre
pane above, and the Administration AI jobs page. The Administration page
lists every recorded job in pages, with the active and failed counts and the
Send to AI switch state, and offers `Stop` for a non-terminal job. Both
refresh when the page is reloaded. There is no live event stream.

### Send to AI switch

Administration → Automation & AI has an AI settings panel. Only an
Administrator can open the page. The panel appears with the Automation
panel, where the Automation client is composed. This is capability `MCP-07`.

The panel holds one checkbox, the Send to AI on/off switch, and one Save.
Turning the switch off refuses new AI jobs at once. A Save that leaves the
switch as it was writes no history. Each change is attributed and kept in
permanent history. An absent switch record means on.

There is no connector address, timeout or token: no Send to AI request is
pushed to a channel.

Health appears on the Administration Health page as the `AI jobs` row. Its
state comes from the Send to AI switch, the active and failed job counts, and
the time of the newest job. There is no connectivity test button.

## States and transitions

AI job states and their transitions are listed under
[AI Job List](#ai-job-list). Settlement proposals are **Awaiting**,
**Accepted** or **Corrected**, as described under
[Targeted sending and reviewed AI proposals](#targeted-sending-and-reviewed-ai-proposals).
The Case's own states are in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels).

## Edge cases and fail-closed behaviour

- An unknown AI job kind is refused; stale or duplicate transitions are
  inert.
- Duplicate, expired or cancelled AI jobs are inert, recorded outcomes that
  never change accepted data.
- Stale work cannot overwrite a newer Case or evidence version.
- `Send Unidentified to AI` on a closed or resolved item is refused.

## Acceptance evidence

Core tests cover every AI job transition. Integration tests cover the
Unidentified record's `Send Unidentified to AI`, the Work Centre AI jobs
pane and the Automation & AI switch.
Deployment and live acceptance are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `AI-07`–`AI-11`, `MAIL-17`, `MCP-06`, `MCP-07` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-04](frd-04-parties-accounts-and-access.md),
  [FRD-10](frd-10-mcp-automation-and-actor-boundary.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-17](frd-17-administration-workspace.md),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md).
- Technical constraints:
  [ADR-0026](../adr/0026-enable-automation-mcp-by-explicit-deployment-configuration.md),
  [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md),
  [ADR-0035](../adr/0035-ai-job-ledger.md).
