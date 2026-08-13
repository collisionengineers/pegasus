# FRD-10: MCP automation and actor boundary
> Owner capabilities: MCP · Migrated from docs/requirements.md · UI behaviour: docs/design.md

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

MCP registration, a tool schema, or an endpoint file is not proof. Each tool
requires an exercised real caller, expected success result, authorization
failure, validation failure, and action-history proof.

Background automation follows the same rule. Queues and timers transport stable
work identities; Core owns transitions and idempotency. Poison work remains
recoverable and observable. No AI proposal or workspace service can mutate case
state directly.
