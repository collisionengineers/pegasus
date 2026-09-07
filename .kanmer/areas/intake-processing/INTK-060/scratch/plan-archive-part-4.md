# Archived original plan — part 4 of 4

Original ticket document: `plan/plan.md`
Original SHA-256: `62649b22a7e43d771820d36c4126a65867fc38d99b636c54a20cc5a6468f3a95`
Character range: 90000–115555 of 115556
Reconstruction: concatenate the payload sections from parts 1–4 in order.

## Payload

e evidence | `/Received/{id:guid}`, `/Received/{id:guid}/Source`, `/Received/{id:guid}/Asset/{assetId:guid}`, `/Received/{id:guid}/Image` | C-owned source review/provenance routes; no new mutation appears on Source/Asset/Image. |
| Cases/new Case | `/Cases`, `/Cases/Create`, `/Cases/{id:guid}`, `/Cases/{id:guid}/Closure`, `/Cases/{id:guid}/Custody`, `/Cases/{id:guid}/Tasks`, `/Cases/{id:guid}/Vehicle`, `/Cases/{id:guid}/Workflow`, `/Cases/{id:guid}/Assessment`, `/Cases/{caseId:guid}/Documents/{occurrenceId:guid}/Download`, `/Cases/{caseId:guid}/Documents/Export`, `/Cases/{caseId:guid}/Eva/Send` | B-owned. C supplies shared shell/assets, address query, pre-case links and provenance contracts only. |
| Search | `/Search` -> `Pages/Search/Index.cshtml(.cs)` with `_CasePreview` and proposed `_RetainedMaterialPreview` | C-owned typed cross-record search; server queries remain authoritative. |
| Triage | `/Triage`, `/Triage/{id:guid}` | C-owned. T references remain distinct from Cases. |
| Unidentified | `/Unidentified`, `/Unidentified/{id:guid}` | C-owned retained unresolved-material workflow; supported actions reuse current Core commands. |
| Image-initiated record | `/VehicleImages/{id:guid}`, reached from Work Centre/Search/Cases | C owns record/detail behavior; B owns its Case-list projection. There is no global Images page. |
| Operations | `/Operations` | A-owned operational runtime page. C links external-work attention rows to it and does not copy its handlers. |
| Administration landing/navigation | `/Administration` and `Pages/Administration/Shared/_AdminNav.cshtml` | A owns the landing page/PageModel; C owns the shared admin navigation partial and shell link matrix; each domain page remains with its named owner. |
| Principal/organization directory | `/Administration/Organizations`, `/Administration/Organizations/Edit/{id:guid}`, `/Administration/Principals`, `/Administration/Principals/Create`, `/Administration/Principals/Replace/{organizationId:guid}/{principalId:guid}`, `/Administration/Principals/EvaSubmission/{organizationId:guid}/{principalId:guid}`, proposed `/Administration/ClaimSources` and `/Administration/ClaimSources/Edit/{id:guid}` | C-owned. EVA page exposes manual optional enablement only. |
| Platform administration | `/Administration/Access`, `/Administration/Accounts`, `/Administration/Accounts/Confirm/{operation}/{staffId:guid}`, `/Administration/Accounts/Edit/{id:guid}`, `/Administration/Roles`, `/Administration/Mailboxes`, `/Administration/MailCategories`, `/Administration/Automation`, `/Administration/Automation/Activity`, `/Administration/Configuration` | A-owned; consumes C navigation/assets. Service health stays Administrator-only. |
| Service health and action logs | proposed `/Administration/ServiceHealth` and `/Administration/ActionLogs` | A-owned platform/audit queries and pages; C supplies navigation/assets only. |
| Report administration/case reports | proposed `/Administration/Reports` and the existing `/Cases/**` report actions | A owns Administration Reports; B owns individual Case report generation/actions. C supplies navigation/assets only. |
| Public upload | `/Uploads/{token}` -> `Pages/Uploads/Request.cshtml(.cs)` | C owns token/session policy and presentation/handlers using the external layout; A owns byte storage/custody adapter and B owns Case-side link create/revoke. C07 supplies the fixed 15-minute session and channel limits. |
| Connector authorization | `/authorize` | A-owned identity/MCP boundary; excluded from C behavior changes. |
| Error/status | `/Error` and `/status/{code:int}` | Existing shared error/status pages remain reachable through the C-owned common layouts/assets; C adds no behavior or explanatory workflow. |

**Change:**

1. Port the v3 shell as Razor/shared assets: rail navigation, the single Ctrl+K
   command palette defined above, record tabs, Add menu, refresh/status and
   notifications. Keep native authorization, routes, anti-forgery, freshness
   and server data; do not paste the prototype's in-memory router, fixtures or
   generated IDs.
2. Inbox retains mailbox/folder/search/queue/unread/sort/page in the URL, shows
   message/attachments/classification/association/thread context, and opens a
   full message without losing the workspace state. Mailbox/folder/search/
   queue/unread/sort/page are URL and retained-query state only. Opening,
   previewing, filtering or changing the unread scope never marks read/unread,
   moves, deletes or categorizes a message in Outlook. A's adapter remains the
   only Graph caller.
3. In `Pages/Mail/Message.cshtml.cs`, add POST handlers `OnPostReplyAsync`,
   `OnPostReplyAllAsync` and `OnPostForwardAsync`, mapping explicitly to the
   corresponding S09 compose modes. `Pages/Mail/Compose.cshtml.cs` uses
   `OnPostSendAsync` for `New`. `Pages/Triage/Details.cshtml.cs` adds
   `OnPostSendChaserAsync`: reply to the selected retained instruction, or use
   `New` only when staff explicitly select a new message. Each invokes A's
   `IStaffMailSend` with server actor, approved mailbox, linked Case/Triage,
   purpose, mode, immutable original-message/thread identity when applicable,
   To/CC, subject/body, authorized attachment versions/hashes, expected context
   version, payload hash and operation key. Require antiforgery. Render A's
   canonical S12 state projection; do not define a C send-state vocabulary.
   Provider acceptance remains Submitted until matching Sent evidence exists.
   Reconciliation is an action on an uncertain operation, not a new send state.
   Same-key/same-payload POST replays; a changed payload or stale/unauthorized
   context makes no send. Unknown never triggers a blind retry. B owns its
   Case/report send handlers against the same A transport.
4. Search returns Cases, retained mail, Triage/Unidentified and Image Intake
   with typed identities and previews. A T reference or Image reference is not
   treated as a Case reference. Invalid/stale selections fail predictably and
   query errors return the existing unavailable response.
5. Work Centre continues to derive its counts and attention list from
   `OperationsSnapshot`; preserve distinct Case, held, mail, Triage and external
   work kinds and route each action to its real production page.
6. Populate the shell notification menu from at most 10 current actionable
   `OperationsSnapshot` attention rows, in the snapshot's stable urgency/order,
   with its typed label and valid production link. Zero rows omits the control's
   list content; do not show a fake `No notifications` item and do not add a
   notification table, client-side list or store.
7. Give Stream B stable shared partial/CSS contracts and a merge window before
   B ports Case pages. C does not edit B-owned Case partials. B must consume the
   shared tokens/assets instead of cloning them.

**Tests and expected outputs:** keyboard actions operate only in safe contexts;
record tabs preserve mail/case context; unauthorized links/actions stay hidden
and server-forbidden; Inbox query state survives preview/full-message/back;
opening is read-only; Search returns each record kind with its own reference and
route; Work Centre counts match source queries and has no N+1 growth. Browser
tests cover narrow/wide layouts, focus, Escape, validation, stale concurrency,
empty and failure states. Snapshot generation includes every routed C page and
the catalogue has no broken local asset/link. Test doubles assert zero Graph
write calls for open/preview/filter/unread/sort/folder actions. All execution
and browser verification uses local substitutes and makes no Outlook, Box,
address-vendor, Glass's, EVA or other external call.
Correspondence tests drive the real PageModel handlers with A's recording local
transport and assert one call on success, zero calls on GET/invalid actor/
mailbox/recipient/attachment/stale context, replay on duplicate POST and visible
`Unknown` without resend after an ambiguous outcome. Public-upload browser tests
prove add, replace, finalize, fixed expiry and version-refusal/reissue against
C07 policy. Notification tests assert 0/1/10/over-10 attention rows, valid links
and no independent query/store.

```powershell
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MailWorkspace|FullyQualifiedName~StaffCorrespondenceWebTests|FullyQualifiedName~PublicUploadSessionTests|FullyQualifiedName~Search|FullyQualifiedName~DashboardCountersWebTests|FullyQualifiedName~WorkCentre"
pwsh -File ./scripts/Update-TestUiSnapshots.ps1
pwsh -File ./scripts/Update-TestUiSnapshots.ps1 -Verify
pwsh -File ./scripts/Test-UiCatalogue.ps1
```

All commands exit 0; snapshot verification uses a fresh capture. Stop if a
shared asset change breaks B's frozen interface, if Web duplicates Core policy,
or if the prototype becomes a production data source.

## C09 - integrate, verify and conduct the fresh whole-stream review

**Owner/model:** a fresh Fable 5.1 context only after C01-C08 are integrated.
It reviews; it does not reopen settled design or add adjacent features.

1. Verify every C-owned production caller is reachable and every A/B handoff is
   implemented at the exact heads recorded in coordination. Run a source-field
   trace from immutable original to reader fragment, policy candidate,
   persisted provenance, C UI and B acceptance/report projection.
2. Produce per-profile and per-report-family matrices with true pass, missing,
   ambiguous and conflict counts. E01-E28 remain explicitly unavailable. A
   passing design corpus is not called an accuracy measurement.
3. Compare PR 639, PR 646 and pinned PR 671
   (`743311a0f4ac68794672510e596abd7d89ae47bb`) preservation tables against the
   final diff. Every
   relevant behavior is present or has a reasoned superseding implementation;
   old PRs remain for the root closeout workflow.
4. Review four lenses: reuse/duplication, simplicity, query/runtime efficiency,
   and abstraction altitude. Fix only in-scope findings and record a disposition
   for every finding.
5. Run the canonical exact-head gates once after focused checks:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category=Corpus"
pwsh -File ./scripts/Test-MigrationGrants.ps1
pwsh -File ./scripts/Update-TestUiSnapshots.ps1 -Verify
pwsh -File ./scripts/Test-UiCatalogue.ps1
```

Every command records cwd, exact SHA, exit code and output. A later pass does
not erase an earlier failure. Completion requires exit 0, no unresolved
cross-stream contract, no source/role loss, no guessed field, no unreviewed
route activation, no N+1 regression, and all C-owned v3 journeys passing. Stop
there; deployment, merge, cloud reset, Outlook/Box/Glass's/EVA writes and old-PR
closure remain outside this stream's implementation authority.
6. After every gate passes and the fresh review findings are disposed, create or
   update exactly one Stream C PR targeting `dev`. Record the PR URL, C head SHA,
   tested SHA and dependencies on the single A and B PRs. Leave all three PRs
   open and unmerged. Do not create a fourth PR, retarget the branch or merge.

## Independent intake-review dispositions

These dispositions close the thirteen findings in `review/intake-review.md`;
the named step and handoff remain the executable authority.

| Finding | Disposition |
| --- | --- |
| C01 | Fixed in C01/C03 and C-F01/C-F03: `AnalyzeRetainedInstruction` is the reachable production command for all fifteen profiles, persists unresolved candidates, and withholds allocation until staff confirmation or an accepted route. Exact clean-target principal IDs/codes are frozen separately from route evidence. |
| C02 | Fixed in C02 and C-F02: page-qualified Azure Document Intelligence `prebuilt-layout` REST, API `2024-11-30`, is wired through the existing external-work path with durable operation recovery and A04 logical-source reads. Missing genuine provider output is `INCONCLUSIVE`, never fabricated. |
| C03 | Fixed in C08 and C-F08: compose/reply/forward/chaser POST handlers call A's one general staff-send port; local recording transport proves handlers without Outlook mutation. |
| C04 | Fixed in C09: exactly one Stream C PR is created or updated against `dev`, recorded and left open/unmerged with the A/B dependencies. |
| C05 | Fixed in C05 and C-B02: the third-party projection enumerates the full report field/version set and is a typed provenance-preserving superset of C-B01. |
| C06 | Fixed in C07/C08 and C-F06: C owns `/Uploads/**` policy/presentation, the fixed non-sliding session and limit-version refusal/reissue; A owns bytes/custody; B owns Case-side link create/revoke. |
| C07 | Fixed in C07 and C-F06: `RetainIncomingArtifact` calls A's custody contract for every received/Unidentified/Triage/Image/public-upload artifact and preserves pending/failed/unknown and logical-version recovery. |
| C08 | Fixed in C06, C-F05 and C-B03: principal default is one inspection-location choice, Image Based Assessment or a sourced/manual address. Third-party requested/observed method stays raw evidence and cannot create a CE physical-attendance method. |
| C09 | Fixed in the exact file map, route matrix, ownership qualification and automatic-EVA split: Foundation/A/B files are read-only dependencies and each owner makes its own changes. |
| C10 | Fixed in C03/C-F03 and the coordination gate: C creates/tests concrete profiles first; A then authors the exact hash-recorded DI patch. No reflection, stubs or no-op registration. |
| C11 | Fixed in C06/C-F05/C-B03: Claim Source has explicit stable identity, contact fields, notes, active/version/audit state and is copied to a Case snapshot independently of locations/routes/principal identity. |
| C12 | Fixed in C08: the menu projects at most ten typed, linked `OperationsSnapshot` attention rows and has no placeholder or independent store. |
| C13 | Fixed in C07/C09: PR 671 is pinned at `743311a0f4ac68794672510e596abd7d89ae47bb`, receives a hunk/behavior disposition, and is checked at the fresh final review. |

## Ticket-by-ticket residual acceptance

This table is additional required scope in the named step, not a separate PR
or licence for adjacent cleanup. Read each linked ticket’s current body/gates.
The current reason overrides stale inherited ticket wording; verify already
integrated clauses and implement only the remaining gap.

| Ticket | Step | Exact residual / acceptance |
| --- | --- | --- |
| CASE-011 | C07 | Triage retains images but lacks the shared viewer. Reuse the current evidence viewer and authorization; do not duplicate files or image controls. |
| CASE-031 | C06 | Claimant address needs extraction, persisted ownership, display and EVA ClmAdd mapping. It is distinct from inspection and repairer addresses. |
| CASE-032 | C07 | Projection change is merged; check source/receipt identity and current pre-case labels, not obsolete image-case allocation language. |
| CASE-037 | C08 | Replace CSP-discarded Search inline actions with the shared shell binding and static href fallback. |
| CASE-041 | C06 | Fast address selection is present. Repairer choice remains inert until INTK-058 supplies real data; source values must be visible before assignment. |
| CASE-042 | C07 | Awaiting instruction is a pre-case queue with no normal Case/PO allocation. Rewrite inherited image-initiated case wording and prove promotion through a real instruction. |
| CASE-045 | C07 | Preserve PR671 optional Image Intake principal behavior with reviewed hunk dispositions; F owns its schema. |
| DELIV-034 | C06 | Verify the merged principal-credential tamper-test correction against its own merge evidence; no need to rebuild the fixed test. |
| DELIV-036 | C03 | Verify the merged QDOS regex-cache/timeout correction while extending source-backed profiles; do not recreate a second regex registry. |
| ENG-011 | C05 | Retain an odometer observation from genuine photo/report evidence with units and source; never infer confirmed mileage from uncertain OCR. B02 owns acceptance/display. |
| ENG-017 | C02 | Use one vehicle-photograph membership policy for intake/completeness/export; logos/document screenshots do not silently qualify. B06 consumes it. |
| INTK-002 | C02 | Name realistic adapter failures and prove composition without creating a generic exception framework. Reuse existing result/refusal conventions. |
| INTK-004 | C02 | One Core decision vocabulary should drive labels and Operations destinations. Separately fix accepted results with no Case identity instead of masking them with labels. |
| INTK-019 | C07 | Use explicit eligible Engineer selection for Triage instead of Assign to me; reuse account query and Core authority. |
| INTK-031 | C05 | Identify issuer and document role, then extract usable report fields with provenance. Its claim that original-report verdict gates normal Case allocation conflicts with current Audit invariants. |
| INTK-032 | C05 | Unknown issuer/layout must not guess an Audit outcome. Use page-level text/OCR fallback and accept only unambiguous labelled fields; expose conflicting and unavailable values individually. |
| INTK-033 | C04 | Triage-request classification is merged into main. The body says it is stranded but later implementation exists; complete exact proof and leave remaining presentation work elsewhere. |
| INTK-034 | C04 | Triage source images are retained in the merged implementation. Shared viewer work remains CASE-011 and should not reopen custody implementation. |
| INTK-035 | C04 | Known-registration promotion from Unidentified is merged. Verify ambiguity/no-registration refusal and close the historical gap after its own gates. |
| INTK-036 | C03 | Instruction date comes only from scoped instruction evidence, never deadline/accident/forward date; preserve source locator. |
| INTK-037 | C07 | Display immutable global T references while retaining internal typed IDs; never allocate normal Case/PO for pre-case material. |
| INTK-038 | C07 | Use shared operator labels for Image Intake analysis and source availability; no raw internal JSON or duplicate label list. |
| INTK-039 | C07 | Grouped matching/custody is merged. Later D50 means image-only material remains pre-case; retire contradictory normal-Case allocation language and verify association. |
| INTK-040 | C07 | Mailbox image routing is merged. Reconcile its destination with Awaiting instruction and preserve grouping and original source identity. |
| INTK-045 | C02 | Share the existing concurrency predicate and inspect every inner layer at the named stores. Surface exhausted conflicts; do not turn this into broad exception normalization. |
| INTK-047 | C08 | Upload pages are ported; per-file details sit beneath one submission decision. Verify the current grouped and public flows after limit/session/custody fixes. |
| INTK-048 | C01 | The recorded implementation and draft PR 639 remain active. Resolve linked Unidentified state through its existing worktree, coordinating with PR-069; do not take it again. |
| INTK-049 | C03 | Resolve only the documented finite machine-read VRM alternatives through existing DVLA/DVSA lookup; exactly one proved result is required, not fuzzy guessing. |
| INTK-051 | C07 | Preserve current upload links/limit generation semantics; after policy change return typed refusal/reissue, never a broken finalize path. |
| INTK-052 | C07 | Enforce accepted separate100MB per-file, approximately200MBmultipart and30MBProviderAPI limits; no derived2GB request budget. F owns host limits. |
| INTK-053 | C02 | A bookkeeping concurrency failure is swallowed without a trace. Record the failure and preserve retry/reconciliation ownership instead of pretending success. |
| INTK-054 | C07 | Append staff Triage notes using existing attributed history and version/replay conventions; no mutable replacement. |
| INTK-055 | C07 | Implement fixed non-sliding15minute public submission sessions with replay-safe finalization, expiry and limit-version refusal; A04 owns durable custody. |
| INTK-056 | C05 | Read standalone Audit outcome from the identified report status, not any repairable/total-loss phrase or previous salvage history elsewhere in the document. |
| INTK-057 | C04 | Two observed historical worker failures expose null CaseType with case_created. Enforce a consistent decision/result before allocation and make unresolved work visible. |
| INTK-058 | C06 | Extract repairer name and address into the per-Case repairer record. Do not confuse it with claimant, principal or inspection location; feed the existing Inspect-at selector. |
| INTK-059 | C07 | Allow an optional known Principal on Triage without making uncertain identity mandatory or creating a normal Case. |
| MAIL-029 | C08 | Preview/search gaps are useful to fix. Restoring a raw Custody column conflicts with the accepted operator evidence view; show actionable availability using existing components. |
| MAIL-034 | C08 | Scope selected-row rules to Inbox so they do not alter Cases rows. Verify both pages at the same viewport. |
| PLAT-028 | C06 | One Principal administration area owns workflow and API settings. Combine overlapping controls with PLAT-050 and top-15 activation evidence; keep credentials secret and scoped. |
| PLAT-029 | C08 | Integrated shell is implemented. Later Case sections and admin health relocation require combined navigation checks; do not preserve dead CSS/routes solely because this ticket introduced them. |
| PLAT-032 | C02 | Unify vehicle inline-image classification across EML/MSG while retaining format differences and excluding logos/signatures. |
| PLAT-043 | C04 | MCP ingress checks its scope, but most Triage commands receive only actor ID text. Pass the typed actor through Core authorization for equivalent Web/MCP policy. |
| PLAT-050 | C06 | EVA toggles and Provider API credentials belong in the existing Principal settings dialog, not a parallel admin concept. |
| PLAT-059 | C08 | Route one Create/Add entry to the accepted instruction-backed allocation dialog, not absent /Cases/Create; B displays the resulting Case. |
| PLAT-061 | C08 | Suppress the empty gated tooltip when no condition exists. Preserve accessible names and real disabled-state reasons. |
| PLAT-065 | C02 | Implement required page-restricted Azure Document Intelligence OCR through existing source reader; F prepares infrastructure, later operator activation proves provider output. |
| PR-069 | C01 | Preserve PR639 reversal/relink lifecycle alongside INTK048, retaining its existing claim/evidence and exact recheck-watermark tests. |
| TICK-001 | C03 | QDOS production acceptance is not implied by passing tests, old deployment or 45.6% workload share. Record operator-reviewed extraction/holdout and complete the usable journey. |
| TICK-034 | C06 | The pack supplies principal/repairer spreadsheets. Normalize candidate addresses with provenance and duplicates for approval; do not bulk-load unreviewed data as business truth. |
| TICK-035 | C03 | The user's top-15 request supersedes post-alpha scheduling. Activate the 14 additional recent-workload principals using the shared route/extraction owners, separately from mailbox onboarding. |
| TICK-041 | C02 | Use Azure OCR only for scan-like/unusable text-map pages; digital extraction first, source/layout retained, no confidence-only acceptance. |
| TICK-058 | C01 | Principal-scoped API is already merged and enabled. PR 646 covers residual behavior; remove the stale API-absent brief and avoid rebuilding the contract. |
| TICK-060 | C04 | Provider status/result behavior exists in the accepted API contract. Verify own-principal receipt/result and failure shapes, then identify only unmet API-03 behavior. |
| TICK-073 | C05 | Use deterministic mappings for supported reports/instructions, OCR for unreadable pages, and AI proposals only for genuinely unsupported material with exact provenance and human acceptance. |
| TICK-074 | C06 | D08 includes sourced directory-backed address suggestions and principal defaults; no separate AI engine or nationwide postcode vendor is needed. B02 saves selected provenance. |
| UIIMP-003 | C08 | Generic prototype integration overlaps the named approved Case/admin tickets. Preserve useful prototype evidence and keep Test UI distinct from deployable Razor changes. |
| UIIMP-009 | C08 | Remove genuinely superseded routes/CSS only after actual caller checks, including dynamic selectors. No speculative whole-site style rewrite or compatibility redirect collection. |
| UIIMP-012 | C08 | Already implementing; preserve its claim. Rename the Triage panel to Notes without changing append-only history semantics and reconcile disabled-action rules with actual preconditions. |



## Stop condition

All assigned implementation, independent review, standalone and combined checks are complete; exactly three replacement PRs target dev, open and unmerged. No merge, deployment, reset or live provider write. External provider/workload evidence remains honestly named operator gates, never fabricated PASS.
