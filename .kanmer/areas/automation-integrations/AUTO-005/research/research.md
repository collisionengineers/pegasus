# Research — AUTO-005: Triage parity for the Automation Actor

## Question

Is Automation access to Triage an unresolved policy choice, or does the accepted Automation Actor architecture already require staff/Automation casework parity; and which existing Triage capabilities must be exposed through the shared Core owners?

## Findings

- This is not an open authority decision. ADR-0011 requires MCP tools to call the same Core use cases as Web/Worker and prohibits a second policy engine. ADR-0021 records the operator decision that `ActorKind.Automation` holds exactly `PerformCasework` and that Pegasus owes a comprehensive toolset with logging parity. `StaffAuthorization` implements that rule: `PerformCasework` accepts Staff or Automation, while staff-application login, administration, credentials, system work, and request-upload rights remain distinct or denied.
- “Parity” means the same ordinary casework capability and Core guards, not impersonating a staff identity or copying UI mechanics. ADR-0011 explicitly says the Automation Actor has its own durable identity and ordinary staff have no MCP access.
- Triage is ordinary casework under FRD-03. It has no MCP tool class, composition registration, tool-inventory entry, scope-specific ingress test, or capability row. This is a missing Automation caller, not a missing Triage business implementation.
- Staff list/detail already use the Core `IListTriage` and `IGetTriage` use cases. Both accept `ActionActor` and call `StaffAuthorization.Require(...PerformCasework)`, so Automation is already authorised at the Core boundary.
- The Triage detail page calls the existing command interfaces directly: `IAssignTriage`, `IUnassignTriage`, `IAwaitTriageInformation`, `IRecordTriageFinding`, `ISupersedeTriageFinding`, `ILinkTriageResponseEvidence`, `IUnlinkTriageResponseEvidence`, `ICompleteTriage`, `ICancelTriage`, `IReopenTriage`, `ILinkTriageCase`, and `IUnlinkTriageCase`. The commands own state, reason, version, replay, evidence, and mutability rules; MCP must call them rather than duplicate those rules.
- Case link/unlink already carries a typed `ActionActor`, requires `PerformCasework`, and uses the normal Case edit lease/version boundary. The Automation caller can reuse the existing Case get/lease tools before invoking the Triage association command.
- Most other Triage mutation request records predate the typed actor boundary and carry an actor subject string. Their Core commands validate identity text, operation key, reason, version, and state but do not independently call `StaffAuthorization`. The Web page supplies a resolved staff identity; an MCP adapter must supply only `AutomationActorResolver`'s subject and must retain the normal scope/auditor boundary. This is legacy contract shape, not permission to accept a caller-supplied actor.
- The staff assignment affordance is specifically “Assign to me” and persists a staff GUID. The Automation Actor is deliberately not a staff GUID and must not impersonate one. Adding an “assign Automation” value would change FRD-03's assignee meaning; accepting an arbitrary staff id would be broader than the current Web caller. Assignment/reassignment is therefore not part of parity unless the Web/Core contract is separately changed. Unassign is also kept out of this surface so Automation does not manage staff ownership while acting as a separate principal. Automation can still perform every Triage business transition because the Core lifecycle does not require assignment.
- The in-scope parity inventory is:
  - list by state and exact detail/history/evidence;
  - retrieve the retained origin receipt/source through the same `IGetIntake` and `IDownloadIntakeSource` owners used by staff;
  - mark Awaiting information;
  - record and supersede a reasoned Triage finding;
  - link/unlink exact approved-mailbox response evidence;
  - complete, cancel, and reopen with existing state/evidence/reason guards;
  - link/unlink a Case through the existing Case lease/version contract.
- FRD-03 does not make Triage findings staff-Engineer-only. ADR-0021's staff-Engineer-only exclusion concerns professional Case-assessment finding confirmation in FRD-11, not the distinct Triage roadworthiness/assessment finding. Triage findings are therefore in scope for ordinary casework parity.
- Source retrieval is part of useful parity: `TriageOrigin.ReceiptId` identifies the retained intake receipt. Existing `IGetIntake` and `IDownloadIntakeSource` already accept Automation under `PerformCasework`; no Triage-specific content store or second download policy is needed.
- The appropriate scope is the existing `automation.intake`: Triage is a pre-Case intake workflow and no second scope or registry is justified.
- AUTO-004 and AUTO-005 are one implementation unit by operator direction. Keep `UnidentifiedMcpTools` and a new typed `TriageMcpTools` separate because the domains/states differ, but register, test, document, review, and deliver them in the same branch/worktree/PR.

## Implications

AUTO-005 should no longer be treated as a decision spike in substance. Its research supplies the Triage half of AUTO-004's combined implementation plan. Build a thin typed `TriageMcpTools` adapter over the existing queries/commands, use `automation.intake`, never accept actor identity from tool input, and prove parity through the real `/mcp` caller and permanent Automation action history.

Do not add a generic queue-action envelope, duplicate Triage state/evidence rules, introduce an Automation assignee, accept arbitrary staff assignment, or conflate Triage with Unidentified. No new ADR is required: ADR-0011 and ADR-0021 already decide the mechanism and access boundary.

## Open questions

None. The operator confirmed parity, and the accepted ADRs/Core authorization establish the boundary. Staff-only assignment identity remains excluded because the Automation Actor must not impersonate staff or gain a broader assignment API than Web.
