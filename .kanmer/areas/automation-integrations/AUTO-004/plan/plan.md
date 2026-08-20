# Plan — AUTO-004: Restore Automation Actor parity for Unidentified and Triage

## Approach

Deliver [[AUTO-004]] and [[AUTO-005]] as one bounded Automation-ingress change: two domain-specific MCP adapters registered under the existing `automation.intake` scope, each calling the same Core queries/commands as the staff Web caller. Complete the orphaned Unidentified adapter and add a thin Triage adapter; reuse the existing resolver, auditor, error mapping, retained-source downloader, group store, Triage lifecycle, Case leases, and exact inventory test. This beats a generic queue/material framework because Unidentified and Triage have different identities and state models, and it beats separate staff/Automation services because ADR-0011 requires one Core policy owner.

## Governing docs

- **`docs/adr/0011-restrict-mcp-to-automation-actor.md` — Meets.** Every tool resolves the distinct Automation actor, calls the same Core use case as Web, records attributable history, and accepts no caller-supplied actor. No staff impersonation, management action, or second policy engine is introduced.
- **`docs/adr/0021-automation-actor-direct-write-assessment-contract.md` — Meets.** The change completes the comprehensive ordinary-`PerformCasework` inventory with logging parity under existing scopes and the shared composition/kill-switch boundary. The ADR's explicit professional Case-finding, report-approval, and outward-dispatch exclusions remain absent.
- **`docs/frd/frd-02-intake-and-source-identity.md` — Meets.** Exact U-reference reads preserve receipt/group identity, enumerate every grouped member, and retrieve bytes only through the existing integrity-checked source owner. U references never become Case/Audit/Image Intake/principal identities.
- **`docs/frd/frd-03-triage.md` — Meets.** Typed tools reuse the settled Triage states, findings, response evidence, reasons, replay/version rules, and Case association lease. Triage remains distinct from Unidentified. Assignment uses an explicit named-Engineer contract, not actor-relative “Assign to me”; that redesign is tracked by [[INTK-019]] and is not duplicated in this PR.
- **`docs/frd/frd-10-mcp-automation-and-actor-boundary.md` — Modifies with explicit operator authorization in this task.** Name the already-decided Unidentified/Triage typed inventory and parity boundary, including retained-source access, same-Core behavior, `automation.intake`, real-caller evidence, and the explicit separation of acting principal from selected assignee. This records behavior implied by ADR-0011/ADR-0021; it does not create a new architectural decision.
- **No new ADR.** ADR-0011 and ADR-0021 already own the actor, access, same-Core, comprehensive-inventory, and logging decisions.

## Steps

1. Establish focused integration fixtures and the governed inventory before changing composition.
   - Extend `AutomationMcpIngressTests.ExpectedTools` with the existing Unidentified names and the chosen typed Triage/source names so an omitted registration fails.
   - Add focused Automation Unidentified/Triage HTTP test classes using `AutomationMcpTestSupport` and existing SQL/Triage/intake fixtures; do not build a second MCP test harness.

2. Complete and register the Unidentified surface.
   - Register `UnidentifiedMcpTools` in `AutomationMcpExtensions`.
   - Keep existing list/get/resolve methods on `IUnidentifiedStore`/`IResolveUnidentified`.
   - Enrich exact detail through `IGetIntake` for Receipt origins and `IIntakeSubmissionGroupStore` plus member receipts for SubmissionGroup origins.
   - Add one exact bounded source-download tool that accepts the U-reference and, for a group, an exact member receipt identity; delegate bytes/hash/integrity validation to `IDownloadIntakeSource` and follow `DocumentMcpTools`' existing inline-limit response convention.

3. Add the typed Triage read/material surface.
   - Create `TriageMcpTools` with list-by-state/page and exact detail through `IListTriage`/`IGetTriage`.
   - Add exact Triage-origin source download through `IDownloadIntakeSource`; expose receipt/source metadata from the Core result, not direct persistence.
   - Resolve `AutomationMcp.IntakeScope` and audit every read/download with the existing conventions.

4. Add typed Triage lifecycle/evidence tools over the existing commands.
   - Separate tools for Awaiting information, record finding, supersede finding, link/unlink response evidence, complete, cancel, and reopen.
   - Construct every request from the resolved Automation subject; never accept actor input.
   - Preserve expected version, bounded reason, `mcp:` operation key, exact evidence identities, replay, and fail-closed state rules.
   - Do not implement assignment in this PR: [[INTK-019]] owns retirement of actor-relative “Assign to me” and the shared explicit named-Engineer assignment contract. Once that Core contract lands, staff and Automation must use it with separate actor attribution and selected assignee identity.

5. Add Triage Case association parity.
   - Expose typed link/unlink tools that accept the exact Case id/version, Triage version, active Case edit-lease token, reason, and operation key.
   - Delegate to `ILinkTriageCase`/`IUnlinkTriageCase`; callers obtain and release/consume leases through the existing Case MCP tools. Do not duplicate the Web page's lease choreography or add an alternate lease owner.

6. Register `TriageMcpTools` under the existing configuration gate and `automation.intake` scope, then prove the complete runtime surface.
   - Cover tool discovery, success, wrong-scope denial, malformed IDs/enums/keys, stale versions, replay/conflicting reuse, missing/incorrect evidence, source integrity failure, grouped-member selection, Case lease conflict, action-history success/failure, and domain-history actor attribution.
   - Verify management, external send, cross-identity actions, and the retired actor-relative “Assign to me” shape remain absent; explicit named-Engineer assignment remains [[INTK-019]].

7. Reconcile canonical documentation to evidence.
   - Update FRD-10 with the authorized parity behavior and explicit exclusions; leave FRD-02/FRD-03/ADRs unchanged unless implementation reveals a direct contradiction, which is a stop condition.
   - Update `docs/capabilities.md` MCP-03 status/inventory only to what the HTTP tests prove.
   - Update `docs/current-architecture.md` to the merged as-built inventory.
   - Update `docs/operations.md` only if an explicitly approved deployment occurs; otherwise retain the deployed inventory and state that the new source inventory is not yet deployed rather than claiming it live.

8. Run the required simplification and verification pass.
   - Review the branch diff through reuse, simplification, efficiency, and altitude lenses; specifically reject duplicate state/reason lists, generic queue/material wrappers, actor-relative assignment shortcuts, new scopes, direct stores where a Core use case exists, and repeated bounded-content code where the existing convention can be reused without inventing an abstraction for one caller.
   - Record findings/dispositions under a dated “Simplification pass” heading in this plan during execution.
   - Run locked restore/build, focused Automation/Unidentified/Triage/Core tests, full relevant IntegrationTests, ArchitectureTests, documentation-link checks, and the full solution suite proportionate to the final diff.

## Verification

Pre-merge evidence for the post-implementation report:

- `dotnet restore --locked-mode` or the exact locked-restore command owned by `docs/runbook.md`.
- `dotnet build Pegasus.slnx --configuration Release --no-restore`.
- Focused Core Triage/Unidentified tests.
- Focused `AutomationMcpIngressTests`, `AutomationUnidentifiedIngressTests`, and `AutomationTriageIngressTests` against the real in-process `/mcp` HTTP surface and SQL persistence.
- Existing QDOS Triage replay/Case-association integration tests.
- `dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build`.
- `pwsh ./scripts/Test-DocumentationLinks.ps1`.
- Full `dotnet test Pegasus.slnx --configuration Release --no-build` when the focused runs are green, recording expected corpus-gated skips separately.

Post-merge `proof.md` must verify the inventory and representative success/denial/history paths on merged code. Any production deployment/live inventory check requires separate explicit approval for the exact target and must refresh both current-state documents before closeout.

## Risks / open questions

- **Large typed inventory.** Mitigate with one thin class per domain and focused tests; do not compress unrelated commands into a generic action envelope.
- **Legacy Triage mutation actor strings.** MCP supplies only the resolved Automation subject and the auditor records actor kind. If a command cannot preserve the accepted authorization/history boundary without changing its Core contract, stop and make the smallest shared typed-`ActionActor` correction rather than authorizing in MCP alone.
- **Submission-group identity.** Require an exact member receipt id belonging to the U-reference's group; reject cross-group or missing members before download.
- **Content size.** Reuse the existing bounded-inline convention and integrity-checked Core download; never read the artifact store directly.
- **Assignment semantics.** [[INTK-019]] owns the explicit named-Engineer selection contract and retirement of “Assign to me.” This PR must not preserve or extend the obsolete actor-relative shape, nor pre-empt the shared replacement contract.
- **Deployment.** Not authorized by this planning task; source/PR work must not be described as deployed.

## Simplification pass — 2026-08-20

- **Reuse:** Kept every domain transition in its existing Core query/command. Reused the existing Automation resolver, auditor, error mapping, scopes, retained-source downloader, group store, Case leases, and HTTP harness. No second policy/state/reason vocabulary was added.
- **Simplification:** Kept one typed adapter per domain. A small shared `IntakeSourceMcpContent` helper is justified by the two concrete Unidentified and Triage callers and owns only bounded inline formatting; it owns no business policy. The Triage-local mutation helper removes repeated actor/audit/reload choreography without creating a generic queue/action envelope.
- **Efficiency:** Found and fixed grouped-source download loading every member receipt merely to validate one selection; it now checks the selected receipt against the already-loaded group membership. Full receipt projection remains only on detail reads where callers need it.
- **Altitude:** MCP remains a transport adapter. Direct store use is limited to the pre-existing Unidentified aggregate and the group membership port for which no Core query exists; integrity, Triage lifecycle/evidence, replay, authorization, and Case edit authority stay below the ingress.
- **Disposition:** Applied the efficiency fix and canonical U-reference validation. No remaining behaviour-preserving simplification finding warrants another abstraction or scope expansion.
