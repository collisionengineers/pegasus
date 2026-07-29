# Deliver the complete QDOS `0.1.0-alpha.1` implementation

Status: **Proposed execution plan — not implementation authorization**

This plan replaces the stale execution baseline in `docs/changes/2026-07-27-qdos-alpha-implementation-plan.md`. It uses the existing change record `docs/changes/2026-07-27-qdos-alpha-reference-corpora.md`; it does not create a second status ledger. It deliberately excludes `docs/reference/imp-docs/requirementsdocs/v1_docs/entire_v1_repo_docs/` and everything below it as instruction, evidence, search input, and implementation source.

## 1. Goal, finish lines, and evidence language

Deliver all source, migrations, local adapters, live adapters whose contracts have been accepted, staff UI, Worker callers, MCP surface, infrastructure, runbooks, and verification needed by the 128 unique `Now` capabilities allocated to `0.1.0-alpha.1`. The implementation runs from a new Windows worktree, preserves a checkpoint commit for each independently working increment, and ends with one pull request to `collisionengineers/pegasus` `main`. The agent never merges that pull request.

Two finish lines must remain distinct:

1. **Implementation-candidate finish line (the requested worktree/PR):** every feature is implemented through its actual Web, Worker, or MCP caller; the complete offline system passes on LocalDB, Azurite, the real Functions host, and approved genuine local material; production registrations fail closed; the professional Operations-first UI passes browser/accessibility review; live-adapter code passes source-verified deterministic replay/contract and any separately approved pre-provision probes; IaC and retained immutable artifacts pass local checks and exact-target validation/what-if; `.azure/deployment-plan.md` is `Validated`; exact-head CI and independent review are green; the PR is open. Managed-identity scope, deployed remote MCP and other target-dependent claims remain pending.
2. **Released-alpha finish line (post-PR, maintainer/release-owner work):** the reviewed change is merged by a maintainer; exact-artifact validation remains applicable; the approved ADR-0009 release runbook executes under separate write approval; the isolated Azure targets pass live dependency/recovery/capacity smoke; staff complete the real QDOS journey; Collision Engineers management accepts it; a narrow release-evidence PR lands; the final gate passes; and `0.1.0-alpha.1` tags that evidence commit. A source PR cannot truthfully prove deployment or acceptance.

The change record must use only these evidence states: **intended**, **implemented**, **locally caller-proved**, **live-adapter proved**, **deployed**, and **operator/management accepted**. A green source test, registration, local adapter, PR, deployment, and acceptance are not interchangeable.

## 2. Evidence baseline

| Area | Evidence already present | What is not proved |
|---|---|---|
| Capability allocation | `docs/product/capabilities.md` declares 229 unique IDs: 128 Now, 32 Next, 40 Later, 29 Not planned. | The file currently has committed conflict markers around `TRI-04`; physical-row counts are therefore 230/129 until deduplicated. |
| Provider-domain snapshot | The active change record documents Step 2: immutable `provider-domains-v1`, 11 provider codes, 16 code/suffix associations, suffix-only content, package/migration/catalog tests. | Current exact-head verification and release acceptance; a domain suffix does not activate a mail route. |
| Intake | Current source has one Development-only `/Intake/Upload` caller, bounded MIME/PDF/DOCX/image parsing, ignored local retention, receipts/drafts/evidence, replay behavior, and no case/reference allocation. | Authentication, live custody, durable Worker dispatch, accepted extraction cohort/holdout, case creation, deployment, operator acceptance. |
| Persistence | One EF Core DbContext/migration stream and local SQL/SQLite test evidence exist. | Full identity/case/Triage/outbox/custody schema, LocalDB contention for all invariants, Azure SQL/Entra/restore proof. |
| Worker | Existing isolated Functions composition and telemetry defaults. | A trigger or Core caller. |
| Web/UI | Development intake/dashboard/review pages and partial token-aligned CSS. | The authenticated Operations-first shell, staff workbenches, Administration, global search, professional visual/accessibility acceptance. |
| Azure | Committed target Bicep/`azure.yaml` and a dated predecessor inventory. | A current approved inventory, isolated Pegasus target, validation/deployment, live scopes, health/recovery, operator acceptance. |
| Change/branch state | The evidence review ran at `467284f` on tracked `document-reconciling`. The prior local delivery branch has two unique commits (`c2c3ebd`, `175bc87`); its provider work was subsequently cut over into current `Pegasus.*` paths, while the old commit also contains obsolete `CollisionSpike.*` paths, stale plan text and generated/binary debris. | The implementation plan still names `b2f40a2` and “no implementation commits”; reconcile that stale claim. Never wholesale-cherry-pick the prior branch—compare behavior and reimplement only a verified current gap. |

The new worktree must re-run the provider package and current tests before relying on historical results. It must not redo or replace a valid immutable package merely to create a checkpoint. Commit `467284f` is the exact planning-evidence head, not an implementation base: activation waits for the repository-orientation pull request to be merged or otherwise resolved, then re-reads and records the exact accepted `main` head before creating the worktree.

## 3. Plan-activation decisions and contradiction resolutions

The implementation starts only after the user accepts this plan and the following proposed resolutions land through one reviewed superseding `docs/decisions/0014-reconcile-qdos-alpha-implementation-boundaries.md`, the affected canonical owners, and the existing change record's next `DOC-CON-NNN` entries. They are proposed here, not silently treated as settled:

1. **Ordinary-image VRM:** the current explicit 128-capability alpha direction supersedes the older ADR-0002/ADR-0005 deferral text for `INT-17`. Alpha includes one suggestion-only ordinary-image registration reader selected by a genuine labelled benchmark, but does not preselect OCR, VLM, in-process or external-service mechanics. The accepted candidate must fit the four-project/caller boundary; image egress, credentials, security and cost require their own accepted contract, and no candidate may add a runtime/deployment unit or activate broader damage/image AI.
2. **Account lockout:** ADR-0002's generic lockout requirement means transient login throttling, not persistent ASP.NET Identity account lockout. Use the already specified 10 attempts/trusted-client-IP/minute and 100 global/minute limit, generic failure, `429`, and `Retry-After`; keep `LockoutEnabled = false`.
3. **QDOS field count:** work-provider/principal is mandatory route evidence and case data but is not one of the ten document-extraction fields. The ten are claimant name, claim number, VRM, make, model, mileage, accident circumstances, incident date, instruction date, and inspection address. Report principal/route accuracy separately.
4. **`TRI-04`:** retain two independently optional findings and require at least one before `Finding recorded` or `Completed`; remove the conflict markers and duplicate row.
5. **Predecessor `Ready`:** map the historical Excel holding-pen label to the canonical alpha state `Review`; do not create a second `Ready` state.
6. **Roadmap outcome count:** the Now horizon has three named outcomes—provider/intermediary foundation, QDOS workflow, and `INT-31` request-scoped uploads. Update the change summary that currently says two.
7. **Design reconciliation:** `EVAL-01`–`EVAL-05`, `MAIL-20`–`MAIL-22`, `OPS-10`, and `OPS-22` are alpha allocations with pre-alpha execution checkpoints, not pre-alpha release allocations. `OPS-23`/`OPS-25` are alpha acceptance gates. Operations-first is the selected shell/direction; remove the traceability status that says no direction is approved. Correct the matrix without changing allocation.
8. **Address precedence / `EXT-18`:** `DATA-02` remains Next/unallocated. Alpha satisfies `EXT-18` through a Core-owned evidence mapper: exact extracted physical-address evidence or the exact `Image Based Assessment` instruction becomes a provenance-bearing suggestion that staff must accept or correct. Missing/conflicting evidence remains unresolved. No spreadsheet lookup, geocoder, AI or provider-domain default is implied.
9. **MCP boundary:** `MCP-01`–`MCP-04` provide transport-neutral authorised staff MCP. A compatible Claude Desktop connection may be acceptance evidence, but it does not activate `AI-09`, an agent queue, or AI-owned policy.
10. **Terminal outcome literals:** use the canonical product/UI labels `Post-report completion`, `Provider cancellation`, `Collision Engineers rejection`, and `Created in error`; older adjectival variants do not become additional states.
11. **Initial case state:** follow the current product/design contract: automatically definitive accepted intake starts `Review` when both completeness requirements are met and otherwise `Not ready`; staff-resolved intake starts `Review` only after explicit staff confirmation of both. Replace the older implementation-plan sentence that forced every accepted intake to `Not ready`.
12. **Sequence exhaustion:** all three-digit values `001` through `999` are allocatable. An attempt after `999` fails closed before case/reference/history/outbox creation with a visible principal/year sequence-exhausted result; it never rolls into four digits or a new year.
13. **First-release rollback:** no compatible prior Pegasus package exists. Before the first accepted cutover, rollback means leave the predecessor/prior caller in place, disable or withdraw the new Pegasus caller, retain the new data-bearing target for reviewed recovery, and never down-migrate. The retained `0.1.0-alpha.1` bytes become the first application-package rollback baseline only after acceptance; production cutover separately approves that one-time boundary.
14. **Canonical conflict resolution:** keep the more specific `TRI-04` contract—Roadworthiness and Assessment are independently optional, at least one is required before Finding recorded/Completed, and replacement is reasoned. Keep `UI-03` as the alpha Needs sorting/Blocked intake queues. Keep `DOC-11` digital signatures `Not planned`/`unallocated`. Remove the current conflict markers and make product/design/traceability wording exact.
15. **Production secret custody:** ADR-0002's specific runtime rule controls the generic “Infisical or Key Vault” wording. Each Azure environment's Key Vault owns Box and every other unavoidable third-party credential, the Web-only authentication/OpenIddict protecting material, and the separate `BoxLinkProtection` key material, accessed only through the explicitly owning managed identity and scoped RBAC. Remove Infisical runtime/deployment configuration; Offline requires neither service.
16. **Deferred OCR infrastructure:** the current QDOS alpha allocation controls the older optional/S0 Document Intelligence topology. EML/freehand, embedded PDF text/images, DOCX text/image occurrences and JPEG/PNG remain supported; scan-only PDF OCR remains deferred. Remove the resource, package, configuration and roles rather than deploy it disabled.
17. **Managed-identity topology:** replace the older system-assigned Web/one-user-assigned-Worker shape with distinct user-assigned Web and Worker identities. Their stable ARM-output client IDs allow explicit SqlClient/Azure client selection and no-directory-read contained SQL bootstrap; neither identity receives deployment or migration rights.
18. **SQL runtime roles:** replace the draft `db_datareader`/`db_datawriter` grants with schema-managed, caller-derived Web and Worker roles. The temporary SQL administrator/migrator group performs DDL/bootstrap only; runtime identities receive no DDL and the deployment principal receives no standing application-data role.
19. **Migration execution RID:** ADR-0009's authorised Windows-terminal release path controls the older Linux-x64 bundle sentence. Build a self-contained `win-x64` EF bundle for that terminal; Web and Worker application packages remain `linux-x64`.
20. **Telemetry retention:** follow ADR-0002's 31-day interactive Log Analytics/Application Insights retention and correct the current 30-day Bicep/deployment draft. Source sampling and an explicit daily cap are mandatory; exact values are accepted from measured alpha workload and cost evidence before target validation.
21. **Azure cost envelope:** retain ADR-0002's combined Development/Production estimate and £75 monthly alerting envelope; it is an alert, never a spending cap or automatic shutdown. Add one subscription-scope Pegasus budget filtered to the two exact environment resource groups, with actual-cost warnings at 80% and 100% and a forecast-cost warning at 100%. Exact notification contacts/action group and billing scope require target approval before provisioning.
22. **Browser/accessibility support:** the alpha production browser is Microsoft Edge Stable on Windows 11; record the exact OS/Edge version at acceptance and run every critical journey in that Edge channel. Pinned Playwright Chromium remains deterministic CI coverage, not a second production-support claim. Windows Narrator is the named screen reader; no release claim is made for Chrome, Firefox, Safari or mobile.
23. **State tokens:** adopt amber incomplete/pending foreground `#7A3E00`, background `#FFF4D6`, border `#A15C00`; adopt navy Review foreground `#143A5E`, background `#EAF1F8`, border `#365F87`. These pairs exceed 4.5:1 for text and 3:1 for non-text boundaries against their mapped surfaces; forced-colour behavior still requires browser proof. Record them in canonical token/foundation authority before runtime CSS.
24. **External credential lifecycle:** each Box, DVLA/DVSA and any external VRM credential has a named operations owner and a provider-specific issue/rotate/revoke/emergency-disable procedure. Production configuration carries only a Key Vault secret URI/version or identity metadata, never a value. Rotation proves new-version success before cutover, old-version revocation/denial where the provider safely supports it, queued-work recovery and no local fallback; emergency revocation disables the caller and leaves work visible.
25. **Operational alert boundary:** each environment uses an operations-owned Action Group configured outside staff UI; Alex's approved operator address is the initial recipient and later recipients are parameter changes, not code changes. Alert delivery covers ingestion/processing, poison, unmatched/matching, custody/Box, overdue `Due by`, chasers, EVA/export, authentication/security, database pressure, availability and cost. Safe firing plus recipient acknowledgement is a release gate; no alert contains business content.
26. **Capacity acceptance:** approve the checkpoint 12 30-minute/eight-session workload mix and thresholds as the initial alpha gate: warm p95 reads `≤2 s`, warm p95 writes `≤3 s`, zero unhandled Web/Worker errors or lost history, queue lag recovered below two poll intervals, no target resource above 80% for five minutes, and cold readiness `≤60 s`. The run requires an operator-approved immutable 2,000-case performance dataset and observed document/source distribution; missing evidence blocks rather than licenses fabricated domain data.
27. **Archive semantics:** `CASE-26` means a reasoned read-only archive status on a terminal case without deletion. No `Unarchive` capability is allocated or implied; returning work to an active state uses the separately authorised reasoned reopen path before archival. A future reversal requires a product decision rather than an invented caller.
28. **MCP throttling:** token/authorization endpoints retain the sign-in limits and add 10 requests/minute per client+source; tool calls allow 60 reads/minute and 20 mutations/minute per current user+client, returning `429`/`Retry-After` before Core mutation. Rates are production configuration with these fail-closed maxima; lowering them is operational, raising them requires reviewed security evidence.
29. **Bootstrap secret transport:** supersede the earlier local-runner proposal to place the first Administrator password in a child-process environment value. The one-shot Web composition-root command reads concealed same-process terminal input; the release bundle does the same. Orchestration may attach that terminal but never receives, forwards, persists or echoes the secret. Automated tests use process-generated technical-fixture credentials through a protected standard-input harness and prove arguments, environment, files, output, logs and history contain none.
30. **Root local-tool manifest:** accept one new top-level `.config/` directory containing only `dotnet-tools.json`. The .NET CLI's standard trusted-root discovery requires that location for root `dotnet tool restore`/`dotnet ef` use; an existing feature folder cannot carry it without changing every working directory and breaking normal manifest discovery. ADR-0014 records this narrow tooling-boundary exception. It is not a project, runtime, deployment unit or general configuration home; adding any other `.config/` file requires separate review.

The following remain evidence decisions with explicit owners and block only their dependent slices: every activated QDOS provider/intermediary route disposition; QDOS field cohort/holdout; Triage/report predicates; the VRM candidate set, mechanism and any data-egress/security/cost contract; `INT-31` lifetime/file/count/rate/reuse limits; DVLA/DVSA fields/licence/mileage rule; the EVA source mapping and real drag/drop; Graph tenant/mailbox scope; Box enterprise/user/root/template-file-request/operations; external credential owners/provider rotation mechanics; Azure targets/resource dispositions; SQL Entra administrator/migrator group and bootstrap identities; release-artifact custody/retention; exact Edge/Windows versions; exact performance dataset/peak burst; telemetry sampling/daily cap; alert recipients/Action Group; and Azure billing scope, current per-environment/combined cost forecast and budget notification targets. The change record must name owner, evidence, decision, date, and affected checkpoint before dependent production behavior is authored or enabled.

## 4. Architecture and implementation boundaries

Retain the existing four-project modular monolith and one solution:

| Owner | Responsibilities | Must not own |
|---|---|---|
| `Pegasus.Core` | Domain types, policy, transitions, validation, explicit use cases and ports, accepted history events, caller-independent query contracts. | EF, ASP.NET, Azure/Graph/Box SDKs, Razor, Functions attributes, transport authentication. |
| `Pegasus.Infrastructure` | Existing DbContext/migrations, SQL transactions, repositories/query adapters, outbox/leases, Blob/Queue, local mailbox/custody/replay, Graph/Box/vehicle adapters. | Business precedence, lifecycle decisions, route predicates in transport code, UI decisions. |
| `Pegasus.Web` | Razor Pages composition, Identity/OpenIddict transport, authentication/authorization filters, PageModel binding, Development evaluator, request-upload HTTP, MCP transport. | Duplicate policy, direct DbContext business mutation, migration on normal startup. |
| `Pegasus.Worker` | Actual queue/timer Functions, configuration, telemetry and calls to one Core use case per trigger. | Business decisions, source bytes in queue messages, vendor-specific fallback policy. |

No new project, top-level directory except the ADR-0014-approved `.config/dotnet-tools.json`, runtime, datastore, migration stream, deployment unit, MediatR/CQRS framework, generic repository, generic rules engine, second classifier, or compatibility shim. Organise the existing projects by feature folders only. Delete the old monolithic/single-policy call path after every caller moves; do not retain aliases.
The existing `workspaces/ai-centre` may own only the VRM candidate harness, acquisition metadata and evaluation evidence. It remains independently buildable and absent from `Pegasus.slnx`, project/package references, runtime loading and deployment. If the accepted candidate has model/native bytes, they may enter the Pegasus release only after the same change record accepts a separate integration contract covering origin, licence, immutable hash, supported RIDs, security, rollback and caller proof; release packaging consumes those pinned bytes without a runtime workspace or network dependency. An accepted external candidate instead adds only an Infrastructure adapter under its separately approved egress/credential/cost contract.

Package admission baseline as of 2026-07-28:

| Purpose | Package/version | Admission rule |
|---|---|---|
| OAuth/OIDC | `OpenIddict.AspNetCore` and `OpenIddict.EntityFrameworkCore` `7.6.0` | Stable line; verify .NET 10 APIs and migrations from official docs/sample before commit. |
| Staff MCP | `ModelContextProtocol.AspNetCore` `1.4.1` | Current stable official C# SDK; do not take the `2.0.0` preview/RC line. |
| Graph | `Microsoft.Graph` `6.2.0` | Add only at live-adapter checkpoint after exact Exchange contract/scope approval. |
| Box | `Box.Sdk.Gen` `1.12.0` | Current stable generated official SDK; add only after Box identity/root/operation approval. |
| Browser | `Microsoft.Playwright` `1.61.0` | Existing Integration test project only. |
| Accessibility | `Deque.AxeCore.Playwright` `4.12.0` | Existing Integration test project only; its declared Playwright lower bound permits the pinned browser package. |
| Azure identity/storage | `Azure.Identity` `1.21.0`, `Azure.Storage.Blobs` `12.29.1`, `Azure.Storage.Queues` `12.27.1` | Current stable NuGet versions; add at the owning runtime checkpoint and prove Azurite plus managed-identity configuration. |
| Key Vault clients | `Azure.Security.KeyVault.Secrets` `4.11.0`, `Azure.Security.KeyVault.Certificates` `4.9.0` | Add only for approved third-party secret-version retrieval and production OpenIddict certificate loading; prove owning-identity scope, version selection, rotation and denial without emitting values. |
| Functions bindings | `Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues` `5.5.4`, `Microsoft.Azure.Functions.Worker.Extensions.Timer` `4.3.1` | Exact stable isolated-worker extensions; keep the existing Worker/SDK pins and prove the actual host caller. |
| Production key persistence | `Azure.Extensions.AspNetCore.DataProtection.Blobs` `1.5.3`, `Azure.Extensions.AspNetCore.DataProtection.Keys` `1.6.3` | Add only with the accepted Blob/Key Vault custody and exact managed-identity denial tests. |
| EF bundle tool | root `.config/dotnet-tools.json` manifest pins `dotnet-ef` `10.0.10`; existing `Microsoft.EntityFrameworkCore.Design` `10.0.10` | Restore and invoke from the trusted repository root through the committed manifest; build one Windows-x64 bundle for the authorised Windows release terminal, never resolve a global/latest tool. |
| Database bootstrap CLI | Microsoft Go `sqlcmd` `1.10.0` | Cloud/release profile only. Doctor verifies the pinned Microsoft binary/version; use `ActiveDirectoryDefault` from the approved terminal identity with strict TLS and no password/secret argument. |

ASP.NET Core shared-framework and EF packages retain the repository's explicit .NET 10/EF `10.0.10` pins; Azure SDKs and Functions bindings use their independent exact rows above rather than a fictitious aligned version line. Any package/tool version change is a reviewed dependency update with restore/build/caller proof, not “latest” resolution at build time. No VRM/model/native package enters the application dependency graph until the benchmark/licence decision.
The listed versions were confirmed present in their exact NuGet v3 flat-container indexes on 2026-07-28; presence is not compatibility proof. At each owning checkpoint, restore from a committed lock/tool manifest, inspect the package's target frameworks/dependency bounds/licence and repository provenance, compile the current official API sample, exercise it through the actual caller, and record package hash plus source URL in the change record. Any incompatibility reopens the dependency decision before code is merged—never invent an API, silently float a version or keep an uncalled package.

### 4.1 Core use-case seams

Use explicit, boring contracts rather than framework abstractions:

- Intake: `ReceiveIntake`, `ProcessIntake`, `ResolveIntake`, `ReevaluateIntake`, `AcceptIntake`, `LinkIntake`, `ReverseIntakeLink`.
- Identity/administration: `InitializeApplication`, `CreateStaffAccount`, `DisableStaffAccount`, `AssignStaffRoles`, `ReviewStaffAccess`, `CreatePrincipal`, `ReplacePrincipal`, `UpdateWorkflowConfiguration`, `UpdateApprovedMailbox`, `RegisterPublicMcpClient`, `RevokePublicMcpClient`, `RevokeStaffMcpAuthorizations`.
- Cases: `CreateManualIntake`, `ConfirmCompleteness`, `AcquireCaseEditLease`, `RenewCaseEditLease`, `ReleaseCaseEditLease`, `SaveCase`, `HoldCase`, `ReleaseCase`, `TransitionCase`, `CloseCase`, `ReopenCase`, `ArchiveCase`, `CreateLinkedReplacement`, `CreateCaseTask`, `AssignCaseTask`, `CompleteCaseTask`, `CancelCaseTask`, `RecordEngineerFinding`.
- Triage: `CreateTriageFromIntake`, `AssignTriage`, `UnassignTriage`, `RecordTriageFinding`, `SupersedeTriageFinding`, `LinkTriageResponseEvidence`, `UnlinkTriageResponseEvidence`, `CompleteTriage`, `CancelTriage`, `ReopenTriage`, `LinkTriageCase`, `UnlinkTriageCase`.
- Documents/integrations: `AddCaseDocument`, `DownloadCaseDocument`, `ExportCaseDocuments`, `CreateBoxFileRequest`, `RevokeBoxFileRequest`, `CreateRequestUploadLink`, `RevokeRequestUploadLink`, `UploadToRequest`, `LogicallyRemoveDocument`, `RequestVehicleLookup`, `AcceptVehicleSuggestion`, `GenerateEvaHandoff`, `LinkReportEvidence`, `UnlinkReportEvidence`.
- Background: `SynchronizeApprovedMailbox`, `DispatchPendingWork`, `ProcessQueuedIntake`, `ProcessQueuedExternalWork`, `ProcessQueuedCustody`, `ProcessQueuedVehicleLookup`, `ReconcilePoisonedQueueWork`, `RunDueChasers`, `ReconcileStagedArtifacts`. The `external-work` dispatcher loads the ID-only durable work row, rejects unknown kinds, and invokes exactly one custody or vehicle handler. Poison reconciliation loads an ID-only poison message, atomically marks its durable row terminal/idempotent before deleting it, and retains malformed-ID evidence only as a content-safe hash/security event. Each Functions trigger calls exactly one Core contract and carries IDs only.
- Queries: `GetOperationsSnapshot`, `ListIntake`, `GetIntake`, `SearchCases`, `GetCase`, `ListTriage`, `GetTriage`, `GetAdministrationSnapshot`; contracts return authorised immutable projections and Infrastructure implements their indexed reads.

Web, Worker, and MCP call these same contracts. Every mutating contract accepts an explicit actor, expected version where applicable, idempotency/operation key, and reason when the domain requires it.
Production composition requires the exact Web/Worker user-assigned identity client IDs. SqlClient uses `Active Directory Managed Identity` with that `User Id`; Blob/Queue/Key Vault/Graph clients and Functions host-storage settings select the owning client ID explicitly. Missing/malformed IDs fail before a listener or business endpoint becomes ready. `DevelopmentOffline` constructs none of these production credentials or clients.

### 4.2 Actor, time, and history contracts

- Actor is a discriminated Core value: enabled staff identity/roles, system Worker identity, request-link identity, or the one-shot bootstrap identity. The bootstrap identity is constructible only by the offline/release bootstrap composition path and may authorize only `InitializeApplication`: one transaction verifies the expected migration and exact approved bootstrap-manifest hash, takes the singleton lock, requires an empty staff/client target with no completion row, creates the two approved initial Administrator accounts and public MCP client through the same Core policies/history classifications, then writes an immutable completion marker. Its stable target/manifest identity remains in history; it cannot call an ordinary command, cannot be exposed by Web/MCP/Worker, cannot run after completion and cannot be reset. The invoking release principal is separately captured in release evidence. No actor is an email/string bypass.
- Store instants as UTC `DateTimeOffset`. Inject `TimeProvider`; calculate case year, day/week metrics and chasers with `Europe/London`, including DST tests.
- `ActionHistory` is append-only and stores aggregate/id, event kind, actor, UTC instant, reason, allowlisted structured before/after values, outcome, policy/config version and correlation ID. It records every successful business mutation; every material denied/failed mutation; automated classification/acceptance/custody/chaser/vehicle/EVA/report outcome; accepted, linked or used external evidence; document revision/removal/protected download/export; and account/role/principal/workflow/mailbox/OAuth-client administration. It excludes secrets, tokens, passwords, message/file bodies and routine reads/searches/dashboard refreshes, empty polls, leases/heartbeats and transport retries.
- `SecurityEvent` separately records sign-in, password, token/client, rate-limit, security-stamp and security-configuration outcomes. Content-safe low-level polling, retry, dependency and performance mechanics use telemetry. Every Web, Worker and MCP path reaches these classifications through the Core use case—transports never duplicate or omit them.
- W3C correlation IDs are persisted on receipts/outbox/history operations; queue payloads remain ID-only. Checkpoint tests assert actor/reason/outcome/before-after parity for each caller and prove routine mechanics do not pollute the permanent ledgers.

### 4.3 Persistence model and invariants

Extend `PegasusDbContext`; use SQL Server `rowversion`, unique indexes, check constraints and transactions. SQLite may cover provider-independent fast tests, but LocalDB owns SQL Server behavior/concurrency proof.

| Aggregate/table group | Required durable shape and invariant |
|---|---|
| Staff/auth | ASP.NET Core Identity/OpenIddict tables plus enabled/forced-password-change state; roles are Administrator/Engineer/User; no public registration. |
| Organization/principal | One organization with independent WorkProvider/InstructionIntermediary roles; immutable principal code after first allocation; successor/predecessor and shared sequence-lineage IDs. |
| Intake | Receipt/source identity/hash/channel, staged-object identity/version/etag/blob disposition tag and durable pending/completed/failed/unmatched/orphan classification with completion time, immutable evaluation revisions/current pointer, evidence/field candidates/assets, block/retry/resolution/acceptance state. Unique `(channel, externalToken)`; same token/different hash is a conflict. Only durably completed objects are cleanup-eligible. |
| Mail policy | Immutable policy key/version and selected route owner/kind/work provider plus evidence. Package-domain evidence is referenced by exact version but never activates a route. |
| Outbox/work | ID-only operation rows, unique operation key, state, due time, attempt, lease token/expiry, external receipt/failure. Renewable claims; five application attempts at 30s, 2m, 10m, 30m, 2h, honoring a longer accepted `Retry-After`. |
| Case/reference | Case GUID, immutable principal/reference, type, optional derived Audit display identity, principal/year sequence lineage, source origin, rowversion. Acceptance/intake link/reference/history/outbox commit once. Values `001`–`999` allocate; the next attempt allocates nothing. |
| Case detail | Typed provider, claimant, claim, vehicle, accident, contact, instruction and inspection data with confirmed/suggested/provenance state; relationships to staff/EVA-assigned Engineer, repairer/bodyshop, insurer and contacts; completeness; due date; lifecycle/Held snapshot; chasers; versioned tasks with open/completed/cancelled status and optional staff assignee. |
| Triage | Stable pre-case identity; immutable origin receipt/source identity/hash and creating evaluation revision; unique source/idempotency key; normalized VRM; state; optional assignee; immutable/superseding findings; exact response evidence; optional case link; rowversion and history. It never owns a case/reference. |
| Documents/custody | Logical document, every occurrence, immutable versions, semantic role, source, remote/staged identities, root ancestry proof, hash, etag, operation key and closed-case lock; protected version/occurrence download and bounded export manifest/stream; Box file-request identity/status plus a protected retrievable bearer URL that never enters logs/history. Bytes never live in SQL. |
| External evidence | Vehicle/MOT suggestions, address/mode evidence, EVA generations/assets/manifest, exact Sent report evidence and first-to-Engineer proxy; immutable external identities and once-only keys. |
| Lease/history | Hashed five-minute edit lease with 60-second heartbeat and case rowversion; append-only ActionHistory/SecurityEvent; no Administrator lease override. |
| Request upload | Request/link ID, case/request association, hashed 256-bit opaque token, expiry/revocation/use policy, accepted limits/version, custody operations and anonymous receipt IDs. Raw token is shown once and never logged/stored. |

### 4.4 Transaction boundaries

1. **Receive:** retain source bytes in guarded staging first; verify hash; then commit receipt plus process-outbox atomically. SQL failure leaves a labelled staging orphan for `ReconcileStagedArtifacts`; failed, unmatched and orphan objects never silently expire or claim missing bytes.
2. **Process:** claim receipt, load/verify retained bytes, run the pinned parser/policy, append an evaluation revision and visible outcome. A Triage disposition with normalized VRM atomically creates or returns exactly one source-linked pre-case Triage record keyed by origin receipt; missing VRM remains `Needs sorting` with the intended disposition. No allocation.
3. **Resolve:** staff correction/block/retry/re-evaluation appends actor/reason/version. Supplying a valid VRM then re-evaluates the same receipt and idempotently creates its Triage record; failure/retry keeps the immutable source and never creates a case or duplicate Triage.
4. **Accept:** in one SQL transaction validate actor/route/type/Audit/completeness/idempotency; allocate principal/year sequence; create case/link/history and custody/external outbox. External failure after commit blocks progress but never rolls identity back or reallocates.
5. **External work:** a Worker claims the outbox, calls one guarded adapter, records the exact external receipt/version/failure, and advances Core only from durable success.

Normal Web/Worker startup never migrates. Local orchestration invokes an explicit Development migration command. Release packaging creates one Windows-x64 EF migrations bundle for the repository-mandated Windows/PowerShell release terminal; the authorised migrator applies it before the Linux Web/Worker packages. Forward-fix or restore only—no production down migration.
Every schema checkpoint appends—never rewrites—a migration, updates `PegasusDbContextModelSnapshot` and the exact `DevelopmentSqliteBaselineGuard`, and proves fresh/idempotent SQLite plus LocalDB migration and refusal of an old/mismatched baseline before mutation.

## 5. Complete business behavior

### 5.1 Shared mail evaluator and route policy

- One MIME/source reader normalises transport evidence. One Core `EmailDecisionPolicy` evaluates separate versioned direct-provider and intermediary routes, provider, instruction type, case association, Received/Sent/Reply category, Triage and exact report predicates.
- Preserve outer CE forward provenance. Use a proved forwarded-message shape with exactly one consistent original external sender; arbitrary quoted `From:` text, zero senders, malformed or conflicting senders fail to `Needs sorting`.
- Evaluate direct and intermediary routes independently. Exactly one selected route records route owner, kind and resolved work provider; two matches are `multiple routes`. The same organization IDs may legitimately be equal.
- `@qdosassist.co.uk` is the only accepted current direct QDOS trait and proves route only. Provider-domain package presence, sender, subject, filename or Case/PO alone never proves activation/type/association.
- Seed the evaluator's review-only labels from retained `docs/reference/CollisionSPikeCurrenttree.txt` evidence: Received `General`, `billing`, `new-instruction-received`, `non-client-related`, `in-progress-cases`, `post-report-emails`, `pre-instruction-emails`, `internal-cc`; Sent `Report sent`, `case-rejected`, `query-sent`, `additional-image-request`; Reply mirrors the corresponding Received/Sent family; reviewer-only `Other` requires label and reason. A label is evidence, not an automatic predicate, application queue, Triage route or Outlook folder destination.
- Development `/Development/EmailEvaluation` requires Administrator auth and owns run-scoped ignored `unchecked`/`checked` roots. An operator copies an approved source into `unchecked` by hash; the evaluator refuses corpus/tracked/reparse/root escapes and never creates a product receipt/case/reference. Durable label/reason/result plus an atomic `unchecked`→`checked` working-copy move are one idempotent operation: retry returns the first result, and destination collision or filesystem failure is visible without source mutation. Single/batch review retains source hash, human/automated comparison, policy/version/evidence and immutable JSONL/CSV reports.
- Its workbench shows source/MIME hash, sender chain, extracted occurrences/completeness/issues, every matched and nonmatched route predicate, organization roles, resolved provider, instruction type, case evidence, mail family, separate Triage yes/no/ambiguous label, conversation/VRM evidence, policy key/version, ambiguity and duration. Human and automated outcomes remain side by side; classification, route, Triage, case association and folder destination are distinct fields.
- The evidence campaign freezes genuine human-reviewed cohorts and untouched holdouts for only the activated QDOS direct/intermediary routes, extraction, Triage and exact-report matchers; it proves positive, negative, ambiguous, forward, collision, retry, correction/reversal and holdout behavior. Additional providers remain unallocated and are not revived through the superseded 88/56-candidate campaigns. Commit only aggregate counts, input hashes, policy versions and limits.
- Field reports include exact-normalized correctness/coverage for the ten extraction fields, separate route/principal correctness, missing/conflict/unreadable counts and false case creations. No route/Triage/report auto-predicate activates before the named operator accepts its holdout; zero false case creation is invariant.
- The QDOS inner extractor runs only after the complete bounded source is readable. It yields typed suggested/confirmed/provenance/conflict states for the ten fields plus the separately resolved principal; it validates VRM, dates, mileage and claim reference without truncating or guessing and never substitutes a hard-coded completeness matrix.
- The ten fields are exactly Claimant Name, Claim Number, Vehicle Registration, Vehicle Make, Vehicle Model, Vehicle Mileage, Accident Circumstances, Date of Incident, Instruction Date and Inspection Address. Work Provider/principal is evaluated separately as route identity.
- Default an absent instruction date from the injected clock and mark it as `SystemDefault`. A later discovered EML/PDF bound, unreadable page or contradictory strong candidate invalidates an earlier draft-ready signal and preserves all candidates for review.

### 5.2 Intake and Worker

- Support manual staff source upload and automatic `instructions@collisionengineers.co.uk` mailbox intake through identical Core paths. Supported parsing remains EML/freehand, PDF embedded text/images, DOCX text/every visible image placement in document relationship order, JPEG/PNG. Reuse of one DOCX image part in two placements yields two occurrence IDs with one shared content hash. DOC/MSG are retained with provenance in `Needs sorting`; scan-like PDF OCR remains deferred.
- Image-led work starts from retained JPEG/PNG evidence and an operator-reviewed provisional VRM. VRM alone never proves principal, instruction type or completeness; it creates a case only after the same definitive authorised QDOS acceptance gates pass.
- Keep aggregate 10 MB current intake bound until an accepted `INT-31` limits decision says otherwise; MIME/type/extension/content checks and existing nested/archive/entry/expansion limits remain fail closed.
- Typed outcomes are Draft ready, Needs sorting, Blocked intake, OCR required, Unsupported and retryable/terminal technical failure. Blocked intake is staff-set with reason/warning/resolve/retry; it is not Triage.
- Add actual Functions: Inbox poll timer, outbox dispatch timer, `intake-work` queue trigger, `external-work` queue trigger, queue/poison reconciliation timer, staged-artifact reconciliation timer, due-work sweep and Sent-evidence poll. Each trigger invokes its one named Core contract. Inbox and Sent polls have a configurable one-minute default. SQL leases/cursors prevent overlap per mailbox or reconciler; a delayed tick/restart resumes from the durable cursor, and acknowledgement follows only a durable idempotent result.
- `intake-work` messages contain one receipt GUID and `external-work` messages contain one outbox-work GUID. The durable row owns kind, target identity, application attempt and state; unknown kinds fail closed. Handlers durably schedule accepted dependency retries or terminal outcomes and then acknowledge; unhandled process/host crashes use native Functions redelivery. After the configured five host attempts, the runtime writes the unchanged ID to explicit `intake-work-poison` or `external-work-poison`; reconciliation idempotently records terminal operator-visible failure before deletion. Poison evidence never contains source bytes.
- Prove both queue triggers and both reconcilers through the real host: duplicate delivery, external custody/vehicle dispatch and unknown-kind denial; equal bytes/different occurrence; repeated DOCX placement order; same identity/different hash; Blob-before-SQL failure; SQL-after-Blob staging orphan; completion-before-Blob-tag and tag-before-staging-delete crashes; each work/poison queue's outage/replay; delayed poll; overlapping poll/reconciler denial; crash/cursor restart; five-attempt poison plus crash-between-mark/delete replay; malformed poison ID; corrupt/oversized/bounded/incomplete input; terminal dependency failure; and no bytes/content in queues/logs. `ReconcileStagedArtifacts` works in bounded batches from the durable staging ledger, completes/tag-cleans or retains operator-visible failures idempotently, and leaves no completed object outside the completed-only lifecycle rule.

### 5.3 Identity, authorization and administration

- Use ASP.NET Core Identity in the existing DbContext and OpenIddict server in Web. Passwords are one-way Identity hashes, minimum eight characters, no composition requirements, no persistent lockout and forced first change. The reviewed bootstrap manifest names Andrew and Alex as the initial Administrator assignments and the approved public client, while `InitializeApplication` prompts for their managed usernames/passwords and writes all initial data plus its completion marker atomically; no person/name/email is a compiled authorization bypass, and no password enters an argument, environment variable, file or log.
- Partition sign-in attempts by the configured trusted client IP at 10/minute and globally at 100/minute; reject spoofed forwarded headers, return one generic authentication failure shape, and use `429` plus `Retry-After` when throttled.
- Cookie: Secure/HttpOnly/SameSite, two-hour sliding idle, immutable original-issue claim enforcing eight-hour absolute lifetime, security-stamp/enabled/current-role revalidation. HSTS/CSP/antiforgery in deployed environments; trusted forwarded IP only from configured proxies.
- Every non-public route/action is authorised server-side and again in Core. Deliberately public routes are nonsensitive liveness/readiness, sign-in/OIDC endpoints, static assets and a valid request-token upload form. Disabled/role-changed users fail on the next browser/MCP validation.
- Administrator alone manages accounts, access reviews/roles, principals/successors, workflow gates and the approved mailbox allowlist through the staff UI. All three staff roles may perform case/intake/document/Triage transitions and the pre-Engineer gate.
- No public registration, external accounts, MFA, person-specific bypass, generic rules editor, cloud/secret UI or account administration through MCP.

### 5.4 Case identity, types and lifecycle

- Definitive authorised QDOS intake creates exactly one case. Automatically definitive complete instructions/images start `Review`; otherwise `Not ready`. Staff-resolved intake reaches `Review` only after explicit confirmation of both `Instruction complete` and `Images complete`; confirming both on an existing `Not ready` case moves it to `Review`.
- Before Engineer assignment, Core evaluates the versioned Administrator-managed completeness and review gates without deployment. When the completeness gate is enabled it requires staff-confirmed `Instruction complete` and `Images complete`; the review gate remains separate. Record the configuration version and outcome, never a hard-coded principal field matrix.
- Types: Inspection, standalone Audit, Inspection + Audit. Diminution and Commercial have no route/control. Standalone Audit requires retained original Engineer report and explicit assessment before allocation: Repairable maps to `a.`, Total loss to `ap.`. Triage evidence never supplies it. Inspection + Audit adds the later Audit display identity to the same case and sequence lineage only after the Engineer finding, and custody creates a nested folder named by that secondary Audit reference under the original Case/PO folder—never a second case or sequence allocation.
- Base reference is `{principalCode}{yy}{nnn}` from one atomic principal-lineage/Europe-London-year sequence shared by all QDOS types; allocate `001` through `999`, then fail the next attempt before creating any case/reference/history/outbox. Principal/reference become immutable immediately. Used-principal replacement creates a linked successor, deactivates the predecessor for new work, continues the shared cutover-year sequence and starts later years at `001`.
- Wrong principal closes original `Created in error`, requires reason, allocates a normal linked replacement, never reuses either reference and never reopens original.
- Active states: Not ready, Review, Report preparation, Post report; Held is an overlay preserving prior state and chase remainder. Terminal outcomes are Post-report completion, Provider cancellation, Collision Engineers rejection and Created in error. Inspection is an activity under Report preparation, not another state.
- Reopen requires reason and a normally valid nonterminal destination; never direct Held or Created in error. Archive is a reasoned read-only status on a terminal case, not deletion; no unarchive caller is allocated.
- Due by is the extracted inspection/equivalent deadline. Chasers use the same Europe/London wall-clock time seven calendar days after entering Not ready and every seven days thereafter. Held stores the prior state and remaining interval; release may return to that state or Review, with Not ready resuming the remainder and Review ending it. Material arrival and terminal closure stop future chasers. Generated chaser text and request links are copyable only—no outbound send; any manual-send record is an actor assertion, not delivery evidence.
- Record the Engineer report's Roadworthiness (`Roadworthy`/`Unroadworthy`) and Assessment (`Repairable`/`Total loss`) as case findings with source and reasoned supersession; Triage findings remain separate reference-only evidence. Staff may record the Engineer relationship assigned in EVA with provenance, but Pegasus never selects or assigns that Engineer. Every staff role can create, assign/reassign, complete and cancel a versioned case task through the Case page; stale/invalid transitions fail, cancellation is retained rather than deleted, and each action has attributable permanent history. Completeness/review outcomes, manual intake/image links and reasoned reversals likewise retain source origin.

### 5.5 Triage and report evidence

- Triage is a separate source-backed pre-case aggregate and queue. `ProcessQueuedIntake`/staff re-evaluation consumes the evaluator's Triage disposition through `CreateTriageFromIntake`, preserving immutable receipt/source/hash/evaluation identity and an idempotency key. It needs normalized VRM; otherwise the source remains `Needs sorting` and no Triage/case/reference is created. States are Open, Awaiting information, Finding recorded, Completed and Cancelled. Roadworthiness (`Roadworthy`/`Unroadworthy`) and Assessment (`Repairable`/`Total loss`) are independently optional, but at least one is required before Finding recorded or Completed; Cancelled is the only terminal path with neither. Assignee is optional; authorised staff can assign, reassign or unassign against the expected Triage version with permanent history. There is no due date/chaser.
- Completion requires the exact reply-chain Outlook Sent item from an approved mailbox. No subject, VRM, sender-only or manual-message fallback. Before-send correction requires reason; after-send correction supersedes the finding and requires a new response. Reopen to Open.
- A Triage links to at most one case, cases may have many; staff link/unlink/relink with reason. Link and findings affect no case/reference/lifecycle/Audit/final decision.
- Report evidence is one exact immutable Sent item, with mailbox, thread, `sentDateTime`, discovery/link times and policy/actor. Accepted auto-match or reasoned staff link may move to Post report; it never proves receipt or closes. Move/delete after confirmation does not erase evidence. Unlink/relink recomputes counts/history. Add no pre-send report-review gate and no report-send action.
- `Sent to Engineer` is recorded once on the first successful approved EVA bundle generation and explicitly remains a proxy. `Reports sent` counts each distinct successful report event.

### 5.6 Custody and request-scoped uploads

- Long-term live custody is Box; offline parity is a guarded local root through the same port. Create the case folder from Case/PO; retain original email/documents/images/correspondence/reports and every version/occurrence. Exact hashes may group review but never collapse occurrences.
- Guard root/descendant scope before every operation; persist remote/staged IDs, ancestry, versions, hashes, etag, semantic role and operation key. Closed-case files are application-read-only. Revision/logical removal requires reasoned reopen. Never hard-delete or accept arbitrary Box IDs.
- Staff view/download uses only an authenticated case-scoped document occurrence and explicit version; the Web caller revalidates case ancestry, version, hash and authorization, streams bytes with a sanitized filename, `nosniff` and private/no-store headers, and never exposes a filesystem path, arbitrary Box ID or bearer URL. Current active versions are the default; historical/logically removed versions remain explicitly identifiable and retrievable because evidence is never deleted.
- Staff export selects occurrences from exactly one case and streams a deterministic ZIP containing the explicitly selected versions plus a manifest of safe filename, occurrence/version ID, semantic role, size and SHA-256. It preserves repeated occurrences, resolves filename collisions without overwriting, never includes transient staging, and records actor/time/case/selected IDs/hashes/outcome in permanent history without bodies or URLs. Authorization, missing/corrupt custody, mid-stream cancellation and retry fail visibly; they never return a success history outcome or a cross-case/partial archive.
- `DOC-06` is distinct from `INT-31`: authenticated staff create/revoke a Box file request for missing information or images through the custody port; the offline adapter provides the contract-equivalent loopback flow and the approved live adapter copies one exact approved Box template request into the case folder, because Box does not create a request from nothing. Persist its remote identity/status and bearer URL only as purpose-scoped Data Protection ciphertext using the separate environment `BoxLinkProtection` ring; only authorised case callers may unprotect it, and neither SQL/query projections nor telemetry/history expose the value. Chaser text uses the protected retrieval path. Revoke by deactivation, never remote deletion. Prove idempotent retry, ring/key rotation and restart, unauthorised/wrong-purpose denial, unavailable/pending/unknown states and template/root/case isolation.
- Add manual WhatsApp material only as ordinary staff-uploaded evidence; no WhatsApp integration.
- `INT-31`: authenticated staff create a request associated to one case/request. The public URL contains a uniformly random 256-bit opaque token; persist only its SHA-256 digest and compare in constant time. Show only the upload form and immediate per-file receipt, never case/request/history. Enforce accepted expiry/reuse/file/count/byte/type/rate limits, antiforgery, no-store/no-index, token redaction, cross-request isolation, revocation and idempotent retry. An immediate `received` success requires hash-verified bytes in private staging plus an atomically committed receipt/custody work item; it never claims final Box filing. Staff see pending/failure, and final custody is recorded only after the Worker confirms the Box version. Malware scanning remains Not planned; never claim it.
- Transient staging has one completed-object lifecycle across intake and request uploads. After durable Box/local-custody version confirmation, SQL records `Completed`; the Worker conditionally tags the exact staged blob `PegasusDisposition=Completed`, attempts guarded immediate deletion, and reconciliation repairs a crash/fault between those steps. Offline cleanup and the Azure lifecycle policy delete only completed objects older than seven days as a backstop. Pending, failed, unmatched and orphan objects never match the rule and remain visible/recoverable until a reasoned operator resolution. Fake-time/local and Azure policy tests prove each classification, crash point and no failed-source expiry.

### 5.7 Vehicle, address and EVA

- Benchmark a mechanism-neutral candidate set on genuine labelled ordinary vehicle photos. Record and freeze the candidates, cohort/untouched holdout, exact-read/false-positive/abstention/latency/operator criteria, platform path, licence/security and any egress/cost criteria before unsealing the holdout. Select one accepted engine; if none meets the preaccepted gate, block the release rather than invent a fallback. Persist suggestion/provider-or-model/version/image evidence; staff acceptance creates provisional identity only.
- AI Centre owns this candidate experiment and evaluation protocol; the application never references, loads or deploys `workspaces/ai-centre`, and the plan names no preferred engine before the decision. An in-process winner must have a reviewed Windows-development/Linux-x64 release artifact and no Python service, Docker, runtime download or unreviewed model. An external winner requires one guarded Infrastructure adapter plus approved image-egress, credential, retention, latency, cost and failure contracts, with deterministic replay for offline work. Neither route may add a runtime/deployment unit or move normalization, uncertainty or staff acceptance out of Core.
- DVLA/DVSA replay consumes only approved ignored exact response fixtures and typed errors; missing fixture is Unavailable. Live adapter is suggestion-only and never overwrites confirmed data. Implement mileage estimation only after accepted source/rule; no valuation.
- A Core evidence mapper derives inspection mode only from the exact extracted physical-address evidence or exact `Image Based Assessment` instruction recorded by the selected policy revision. Show value/source/provenance; require staff to accept or correct a suggestion; leave missing/conflicting evidence unresolved. No spreadsheet, geocoder, AI or provider-default fallback.
- EVA remains manual. Before authoring mapping, accept the exact 13-key order, every source field (especially `Reference`), null/empty/date/mileage normalization, readiness, image selection/order/name and real drag/drop result. Generate deterministic UTF-8 JSON, selected custody-confirmed images and SHA-256 manifest; no EVA network call. First success records the proxy once; regeneration is a revision.
- Preserve the observed candidate key order exactly for the acceptance decision: `Work Provider`, `VRM`, `Vehicle Model`, `Claimant Name`, `Reference`, `Incident Date`, `Instruction Date`, `Inspection Date`, `Inspection Address`, `Accident Circumstances`, `VAT Status`, `Mileage`, `Mileage Unit`. Observation does not settle any unresolved source mapping.

### 5.8 Staff MCP

- Use the stable official C# MCP ASP.NET Core package verified at implementation time and one Streamable HTTP `/mcp` endpoint. OpenIddict exposes authorization-code + S256 PKCE, exact resource/audience/scopes, 15-minute access tokens, rotating refresh tokens bounded by the eight-hour session maximum, protected-resource metadata and pre-registered public clients only; no DCR/client secret/password/cookie at MCP.
- Every authorization request passes through the signed-in staff member's explicit consent page. Show the pre-registered client display name, exact MCP resource and requested scopes; permit approve or deny with no scope broadening. Persist an individual client+subject+scope authorization and its grant/revocation history, issue tokens only from that grant, and require fresh consent for a new client or additional scope. Deny/cancel returns the standard OAuth error without a grant or token; no Administrator or bootstrap action consents for another staff member.
- Register or revoke local/approved remote public clients only through explicit idempotent audited operator/bootstrap commands using reviewed non-secret client ID and redirect-URI metadata. Validate HTTPS and RFC 8252 loopback redirects. Revocation disables the client plus its authorizations/tokens; account disable/password/role change and `RevokeStaffMcpAuthorizations` revoke the affected authorizations/tokens before another tool call. Expose no OAuth-client administration UI and no DCR. Remote compatibility must use the client's supported custom-credential path and may not introduce a shared secret unless separately approved.
- Maintain one reviewed tool manifest mapping every discoverable tool to its Core use case, authorization policy/scope, input/output schema and accurate MCP `readOnlyHint`, `destructiveHint`, `idempotentHint` and `openWorldHint` annotations. Expose only the explicit case read/mutation, intake queue review/resolve/accept, Triage read/mutation, document metadata/custody, file-request, EVA and report-evidence allowlist. Client approval hints never authorize; each call returns version/lease/conflict/denial outcomes.
- Enforce 10 authorization/token requests per minute per client+source, 60 read tools and 20 mutating tools per minute per current user+client, with generic `429`/`Retry-After`, security-event attribution and no Core mutation on rejection. Exclude accounts, roles, principals, configuration, OAuth client administration, cloud/credentials, arbitrary custody IDs, generic email, permanent delete and AI queue/agent tools.
- Local acceptance uses a real OAuth/HTTP client and asserts discovery annotations plus register/use/revoke, token rotation, rate-limit and current-role behavior. Remote acceptance may use an approved compatible staff client (including Claude Desktop) with permanent attribution; unsupported client behavior never weakens OAuth or Core policy.

## 6. Professional Operations-first Web experience

Build every screen as a complete usable vertical slice when its use case lands; checkpoint 10 integrates and polishes the system, not rescues placeholder pages. Razor PageModels bind, authorise and translate only. Reusable Razor partials/view components own shell, page header, freshness, metric cards, status chips, errors, provenance, history, document rows, leases and reason dialogs; they do not become a second component library.

### 6.1 Information architecture and routes

| Route | Contract |
|---|---|
| `/` | Operations cockpit: Not ready, Review, Held, Needs sorting, Blocked intake, Triage, Due today, In today, Sent to Engineer today/week and Reports sent today/week. Every number distinguishes zero/unavailable/stale/partial/failure and opens its exact filter. Recent request uploads, expiring/failed requests and bounded mailbox-processing failures link to their owned workspaces. |
| `/Intake`, `/Intake/{id}` | Queue plus three-column source/evidence/decision workbench. Source identity, parser/evaluation version, fact/suggestion/confirmed states, missing/conflicts, assets and no-case consequence stay visible. Authenticated manual source intake enters here through `ReceiveIntake`/`ResolveIntake`/`AcceptIntake`; it never inserts a case directly. |
| `/Triage`, `/Triage/{id}` | Dedicated list/detail, state/findings, reply-chain evidence, optional assignee, correction/supersession and reasoned case link. No due/chaser controls. |
| `/Cases`, `/Cases/{id}` | Search/filter by Case/PO, registration, claimant, claim number, principal, stage/status, assigned Engineer, received/instruction date or range, and image- versus instruction-led origin. Progressive workbench: identity, overview/data/provenance, documents/images, vehicle/MOT, inspection address/mode, completeness, tasks/chasers, Box file request, Pegasus request-upload link, Report preparation, EVA, report evidence, lifecycle and history. Read-only until lease. |
| `/Operations/Email`, `/Operations/Requests` | Authenticated management for bounded approved-mailbox Received/Sent processing outcomes and for Box/Pegasus request links/uploads: state, case/principal, expiry/limits, last activity, failure/retry and exact owner action. No generic compose, arbitrary mailbox browsing, credential field or anonymous case disclosure. Creation remains case-scoped; these queues deep-link to the owning intake/Triage/case/request. |
| `/Administration` plus focused child workspaces | Administrator-only account/access/role, principal successor, configuration and approved-mailbox work. Name/map child routes when their real PageModels land; no generic rule editor, secret field or cloud operation. |
| `/Search` | Global exact search with query preserved in URL and authorised results only. |
| `/Account/*` | Sign-in, forced password change, sign-out and access denied. No registration. |
| `/Development/EmailEvaluation` | Administrator-only and Development-only; absent from production endpoint metadata and navigation. |
| `/Uploads/{token}` | Bounded anonymous request upload with no case/request disclosure. |
| OAuth metadata/endpoints and `/mcp` | Exact protected resource/audience and authorised MCP transport. |

Header order is `Operations | Intake | Triage | Cases | Administration | Search | User`. Hide Administration navigation for non-Administrators but retain server/Core enforcement. Remove Privacy, scaffold text, fake `CE` mark and retired `/Intake/Qdos`.

### 6.2 View decomposition and state ownership

| Surface | Page owner | Focused child regions |
|---|---|---|
| Operations | One cockpit PageModel owns the as-of snapshot and authorisation; focused Email and Requests PageModels own their URL query state | Freshness banner; metric strip; Case/Intake/Triage queue tables; exact-filter links; approved-mailbox processing outcomes; Box/Pegasus request/upload states; partial/error state. |
| Intake | Queue PageModel owns URL filter/page/selection; detail PageModel owns one immutable evaluation view and commands | Source preview; occurrence/assets; field evidence/provenance/conflicts; decision history; resolve/block/retry/re-evaluate/accept actions. |
| Triage | List/detail PageModels own query state and one Triage version | Identity/state; optional assignment; findings/supersession; reply evidence; case link; history. |
| Case | List/detail PageModels own search/filter and the loaded case/query version | Persistent identity/state header; overview/data; provenance; documents/images; vehicle/MOT; address/mode; completeness/tasks/chasers; Box/Pegasus requests and uploads; report/EVA/lifecycle/history; lease/conflict rail. |
| Administration | One landing PageModel links only workspaces whose commands exist | Account/access/roles; principal successor; approved configuration/mailbox. |

GET flows are Core query -> authorised immutable view model -> page/partials. POST flows are bound input + antiforgery + actor/version/lease/idempotency -> one Core command -> Post/Redirect/Get to the canonical URL, with field/summary errors retaining safe input. Durable domain state stays server-side; filters/sort/page live in the URL; JavaScript owns no business state. Extract a shared partial/view component only after the same semantic contract appears on multiple pages—no generic data-grid/form framework or client store.

Every protected route and action ships caller-tested unauthenticated, disabled-session, stale-role, denied, loading and success outcomes. Navigation hiding is presentation only: server authorization and the same Core policy run on direct HTTP and MCP calls, current account/role state is re-evaluated, and denial discloses no record existence or protected content.

Every query surface distinguishes loading, empty, successful, stale/partial with last-good time, transient failure/retry and unavailable. Every mutation presents validation, explicit confirmation where consequential, success, denied, stale-version, lease-lost, dependency-unavailable, idempotent replay, conflict and recovery without dropping safe input or claiming an uncommitted external effect.

### 6.3 Visual implementation

- Use only `design/brand/logos/logo_no_margin.png`, checksum `E7247BE45911C46905343473E4C57B9F6ED7A450563D19C508C2D9652C2C63E2`, without redrawing/recolouring/extracting it. Record its runtime destination and checksum; remove the unapproved fake mark and any unproven favicon link rather than inventing an asset.
- Generate CSS custom properties from the exact design tokens: red `#DB0816`, pressed/dark red `#8F1422`, red tint `rgba(219,8,22,.07)`, warm charcoal `#2C2A27`, ink `#16191D`, white `#FFFFFF`, light `#F5F4F2`, border `#E6E4E1`, muted `#6B6B6B`, success `#16833B`; amber incomplete/pending foreground `#7A3E00`, background `#FFF4D6`, border `#A15C00`; navy Review foreground `#143A5E`, background `#EAF1F8`, border `#365F87`. Map these roles in canonical token authority and never encode state by colour alone. Use the exact system UI stack and 4 px rhythm with `4, 8, 12, 14, 18, 24, 32, 40, 64px` retained steps, 1 px borders, 2 px corners and rare shadows.
- Dense desktop workbenches at 1280+ use persistent identity/state and meaningful panes. At 1024–1279 or 200% zoom, secondary panes become labelled tabs/drawers/ordered sections; identity, state, error and consequential actions remain first. Do not create a mobile product; show a supported-device notice only for a genuinely unsupported device, never as a CSS-width substitute.
- Use 44 px action targets, 24 px primary gutters, clear row selection and the exact 3 px `rgba(219,8,22,.38)` keyboard focus ring. Never encode state by colour alone. Motion is limited to understandable loading/refresh feedback and has a static reduced-motion equivalent.
- Replace current Unicode/emoji-like glyphs with only the required symbols copied byte-for-byte into a small local sprite from one source-verified pinned Lucide release; record upstream path/version/checksums in the design source/runtime mapping. Render the approved 24×24, 2 px, round-cap, `currentColor` treatment with adjacent/accessible names; add no 47 MB `lucide-static` package, CDN/runtime fetch or decorative imagery.
- Use real empty/loading/error/stale/partial/conflict/external-unknown content. Do not fabricate explanatory operational prose or synthetic business records. Operator copy uses settled business terms and never exposes Azure, OCR/AI, queue, parser, package or other implementation mechanics.
- Source preview renders safe extracted/plain text and protected image/PDF downloads; never execute inbound email HTML. Set CSP and content-disposition/type headers on protected files.

### 6.4 Interaction and accessibility

- Semantic landmarks, skip link, one `h1`, labelled nav, table captions/headers/sort state, associated field errors and error summary, restrained live regions, deterministic focus return and keyboard queue selection.
- Reason dialogs name requirement/consequence, require labelled reason, trap focus, support safe Escape/cancel and return focus to invoker.
- Case-edit JavaScript only acquires/heartbeats/releases a server lease and presents conflict/recovery; save always submits lease token and rowversion. A read-only viewer remains available.
- Preserve filters/page/sort in URLs. Manual refresh visibly updates timestamp. Search uses indexed normalised fields without leaking unauthorised records.
- Browser automation uses Microsoft.Playwright `1.61.0` and `Deque.AxeCore.Playwright` `4.12.0` unless a later stable compatible pair is source-verified and pinned before the first package commit. Every UI checkpoint runs the affected journeys against the pinned bundled Chromium in CI with fixed seed/data, viewport, colour scheme and reduced-motion settings; the final gate reruns all critical journeys using Playwright's `msedge` channel on the exact accepted Windows 11/Edge Stable build. Axe supplements, never replaces, keyboard, Narrator, forced-colours, 200%-zoom and operator inspection.
- Browser evidence is split by data class. CI may publish only shell/empty/security/technical-fixture screenshots, traces and axe reports after an artifact guard rejects message/document/image bodies, case/person identifiers, request/bearer tokens, protected-file bytes and unredacted URLs. Genuine/corpus journeys run with screenshot/video/trace capture disabled by default; any approved diagnostic capture remains in a separately approved restricted local store and is never CI/release-uploaded. The change record publishes only source SHA, browser/OS, seed/profile identifier, route matrix, counts/result and bundle/report hash. Retain the accepted non-domain CI bundle under the approved policy; these artifacts are regression evidence, not business acceptance.

## 7. Reproducible local development and tests

### 7.1 Tool profiles and commands

The offline profile uses Windows 11, PowerShell `7.6.3`, .NET SDK `10.0.302`, Node 24/npm 11, Python 3.11+ standard library for provider authoring only, Azurite `3.36.0`, Functions Core Tools `4.12.1`, SQL Server Express LocalDB, trusted Development HTTPS and pinned Playwright browsers. It requires no Azure/Box/Infisical login, Docker or vendor credential. Package/browser restore may use package feeds; ordinary Start/Smoke must not call cloud/vendor hosts.
Candidate-only VRM evaluation tools are separately pinned, licence/checksum verified and isolated under the AI Centre-owned `Corpus` harness; ordinary Offline initialization neither installs nor loads rejected candidates. For an in-process winner, the Offline doctor and release build verify only the accepted production model/native bytes for Windows development and Linux-x64 release. For an external winner, Offline uses accepted replay and production validates the remote configuration without selecting a local fallback. No candidate harness becomes an application runtime, and neither route may require Docker, a Python service or a runtime download.

Implement and document:

```powershell
pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline
pwsh ./scripts/Initialize-LocalDevelopment.ps1
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Status
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Smoke
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Stop
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Reset
```

- Doctor checks only the selected profile and prints exact CurrentUser repair commands; it never installs/logs in.
- Initialisation runs `npm ci`, installs pinned browsers, checks Development HTTPS/LocalDB/Functions/Azurite and creates ignored state only.
- Each Start creates a run ID, loopback ports, GUID LocalDB database, Azurite storage, mailbox/case-file roots, logs and ownership manifest under `artifacts/local-development/<run-id>/`; starts dependencies in order; runs explicit migration/bootstrap; waits for real readiness; starts actual Worker and Web.
- The one-shot bootstrap creates the configured initial assignments atomically and refuses a completed marker or nonempty unowned Identity state rather than duplicating/overwriting accounts. Human setup uses concealed prompts; the automated acceptance runner creates unique technical credentials in memory and sends them only over process stdin. Neither path places a password in an argument, environment variable, file, process listing, output or log.
- Status probes process ownership and application readiness, not just PIDs. Stop/Reset act only on resources whose manifest/run ID/path/database match. Ambiguity refuses action. Failed-run state is retained for diagnosis.
- Parallel run IDs must coexist. No operation touches tracked files, corpus, another run, Azure or predecessor resources.
- Production registrations fail at startup when required configuration is absent. A `DevelopmentOffline` adapter or development key outside Development fails startup; no silent fallback.

The optional Cloud profile pins the supported Azure CLI/azd/Bicep/GitHub/Box/SqlServer/Exchange tools, but neither login nor profile selection authorises a read/write. `Invoke-LivePreflight.ps1` is separately exact-target approval-gated; Infisical is not an alpha runtime or deployment dependency.

### 7.2 Test ownership and evidence

Use only the existing test projects:

- `Pegasus.Core.Tests`: policy, transitions, invariants, boundary/error/property cases and deterministic clocks.
- `Pegasus.IntegrationTests`: EF/LocalDB transactions and concurrency, Azurite, actual Functions process, WebApplicationFactory/actual Web HTTP, OAuth/MCP, adapter contract suites, Playwright/axe and approved live categories.
- `Pegasus.ArchitectureTests`: dependency direction, one policy owner, composition-only PageModel/trigger boundaries, no Development/live fallback and no forbidden workspace/application reference.
- Existing Web tests remain in the current owner if present; do not add a test project.

Test categories:

| Category | Input/dependency | Normal CI |
|---|---|---|
| default | deterministic Core/EF/Web contract tests | Yes |
| `LocalService` | LocalDB/Azurite/actual Functions host | Yes, Windows job |
| `Browser` | started offline system and Playwright | Yes, focused Windows job |
| `Corpus` | approved genuine ignored working copies/holdout; AI Centre-owned candidate evaluation plus application caller checks | No; explicit local evidence gate |
| `LiveIntegration` | exact approved Graph/Box/DVLA/DVSA/Azure targets | Never implicit; explicit approved run only |

Tests must assert observable caller outcomes and plausible failures, not source text or registration. Approved genuine local material is copied read-only into the run workspace with hashes; corpus is never modified, committed, uploaded or used from the forbidden subtree. CI may generate minimal non-domain container/format/fault fixtures, but never fabricates domain emails/images/work instructions or treats technical fixtures as business acceptance.
The current synthetic QDOS constructors in `MultiFormatIntakeWebTests.cs` and `ProcessIntakeTests.cs` are known baseline violations, not grandfathered evidence. Checkpoint 1 inventories every synthetic domain email/image/work-instruction fixture; checkpoints 4/5 replace their business assertions with approved repository examples under the guarded `Corpus` gate while preserving non-domain format/bounds/fault coverage in normal CI, then enable a repository guard against regression.
With `PEGASUS_REQUIRE_CORPUS=1`, a missing cohort/holdout, a skipped corpus fact, a changed input hash or a path under the forbidden subtree fails the command rather than producing a green partial run.
- A successful restricted Corpus run emits an allowlisted content-safe attestation only: exact source SHA/release-input hash/version/gate, opaque cohort/holdout set hashes and counts, policy/model/adapter versions, pass/fail counts, result and attestation SHA-256—never paths, filenames, source fields or content. The genuine inputs and detailed capture remain local and immutable. The final PR evidence block carries that attestation and its approved restricted location; hosted CI validates its schema, result, hash and exact-head bindings while independently rerunning every CI-safe test. CI never runs, receives or claims to reproduce the Corpus category.

### 7.3 Adapter contract parity

Define shared behavioral contract suites for mailbox, staged artifacts, custody, queue/outbox, vehicle/MOT and time. Deterministic local adapters, exact approved replay and transport/SDK fault injection prove success, denied scope, not found, immutable identity/version, idempotency, throttle/retry, terminal failure and cancellation. Separately approved live probes prove only their exact bounded success/scope/not-found observations and any naturally observed throttle; never drive a service into throttling without an exact safe-operation approval. Local/replay evidence never proves remote scope, throttling, delivery, retention or durability.
Local orchestration exposes explicit test-only failure controls scoped to one run ID: kill the Worker during intake and external-work dispatch; force Web or Worker startup failure after owned-resource acquisition; deny/interrupt Blob, Queue and SQL; exhaust a bounded staging volume/quota; and inject CPU/memory/processing delay at the adapter boundary. Acceptance proves nonzero/timeout propagation, child-process teardown, ownership-safe cleanup, durable staged/orphan/outbox recovery, poison visibility, no cross-run damage and eventual successful replay. These controls are unavailable in deployed configuration.

## 8. External/live implementation and deployment-readiness boundary

Live code starts only after the offline acceptance checkpoint and the exact contract/target approval for that dependency. Add one adapter at a time; failure never selects local behavior in a deployed environment.

### 8.1 Graph/Exchange

- Use the pinned Microsoft Graph .NET SDK from the package table, MIME/attachment reads, delta query and `Prefer: IdType="ImmutableId"` on every supported request. Local/replay contracts precede provisioning; only an approved provisioned target may prove the Worker managed identity's application token and Exchange scope.
- Scope `Application Mail.Read` through Exchange Online Application RBAC to exactly the four approved shared mailboxes: `instructions@collisionengineers.co.uk`, `desk@collisionengineers.co.uk`, `engineers@collisionengineers.co.uk` and `info@collisionengineers.co.uk`; remove/avoid additive unscoped Entra `Mail.Read`. Exchange scope is mailbox-level, so the application allowlist additionally permits Inbox ingestion only for `instructions@…` and Sent-evidence reads for the approved Sent folder in all four mailboxes; it denies every other mailbox/folder/action before Graph.
- Persist immutable message/folder/thread identity, delta link, received/sent times and MIME hash. No move/delete/mark/category/send. Recover `410`/cursor reset through an accepted bounded resync that remains idempotent.
- Pass local/replay contracts for the four-mailbox/folder/action matrix before the PR live checkpoint. After the approved Worker identity and Exchange policy exist, the post-provision gate uses `Test-ServicePrincipalAuthorization` plus managed-identity reads to prove every permitted mailbox/folder path and one out-of-scope mailbox control; that managed-identity proof is not a pre-provision checkpoint.

### 8.2 Box

- Use the current stable generated official .NET SDK only after exact enterprise, application/service user, root and operations are approved. Store Box key material only in the owning environment's Key Vault and retrieve it through the owning managed identity.
- Prove root ancestry before every SDK call; create Case/PO folder; retain file/folder/version IDs, etag and hashes; use version-only updates and application logical removal. No arbitrary ID or destructive delete.
- Box File Request creation copies one exact approved template request into the proven case folder. Allowlist that template ID separately from the descendant root, persist the returned request ID/status/protected URL, read/update only that request, and deactivate rather than call destructive delete. Prove wrong-template, wrong-folder and cross-case denial.
- Pass local custody contracts and an approved permitted/denied live fixture. After confirmed Box version, durable completion drives conditional tag/delete plus reconciliation; a Blob-index lifecycle rule matches only `PegasusDisposition == Completed` and deletes after seven days as a backstop. Failed, unmatched and orphan staging remains visible/recoverable and never matches that policy.

### 8.3 DVLA/DVSA and VRM

- Source-verify the official DVLA Vehicle Enquiry and MOT History contracts, licence, fields, limits, error/retry semantics and credentials. The operator accepts the mileage rule before code activation.
- Live responses remain suggestions with source/time. No valuation. Missing/denied/throttled stays visible.
- Run the accepted VRM mechanism selected under section 5.7. An in-process winner ships reviewed Windows-development/Linux-x64 model/native bytes with no runtime download or fallback; an external winner uses one guarded cross-platform Infrastructure adapter under the accepted egress/credential/retention/cost contract. Both preserve the same Core suggestion/uncertainty/staff-acceptance contract and deterministic Offline replay.

### 8.4 Azure preparation

- Refresh `docs/azure/current-inventory.md` only after exact subscription/resource-group read approval. Classify every resource by exact ID as predecessor-only, shared, data-bearing/undecided or Pegasus. Predecessor teardown remains a separate destructive change/approval and is not smuggled into this implementation PR.
- Prepare distinct Pegasus Development and Production targets in the existing `infra/` and `azure.yaml`; remove Document Intelligence and unused roles/config. Per environment keep one Linux Web App (Development F1, Production B1), one Linux FC1 Functions app, one Azure SQL logical server/database (Development Basic, Production S0), one Standard LRS storage account with the two work queues `intake-work`/`external-work`, their explicit Functions poison queues `intake-work-poison`/`external-work-poison`, and separate private transient-intake, Web-authentication-ring, Box-link-ring and deployment-package containers, one Standard Key Vault, one Log Analytics/Application Insights pair with 31-day retention, and distinct user-assigned Web/Worker identities. Preserve the accepted alpha public-service-endpoint topology and Azure SQL `AllowAllWindowsAzureIps` reachability with Entra/RBAC enforcement; private networking remains `Not planned`, not a dormant migration.
- Keep the existing one Linux FC1, .NET-isolated Worker deployment unit and its required `functionAppConfig`/deployment storage; adding one app per trigger would violate the accepted boundary. Use source-verified supported Bicep API versions, disable Storage shared-key/public-container access, retain Key Vault soft-delete/purge protection, and use managed identity for Functions host/business storage.
- Azure SQL is Entra-only. Before exact-target validation can bind an administrator object ID, the release owner uses a separately approved exact Entra write to create—or verify and adopt—one dedicated, assigned-membership, security-enabled, non-mail SQL administrator/migrator group; record its approved name, tenant and immutable object ID, prove it has no members and grant it no unrelated role. This identity prerequisite is not an application deployment. Bicep creates the Web/Worker user-assigned identities, sets that group as SQL Entra administrator and emits each identity's object/client IDs. The immutable schema bundle creates only reviewed least-privilege application roles/grants. During an approved migration window, the release principal receives temporary group membership, obtains a fresh token and applies the bundle. The committed bootstrap script then consumes the provisioned Bicep identity outputs, converts each SQL `UNIQUEIDENTIFIER` client ID to `VARBINARY(16)`, uses pinned `sqlcmd` with `ActiveDirectoryDefault` and strict TLS to create contained users by client-ID `SID`, `TYPE = E`, and assigns only the owning role. Remove membership, discard the token, wait for propagation/token expiry, acquire a new token and prove DDL denied before cutover; readiness uses read-only `HAS_PERMS_BY_NAME` denial and the deployed caller smoke proves each identity's allow/deny matrix.
- Use two physically separate environment Data Protection rings, Blob containers and versionless Key Vault protecting keys. The Web-only authentication ring protects staff cookies, antiforgery, OpenIddict state/tokens and no cross-process payload; grant only the Web identity its Blob/key access and production OpenIddict signing/encryption-certificate reads. A separately named `BoxLinkProtection` ring is accessible only to the Web and Worker identities and is instantiated solely for one versioned, purpose-scoped Box bearer-URL payload—never as the Web cookie/OpenIddict provider. Prove Worker denial to the authentication ring/key/certificates, cross-environment and unrelated-container/key denial, purpose separation, rotation with old-ciphertext read/new-key write, and backup/restore of both rings, protecting keys and certificates. Production refuses generated Development keys/certificates.
- The accepted public-endpoint threat model names Internet scanning/brute force, token/credential theft, SSRF, cross-tenant access and data exfiltration. Compensating controls are HTTPS-only/current TLS, HSTS/CSP/secure cookies/rate limits, no public registration, Entra-only SQL, no shared storage keys/public containers, Key Vault/Storage/SQL caller-specific RBAC and firewall/access rules, disabled FTP/remote debugging/SCM basic auth, no deployment role on runtime identities, content-safe logs and security/egress alerts. Validation proves every deny path and no broad wildcard role/firewall rule beyond the specifically accepted Azure SQL service reachability; any unsupported control blocks release or reopens the topology decision.
- Immediately before target validation, refresh the official Azure Retail Prices feed in GBP for the exact UK South SKUs/meters. Record retrieval date, query filters, meter/product/SKU IDs, unit rates, 730-hour convention, Development/Production workload assumptions and separate fixed/variable monthly totals for App Service, SQL/backup, Functions, Storage operations/capacity, Key Vault, Monitor/Log Analytics/Application Insights and bandwidth. State exclusions for VAT, negotiated discounts and Box/Microsoft 365/other-vendor licences. Compare the forecast with the accepted £35–£70 range and £75 alerting envelope; because £75 is not a cap, any variance requires an updated documented estimate and the named expenditure owner's approval rather than silent downsizing or automatic shutdown.
- Add correlated Web/Worker telemetry with accepted source sampling/daily cap and readiness. Azure Monitor alerts explicitly cover ingestion/processing latency or failure, poison/exhaustion, unmatched/matching failures, custody/Box, overdue `Due by`/chasers, EVA/export, authentication/security, database pressure, availability and cost, all through the approved Action Group without business content. Separately add the subscription-scope £75 combined Pegasus monthly budget filtered to the exact Development/Production resource groups, with explicit start/end dates and enabled actual 80%/100% plus forecast 100% notifications. Alerting never stops service or changes business state.
- Checkpoint 14 builds versioned candidate Linux Web/Worker packages plus Windows-x64 migration and one-shot Web-composition-root bootstrap bundles under ignored `artifacts/`. The bootstrap bundle accepts only the reviewed manifest, non-secret target/client metadata and concealed interactive passwords; it is the sole composition path for `InitializeApplication`, is never exposed by a deployed host and is never deployed as another unit. The build manifest emits source revision, `0.1.0-alpha.1`, target runtimes, SDK/package/tool versions, paths, lengths and SHA-256; exact-head CI independently reproduces and retains the candidate artifact for the accepted window. Before release deployment, the release owner builds once from the reviewed merge only if provenance changed, uploads the exact packages/manifest to approved private versioned custody, records version IDs/hashes/custodian/retention, and makes validation/deployment consume those bytes without rebuild.
- Define the release-input tree hash deterministically from tracked path, mode and file bytes for the entire reviewed repository except the sole outcome record `docs/changes/2026-07-27-qdos-alpha-reference-corpora.md`; exclude Git metadata, timestamps, ignored `artifacts/` and immutable `corpus/`. This deliberately makes any source, dependency, IaC, migration, script, runbook, product, design or other documentation change invalidate the release input. A post-deployment evidence commit may change only that one excluded outcome record, and the gate separately proves the exact base/descendant diff before accepting the unchanged release-input hash.
- Locally run restore/build/test, Bicep build/lint and deployment-plan consistency. Only when every implementation/local/replay prerequisite and exact target/identity/artifact-custody decision is complete may `.azure/deployment-plan.md` move from `Draft` to `Ready for Validation`; exact subscription/resource-group read and validation approval then permits the committed policy/quota/region/RBAC/what-if checks, and only a fully passing recorded result moves it to `Validated`.
- Never invoke `azd up` or another resource mutation from the implementation PR. The committed ADR-0009 release runbook pins `azd`, target and environment; after merge and exact write approval it permits `azd provision --environment <approved-environment>`, the separate SQL bootstrap/migration boundary, then `azd deploy web --environment <approved-environment> --from-package <hash-verified-web.zip>`, Web readiness, and finally `azd deploy worker --environment <approved-environment> --from-package <hash-verified-worker.zip>` plus Worker smoke. It refuses `azd up`, release-time `azd package`, unscoped/plain `azd deploy`, remote build, a path/hash outside the immutable manifest or a target mismatch. The release owner obtains/revalidates approved artifact custody before these commands and proves health/rollback custody after them. The executing harness uses its required Azure workflow skills without making tool names repository authority; every target-dependent managed-identity/remote proof follows provisioning and blocks cutover, not source completion.

## 9. Exhaustive 128-capability allocation

The unique-ID set below is the release contract. Checkpoint mapping is additive; the final evidence matrix names exact tests/callers and never drops an ID because live proof is pending.

| Family / owner | Exact Now IDs | Count | Primary checkpoints |
|---|---|---:|---|
| Evaluation / Intake | `EVAL-01`, `EVAL-02`, `EVAL-03`, `EVAL-04`, `EVAL-05` | 5 | 4, 12 |
| Mail / Intake | `MAIL-14`, `MAIL-15`, `MAIL-16`, `MAIL-18`, `MAIL-20`, `MAIL-21`, `MAIL-22` | 7 | 4, 7, 10, 12, 13 |
| Accounts / Identity | `ACC-01`–`ACC-11` | 11 | 3, 10, 11, 12 |
| Intake / Intake | `INT-01`, `INT-02`, `INT-03`, `INT-08`, `INT-09`, `INT-10`, `INT-11`, `INT-12`, `INT-13`, `INT-17`, `INT-18`, `INT-19`, `INT-20`, `INT-21`, `INT-22`, `INT-23`, `INT-24`, `INT-25`, `INT-26`, `INT-27`, `INT-29`, `INT-30`, `INT-31` | 23 | 4–9, 12, 13 |
| Triage / Intake | `TRI-01`–`TRI-09` | 9 | 4, 7, 10, 12, 13 |
| Cases / Intake | `CASE-01`, `CASE-02`, `CASE-03`, `CASE-04`, `CASE-07`, `CASE-08`, `CASE-09`, `CASE-10`, `CASE-11`, `CASE-12`, `CASE-13`, `CASE-14`, `CASE-15`, `CASE-16`, `CASE-17`, `CASE-18`, `CASE-19`, `CASE-20`, `CASE-21`, `CASE-24`, `CASE-25`, `CASE-26`, `CASE-27`, `CASE-28`, `CASE-29`, `CASE-30` | 26 | 6–10, 12 |
| UI / Platform | `UI-01`, `UI-02`, `UI-03`, `UI-04`, `UI-05`, `UI-06`, `UI-07`, `UI-08`, `UI-09`, `UI-11`, `UI-13` | 11 | 3–10, 12 |
| Documents / Integrations | `DOC-01`–`DOC-08` | 8 | 6, 8, 9, 10, 12, 13 |
| External / Integrations | `EXT-01`, `EXT-02`, `EXT-03`, `EXT-14`, `EXT-18` | 5 | 8, 9, 12, 13 |
| MCP / Interfaces | `MCP-01`–`MCP-04` | 4 | 3, 11, 12 and post-PR remote-client acceptance |
| Data / Integrations | `DATA-01` | 1 | 1, 12 |
| Operations / Platform | `OPS-01`, `OPS-02`, `OPS-03`, `OPS-04`, `OPS-05`, `OPS-06`, `OPS-07`, `OPS-08`, `OPS-09`, `OPS-10`, `OPS-11`, `OPS-13`, `OPS-14`, `OPS-20`, `OPS-22`, `OPS-23`, `OPS-24`, `OPS-25` | 18 | 1–14 and post-PR release |
| **Total** |  | **128** |  |

### 9.1 Executable application acceptance gate

- Implement `QdosAlphaAcceptanceGate` as a Core application service, with callers in the existing integration/architecture test harness and release script—not a staff UI, new project or generated status authority. It consumes typed observations from actual Web, Worker and MCP journeys: capability ID, scenario, caller, route/type/role variant, evidence state, source revision, product/assembly/package version, observed outcome and artifact hash. It rejects unknown, duplicate, skipped or unproved IDs, any value other than `0.1.0-alpha.1`, and any version/source/artifact mismatch; the ID set is repository-checked against the canonical 128-ID table above.
- The checkpoint-12 `OfflineCandidate` profile requires the complete 128-ID ownership map and every checkpoint-1–12 local UI/use-case, actual Web/Worker/MCP caller, case/route/role/failure, capacity, accepted evaluator/VRM cohort and untouched-holdout observation. It permits only the machine-checked checkpoint-13 production-adapter replay/contracts and explicitly target-dependent deployed/operator/management observations to remain `pending`; it is an interim coverage result, never an implementation or release claim.
- The `ImplementationCandidate` profile requires every ID to map to its real UI/use-case or declared non-UI owner, actual caller proof for every locally exercisable contract, the three QDOS case variants, direct/intermediary route matrices, roles, failure/recovery paths, accepted evaluator/VRM cohorts and untouched holdouts, and deterministic adapter contract/replay evidence. It permits only the explicitly target-dependent deployed/operator/management observations to remain `pending` and returns a candidate result that cannot be presented as released alpha.
- `CiHeadVerification` is a non-acceptance composition mode for hosted PR CI. It runs every CI-safe/default/LocalService/Browser observation, parses only the allowlisted Corpus attestation block from the pull-request event, validates its SHA-256/result and exact source/release-input/version/gate bindings, and compares the current 128-ID/pending-set schema. It fails on missing/stale/malformed fields and can report only CI verification—not `OfflineCandidate`, `ImplementationCandidate` or release status.
- The `ReleaseEvidenceCandidate` gate runs only at the exact head of the single-file release-evidence PR. It requires every `ReleasedAlpha` target observation, immutable release-input/artifact hashes, `OPS-23`/`OPS-25` approvals and the proposed `accepted` record, while proving the PR base descends from the deployed source and the tree diff contains only that outcome record. Its result is prospective and cannot authorize a tag, issue closure or released-alpha claim.
- The `ReleasedAlpha` gate runs only at the merged release-evidence commit. It proves the accepted record is repository authority, that the merge tree changes only the excluded `docs/changes/2026-07-27-qdos-alpha-reference-corpora.md` outcome record relative to the deployed source and that the exact deployed `0.1.0-alpha.1` release-input tree/artifact hashes remain unchanged, then requires every target-dependent Graph/Box/vehicle/VRM/MCP/managed-identity/RBAC/SQL/Blob/Queue/health/alert/restore/capacity observation, the genuine deployed QDOS journey, and recorded operator/management acceptance. Any pending, mismatched, stale, locally substituted or wrong-target observation fails before tag creation.
- Generated evidence bundles are ignored local artifacts or CI/private release artifacts under an accepted retention policy; only exact commands, revisions, target identities, hashes, results, limitations and durable artifact locations are recorded in the existing change record. The gate never rewrites product authority or creates a second status ledger.

`DATA-02` remains Next/unallocated. No `DATA-02` inspection-location/default import shape, runtime reference-data reader, migration, control or fallback is added; `EXT-18` uses only its accepted Core evidence mapper. All 32 Next, 40 Later and 29 Not planned IDs remain absent from callers/UI except stable seams explicitly required by a Now aggregate.

## 10. New-worktree and checkpoint-commit execution

### 10.1 Activation and worktree creation

No implementation command runs until the user accepts this plan, the current-repository GitHub issue is activated, and a separately reviewed prerequisite documentation change has landed on `main`. In a new clean short-path plan-activation worktree—not the current dirty worktree—record the accepted plan text/checksum and add accepted ADR `0014-reconcile-qdos-alpha-implementation-boundaries.md`; record the section 3 `DOC-CON-NNN` resolutions in the existing change record; reconcile capability/roadmap/gap/design/token/traceability/architecture/deployment-plan authority; and point the change record to the current repository issue. Commit only those plan/authority files, open the prerequisite PR against the recorded `origin/main`, obtain independent exact-head review and leave merge to a maintainer. It contains no production code and uses the same issue/change identity as implementation. The stale predecessor-repository issue URL is not assumed valid.

```powershell
git fetch origin main
$PlanBaseSha = (git rev-parse origin/main).Trim()
git show --no-patch --format='%H %cI %s' $PlanBaseSha
git worktree add -b docs/20260728-accept-qdos-alpha-plan C:\src\pegasus-alpha-plan $PlanBaseSha
Set-Location C:\src\pegasus-alpha-plan
```

If either activation branch/path exists or `origin/main` no longer equals the reviewed base, stop and select a unique recorded branch/path rather than reusing or force-moving it. After the prerequisite PR is merged by a maintainer, create the implementation worktree from that exact merge lineage:

Create the clean short-path worktree from the exact accepted `origin/main` head after that prerequisite lands. Commit `467284f` remains planning evidence only and must not be used as the implementation base:

```powershell
git fetch origin main
$BaseSha = (git rev-parse origin/main).Trim()
git show --no-patch --format='%H %cI %s' $BaseSha
git worktree add -b workflow/20260728-deliver-qdos-alpha C:\src\pegasus-alpha $BaseSha
Set-Location C:\src\pegasus-alpha
```

If the accepted base, branch name or path has changed/already exists, stop rather than reuse or force-move it; review the exact delta, update the accepted base, then select a unique delivery branch/worktree and record it in the same issue/change record. Verify the base contains the accepted canonical plan/change record and no unresolved canonical conflict marker. Freeze the accepted plan checksum and base SHA before creation. Do not copy `.git`, `artifacts/`, `corpus/`, local settings, package caches or credentials. Approved corpus material is accessed read-only and copied by hash into ignored run state only when its evidence checkpoint begins. The final PR targets `main` from this recorded base; later base movement is reviewed explicitly rather than hidden in an automatic rebase.

At every checkpoint:

1. Inspect current caller/owner and LSP references before exported-symbol changes.
2. Implement one complete vertical slice; remove superseded code in the same slice.
3. Run the focused caller smoke and tests listed below.
4. Update affected canonical product/design/architecture/operations/change evidence in the same commit.
5. Stage literal scoped paths only; inspect staged diff; commit with the exact checkpoint subject or an equally narrow factual subject.
6. Do not proceed on a failing gate. Do not amend/rewrite earlier checkpoints merely to make history prettier.

### Checkpoint 1 — reconcile authority and verify the imported baseline

**Change**

- Verify accepted ADR 0014, every section 3 `DOC-CON-NNN` resolution, corrected `TRI-04`/`UI-03`/`DOC-11`, the `EXT-18` evidence-mapper owner, exact state tokens, browser support, the three Now outcomes, design horizons and current-repository issue identity on the recorded base; do not reopen or silently reinterpret them during implementation.
- Update the reference-corpora change record's baseline/current-state evidence only; it remains the sole evidence/outcome record.
- Add a repository guard that excludes the forbidden v1 subtree from active plan/source inputs and rejects unresolved conflict markers in canonical product/design documents.
- Verify, do not regenerate unnecessarily, the immutable provider-domain source/package/hash/schema/migration/catalog and 128 unique-ID matrix.
- Compare the prior `c2c3ebd`/`175bc87` work at behavior/symbol level against current `Pegasus.*` callers and tests. Record which accepted Step 2 behavior is already present and which genuine gaps remain; do not import obsolete paths, stale documentation, `.pyc`, editor/config churn or oversized reference copies.
- Inventory every current synthetic domain email/image/work-instruction constructor, including `tests/Pegasus.IntegrationTests/Intake/MultiFormatIntakeWebTests.cs` and `tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs`; classify each as removable business evidence or permissible non-domain format/bounds/fault scaffolding, and record its checkpoint 4/5 replacement before enabling the regression guard.

**Exit proof**

- Repository policy reports 229 unique capabilities/128 unique Now with no markers.
- Provider `-Verify`, focused Core and integration tests pass at the exact head; generated bytes match committed bytes and no full address/local part exists.
- The issue, change record, plan, roadmap, gap and design matrix agree on target/evidence states.

**Commit:** `docs: reconcile QDOS alpha execution authority`

### Checkpoint 2 — make the offline runtime reproducible

**Change**

- Implement Offline/Cloud doctor profiles, dependency pins, initialization and run-scoped Start/Status/Smoke/Stop/Reset orchestration, including run-ID-scoped startup/process/storage-pressure failure controls that cannot be enabled in deployed profiles.
- Move Development migration/bootstrap out of normal Web startup. Add fail-closed `DevelopmentOffline` registration, actual LocalDB/Azurite/Functions/Web process readiness, child-process ownership manifests and ownership-safe recovery.
- Add pinned Playwright/axe dependencies to an existing test project; publish root quick start and local-development/testing runbooks.
- Cut over `Directory.Build.props` and `package.json` cleanly from `0.0.0-development` to `0.1.0-alpha.1`; expose that version plus source SHA only in the release manifest/non-sensitive diagnostics, and add a guard rejecting any assembly/package/runtime mismatch.

**Exit proof**

- Fresh worktree setup succeeds; actual Web and Functions host answer readiness.
- Two simultaneous run IDs remain isolated; forced Web/Worker startup failure, child-process termination and bounded local-volume exhaustion preserve diagnostics; Stop/Reset returns failure accurately and cannot touch another run or an unowned resource.
- Start/Smoke succeeds after outbound network is unavailable and constructs no live adapter/client; every induced failure cleans up or leaves an explicit ownership-safe recovery path.
- Restored assemblies, npm metadata and diagnostics all report `0.1.0-alpha.1`; the version guard fails on a deliberately mismatched package.

**Commit:** `build: make offline Pegasus runtime reproducible`

### Checkpoint 3 — add staff identity, security and administration

**Change**

- Add Identity/OpenIddict to the existing DbContext/migration stream; implement the transactionally atomic `InitializeApplication` command, singleton completion marker and local/release bootstrap caller; then add ordinary client registration, cookie/rate-limit/security-event behavior, security-stamp invalidation and Core actor/authorization/history ports.
- Replace the scaffold shell with a complete authenticated design-system shell and account/password/access-denied pages.
- Deliver account/access-review/role administration with negative Core and Web authorization. Add principal/config/mailbox navigation only when its real actions land.

**Exit proof**

- Real browser sign-in, forced first password change and sign-out work over Development HTTPS.
- Seven-character password fails; eight-character composition-free password succeeds; repeated failures throttle without persistent lockout; idle/absolute expiry and disable/role-change are clock/request tested.
- Parallel browser sessions prove that password change, disable, role removal and role elevation rotate the security stamp: the prior cookie cannot read or mutate newly unauthorized state, no in-progress form bypasses revalidation, and only a fresh sign-in receives the new role. Anonymous/wrong-role/antiforgery/stale-stamp actions fail without business mutation; business and security records remain separate.
- Bootstrap proof starts from the migrated empty database: the approved manifest creates exactly Andrew, Alex and the approved public client with the one-shot actor and permanent history, while concurrent invocation, altered manifest, non-empty target, partial-failure rollback, repeat invocation and any other command all fail without partial data or secrets. No deployed route or Worker service can resolve the bootstrap caller/actor; after completion, recovery uses authorised administration rather than deleting the marker.

**Commit:** `feat(auth): add staff identity and administration`

### Checkpoint 4 — deliver the evaluator and accepted mail policy

**Change**

- Refactor the current parser/extractor into one source reader plus Core route/classification policy; remove competing single-policy paths after caller migration.
- Add the authenticated Development evaluator, exact Received/Sent/Reply taxonomy, guarded run-scoped `unchecked`/`checked` roots, atomic idempotent review/move contract and deterministic evidence report.
- Execute the genuine QDOS cohort/untouched-holdout campaign for the activated direct/intermediary routes, extraction, Triage and exact-report predicates. Do not revive the superseded 88/56-candidate general-provider campaigns; additional provider routes remain unallocated. Record each accepted predicate separately; insufficient evidence remains fail closed.

**Exit proof**

- Single/batch graphical evaluation copies approved input by hash into its guarded `unchecked` root, atomically persists review and moves only that working copy to `checked`, writes ignored reports and creates no product case/reference; collision, filesystem failure and retry preserve source and first result.
- Repeat evaluation is byte/result deterministic; policy pin/re-evaluation history, direct/intermediary collision, malformed forward and `Other` reason work.
- Field/route/principal/conflict/unreadable/false-case report and untouched holdout are reviewed by the named operator; no corpus/source mutation.

**Commit:** `feat(mail): add the shared evaluation workbench`

### Checkpoint 5 — make intake durable through the real Worker

**Change**

- Add staged receipt/evaluation/outbox schema and `Receive`/`Process`/`Resolve`/`Reevaluate` operations.
- Stage Web/local-mail bytes in Azurite, atomically commit ID-only work, and implement the actual one-minute-default Worker timers, `intake-work`/`intake-work-poison` queues, intake trigger, poison reconciliation, staged-artifact reconciliation, outbox dispatch, cursors, leases and restart/recovery.
- Replace the development queue/review UI with authenticated Intake list and three-pane detail; correct DOCX traversal so repeated visible placements retain distinct occurrence identities in order, while preserving the remaining supported parser behavior and explicit DOC/MSG/OCR deferrals.

**Exit proof**

- Manual and local-mail delivery traverse actual Web/Functions/Azurite/LocalDB callers and persist one review outcome.
- Duplicate, hash conflict, equal-content occurrences, repeated DOCX-part placements, Blob-before-SQL and every SQL/tag/staging-delete crash window, work/poison-queue outage/replay, delayed/overlapping poll or reconciliation, Worker crash/cursor restart, five-attempt poison and reconciliation replay, malformed poison ID, bounded input, retry schedule and terminal failure preserve pre-case/no-reference invariants.
- The `intake-work` and poison payloads plus all logs contain only IDs/content-safe hashes and allowlisted telemetry; the real Functions host proves dispatch, poison and staged-artifact reconciliation, completed-object lifecycle tagging/cleanup and operator visibility.

**Commit:** `feat(intake): make intake durable through Functions`

### Checkpoint 6 — implement organizations, principals and immutable case identity

**Change**

- Add organization roles, principal/successor/sequence lineage, complete typed QDOS case model and one transactional `AcceptIntake`.
- Add the custody port plus `external-work`/`external-work-poison` queues, trigger, typed dispatcher and extension of poison reconciliation. Implement the minimal local custody handler required to create the Case/PO root and retain the accepted original source, including the nested secondary-Audit-reference folder for Inspection + Audit; identity may commit with custody outbox pending, but the case remains visibly blocked until the source version is durably confirmed.
- Add Administrator principal/successor UI, authenticated manual intake, Case list/identity/overview/provenance, all three alpha types and source-preserving manual links/reversal.
- Implement shared Europe/London yearly allocation, Audit gates/derived identity, completeness-created initial state, wrong-principal replacement and SQL constraints.

**Exit proof**

- Inspection, standalone Audit Repairable/Total loss and Inspection + Audit each allocate exactly one correct identity through Web; the combined case creates one original Case/PO folder plus its nested secondary Audit-reference folder without a second case/sequence. The actual Functions host proves duplicate custody dispatch, crash/replay, unknown-kind denial and poison visibility; injected custody failure preserves the one identity and blocks progression. Faults after custody confirmation/SQL completion/blob-tagging are reconciled idempotently and never make failed staging cleanup-eligible.
- Parallel LocalDB acceptance yields unique ordered references; duplicate accepts one; transaction rollback and the first attempt after allocated `999` allocate nothing; successor cutover/next-year and Created-in-error replacement preserve every identity.
- Standalone Audit with missing/ambiguous Audit evidence, and any non-QDOS/unknown/multiple route or unauthorized actor, creates no case/reference. Inspection + Audit with otherwise accepted Inspection evidence retains its one base Inspection case/reference and original folder but allocates no `a.`/`ap.` identity or nested Audit folder until an authorised Engineer records an unambiguous Repairable/Total-loss finding; both later identity variants and retry/idempotency are proved.

**Commit:** `feat(cases): add immutable QDOS case identity`

### Checkpoint 7 — complete lifecycle, Triage, work and email evidence

**Change**

- Add lifecycle/Held/chaser/completeness-review gates, complete versioned task create/assign/reassign/complete/cancel transitions, Engineer findings, terminal/reopen/reasoned-read-only archive behavior, edit leases and exact report evidence.
- Add the source-receipt-to-Triage branch, immutable origin/idempotency persistence, assign/reassign/unassign, Worker Sent poll, dedicated list/detail, findings/correction/reopen/link and exact reply-chain completion.
- Add local mailbox Sent identity/move/delete/throttle fixtures and all Operations query projections needed by these events.

**Exit proof**

- Full Not ready/chase/Held/Review/Report preparation/Post report/terminal/reopen flows pass at DST boundaries and through authenticated pages; archive is reasoned/read-only/non-deleting and has no discoverable unarchive action.
- Two browsers prove lease holder/read-only/expiry/stale-version/recovery with no overwrite or Administrator bypass.
- An evaluated Triage source with VRM creates exactly one source-linked pre-case record; duplicate/retry/re-evaluation reuses it, while missing VRM stays `Needs sorting` and creates no Triage/case/reference. Task and Triage assign/reassign/unassign pass through authenticated pages with history; stale/invalid/denied attempts fail. No-reply/ambiguous response cases fail, while findings/supersession/new response/link history never alter case decisions.
- Exact report staff/automatic evidence, move/delete finality and unlink/relink count recomputation pass without sending/receipt claims.

**Commit:** `feat(workflow): add lifecycle triage and email evidence`

### Checkpoint 8 — complete custody and request-scoped uploads

**Change**

- Add the local custody adapter and full document/version/occurrence upload, view, authenticated download and deterministic one-case export operations, Case/PO root, closed-case lock, logical removal and manual WhatsApp coexistence. Add the separate contract-equivalent Box file-request create/read/deactivate flow, protected link/chaser UI and explicit unavailable/pending/unknown states.
- Obtain and record exact `INT-31` lifetime/reuse/file/count/byte/type/rate limits; implement hashed-token creation/revocation, bounded public upload and same-custody intake.
- Extend the custody external-work handler through full version/request behavior, including retry/poison/reconciliation, and add document/custody/request panels with explicit current/historical versions, safe inline view/download, selected ZIP export and MCP-ready Core document contracts—not the MCP transport yet.

**Exit proof**

- Shared custody contracts prove root/descendant and template denial, versions, hashes, occurrence preservation, idempotency, closed lock/reopen, authenticated current/historical view/download, deterministic one-case export manifest/collision handling/history, Box file-request copy/status/deactivation/link behavior and no hard delete/arbitrary ID/path/bearer disclosure.
- The actual Functions host proves duplicate custody dispatch, dependency retry, crash/replay, poison and recovery from the ID-only durable row. Valid request upload retains each occurrence and returns only immediate receipt; expired/revoked/over-limit/wrong-request/replay/concurrent/cross-request/rate-limited/invalid-content attempts disclose no case state and cannot cross scope.
- Completion, tag and immediate-delete crash points reconcile idempotently; fake-time cleanup removes only completed staging after seven days. Pending/failed/unmatched/orphan staging stays visible and recoverable, and no fault claims successful custody.

**Commit:** `feat(documents): add custody and request uploads`

### Checkpoint 9 — complete vehicle, address and EVA boundaries

**Change**

- Run and record the AI Centre-owned mechanism-neutral VRM benchmark decision and accepted integration contract. Preserve one provisional Core suggestion/staff-confirmation flow and deterministic Offline replay. For an in-process winner, add only its reviewed pinned Infrastructure bytes/adapter here; for an external winner, defer the production transport to checkpoint 13d while replay proves the caller contract.
- Add DVLA/DVSA replay contracts, the accepted mileage calculation and vehicle external-work handler through the real queue trigger. Implement the exact `EXT-18` address/`Image Based Assessment` evidence mapper and staff accept/correct flow; add no provider/default/reference-data path.
- Resolve the EVA mapping from genuine cases and drag/drop; add exact deterministic bundle/version/manifest, selected custody images and once-only proxy.

**Exit proof**

- VRM cohort/untouched-holdout reports exact reads, false positives, abstentions/uncertainty, latency, platform and operator criteria; original/provider-or-model/version are visible; accepted suggestion never overwrites confirmed VRM.
- Vehicle/MOT fixtures and the actual Functions trigger prove dispatch, duplicate/crash/replay/poison, success/not-found/throttle/error/unavailable and deterministic mileage; no valuation.
- Exact extracted physical-address or exact `Image Based Assessment` evidence maps with source/version; accepted/corrected staff resolution persists history, ambiguity stays unresolved, and repository/runtime contains no `DATA-02` default.
- EVA repeat bytes/hash/order match, missing readiness/custody blocks, regeneration retains revisions and one proxy, and a real approved drag/drop succeeds without a network call/assignment claim.

**Commit:** `feat(integrations): add vehicle and EVA boundaries`

### Checkpoint 10 — finish the professional Operations-first UI

**Change**

- Complete Operations metrics/queues/freshness/filter links, focused Email and Requests/Uploads management, global search, Case progressive workbench, Intake/Triage/Admin visual integration and every required loading/empty/stale/partial/error/denied/conflict/external-unknown state.
- Finish approved logo/tokens/source mapping—including the exact amber/navy role values—constrained desktop/200% layout, semantic tables/tabs/dialogs and safe source/document rendering.
- Remove every scaffold/placeholder/Next/Later control and duplicated UI business rule.

**Exit proof**

- Every metric opens an exact query; London day/week, zero/unavailable/stale, mailbox outcome, request/upload and search filters are caller-tested.
- Playwright covers authenticated role routes and persisted effects at 1280+, constrained desktop and 200% zoom. Pinned Chromium CI plus the exact accepted Windows 11/Edge Stable build produce source-SHA-bound non-domain report bundles. There are zero unresolved axe violations; an individually documented tool false positive may be adjudicated only with equivalent DOM/keyboard/Narrator evidence and independent review. Keyboard-only, Narrator semantics, focus/error/dialog, forced-colours and reduced-motion inspection have no unresolved manual blocker.
- Named operator reviews complete flows using approved genuine immutable material with capture disabled; screenshots alone are not acceptance.

**Commit:** `feat(web): complete the Operations-first staff experience`

### Checkpoint 11 — expose authorised staff MCP through the same Core

**Change**

- Add production-safe OpenIddict signing/key configuration, protected-resource metadata, individual explicit-consent page and grant persistence, plus audited authorization/token/client revocation and one Streamable HTTP endpoint.
- Implement the reviewed per-tool manifest/allowlist as thin adapters over existing use cases and exact OAuth scopes, annotations and read/mutation rate limits; add idempotent local public PKCE client register/revoke commands and an actual HTTP integration driver.
- Add operator-facing OAuth-client registration/revocation/emergency-disable runbook and remote-client prerequisites without activating AI-09.

**Exit proof**

- Full authorization-code/S256 flow signs in one staff member, displays the exact client/resource/scopes, records that member's approval and obtains resource-bound tokens whose read/mutation effects are visible in Web/history; a second staff member must consent independently. Deny/cancel, additional-scope consent and grant revocation are caller-proved. Discovery exposes exactly the reviewed schemas/scopes and accurate read-only/destructive/idempotent/open-world annotations.
- Missing/expired/revoked/wrong audience/scope/resource, disabled/password- or role-changed user, revoked client/authorization, stale version/lease, excluded tool and per-user/client rate exhaustion all fail through HTTP without Core mutation; `429` carries bounded `Retry-After`.
- Browser/MCP parity and actor attribution hold; registration/use/refresh/revoke is audited, and no admin/cloud/generic-email/delete/AI tool is discoverable.

**Commit:** `feat(mcp): expose authorised staff use cases`

### Checkpoint 12 — prove the full offline alpha and capacity

**Change**

- Add/finalise caller-backed tests, actual-host smoke, browser/accessibility runner, backup/restore drill, failure/pressure controls, capacity harness and CI jobs. Implement the Core `QdosAlphaAcceptanceGate` and `scripts/Invoke-QdosAlphaAcceptance.ps1` as its fail-fast actual-caller orchestrator with run-ID evidence and unconditional owned-resource Stop/Reset in `finally`; fix product defects exposed by the gate.
- Update local/developer/testing/operations runbooks and the change-record evidence matrix with exact capability/caller/variant coverage, revision, command, result, artifact hash and limitation.

**Exit proof**

1. Fresh setup plus two-run/failure/reset scenario succeeds with no cloud credential/login/hostname; forced Web/Worker failure, Worker kill, SQL/Blob/Queue denial, staging-volume exhaustion and bounded CPU/memory/delay injection all retain observable recovery state, clean only owned resources and replay successfully.
2. All three QDOS case types complete intake through local custody, Box-file-request parity, EVA, report and terminal outcomes; Triage and the separate Pegasus request-upload link complete their own paths.
3. Negative matrix covers auth/session elevation and revocation, CSRF/open redirect/input injection, route, parsing, custody/download/export, allocation, lease, lifecycle, Triage, report, external credential redaction/revocation, MCP/OAuth and recovery boundaries.
4. Capacity uses an operator-approved immutable performance dataset representing one 2,000-new-case month and observed document/source distributions; absence of that dataset blocks the gate. Eight authenticated concurrent staff run a 30-minute mixed soak (60% dashboard/search/detail reads, 25% versioned case/task/Triage writes, 10% protected document reads/exports, 5% intake/request actions) while inbox and both queues process the measured peak burst. Excluding expected conflicts/denials, warm p95 reads are ≤2 s, warm p95 writes are ≤3 s, unhandled HTTP/Worker error rate is 0, no data/history is lost, queue lag returns below two poll intervals, and no process/SQL/storage resource stays above 80% for five minutes. One cold start may be measured separately and must restore readiness within 60 s. Record local/Azure non-equivalence.
5. LocalDB backup/restore plus Azurite/custody reconciliation restores a consistent run inside four hours; record that this does not prove Azure SQL 15-minute RPO.
6. Full restore/build/tests/repository check, exact-head CI and clean-workstation/operator runbook exercise are green. Each publishable browser/axe bundle records source SHA, OS/browser, seed/profile, routes, result and hash and passes the no-business-content/token/file artifact guard; genuine-data runs record only hashes/counts/results with capture disabled.
7. `OfflineCandidate` passes for exactly 128 IDs with every checkpoint-1–12 UI/use-case, role, route/type, negative, capacity, cohort and holdout observation; its pending set is exactly the applicable checkpoint-13 adapter-contract/replay work plus named post-provision/operator evidence. `ImplementationCandidate` deliberately fails on that adapter set and `ReleasedAlpha` also fails on target evidence, proving this offline checkpoint cannot overclaim completion.

**Commit:** `test: prove offline QDOS alpha acceptance`

### Checkpoint 13 — implement approved production adapters and bounded contract proof

**Change**

This checkpoint has four independently approval-gated sub-checkpoints. Each required slice must have its exact contract/owner/credential boundary before production code is added. Deterministic replay and SDK/transport fault injection are mandatory implementation-candidate proof; an exact already-existing disposable service target may add a separately approved bounded live read/write probe. Neither form is evidence for the future deployed managed identity, production root or remote endpoint; those remain post-provision cutover gates.

**13a Graph/Exchange:** add only the Infrastructure Graph adapter, production configuration validator and replay/live-test profile. Contract proof exercises the four-mailbox allowlist, `instructions@…` Inbox ingestion, all four approved Sent-evidence folders, pre-call folder/action denial, delta/MIME, immutable IDs, `410` bounded resync, SDK throttling/retry and absence of write/send operations. Record the Exchange Application RBAC grant/revoke/emergency-disable owner and runbook. Permission for all four mailboxes, denial of a fifth control and managed-identity caller proof can occur only after the target Worker identity and policy exist. **Commit:** `feat(mail): add the accepted Graph mailbox adapter`.

**13b Box:** add only the Infrastructure generated-SDK adapter, production configuration validator and accepted service-user/secret boundary. Local/replay contracts prove root/descendant and exact-template denial, folder/version custody, File Request template-copy/read/update/deactivate/link behavior and custody of Pegasus request-link uploads. If the approved disposable subtree and service user already exist, run the exact bounded fixture there; do not treat it as deployed-identity proof. Add provider-specific issue/rotate/revoke/emergency-disable and queued-work recovery steps, with secret-version selection through Key Vault and old-version denial where safely supported. Store bearer links through the accepted protection boundary, never in logs/history, and perform no destructive delete. **Commit:** `feat(custody): add the accepted Box adapter`.

**13c DVLA/DVSA:** add only the Infrastructure vehicle/MOT adapter after contract/licence/mileage and credential-owner acceptance. Replay proves success/not-found/denied/throttle/error, confirmed-value preservation, new/old credential cutover, emergency disable and queued-work recovery; an exact approved non-mutating account may prove bounded success/not-found/denied, but throttling is never induced. **Commit:** `feat(vehicle): add accepted DVLA and DVSA adapters`.

**13d external VRM, only if selected at checkpoint 9:** add the one guarded Infrastructure adapter under the accepted image-egress/credential/retention/cost contract. Replay proves response, uncertainty, timeout/throttle/error, new/old credential cutover, emergency disable, queued-work recovery and no confirmed-value overwrite; an approved disposable endpoint/account may prove only bounded ordinary-image reads. Omit this sub-checkpoint entirely for an in-process winner, whose accepted bytes are caller-proved at checkpoints 9/12. **Commit:** `feat(integrations): add the accepted VRM adapter`.

A hosted remote MCP client cannot be proved before a reachable deployment exists; checkpoints 11/12 prove the real local OAuth/HTTP caller, and deployed client compatibility remains a post-provision gate.

After each applicable sub-checkpoint, update the one change record with exact contract, target if any, caller, credential/RBAC owner, issue/rotate/revoke/emergency-disable evidence, result and limitation. Secrets/content must not appear in configuration output, logs, telemetry, history or PR.

**Exit proof**

- Every applicable adapter is reached through its real Core-owned port and caller, is selectable only by the approved live profile, and fails startup on incomplete contract/identity/credential configuration; disabled or unapproved adapters expose no route or Worker caller.
- Deterministic replay and SDK/transport fault suites pass for allowed, denied, not-found, retry/throttle, idempotency, recovery and terminal-failure cases. The `external-work` trigger dispatches both custody and vehicle values through the selected adapters with poison/replay proof; any separately approved bounded live fixture records only its exact scope and limitation.
- The change record names each applicable contract, target if any, credential/RBAC owner, issue/rotate/revoke/emergency-disable procedure and proof. No secret or business content appears in output, evidence artifacts, history or the PR.
- After all applicable 13a–13d commits, rerun the corpus-backed gate at that exact head: `ImplementationCandidate` passes for exactly 128 IDs and permits only the explicitly target-dependent post-provision/operator/management observations to remain pending; `ReleasedAlpha` fails on exactly that set.

### Checkpoint 14 — prepare the isolated Azure release

**Change**

- Under exact read approval, refresh inventory and target decisions. Record the Pegasus subscription/resource groups, approved SQL administrator/migrator group name/tenant, deterministic application-identity names/ARM IDs and Bicep output wiring, Action Group/recipients, billing scope and artifact-custody target; generated identity object/client IDs are captured only after approved provisioning. After static checks and under a separate exact Entra write approval, the release owner—not an implementation test or hook—creates/verifies the empty SQL group, records its object ID and binds that reviewed value into target validation. Keep predecessor teardown in its own linked destructive change.
- Require the checkpoint 2 `0.1.0-alpha.1` version to remain identical in `Directory.Build.props`, `package.json`, assemblies, package manifests and non-sensitive diagnostics, with source SHA as informational metadata; do not restamp during packaging. Add `scripts/Build-ReleaseArtifacts.ps1`, `scripts/Invoke-AzureDatabaseBootstrap.ps1` and `scripts/Test-AzureDeploymentPlan.ps1`; update existing Bicep/parameters/`azure.yaml`; remove Document Intelligence, Infisical runtime configuration and unused roles/settings. Enforce exact identities/RBAC/Key Vault custody, both work queues and both poison queues, completed-only seven-day Blob lifecycle, 31-day telemetry retention plus accepted sampling/daily cap, readiness and the named operational alert matrix/Action Group, Entra-only SQL, explicit schema/principal bootstrap and immutable package consumption. Add the subscription-scope £75 monthly budget filtered to both exact Pegasus resource groups with actual 80%/100% and forecast 100% notifications; pass explicit month-first start and supported end dates from reviewed release parameters rather than runtime `utcNow`. All generated packages/manifests/bootstrap SQL stay under ignored `artifacts/` or approved CI/release custody.
- Complete local Bicep/build/package checks, set `.azure/deployment-plan.md` to `Ready for Validation`, and—only after the SQL group prerequisite and exact subscription/resource-group/what-if approval—run the committed repository Azure validation/what-if procedure, resolve every failure and record `Validated`. The executing harness may use its authorised validation skill, but repository authority names only the reproducible command/result. Apart from the separately approved empty-group prerequisite performed outside the worktree, do not provision, deploy, change group membership or mutate any resource from the implementation worktree.

**Exit proof**

- Bicep build/lint, deployment-plan consistency and the approval-gated Azure validation/what-if are green at the checkpoint head. Evidence proves the bound SQL group object ID exists and remains empty; the preview names exact resource IDs, user-assigned identity outputs/role assignments, two work and two poison queues, 31-day retention, sampling/cap and every alert, the combined £75 budget's scope/start/end, two resource-group filters, actual/forecast thresholds and approved Action Group/recipients, and changes no retained/shared/predecessor resource. This is checkpoint proof only: checkpoint 15's final tracked documentation/evidence commit changes the release-input hash and must be followed by a freshly approved target validation/what-if at that final head. Without both previews, the PR deployment-ready finish line remains blocked.
- Checkpoint-head Linux Web/Worker packages and Windows-x64 migration/bootstrap bundles report `0.1.0-alpha.1`, embed that source SHA and match the checkpoint manifest hashes; production configuration cannot select local adapters, development keys or a runtime rebuild. Checkpoint 15 must reproduce them once more from its later final tracked head.
- Structural role review proves the template creates the two user-assigned identities and grants only the planned DDL/DML/storage/queue/secret scopes from their ARM outputs; every external identifier is configuration. SQL bootstrap consumes post-provision object/client IDs and proves runtime Web/Worker roles are disjoint. Actual managed-identity allow/deny evidence remains a mandatory post-provision gate.
- The release runbook preserves migration → Web live/ready → Worker live/ready ordering and names exact artifact custody; temporary SQL administrator/migrator membership; client-ID SQL bootstrap; concealed, transactional Andrew/Alex/public-client `InitializeApplication`; permanent post-completion denial and ordinary Administrator recovery; dependency/alert/managed-identity/remote-client smoke; trigger-by-trigger enablement; rollback/restore evidence; and every exact approval.

**Commit:** `ops: prepare the isolated Pegasus release`

### Checkpoint 15 — exact-head remediation, evidence and pull request

**Change**

- Remove temporary scaffolding, stale flags/routes/tests and obsolete aliases only after the full smoke is green. Update architecture, operations, design source/runtime mapping, capability/gap claims, current handoff and the single change record with the latest factual checkpoint evidence plus deterministic evidence locations; do not create a generated status ledger.
- Commit that complete tracked state as `docs: record the QDOS alpha candidate evidence` **before** final proof. From that commit onward, run the complete default/LocalService/Browser/Corpus local ladder, `ImplementationCandidate` gate, local Azure-plan checks and reproducible release-artifact build at the exact final source head; then, under renewed exact target/read/what-if approval, rerun target validation/what-if for that same source/release-input hash. Make no subsequent tracked edit. Retain the restricted Corpus detail locally; place only its content-safe attestation fields/hash plus run IDs, commands, results, limitations, target-preview result, release-input tree hash and artifact hashes in the PR/check evidence. Any failure, changed target state or tracked edit creates a new narrow commit and restarts the complete exact-head local ladder, artifact build, target validation, CI and review.
- After local exact-head proof is green, push the checkpointed branch and submit one PR to `collisionengineers/pegasus:main` linked to the active issue/change record. Include base/head SHAs, 128-ID evidence summary, the content-safe Corpus attestation and approved restricted location, approved non-domain report artifact locations, migration/deployment impact, exact external operations performed, deferred exclusions and remaining release-only gates; never attach corpus/genuine content or a restricted local capture. Hosted CI independently reruns the default/LocalService/Browser and reproducible-build/deployment-plan subset, computes the same source/release-input/artifact hashes and fails if the PR attestation is absent, malformed, non-passing or bound to another head.

**Exit proof**

- Exact-head local proof runs the complete ladder including the restricted Corpus category, reproducible artifacts and the separately approved target validation/what-if after the final tracked commit. Exact-head hosted CI independently runs every CI-safe/default/LocalService/Browser, repository, security and local deployment-plan check and reproduces the Linux Web/Worker and Windows migration/bootstrap artifacts from the recorded `0.1.0-alpha.1` release-input tree; it validates—but does not rerun—the content-safe Corpus and target-preview attestations. CI source SHA, release-input hash and artifact hashes equal the local final manifest, with no tracked file changes after any final proof.
- PR CI and both required attestation contracts are green at the exact head. An independent reviewer compares that head to its exact base, target preview and approved restricted evidence location and finds no unresolved blocker/required finding; remediation is new narrow commits followed by complete local rerun, target revalidation, CI and review.
- The PR remains open for maintainers. No merge, tag, production deployment, predecessor deletion or claim of released-alpha acceptance.
- Change record remains honest: implementation-candidate evidence may be complete, but deployed/operator/management states remain pending until the post-merge release sequence observes them.

**Commit before exact-head proof:** `docs: record the QDOS alpha candidate evidence`

## 11. Verification ladder

### 11.1 Canonical commands

From the new worktree and pinned tools:

```powershell
# Exact-head restricted local gate; never run on hosted CI
dotnet tool restore
npm ci
dotnet restore ./Pegasus.slnx
dotnet build ./Pegasus.slnx --configuration Release --no-restore
pwsh ./scripts/Build-ProviderReferenceData.ps1 -Verify
pwsh ./scripts/Invoke-RepoCheck.ps1
pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline
pwsh ./scripts/Initialize-LocalDevelopment.ps1
$CorpusRoot = (Resolve-Path -LiteralPath (Read-Host 'Approved immutable corpus root')).Path
pwsh ./scripts/Invoke-QdosAlphaAcceptance.ps1 -Profile Offline -Gate ImplementationCandidate -CorpusRoot $CorpusRoot -RequireCorpus
pwsh ./scripts/Build-ReleaseArtifacts.ps1 -Configuration Release -ApplicationRuntime linux-x64 -MigrationRuntime win-x64 -BootstrapRuntime win-x64 -VerifyReproducible
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local
```

After the final tracked commit, and only under renewed approval for the exact subscription, resource groups, SQL-group object and read/what-if operations:

```powershell
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Target -SubscriptionId $ApprovedSubscriptionId -DevelopmentResourceGroup $ApprovedDevelopmentResourceGroup -ProductionResourceGroup $ApprovedProductionResourceGroup -SqlAdministratorObjectId $ApprovedSqlGroupObjectId
```

Hosted pull-request CI has no Corpus path or material. Its Windows acceptance job runs:

```powershell
pwsh ./scripts/Invoke-QdosAlphaAcceptance.ps1 -Profile Offline -Gate CiHeadVerification -PullRequestEventPath $env:GITHUB_EVENT_PATH
pwsh ./scripts/Build-ReleaseArtifacts.ps1 -Configuration Release -ApplicationRuntime linux-x64 -MigrationRuntime win-x64 -BootstrapRuntime win-x64 -VerifyReproducible
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local
```

The restricted local `Invoke-QdosAlphaAcceptance.ps1` run expands visibly to the two-run/failure/reset scenario, owned Start/Status/Smoke, the complete default/LocalService/Browser/Corpus test matrix, authenticated journey, eight-user load, backup/restore/reconciliation, `QdosAlphaAcceptanceGate` evaluation, content-safe Corpus attestation and guaranteed cleanup; it fails on any skipped/duplicate/unknown capability, missing caller/variant/holdout evidence, changed corpus hash or stale artifact. The hosted `CiHeadVerification` invocation independently repeats every CI-safe part and validates the PR attestation bindings without accepting or reading genuine material. Focused commands may narrow projects/categories during checkpoints, but neither final path may silently skip its declared set. Live runners remain separately approval-gated. A file, DI registration or directly invoked service is never caller proof.

### 11.2 Mandatory failure matrix

| Boundary | Failures that must leave the invariant intact |
|---|---|
| Identity | anonymous/wrong role, seven-character password, bad antiforgery, per-IP/global limit, idle/absolute expiry, disable/role change, invalid OAuth resource/scope/client/PKCE. |
| Intake/mail | unsupported/corrupt/encrypted/oversized/bounded/incomplete, malformed forward, unknown/non-QDOS/multiple route, hash conflict, duplicate/replay, parser/dependency unavailable, policy re-evaluation. |
| Case/reference | missing Audit report/finding, first attempt after allocated `999`, concurrent duplicate, transaction rollback, successor overlap, principal/reference mutation, Created-in-error reopen. |
| Workflow/Triage | invalid transition/gate, Held destination, stale lease/version, task assignment/completion/cancellation conflict, Triage assignment conflict, missing VRM/finding/reply evidence, post-send correction without new reply, Triage-to-case influence. |
| Custody/upload | root escape/arbitrary ID/hash mismatch, Box/staging failure, closed revision, download wrong-case/version/path disclosure, export collision/corrupt source/mid-stream failure/cross-case selection, token leak/expiry/revocation/cross-request/replay/rate/size/type limit. |
| Vehicle/EVA/report | false/uncertain VRM, missing/denied vehicle fixture, overwrite confirmed data, unresolved address, incomplete EVA map/assets, duplicate proxy, ambiguous Sent match, Outlook move/delete. |
| Worker/operations | Blob-before-SQL, SQL-after-Blob orphan, queue outage, crash, poison/exhaustion, concurrent pollers, expired outbox claim, restore/reconciliation, unavailable/stale dashboard projection. |
| UI/MCP | hidden-only authorization, keyboard trap, lost focus/error, 200% loss of action/context, wrong-resource tool, direct storage/DbContext bypass, disabled-user cached token. |

### 11.3 Full offline journey

Drive the actual system, not fixtures alone:

1. Administrator bootstrap/sign-in and account/role setup.
2. Genuine QDOS direct and CE-forward intake through local mailbox, plus manual EML/freehand, PDF, DOCX and image-led sources through the same Core path; definitive Inspection creates one case, ambiguous source remains pre-case and DOC/MSG remain retained in Needs sorting.
3. Standalone Audit Repairable and Total loss with retained Engineer report; Inspection + Audit later identity; authenticated manual case creation re-enters the intake/acceptance path rather than inserting a case.
4. Completeness, Not ready chase, Held/release, Review, edit lease, task create/assign/complete/cancel, Report preparation and Engineer findings.
5. Documents/versions/download/export/manual WhatsApp, request link upload, vehicle/MOT/VRM/address review.
6. EVA bundle first generation/revision, local exact Sent report evidence, Post report, each terminal outcome, valid reopen, archive and wrong-principal replacement.
7. Separate Triage create/assign/unassign/find/correct/reply/complete/reopen/link path.
8. Equivalent authorised MCP reads/mutations reflected in Web/history.
9. Backup, destructive local-run-only fault, restore and reconciliation.

Each step records aggregate IDs, state/version, actor, persisted result and operator-visible result without recording business bodies/secrets.

## 12. Documentation and file ownership

Update in the checkpoint that changes behavior:

- Product: `docs/product/qdos-alpha-gap.md`, the five product areas, `docs/product/capabilities.md`, `docs/roadmap.md`, `docs/product/open-decisions.md`.
- Change/evidence: existing `docs/changes/2026-07-27-qdos-alpha-reference-corpora.md` and this implementation plan only.
- Design: `design/product/requirements.md`, `ui-spec.md`, `traceability-matrix.md`, source/runtime mappings, component/pattern/foundation indexes; brand assets remain authoritative.
- Architecture/operations: `docs/architecture.md`, `docs/operations.md`, `.azure/deployment-plan.md`, existing Azure inventory/replacement plan and runbooks.
- Developer/operator: root `README.md`, `docs/runbooks/developer-workstation.md`, `docs/runbooks/local-development.md`, `docs/runbooks/testing/local-testing.md`, and approval-gated predecessor/release runbooks.
- Source: existing `src/Pegasus.Core`, `src/Pegasus.Infrastructure`, `src/Pegasus.Web`, `src/Pegasus.Worker`, tests/scripts/CI/infra, plus `workspaces/ai-centre` only for its isolated VRM candidate/evaluation ownership; never add it to the solution, application references or deployment.

Do not update capability inventory as an implementation status ledger. Do not rewrite operator truth to fit code. Do not add generated handoff/status JSON or another workflow database.

## 13. Post-PR release handoff required for true full-alpha status

This sequence is intentionally outside the agent worktree/PR execution:

1. Maintainer reviews and merges the exact green implementation PR using repository policy; the release owner checks out that immutable merge and does not merge or rebuild opportunistic changes.
2. Verify or build the `0.1.0-alpha.1` release artifacts once from that merge, verify source/release-input hashes and provenance, record approved private Blob/GitHub custody and retention, and rerun the committed target validation/what-if under exact approval whenever merge provenance, artifact hashes or target state differ.
3. Obtain exact Development writes. Reverify that the prevalidated SQL administrator/migrator Entra group still exists, is empty and has the exact bound object ID; any drift stops provisioning and requires refreshed target validation rather than silent recreation. Provision the isolated infrastructure and distinct user-assigned identities; upload the exact packages/manifest to versioned private custody; and provision the approved Web-only authentication/OpenIddict Data Protection ring/key/certificates, separate shared `BoxLinkProtection` ring/key, plus third-party secret references. Keep Web cutover and every Worker trigger disabled.
4. Temporarily add the named release principal to the SQL group, obtain a fresh token, apply the immutable migration bundle, then run the client-ID-based Web/Worker SQL-principal bootstrap from the provisioned Bicep outputs. Using the reviewed Windows bootstrap bundle and concealed interactive password input—not arguments, environment, files or logs—invoke `InitializeApplication` with the exact accepted manifest to create the Andrew/Alex Administrator accounts with forced first change, the approved public MCP client/redirect metadata and immutable completion marker in one transaction. Prove a post-completion rerun and an altered manifest fail closed and are audited. Remove temporary group membership, discard the token, wait for propagation/expiry, acquire a new token and prove release-principal DDL/application-DML denial.
5. Deploy the hashed Web package only. Prove live/ready, version/source manifest, Web managed-identity SQL/Blob/Key Vault allow-and-deny, HTTPS/security headers and no local fallback. Sign in to each initial account, complete forced password change and verify current-role/session behavior; register/revoke a disposable public client record to prove the recovery path. Worker remains undeployed or trigger-disabled.
6. Configure and prove every target boundary whose identity now exists: Exchange Application RBAC permits the four approved shared mailboxes and denies a fifth control, with no additive `Mail.Read`; the application permits only `instructions@…` Inbox ingestion and approved Sent-evidence folders/actions; Box enterprise/service-user/root/template/disposable-subtree ancestry and custody; DVLA/DVSA and any external VRM account/egress; accepted credential rotation/revocation; Blob/Queue/SQL deny paths; Web-only authentication/OpenIddict ring/key/certificate access, shared purpose-scoped `BoxLinkProtection` access and Worker denial to all Web-authentication material; deployed OAuth/Streamable HTTP with the approved staff client; readiness and the full alert matrix. Read back the £75 budget scope/start/end, both resource-group filters, actual 80%/100% and forecast 100% notifications, and approved Action Group/recipients; under separate approval safely fire and acknowledge every alert/notification route without inducing spend.
7. Deploy the hashed Worker package only after Web readiness is green. Start with every business trigger disabled; prove Functions-host/deployment-storage readiness, Worker SQL/Blob/Queue/Key Vault/Graph allow-and-deny—including access to only the Box-link ring and denial to the Web-authentication ring/key/certificates—plus both work-queue bindings and both poison paths. Enable poison reconciliation first, then staged-artifact reconciliation, the `intake-work` and `external-work` queue triggers, outbox dispatch/recovery, due-work sweep, Sent-evidence poll and finally Inbox poll one at a time, only after each caller's dependency/deny/recovery gate passes; observe one full interval and durable cursor/outbox/reconciliation state before the next. Then run the end-to-end smoke. This preserves ADR-0009's migration → Web ready → Worker ready order.
8. In isolated Development, prove the accepted eight-session/2,000-case workload gate, 15-minute Azure SQL RPO/four-hour RTO restore, completed-only transient lifecycle, package withdrawal/caller disablement and retained-data recovery. Because this is the first Pegasus release, retain the accepted packages as the next release's rollback baseline and do not claim an earlier Pegasus package rollback.
9. Alex/relevant staff perform the genuine deployed QDOS journey and a compatible remote MCP journey with capture disabled; record `OPS-23` operator evidence. Resolve every failure before asking management to release Production.
10. Present the exact reviewed source/artifact manifest, 128-capability candidate evidence, Development `OPS-23` outcome, Production target/validation results, change window and rollback/withdrawal plan to Collision Engineers management; obtain and record `OPS-25` approval for that exact Production release before any Production business caller is enabled. Obtain the separate exact Production target/migration/cutover write approval, then repeat steps 2–8 against Production—including separate concealed initial-account/client bootstrap, migration → Web ready → Worker ready ordering and target-specific allow/deny/alert/restore smoke—using the same reviewed release-input manifest. Enable callers only in the approved order, complete the production QDOS journey and observation window, and retain rollback custody. A target/artifact/control deviation invalidates `OPS-25` and stops or withdraws cutover pending renewed approval; without the pre-cutover approval, leave every business caller disabled and the tag absent.
11. A maintainer opens one narrow release-evidence PR that changes only `docs/changes/2026-07-27-qdos-alpha-reference-corpora.md`, recording exact deployed source SHA/release-input hash, artifact/version IDs, target IDs, commands, observations, `OPS-23`/`OPS-25` approvals, limitations and durable evidence locations, and proposing the final `accepted` status/outcome. Because an unmerged PR is not repository authority, the accepted claim takes effect only on merge. PR CI runs `Invoke-QdosAlphaAcceptance.ps1 -Profile Deployed -Gate ReleaseEvidenceCandidate` at the exact PR head; this candidate gate requires the proposed accepted record, exact deployed-source ancestry, the sole allowed file diff, unchanged release-input hash, immutable `0.1.0-alpha.1` manifest and all 128 complete IDs. Any other tracked change—including a runbook or product edit—forces reviewed correction and repetition of the affected build/validation/release gates.
12. An independent reviewer approves and a maintainer merges that evidence-only PR only while its exact-head candidate gate is green. Run `Invoke-QdosAlphaAcceptance.ps1 -Profile Deployed -Gate ReleasedAlpha` at the immutable merge commit; it additionally proves the accepted record is now repository authority and that the merge tree is the guarded one-file descendant of the deployed source. No tracked acceptance change follows this proof. Only then tag that exact evidence commit `0.1.0-alpha.1` and close the issue. A post-merge gate failure disables/withdraws affected callers, leaves the tag and issue closure absent, and requires an immediate narrow corrective evidence PR before release work resumes.
13. Predecessor teardown runs, if still desired, only through its separate reviewed manifest/change/approval. Never begin by deleting `rg-collisionspike-dev`.

Failure at any release step keeps or restores the prior caller where applicable, leaves the new caller disabled/withdrawn, preserves the new data-bearing target for reviewed recovery, and leaves the change unaccepted and tag absent.

## 14. Explicit exclusions

No provider API; additional activated providers; four-mailbox email workspace/folder management (Administrator allowlist management and approved Sent-evidence use remain included); general email/image/case matching; DOC/MSG extraction; scan-PDF OCR; post-report query/dispute workspace; Diminution/Commercial; automated WhatsApp integration; automated outbound chaser sending (seven-day scheduling and copyable text remain included); direct EVA API/EVA replacement; valuation/estimating/invoice/report generation or automatic report sending (exact Sent-evidence matching remains included); guided capture/Tractable/Ravin/custom domain; external accounts; MFA; malware scanning; SMS/Teams/portal; predecessor data import; QA/UAT/staging/demo/training environments; GitHub deployment; slots/S1; private networking; zone/multi-region design; AI assistant/agent queue or AI-owned business policy. Do not add flags, routes, schemas, packages or placeholders for them.

## 15. Risks and stop conditions

- Genuine route/QDOS/Triage/report/VRM/EVA evidence is insufficient: stop that behavior; do not lower thresholds, invent data or activate a fallback.
- `INT-31` limits or external contracts are not accepted: do not author guessed business rules; the checkpoint and PR finish line remain blocked.
- Graph/Box/DVLA/Azure target approval is unavailable: local parity remains evidence only; do not label the implementation deployment-ready/full alpha.
- A live adapter cannot meet the Core contract: fix the contract only through accepted product evidence, otherwise reject the adapter.
- A new project/store/runtime/deployment unit appears necessary: stop and obtain an accepted ADR proving the current boundary cannot carry it.
- Any caller duplicates Core policy, a normal startup migrates, a deployed profile can select local adapters, or a queue/log carries content: stop condition.
- Current `main` changes during delivery: pause and review the exact base movement. If the original merge base remains reviewable and compatible, keep it and make the PR's base/head evidence explicit. If the accepted current `main` must be incorporated, create a new branch/worktree from that exact head and replay only the scoped QDOS commits through the repository-permitted non-merge route, recording the replacement checkpoint hashes; never merge, reset, force-move or hide the changed ancestry. Rerun the complete exact-head gate and independent review against the new base.
- Any authoritative business contradiction not covered above receives the next `DOC-CON-NNN` and direct user resolution before affected code.

## 16. Evidence sources

Repository authority used by this plan:

- `docs/index.md`, `docs/product/capabilities.md`, `docs/product/qdos-alpha-gap.md`, the five `docs/product/areas/*.md`, `docs/roadmap.md`, `docs/product/open-decisions.md`.
- Authoritative `docs/operator-notes/business-process/*`, `docs/operator-notes/product-requirements/*`, and the current Outlook/Box/EVA/WhatsApp/Excel/vehicle integration notes.
- `design/README.md`, `design/product/requirements.md`, `design/product/ui-spec.md`, `design/product/traceability-matrix.md`, foundations/tokens/brand/component/pattern sources.
- Accepted ADRs/decisions, especially ADR-0001/0002/0003/0004/0005/0006/0009 and Decision 0011; existing `DOC-CON-001`–`DOC-CON-011`.
- Current Core/Infrastructure/Web/Worker source, tests, scripts, CI, IaC, deployment plan and the active change record. Historical plans and predecessor material are evidence only.

Implementation-source references to refresh before package/API code:

- ASP.NET Core Identity configuration: <https://learn.microsoft.com/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0>
- Azure Functions Queue triggers and identity connections: <https://learn.microsoft.com/azure/azure-functions/functions-bindings-storage-queue-trigger> and <https://learn.microsoft.com/azure/azure-functions/functions-reference>
- Exchange Online Application RBAC: <https://learn.microsoft.com/exchange/permissions-exo/application-rbac>
- Microsoft Graph immutable Outlook IDs/delta/MIME: <https://learn.microsoft.com/graph/outlook-immutable-id>, <https://learn.microsoft.com/graph/delta-query-messages>, <https://learn.microsoft.com/graph/outlook-get-mime-message>
- OpenIddict server/PKCE: <https://documentation.openiddict.com/>
- Official C# MCP SDK: <https://github.com/modelcontextprotocol/csharp-sdk>
- MCP authorization/client registration, tool annotations and Claude remote-client compatibility: <https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization>, <https://modelcontextprotocol.io/specification/2025-11-25/server/tools> and <https://claude.com/docs/connectors/building/authentication>
- VRM candidate implementation has no preferred library/service reference in this plan. After the candidate set and gate are accepted, pin each candidate's primary source, licence, model/API version, platform/egress boundary and immutable acquisition metadata in the AI Centre evaluation record before execution.
- Official generated Box .NET SDK and File Request API: <https://github.com/box/box-dotnet-sdk-gen> and <https://developer.box.com/guides/file-requests/>
- DVLA Vehicle Enquiry and MOT History APIs: <https://developer-portal.driver-vehicle-licensing.api.gov.uk/apis/vehicle-enquiry-service/vehicle-enquiry-service-description.html> and <https://documentation.history.mot.api.gov.uk/>
- EF Core migration bundles: <https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying>
- Azure SQL Entra administrator and contained managed-identity users: <https://learn.microsoft.com/azure/azure-sql/database/authentication-aad-overview?view=azuresql#microsoft-entra-administrator> and <https://learn.microsoft.com/sql/t-sql/statements/create-user-transact-sql?view=sql-server-ver17#k-create-a-contained-database-user-from-a-microsoft-entra-principal-without-validation>
- Microsoft Go `sqlcmd` Entra authentication: <https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-authentication?view=sql-server-ver17>
- ASP.NET Core Data Protection Blob/Key Vault persistence: <https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0>
- Azure Developer CLI separate provision/deploy and hash-verified ZIP `--from-package`: <https://learn.microsoft.com/azure/developer/azure-developer-cli/reference#azd-deploy> and <https://learn.microsoft.com/azure/app-service/deploy-run-package>
- Azure Blob versioning/completed-only lifecycle management and ARM/Bicep what-if: <https://learn.microsoft.com/azure/storage/blobs/versioning-overview>, <https://learn.microsoft.com/azure/storage/blobs/lifecycle-management-policy-delete>, <https://learn.microsoft.com/azure/storage/blobs/storage-manage-find-blobs#platform-integrations-with-blob-index-tags> and <https://learn.microsoft.com/azure/azure-resource-manager/bicep/deploy-what-if>
- Azure Retail Prices API: <https://learn.microsoft.com/rest/api/cost-management/retail-prices/azure-retail-prices>
- Azure Consumption budgets and Monitor alerting: <https://learn.microsoft.com/azure/templates/microsoft.consumption/budgets> and <https://learn.microsoft.com/azure/azure-monitor/alerts/alerts-overview>
- GitHub Actions artifact retention: <https://docs.github.com/actions/using-workflows/storing-workflow-data-as-artifacts#configuring-a-custom-artifact-retention-period>
- Playwright .NET and Deque axe integration: <https://playwright.dev/dotnet/> and <https://www.nuget.org/packages/Deque.AxeCore.Playwright>
- Lucide source and static-package guidance: <https://github.com/lucide-icons/lucide> and <https://lucide.dev/guide/packages/lucide-static>

External documentation verifies API mechanics only. It does not supply Pegasus business rules, approval or live evidence.

