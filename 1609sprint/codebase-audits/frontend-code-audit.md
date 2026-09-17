\# Frontend / Web boundary audit — plan



\## Context



Alex asked: "is there any JavaScript that performs functions that should be

backend? Is Web owning anything that it should not?" Governing rule

(CLAUDE.md, `docs/current-architecture.md:20-22`): Core owns business policy

and ports; Infrastructure implements ports; Web and Worker only compose.



Three read-only sweeps ran (all 11 `wwwroot/js` files in full; every Web page

model and `Presentation/` type; every non-page `.cs` in Web plus `Program.cs`,

the csproj and `tests/Pegasus.ArchitectureTests`). The high-severity claims

below were re-verified by hand against source. No writes have been made.



Deliverable: a written audit under `artifacts/audits/2026-09-16/` (ignored

path, same shape as the 2026-09-11 audits, so no Markdown placement gate), plus

— if Alex chooses — remediation of the ranked highs and guardrail tests.



\## Headline answer



\*\*JavaScript: clean.\*\* No policy is enforced or derived only in the browser.

Every mutation is a real form/handler; the server re-validates version, lease

and every value; JSON endpoints return pre-labelled display strings; no

authorization decisions, no unsafe redirects, no innerHTML of user data. The

only findings are duplicated vocabulary/tables and one no-script gap.



\*\*Web C#: not clean.\*\* Web is a second policy owner in several places and an

Infrastructure adapter in a few more. The MCP surface and most Case capability

pages are thin and correct; the leakage concentrates in `Details.cshtml.cs`,

`Mail/\*`, `Triage/Details`, `Create`, `Presentation/UploadCaseDecision.cs`,

`GraphMailWebhook.cs` and `Mcp/\*` auth plumbing.



\## Verified findings, ranked



\### HIGH — outcome-changing policy that exists only in Web



| # | Finding | Evidence | Core home |

|---|---|---|---|

| H1 | "Only an Engineer can generate or deliver reports" is Web-only. Core `GenerateCaseReport` requires only `PerformCasework`. | `Pages/Cases/Details.cshtml.cs:1890,1938`; `Core/Reports/CaseReportGeneration.cs:664`; no Engineer check in Infrastructure | `CaseReportGeneration` — add `RequireEngineer` like `RepairSpecifications.RequireEngineer` (`Core/Assessment/RepairSpecifications.cs:97`) |

| H2 | Automation scope authorization exists only in the Web MCP wrapper. `ActionActor.Automation(grantId)` carries no scopes, so Core cannot refuse an automation actor by scope. | `Mcp/AutomationActorResolver.cs:60-71`; `Mcp/AutomationMcp.cs:30-42` | Core `AutomationScope` + `AutomationAuthorization.Require(actor, scope)`; Web maps token claims → actor scopes |

| H3 | Automation is blocked from writing estimate-owned and signatory assessment fields only by the MCP tool. Core `NormalizeWritableField` has `DerivedPaths`/`CaseOwnedPaths`/`AdoptedFindingPaths` gates but no estimate/signatory set. | `Mcp/AssessmentMcpTools.cs:442-491`; `Core/Assessment/AssessmentPolicy.cs:103-131` | Add `AssessmentVocabulary.EstimateOwnedPaths` / `SignatoryPaths` and refuse in `AssessmentPolicy` per actor kind; delete the Web check |

| H4 | Partial-update ("null = keep confirmed; contact number/address never patchable") merge policy for case details lives in the MCP tool; `ISaveCase` has no other caller. | `Mcp/AssessmentMcpTools.cs:546-574`; `ISaveCase` callers: only that file | Core `PatchCaseDetails` use case owning the merge |

| H5 | "Which Approved mailbox may staff-send" is written four ways in Web and once privately in Core. Triage's version has 4 conditions (no SentEvidence, identity, activation or size limit) vs 8–9 elsewhere → the Triage chaser can offer a mailbox Compose refuses. | `Pages/Triage/Details.cshtml.cs:670-674, 1014-1018`; `Mail/Message.cshtml.cs:1221-1230`; `Mail/Compose.cshtml.cs:364-374`; `Administration/Mailboxes.cshtml.cs:1190-1198`; `Core/Reports/CaseReportDeliveryPreparation.cs:529-534` (private) | Public `ApprovedMailboxPolicy.CanStaffSend(mailbox)` in `Core/Identity`; all five call it |

| H6 | Upload-to-case attach is a full use case in `Presentation/`: SHA-256 operation-key derivation, per-member version arithmetic (`expectedCaseVersion + index`), lease+link loop, then reconcile; a mid-loop failure leaves a group partially attached. | `Presentation/UploadCaseDecision.cs:275-383, 437-576, 605-617` | Core `AttachUploadGroupToCase` owning key derivation and atomicity |

| H7 | Case creation from reviewed intake is a three-write Web sequence with a Web-owned version chain and store-port call; `CanBecomeCase` is extended by hand (`OcrRequired \\|\\| NeedsSorting`) in three Web files. | `Pages/Cases/Create.cshtml.cs:352-436, 783-786, 888-891`; `Presentation/UploadOutcome.cs:293-295, 373-374`; `Pages/Unidentified/Details.cshtml.cs:86` | Core `CreateCaseFromReviewedIntake`; `IntakeDecisionPolicy.CanBeReviewedIntoCase` |

| H8 | `GraphMailWebhook.cs` is a Microsoft Graph adapter in Web: validation handshake, clientState/tenant checks from raw `IConfiguration`, resource-string parsing whose format is \*produced\* in `Infrastructure/Email/GraphMailboxChangeSubscriptions.cs:79-80`, lifecycle-event mapping. | `src/Pegasus.Web/GraphMailWebhook.cs` (whole file) | `Infrastructure/Email/GraphChangeNotificationParser` fed by `GraphApprovedMailboxOptions`; Web endpoint becomes parse → lookup → enqueue |

| H9 | `Mcp/KeyVaultOAuthCertificateLoader.cs` is a hand-rolled Key Vault REST client (static `HttpClient`, sync-over-async at `:84`). | whole file | Infrastructure adapter behind a small port, beside `GraphMailboxChangeSubscriptions` which already does token+HttpClient properly |

| H10 | `Program.cs` builds `DefaultAzureCredential`, `BlobServiceClient`, `QueueClient` inline and owns the literals `"transient-intake"` / `"intake-work"`. Worker has a tested `WorkerAzureClientFactory`; Web has none. | `Program.cs:192-225, 255` | Shared Infrastructure Azure client factory used by both hosts |



\### MEDIUM — duplicated with Core, or a Web variant that can drift



\- M1 Estimate import: Web sequences retain → `expectedVersion + 1` → lease → import; `estimate-import:` prefix parsed back in Web with a different meaning than Core's (`Details.cshtml.cs:341, 2905-2980`; `Core/Assessment/EstimateImport.cs:171`); 10 MB estimate size limit Web-only (`:330`).

\- M2 Lease held across two Core calls, five copies: `Mail/Message.cshtml.cs:621-680`, `Triage/Details.cshtml.cs:685-797`, `Pages/Index.cshtml.cs:554-612`, `Operations/Index.cshtml.cs:363-400`, `Unidentified/Details.cshtml.cs:170-207`.

\- M3 Read-only/editability restated beside the Core call: `Details.cshtml.cs:463-468, 1334-1340` vs `AssessmentAccessPolicy.IsReadOnly`; `Details.Frame.cs:60-68` `CanCreateAudit` vs `CreateAuditCase.cs:157-189`; `Eva/Send.cshtml.cs:70-75` state list; EVA "offer retry" composed twice (`Details.cshtml.cs:3124-3152`, `Eva/Send.cshtml.cs:97-118`).

\- M4 Engineer roster re-derived with a silent 100-account cap in `Pages/Index.cshtml.cs:489-493` and `Details.cshtml.cs:3113-3118` while `Triage/Details.cshtml.cs:204` correctly uses Core `ICaseEngineerChoices`.

\- M5 Mail composition rules (reply recipients, own-address exclusion, `MailAddress` validation, Re:/Fwd:) in `Mail/Message.cshtml.cs:1342-1404` and duplicated in `Triage/Details.cshtml.cs:1241-1276`; `Compose.cshtml.cs:353-357` skips validation; `IsActiveOperation` and `retained:{id}:{guid}` key convention duplicated.

\- M6 Damage derived-cells rule disagrees with Core: `AssessmentPolicy.DeriveImpactValues` says `Count > 1 → "multiple"`; `\_CaseDamage.cshtml:49-57` and `case-workspace.js:1041-1066` say distinct headline locations > 1. \*\*Verified.\*\*

\- M7 Report-generation policy composed only by JS: `Contacts/Edit.cshtml:71` selects have no `name`; `contacts-edit.js:70` writes the hidden enum; no-script path cannot change it. \*\*Verified.\*\*

\- M8 EF/Azure exception types caught in pages: `Operations/Index.cshtml.cs`, `ImageIntake/Details.cshtml.cs`, `Administration/Glass/Index.cshtml.cs`, `Uploads/Request.cshtml.cs:205`, `Documents/Download.cshtml.cs:460`. Infrastructure should translate to Core exceptions (as `AzureBlobIntakeArtifactStoreTests` already demands for one adapter).

\- M9 Glass's port types live in Infrastructure and are consumed by pages (`Details.cshtml.cs:23-24`, `Integrations/Glass/Callback.cshtml.cs`); pages type-test `UnavailableStaffMailSend` for feature availability.

\- M10 MCP: allocation-status → code switch written 3× (Core `IntakeAllocation.cs:97-110`, `IntakeMcpTools.cs:189-197`, `Mail/Message.cshtml.cs:1939`); cursor protect/unprotect in-tool for Mail/AiJob only (`MailMcpTools.cs:163-179`, `AiJobMcpTools.cs:87-104`); 20 MiB export bound written twice in Web; `"mcp:"`/`"automation:"` identity formats Web-only; "only UnidentifiedQueuePass may be created by automation" (`AiJobMcpTools.cs:134-138`); kill-switch state encoded as an OpenIddict permission and `IAutomationIngressStatusQueries` implemented in Web; audit event-name vocabulary defined in Web and read by Core.

\- M11 `Program.cs`: Identity password policy (`:355-367`), must-change-password path allow-list (`:1104-1128`), `LimitsVersion == AcceptedLimitsVersion` (`:262-298`), `PegasusDbContext` transaction + `Version++` (`:1296-1300`); OpenIddict `UseDbContext<PegasusDbContext>` in `Mcp/AutomationMcpExtensions.cs:37-40`; `IDbContextFactory` in `Health/` and `Authentication/`.

\- M12 JS duplicated tables: VAT defaults (`case-workspace.js:1404` vs `Estimates.cs:52-58`), sign-off role allow-list inverted vs Core deny-list (`accounts.js:48`, `Accounts/Index.cshtml:112`, `StaffAccountAdministration.cs:630`), default severity `'moderate'`, previewable-media rule (2× JS, 2× C#), inspection-mode sentinel (`site.js:805-819` vs `CaseDataPolicy.InferInspectionMode`).

\- M13 Other Web-only rules in `Details.cshtml.cs`: empty settlement control over an Automation proposal = "not decided" (`:1376-1380`); `IsOverride` VAT decision (`:2710-2713`); `CaseWorkspaceLabels.Editors.IsAssessmentField` used as a write gate (`:1301-1303`); closure-outcome chooser probes Core with a placeholder request (`:651-706`); guide month parsed from AI job free text (`Details.Valuation.cs:43-55`); `IsCaseImage`/`IsViewable`/`IsInlineSafe` media policy twice.



\### LOW

`ChannelAiHandOffTransport` (a correct port impl in the wrong project); `UnidentifiedMcpTools` composing a store query; `localStorage` working set holds registrations; hardcoded `site.js:211` 60 s heartbeat; magic strings in `OperatorLabels`; required-keys list duplicating options validation; `"alex"` bootstrap literal.



\### Guardrail gaps in `tests/Pegasus.ArchitectureTests`

`DependencyDirectionTests.cs` enforces Core's isolation and project-reference direction only. Nothing forbids Web from: referencing `PegasusDbContext`/`IDbContextFactory`/`Microsoft.EntityFrameworkCore`/`Azure.\*`; importing `Pegasus.Infrastructure.\*` outside `Program.cs`/DI extensions; implementing Core ports (`IAiHandOffTransport`, `ICursorProtector`, `IAutomationIngressStatusQueries` are implemented in Web unnoticed); calling `\*Store` write ports directly; using `HttpClient`. Worker's `DefaultAzureCredential` exclusions are tested; Web's are not.



\### Done right (for contrast)

`Workflow/Closure/Tasks/Custody/Vehicle.cshtml.cs` (thin `ExecuteCaseCommandAsync`); `Upload.cshtml.cs` (Core limits/file policy); `Details.Valuation.cs` (arithmetic in Core); every MCP tool's resolve → audit → Core use case → map shape; `AutomationMcpErrors` translating Core exceptions; `ProviderApiEndpoints` deferring to `ProviderSubmissionPolicy`; `DataProtectionCursorProtector`; JS sending server-issued facts only.



\## Work (Alex chose: audit report only — no code changes)



Write one file: `artifacts/audits/2026-09-16/web-boundary-audit.md`, in the

shape of `artifacts/audits/2026-09-11/architecture-audit.md` +

`improvement-backlog.md`:



1\. Header: read-only audit, source root = primary checkout on `dev` @ current

&#x20;  HEAD (`9c4c09eb7`), no writes other than the file.

2\. \*\*Headline answer\*\* (the two paragraphs above).

3\. \*\*Ranked findings H1–H10\*\* in the A-n prose format: Finding / Evidence

&#x20;  (`file:line` + short excerpt) / Core home / Proposed change / Proof of no

&#x20;  functional change (which existing test file covers it, or "needs new test:

&#x20;  X").

4\. \*\*Medium M1–M13\*\* and \*\*Low\*\* as compact tables.

5\. \*\*Done right\*\* section (so future work copies the correct pattern).

6\. \*\*Guardrail gaps\*\* in `tests/Pegasus.ArchitectureTests` with the concrete

&#x20;  rules a `WebBoundaryTests` class would assert and today's allow-list.

7\. \*\*Backlog table\*\* (ID / Src / Change / Files / Proof / Eff / Value / Rank)

&#x20;  covering every H and M item, sized S/M/L. Recommended first tranche

&#x20;  (smallest blast radius, existing Core pattern to reuse): H1

&#x20;  (`RepairSpecifications.RequireEngineer` pattern), H3

&#x20;  (`AssessmentVocabulary.\*Paths` pattern at `AssessmentPolicy.cs:106-122`),

&#x20;  H5 (`ApprovedMailboxPolicy.CanStaffSend`), M6, M7, guardrail tests.

8\. \*\*Decisions for Alex\*\* — items where the fix changes behaviour: H1 (a

&#x20;  `User`-role account can currently generate reports via any non-page route),

&#x20;  H5 (Triage chaser will stop offering under-configured mailboxes), H2 (scope

&#x20;  model shape), H4 (whether automation may clear a field).

9\. \*\*Out of scope / sources\*\*: Worker not audited; `.cshtml` markup audited

&#x20;  only where a finding pointed at it; corpus untouched.



Re-verify every `file:line` cited in the H section against the working tree

while writing (the M/L lines come from the sweeps and were spot-checked: M6,

M7, H1, H3, H5 confirmed by hand already).



\### Verification

Prose-only in an ignored path: no build/test, no placement gate. Check: every

H citation opens to the quoted code; the file renders (headings, tables); no

`corpus/` reference; nothing written outside `artifacts/audits/2026-09-16/`.



