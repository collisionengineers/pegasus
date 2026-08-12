# Rework `/Received` as information and management

## Summary

Replace the mailbox/receipt duplicate at `/Received` with a staff-readable `Received` information screen. It will show only usable chaser upload links, API-submitted files with their resulting case links, and successful API writes. It will have no approval, retry, withdrawal, or other mutation controls.

Move the present `/Operations/Requests` management functionality to `/Received/Manage`, including upload-link withdrawal and external-work retries. Remove `/Operations/Requests` so it returns 404.

## Implementation changes

- Add a Core-owned, read-only received-information projection and EF query:
  - active Pegasus upload links only: `Active` and not expired;
  - Automation-channel file receipts with processing/allocation state and any linked Case;
  - successful Automation API writes only, classified from the known mutating MCP operations; exclude reads, denials, failures, correlation IDs, raw targets, and credentials.
  - Use independent, bounded pagination for the three lists; no migration or new persistence store.

- Rebuild `Pages/Intake/Index` as `/Received`:
  - sections: `Active upload links`, `Files received via API`, and `API activity`;
  - retain only informational links to safe Case/receipt detail routes;
  - remove Received/Sent tabs, mailbox filters, email failures, retry handler, mail-operation dependencies, and all email/receipt list UI.
  - label the primary navigation item `Received`, keeping Inbox as the separate email workspace.

- Transfer the existing `Operations/Requests` Razor PageModel and all management handlers into `Pages/Intake/Management` at `/Received/Manage`; retain existing authorization, antiforgery, leases, withdraw, and retry behaviour.
  - Delete the old Operations Requests page so `/Operations/Requests` is an actual 404.
  - Update the dashboard drill-down and accessibility/navigation references to the new management route.

- Update architecture, design, and operations documentation to state that `/Received` is a read-only staff observability view of upload/API outcomes; this does not expose an API in the browser or grant staff API access.

## Test plan

- Add focused Core/persistence tests proving:
  - only usable upload links appear;
  - only Automation-origin receipts appear, with linked-case state;
  - only successful, classified Automation writes appear; reads, failures, denials, identifiers, and unrelated audit records do not.

- Replace `/Received` web assertions that expect mailbox rows, filters, or retry forms with information-screen assertions.
- Move the existing request-management tests to `/Received/Manage`; assert every inherited handler preserves PRG and authorization behaviour.
- Assert `/Operations/Requests` returns 404, dashboard/navigation use the new route, and both pages meet existing accessibility coverage.
- Run the repository’s locked restore, Release build, and focused Web/Core test profiles before full-suite verification.

## Assumptions

- “API” means the existing Automation/MCP ingress, not a deferred provider API.
- A Case is shown only when it is the actual allocation/link outcome of an Automation receipt; Pegasus has no direct API case-creation endpoint.
- “Links sent as chasers” means currently usable chaser upload links. The screen will not claim a link was delivered, because current chasers are manually copied and do not prove delivery.
