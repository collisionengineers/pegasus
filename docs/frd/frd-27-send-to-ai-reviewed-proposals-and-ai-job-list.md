# FRD-27: Send to AI, reviewed proposals and the AI Job List

> Owner capabilities: AI-07 to AI-11, MAIL-17, MCP-06, MCP-07 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- AI never decides anything. Its output is a proposal until a staff member
  accepts or rejects it.
- `Send to AI` hands a scoped worker a pointer to one Case, never Case
  content. The worker writes back through the same Core commands as staff.
- The AI Job List is one durable ledger of named AI jobs. External clients
  claim jobs; Pegasus never runs one and never applies a result itself.
- A targeted report send is idempotent and records exact send evidence.
- An Administrator sets the Send to AI connector address, timeout, token and
  on/off switch in Administration.

## Purpose

This document owns targeted report sending, how AI proposals are reviewed,
the AI Job List, and the Send to AI connector settings. How reports are
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

**Send to AI.** The vendor-neutral `Send to AI` transport
([ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md))
hands a scoped worker a pointer to one Case, never Case content. The worker
writes back through the same Core commands, edit lease, operation-key replay
and version guards as a staff save, attributed and recorded like any human
action. What the automation records is unconfirmed working data for the
assigned Engineer to review. Confirming a professional finding is for staff
with Engineer capabilities, including Administrator. Report approval and
sending stay human acts. No model, skill, prompt or external source ever
issues an accepted Case, engineering, financial, legal or report outcome.

**Settlement proposals.** For Outcome, Engineer's Value, Salvage category,
Salvage value, Roadworthiness and the unroadworthy reason, Pegasus keeps the
latest Automation value as a proposal with a status. It reads **Awaiting**
until staff record the field. A matching staff value makes it **Accepted**; a
different value or a clear makes it **Corrected**, recording who resolved it.
It stays resolved until the next Automation value. The Settlement section
shows the Proposed column only when a proposal exists. It offers Accept and
Accept all only while a proposal is Awaiting and the Case is being edited.
The Engineer's Value row offers Apply in Valuation instead, because that
value is adopted only through an explicit valuation Apply. A Save that leaves
an Awaiting field untouched leaves it Awaiting.

Durable Send to AI work has stable request, hand-off, reply and disposition
identities. Stale work cannot overwrite a newer Case or evidence version.
Duplicate, expired or cancelled requests are inert, recorded outcomes that
never change accepted data. No AI caller confirms, approves or sends on its
own.

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
| Estimate | Estimate section `Send to AI` (With Engineer or later) | Direction text and an optional target percentage of the recorded Engineer's Value, 0 to 80 %, no default; the amount is shown as derived from that value and is guidance only, never an accepted figure. Refused without an Engineer's Value | A drafted estimate saved on the Case through the estimate tools, citing the job; state `Draft` | An Engineer accepts the draft (`Use estimate`), which makes it the Current estimate |
| Unidentified resolution | Operations `Send Unidentified to AI` for one U reference | The U reference only | A proposed destination (existing Case, new Case from an accepted instruction, Image-initiated Case, or close) and a reason | Staff confirm through the existing Unidentified resolve action; the proposal never resolves the item itself |
| Query response | A retained post-report query linked to a Case | The message reference only | Draft reply text | Offered to the composer or Case notes; never sent automatically |
| Unidentified-queue pass | An external scheduler through the Actor `create` tool; Pegasus runs no timer | The queue scope | One Unidentified-resolution proposal per item examined | As Unidentified resolution, per item |
| MarketResearch | **AI market research** in the Case record's Valuation section, while editing, for the chosen Valuation month. The section shows a "Researching · {month}" card while the job is Queued or Taken; a re-run replaces the card | The Case and its valuation context. External Claude Cowork uses the Pegasus connector plus research tools outside this repository | Research files attached to the Case through the connector, with attributable evidence and optional source-labelled valuation entries | The Automation Actor marks the job Completed after attachment. No staff completion gate and no automatic adoption as the Engineer's Value |

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

**Operations panel.** The AI Job List on `/operations` shows every
non-terminal job and the terminal jobs of the current day: Job (kind and
detail), Record, Started by, Created, State, Action. The action is one of
`Review estimate` (opens the Estimate section), `Open query` (opens the
message) or `Review` (opens the Unidentified item) for a `Draft ready` job;
`Complete job` for a `Draft ready` Query response or Unidentified-queue
pass; `Cancel` (reason required) for any non-terminal job; otherwise
nothing. `Send Unidentified to AI` creates an Unidentified-resolution job for
a chosen U reference.

**Administration.** Automation & AI shows the active and failed job counts
and the Stop/Start automation control. That control is the
[ADR-0026](../adr/0026-enable-automation-mcp-by-explicit-deployment-configuration.md) kill switch, so stopping
automation also stops the ledger. The Operations panel is the live queue;
the history of the same jobs is
[Action logs](frd-04-parties-accounts-and-access.md#permanent-action-history),
where an AI job row's Reference opens the Case or Unidentified record.

**Where the list appears.** The list appears in two places: the Operations
page panel above, and the Administration AI jobs page. The Administration
page lists the recorded jobs in pages, with the active and failed counts and
the Send to AI channel state, and offers `Stop` for a non-terminal job. Both
refresh when the page is reloaded. There is no live event stream.

### Send to AI connector settings

Administration → Automation & AI has an AI settings panel. Only an
Administrator can open the page. The panel appears only when Send to AI is
part of the deployment. This is capability `MCP-07`.

The Administrator sets:

- the connector address. It must be a bare URL with no path and no query.
  The code accepts only an `http` loopback address, which is the
  [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md)
  transport decision;
- a timeout in seconds, from 1 to 60 inclusive;
- the outbound Send to AI on/off switch. Turning it off refuses new
  hand-offs at once.

The Administrator can also enter, rotate or remove the connector token. A
new token must be at least 32 characters. Removing the entered token returns
the connector to the configured one. The token is write-only: the page shows
only that one is held and when it last changed, never the token itself.

One Save applies the address and timeout, a replacement token if one was
entered, and the switch if it changed. An unchanged switch writes no switch
history. Each change is attributed and kept in permanent history. A blank
address or timeout means the deployment's configured value applies.

Connector health appears on the Administration Health page as the `AI jobs`
row, whose dependency is the AI connector. Its state comes from the Send to
AI switch, the active and failed job counts, and the time of the newest job.
There is no connectivity test button.

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
- Duplicate, expired or cancelled Send to AI requests are inert, recorded
  outcomes that never change accepted data.
- Stale work cannot overwrite a newer Case or evidence version.
- A connector address with a path or query, a timeout outside 1 to 60
  seconds, or a token shorter than 32 characters is refused.

## Acceptance evidence

Core tests cover every AI job transition and the connector address, timeout
and token rules. Integration tests cover the Operations panel actions.
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
