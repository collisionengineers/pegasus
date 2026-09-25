# FRD-10: MCP automation and actor boundary

> Owner capabilities: MCP-01 to MCP-05 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- MCP is a controlled entry point for one named Automation Actor. Ordinary
  staff never use it; they use the Web UI.
- Every tool calls the same Core command a staff member would. The Actor has
  its own identity and history and no management powers.
- Lists page by cursor, 50 by default and 100 at most. Documents are checked
  for permission before any content is read.
- The Actor can work Unidentified items, Triage Cases, AI jobs and estimates. It
  cannot send mail on its own or touch Glass's credentials.
- A tool counts as delivered only after a real caller has proved success,
  authorisation failure, validation failure and history.

## Purpose

This document owns what automation may do through MCP, how it is identified,
and where its authority stops. Automation lists and resolves Unidentified
items by exact U-reference through the same Core command as staff
([FRD-02](frd-02-intake-and-source-identity.md#unidentified-destination-and-reference)).

## Behaviour

### MCP automation and actor boundary

**One Actor.** MCP is a management and development controlled entry point for
one named, vendor-neutral Automation Actor. Ordinary staff have no MCP access.
The Actor calls only its approved list of ordinary Core actions, with its own
authentication, identity and permanent history. It has no Administrator,
configuration, credential, cloud, release, deletion or other management
authority.

**Grants.** Each connector grant has its own durable identity. History keeps
the grant identity, the shared client ID and the human approver separately.
An approved scope never grants human staff authority. Code exchange and refresh
keep the grant. A revoked, expired or wrong-audience credential fails before
any tool runs. Production signing and encryption use separate persistent
certificate purposes with a rotation overlap, so a restart or replica change
does not invalidate valid tokens. Missing production keys fail closed.

**Paging.** Lists use stable `(sort value, immutable ID)` continuations,
default 50 and maximum 100. A continuation is bound to the caller and the
filters; a malformed, oversize or foreign cursor fails. Case detail returns
bounded summaries with separate continuations for documents, history and
estimates rather than cutting silently. Case search and the per-Case document,
history and estimate lists use `CursorPage<T>`; protected tokens bind actor,
filters and order.

**Raw estimate import** is one Core command, `IImportRawEstimate`, run after
custody retention. The Case page and MCP both call it; there are no separate
import paths.

**Documents.** Permission and immutable metadata are checked before content
is read. Small files come back inline, bounded. Larger files return their
logical identity, size, media type, hash and an authenticated
`/automation/documents/{id}/versions/{version}` URL. That endpoint needs the
same bearer audience and Documents scope, rechecks Case and source
authorisation, and supports exact-version ETag and ranges. A metadata-only
request fetches zero content bytes. There are no public signed links and no
fetches of arbitrary URLs.

**Exports.** A document export accepts at most 32 exact occurrence and
version selections. Small archives come back inline. Larger ones return a
five-minute, grant-bound `/automation/document-exports` URL, which needs the
Documents scope again and keeps the original lease, version and operation
identity. ZIP output streams in order, without ranges. An invalid, expired or
foreign export ticket gets the same non-disclosing unavailable response.

**Assessment writes.** `pegasus_assessment_update` writes only non-finding
assessment fields a staff member records, and so confirms or clears, on the
Case: the fields a Case section's editor posts and those a section writes
through its own typed member (damage entries, mileage source, storage per
day, recovery charge and report date). Any other field is refused and named,
so every unconfirmed Automation value is one the next staff Save of its
section confirms or clears (operator, 24 September 2026). Professional
findings (including the Engineer's Value and its basis card's retail and
trade), Case-owned facts, fields derived from damage entries and the facts
the DVLA/DVSA lookup alone records (engine, fuel, colour, tax and MOT expiry)
are refused. Case facts, including the Inspection date the report prints as
the date the damage was assessed, change through
`pegasus_case_update_details`, and `pegasus_assessment_get` returns them
under `caseOwned`. The Case's Received date, which is its instruction date,
is read-only: `caseOwned.receivedDate` and the Case summary's
`receivedAtUtc` carry it, and no tool accepts an instruction date.
Estimates go through the named estimate tools, with the same actor, lease,
version and replay checks as the Case UI
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)). Unidentified reason
codes on the wire are the Core list under `unidentified`. The tool list has
no autonomous Send and never exposes Glass's credentials or sessions.

**Network-drive scanning.** An externally scheduled client may scan an
approved network-drive scope and submit immutable source occurrences through
the approved MCP document actions. Claude Desktop may supply the first
accepted client evidence without owning the Actor identity or the Core
action. The client, its schedule and the filesystem stay outside Pegasus.
Custody starts only with an authenticated accepted MCP submission. Each
occurrence follows the ordinary source-occurrence, idempotency, matching,
classification and history rules. Scanning never associates material and
never allocates a Case or reference.

**Proof.** Registration, a tool schema or an endpoint file proves nothing.
Each tool tranche needs a real caller exercised for success, authorisation
failure, validation failure, and its history line.

**Background automation** follows the same rule. Queues and timers carry
stable work identities; Core owns transitions and idempotency
([FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels)). Poison
work stays recoverable and visible. No AI proposal or workspace service can
change Case state directly.

### Triage automation contract

Triage is ordinary `PerformCasework`. The Actor may list and inspect a
Triage, retrieve its retained origin source, mark it Awaiting information,
record or supersede a finding, link or unlink exact response evidence,
complete, cancel or reopen it, and link or unlink a Case under the normal
Case edit lease and version guards. The tools identify a Triage by its Case
id (`caseId`) and return its `t.` Case/PO. Each action calls the same Core
query or command staff use, supplies the resolved Automation identity rather
than caller-provided actor data, and keeps Triage distinct from Unidentified.

Assignment names a selected staff assignee, separate from the acting principal.
An actor-relative `Assign to me` is not part of the Automation contract and
is not offered to it.

### AI job and estimate tools

The Actor's inventory includes the AI job ledger tools from
[ADR-0035](../adr/0035-ai-job-ledger.md). Job kinds and states are owned by
[FRD-27 AI Job List](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list).
Every tool calls the same Core command as the staff application, supplies the
resolved Automation identity and the connecting client's name, needs an
operation key and expected version on every change, and records permanent
history. The ADR-0031 kill switch and attribution rules still apply: a
stopped automation client is refused before any tool runs.

| Tool | Scope | Action |
| --- | --- | --- |
| `pegasus_ai_job_list` | `automation.jobs` | List jobs by state and kind; a client sees every queued job and its own taken jobs |
| `pegasus_ai_job_create` | `automation.jobs` | Create a job of a catalogued kind for a named record; the only way an external scheduler starts an Unidentified-queue pass |
| `pegasus_ai_job_take` | `automation.jobs` | Claim one queued job under a bounded lease held by the client's name; refused when the job is not queued or the kill switch is on |
| `pegasus_ai_job_progress` | `automation.jobs` | Renew the lease and record a short progress note; refused after cancellation or lease expiry |
| `pegasus_ai_job_complete` | `automation.jobs` | Complete MarketResearch after its retained Case files are attached; other proposal kinds become `Draft ready`, naming their result |
| `pegasus_ai_job_fail` | `automation.jobs` | Mark the job `Failed` with a reason |
| `pegasus_ai_job_release` | `automation.jobs` | Return a taken job to `Queued` before the lease ends |
| `pegasus_estimate_save` | `automation.assessment` | Save an AI-draft estimate on a Case; must cite the Estimate job it fulfils and always lands as `Draft` |
| `pegasus_estimate_list` | `automation.assessment` | List a Case's estimates with their state and source |
| `pegasus_estimate_import` | `automation.assessment` | Import one retained raw estimate through the canonical Core command using its name, Case and document occurrence/version identities, SHA-256, typed actor, expected Case version, edit lease and operation key; return the estimate identity or the same structured refusal as the Case caller |

`pegasus_estimate_import` and **Import estimate** on the Repair Spec section
are two callers of one Core command. Both use the same parser types, the same
fail-closed provider detection, the same provider-plus-sequence Draft naming
and the same replay rule. The caller does not choose a trusted provider
route. Even a source-hash replay needs the current actor, version and lease
authority and the exact retained source tuple. An unsupported estimate
document is refused without OCR or partial rows. The import stays an
unconfirmed Draft with no AI job reference and cannot become Current through
MCP. These contracts do not prove live provider acceptance.

**Scopes.** `automation.jobs` is its own scope with a consent description on
the Administrator consent page; a token without it cannot see the ledger. The
estimate tools stay under `automation.assessment` because they write
assessment values. `pegasus_estimate_save` takes AI drafts only: an estimate
without a job reference, or naming a job not taken by the calling client, is
refused. `pegasus_estimate_import` names a retained PDF, XML or JSON file for
shared extraction and needs no AI job reference. `automation.mail` is granted
today without a consent description; it must have one before any connector is
consented to it. Every tool is proven under the tranche rule above.

## States and transitions

The Actor changes no state of its own. Each tool moves the record it acts on
through that record's owner: Unidentified and intake in FRD-02, Triage in
FRD-03, Cases in FRD-13, AI jobs in FRD-11.

## Edge cases and fail-closed behaviour

- A revoked, expired or wrong-scope token fails before the tool runs.
- A foreign or oversize cursor, export ticket or document request gets a
  non-disclosing failure.
- A stopped automation client is refused by the kill switch.
- A generic assessment update that names a finding, a Case-owned or derived
  field, or a field no Case section records is refused, naming the field, and
  writes nothing.
- Missing production signing or encryption keys fail closed.

## Acceptance evidence

Each tool tranche is proven with a real caller: success, authorisation
failure, validation failure and the resulting history line. Integration
tests cover the automation ingress over real HTTP. Deployment and live
acceptance are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `MCP-01`–`MCP-05` in [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-03](frd-03-triage.md),
  [FRD-05](frd-05-documents-extraction-and-custody.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md).
- Technical constraints:
  [ADR-0011](../adr/0011-restrict-mcp-to-automation-actor.md),
  [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md),
  [ADR-0035](../adr/0035-ai-job-ledger.md).
