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
