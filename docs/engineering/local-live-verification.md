# Local live-integration verification matrix

The hosted local test instance runs the `DevelopmentOffline` lifecycle with the
opt-in features described in the [configuration reference](configuration.md#local-opt-in-features)
and the [runbook's local live-integration run](../runbook.md#local-live-integration-run).
This matrix names what each row proves there, how it is driven, and what
remains outside local proof. Capability identities come from the
[capability inventory](../capabilities.md); the owning FRD states the
behaviour. A green row is local evidence for that flow, not acceptance.

`Invoke-LocalVerification.ps1` runs the scripted rows against one running run
and writes `artifacts/local-verification/<run-id>/result.json` plus one
screenshot per step. A row whose flag is off is recorded `skipped`, never
`passed`. Manual rows are walked in a browser on the same machine and recorded
in the same folder by the operator.

## Scripted rows

| Capability | Flow | Driver step | Passes when | Needs |
| --- | --- | --- | --- | --- |
| OPS health | Web readiness and version | `health`, `version` | `/health/ready` is 200 and `sourceSha` equals the manifest | run `Running` |
| ACC-01, ACC-04 | Password sign-in and the anonymous challenge | `sign-in` | anonymous `/Cases` redirects to `/Account/SignIn`; the Administrator signs in and receives `__Host-Pegasus` | `Features__PasswordSignIn` |
| ACC-02, ACC-04 | Role matrix | `administration` | `/Administration` is 200 as Administrator and refused as Engineer or User | accounts created in Administration > Accounts |
| UI-02 | Work Centre and Case queues | `work-centre`, `cases-list` | pages render with the queue sections | — |
| INT-26, CASE-02 | Manual Case creation | `create-case` | the new Case opens with its reference | — |
| INT-01, UI-08, INT-19 | Manual upload and the confirmation surface | `upload`, `received-list` | the uploaded source appears in Received with its draft fields | files passed with `-UploadFiles` |
| CASE-01 | Case record | `case-details` | the Case page renders its workspace sections | a Case |
| EXT-01, EXT-02 | Live DVLA/DVSA lookup | `vehicle-lookup` | the request is recorded as `production_live` and the Worker returns vehicle data within the poll window | `Features__LiveVehicleLookup`; a real registration |
| DOC-01, DOC-02, DOC-03, INT-09 | Box custody of the Case folder and filed artifacts | `documents` | the Documents page lists the filed artifact and the Case folder exists under `425169015650` | `Features__LiveBoxCustody` |
| EXT-06 | Glass's control availability | `glass-control` | the Launch control is present for an account holding a Glass's credential when `Features__LiveGlass` is on, and absent otherwise | per-account credential (ADR-0043) |
| API-01, API-02 | Principal API surface | `principal-api` | an unauthenticated submission is refused with 401 and the route exists | `Features__PrincipalApi` |
| MCP-01 | Automation MCP ingress | `mcp` | the token endpoint exists and refuses an anonymous call | `Features__AutomationMcp` with development keys |

## Manual rows

| Capability | Flow | Passes when | Notes |
| --- | --- | --- | --- |
| EXT-06, ENG-01 | Glass's launch, estimator, return and import | the export lands on the Case as the current estimate and the file is filed under the Case folder in Box | needs the staff member's own browser on this machine with the dev certificate trusted; cannot run headless |
| EXT-12 | Estimate PDF or XML import | the parsed estimate matches the source | needs real estimate files from the supplied corpus |
| INT-02, MAIL-01, INT-23 | Mailbox-folder ingestion and Unidentified routing | `.eml` files seeded into `<run>/mailbox/inbox` become Received items, matched Cases or Unidentified items | folder adapter, not Graph; see seeding |
| MAIL-14 | Sent-item evidence | `*.sent.json` seeded into `<run>/mailbox/sent` attach as report-sent evidence | folder adapter |
| INT-13, INT-17 | Image intake and plate reading | uploaded vehicle photos read their registration and group | in-process ONNX |
| TRI-01 | Triage Case lifecycle | a Triage Case walks its states | the outcome reply cannot send |
| RPT-01, RPT-03 | Report rendering | the Inspection and Audit PDFs render and download | in-process renderer |
| CASE-28, ENG-01 | Assessment findings and repair specification | the Engineer workbench saves and the report reflects it | — |
| ACC-03, ACC-15, ACC-05, ACC-07, ACC-08, UI-11 | Administration workspace | accounts, Contacts, mailbox allowlist and configuration save | local mailbox resolver stands in for Graph |
| OPS-26 | Release notes | a published note is shown once | — |
| OPS-27 | Report a problem | the report is kept as Not sent and can be retried | the GitHub sink is deliberately unconfigured locally |
| MI-01 | MI reports | figures reflect the seeded data | needs data in the database |

## Not provable locally

| Area | Why | Owner |
| --- | --- | --- |
| EVA API submission, automatic Review intents | no EVA route is composed offline by decision; intents stay Pending and the Case offers export only | [FRD-07](../frd/README.md) |
| Staff mail send, report send, Triage reply | `Unavailable` offline until a Graph test mailbox is approved | [FRD-21](../frd/README.md) |
| Graph subscriptions, webhooks, folder move, deleted-mail search | folder adapters replace Graph | [FRD-26](../frd/README.md) |
| OCR (INT-16) | Document Intelligence needs the Worker managed identity | [ADR-0040](../adr/0040-qualified-document-intelligence-ocr.md) |
| Problem-report issue creation | a real token would file issues in the production repository | [ADR-0055](../adr/0055-github-issues-as-the-problem-report-sink.md) |
| Durable cloud behaviour, RBAC, deployment | the local run proves the process graph and these flows only | [runbook](../runbook.md#status-and-smoke) |
