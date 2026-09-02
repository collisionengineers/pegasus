# FRD-10: MCP automation and actor boundary

## Unidentified automation contract

Automation may list and look up Unidentified items by exact U reference, including
canonical reason, origin, state, history, and retained source metadata. Receipt
origins expose their exact receipt; submission-group origins enumerate every member
and require an exact member receipt for download. Source bytes use the same
integrity-checked, bounded download owner as the staff application. Any resolution mutation uses the same
Core command as Web and requires an authorised actor, expected version, bounded
reason, and operation key. U references are never accepted where a Case/PO, Audit,
Image Intake, or Principal identifier is required.

## Triage automation contract

Triage is ordinary `PerformCasework`. Automation may list and inspect Triage,
retrieve its retained origin source, mark it Awaiting information, record or
supersede a finding, link or unlink exact response evidence, complete, cancel or
reopen it, and link or unlink a Case through the normal Case edit-lease and version
guards. Each action invokes the same Core query or command used by staff, supplies
the resolved Automation identity rather than caller-provided actor data, and keeps
Triage distinct from Unidentified.

Assignment is an explicit selected-Engineer relationship, separate from the acting
principal. Actor-relative `Assign to me` is not an Automation contract and is being
retired under INTK-019; this tool tranche does not preserve that shortcut or create
an alternative assignment policy.
> Owner capabilities: MCP · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## MCP automation and actor boundary

MCP is a management/development-controlled ingress for one named,
vendor-neutral Automation Actor, not an ordinary staff interface. Ordinary
staff have no MCP access and use the Web UI. The Actor invokes only its approved
ordinary operational Core-action inventory with its own authentication,
identity, and permanent history; it has no Administrator, configuration,
credential, cloud, release, deletion, or other management authority.

An externally scheduled automation client may scan an approved network-drive
scope and submit immutable source occurrences through its approved MCP
document-action inventory. Claude Desktop may provide the initial accepted
client evidence without owning the durable actor identity or Core action. The
client, schedule, and filesystem remain outside Pegasus; custody begins only
with an authenticated accepted MCP submission. Each occurrence follows ordinary
source-occurrence, idempotency, matching, classification, and action-history
policy. Scanning neither associates material nor allocates a Case or reference.

MCP registration, a tool schema, or an endpoint file is not proof. Each tool tranche
requires an exercised real caller, expected success result, authorization
failure, validation failure, and action-history proof.

Background automation follows the same rule. Queues and timers transport stable
work identities; Core owns transitions and idempotency. Poison work remains
recoverable and observable. No AI proposal or workspace service can mutate case
state directly.

## AI job and estimate tools

The Automation Actor's inventory gains the AI job ledger tools decided by
[ADR-0035](../adr/0035-ai-job-ledger.md); the behaviour of jobs, kinds and
states is owned by [FRD-11 § AI Job
List](frd-11-reports-correspondence-and-reviewed-proposals.md#ai-job-list).
Every tool invokes the same Core command as the staff application, supplies
the resolved Automation identity and the connecting client's name rather
than caller-provided actor data, requires an operation key and expected
version on every mutation, and records permanent history. The ADR-0031 kill
switch and attribution rules are unchanged: a stopped automation client is
refused before any tool runs.

| Tool | Scope | Action |
| --- | --- | --- |
| `pegasus_ai_job_list` | `automation.jobs` | List jobs by state and kind; a client sees every queued job and its own taken jobs |
| `pegasus_ai_job_create` | `automation.jobs` | Create a job of a catalogued kind for a named record; the only route by which an external scheduler starts an Unidentified-queue pass |
| `pegasus_ai_job_take` | `automation.jobs` | Claim one queued job under a bounded lease held by the client's name; refused when the job is not queued or the kill switch is on |
| `pegasus_ai_job_progress` | `automation.jobs` | Renew the lease and record a short progress note; refused after cancellation or lease expiry |
| `pegasus_ai_job_complete` | `automation.jobs` | Mark the job `Draft ready`, naming the draft or proposal it produced |
| `pegasus_ai_job_fail` | `automation.jobs` | Mark the job `Failed` with a reason |
| `pegasus_ai_job_release` | `automation.jobs` | Return a taken job to `Queued` before the lease ends |
| `pegasus_estimate_save` | `automation.assessment` | Save an AI-draft estimate on a Case; must cite the Estimate job it fulfils and always lands as `Draft` |
| `pegasus_estimate_list` | `automation.assessment` | List a Case's estimates with their state and source |
| `pegasus_estimate_import` | `automation.assessment` | Import one raw estimate artifact on a Case through the same Core command as the Assessment page drop (D16): takes `case_id`, `expected_version`, `edit_lease_token`, `operation_key`, `file_name`, `media_type` and base64 bytes, and returns the Draft identity, name and status, the replay state, the source hash, the detected parser/provider and structured blockers or errors |

`pegasus_estimate_import` and the Assessment page's drop are two callers of
one shared Core command, not two implementations: the same registered parser
types, the same fail-closed provider auto-detection, the same
provider-plus-sequence Draft naming and the same replay rule apply to both
(D16, 2026-09-01). Allocated to [[ENG-033]] and [[AUTO-016]]; not delivered.

`automation.jobs` is a new scope with its own consent description on the
Administrator consent page; a token without it cannot see the ledger. The
estimate tools stay under `automation.assessment` because they write
assessment values, but they accept AI drafts only: an estimate saved without
a job reference, or naming a job that is not taken by the calling client, is
refused. The existing `automation.mail` scope is granted today without a
consent description; it must carry one before any connector is consented to
it. Each tool is proven under the tranche rule above — real caller, success,
authorization failure, validation failure, and action-history evidence.
