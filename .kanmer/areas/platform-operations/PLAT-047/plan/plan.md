# Plan — PLAT-047

Docs-only. Diff estimate: ~70 lines added / ~5 changed across two files
(`docs/frd/frd-01-case-identity-and-lifecycle.md`,
`docs/frd/frd-04-parties-accounts-and-access.md`). Plan is shorter than the
diff.

## Premises verified (read-only)

- `CaseLifecycleState` (Core `Workflow/CaseWorkflowContracts.cs`): NotReady,
  Held, Review, ReportPreparation, PostReport, PostReportComplete,
  ProviderCancelled, CollisionEngineersRejected, CreatedInError,
  SourceEmailUnlinked — unchanged by this ticket.
- `CaseLifecycle.cs`: `LinkReportEvidence` moves Report preparation → Post
  report on a retained approved-mailbox Sent item; `CloseCase` is the separate
  closure; `ReopenCase` to ReportPreparation requires an assigned Engineer.
- `AssessmentAccessPolicy.CanOpen` today allows Review or ReportPreparation
  once exported — D11 tightens this to With Engineer onwards (FRD wording only
  here; code change belongs to the Assessment lane).
- `CaseContracts.cs` / `OrganizationAdministration.cs`: Organization 1—*
  Principal; CreateOrganization, CreatePrincipal, ReplacePrincipal,
  UpdatePrincipalEvaSubmission exist.
- FRD-09 §Provider API principal and contract boundary (API-01..04) and FRD-07
  §Direct EVA API submission / ADR-0034 exist for citation.

## Operator-notes quotes (protected — read, not edited)

- Post-report (line ~202): "A retained acknowledgement, source receipt,
  outbound message record, or `Report sent` event is not post-report
  completion. Report sent enters post-report work; the separately named,
  reasoned closure outcome ends it."
  → The brief's item 1(c) says a detected report send "completes the Case".
  That contradicts this statement, the existing FRD-01 rule and Core
  (`LinkReportEvidence` enters Post report; `CloseCase` is separate). D10 itself
  says only "auto-links / detected and attached; no manual assertion".
  **Stopped on the "completes the Case" part**: the FRD records evidence-driven
  detection and auto-linking that enters Post report (displayed as
  "With Engineer" under D3); `Post-report complete` stays the separate reasoned
  closure. Reported to the orchestrator.
- Assessment (line ~559): "The Assessment workspace is unavailable while a
  case is `Not ready`. It opens only after a successful EVA export in the
  current Review cycle." → D11 keeps "only after export" (Send to EVA is the
  export and carries Engineer assignment) and adds "never while in Review".
  Not read as a contradiction; flagged in the report for the operator.
- Parties (line ~348): "The operator decisions distinguish reusable
  organisation identity from the function that organisation or person performs
  on one case." → D2 keeps the organisation as the reusable identity and
  case-party owner; only the administration surface merges. No conflict.
- Staff roles (line ~400): Administrator work includes "account
  creation/disable/access review/role assignment; principals; workflow
  configuration; approved Outlook mailbox allowlist". → Access review remains
  an Administrator action and a permanent record; only its separate page is
  retired. No conflict.
- No operator statement names a UI stage label; "Not ready"/"Ready" (line
  ~501) describe the Excel holding log and are unaffected.

## Steps

1. FRD-01: add "Workflow display labels" (D3), Assessment access (D11),
   evidence-driven report-sent detection (D10, without auto-completion), Return
   to Engineer, Send to EVA carrying Engineer assignment (cite FRD-07/FRD-08).
2. FRD-04: add "Principals administration" (D2, D8 settings dialog citing
   FRD-09/FRD-07/ADR-0034), "Staff accounts" table behaviour, and "Action
   Logs" replacing the Access review page; keep the matrix rows.
3. `pwsh ./scripts/Test-DocumentationLinks.ps1`; commit; PR to `dev`.

## Out of scope

Core enum, `AssessmentAccessPolicy`, FRD-07's two-flag EVA policy wording vs
the three-valued dialog (reported), Presentation label code.
