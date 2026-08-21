# Release 16 research — estate, merge audit, deploy inputs

## Estate at start (2026-08-21, all read-only checks)

- Production serves release 15: code SHA 6d04f89d, image digest sha256:07c05faa…, migration head 20260820144004_RetainedMailFolderMoves, main=f0b01f39.
- origin/dev ahead of main by: #490 (tick-052/MAIL-10), #491 (tick-057/UI-14), #492 (tick-056/UI-10) — merge commits carrying stacked branches tick-047/049/050/051/053/064 — plus squash merges #493 (vehicle-lookup grant), #494 (extraction letter shapes), #498 (message page + MAIL-006/008/PLAT-019), #499 (DOCS-006 evidence images), #500 (MAIL-007 footer trim), #501 (INTK-025 report facts/circumstances). Phase 0 added 82fd4fd3 (local design mockups + reference sync) and e9cdf2b2 (principal-rules-and-mappings docs).
- Five open PRs at start: #470 AUTO-004/005, #473 MAIL-004, #495 ENG-007, #496 PLAT-020, #497 PLAT-021 — operator decided: review + merge all five into this release.
- All 16 non-kanmer worktrees clean (0 dirty files each).

## Lost-work audit (Phase 2)

`git cherry origin/dev <tip>` = 0 unmerged patches for every branch merged since f0b01f39: tick-047, tick-049, tick-050, tick-051, tick-052, tick-053, tick-056, tick-057, tick-064, intk-020, intk-021, plat-014, case-005-allocation-deadlock, vehicle-lookup-grant.

Overlap spot-checks on dev head (mail workspace touched by both #490–#492 and #498/#500): quick-preview markers (14 hits `data-mail-preview` in Mail/Index.cshtml), queue refinements, Deleted Items search, case-association handlers (Message.cshtml.cs), folder-move dialog + recommendation (Message.cshtml) — all present. The codex-added Mail browser/web tests also ran green in the #498/#500 merge CI, which is the systematic evidence beyond the spot-check. Grants census overlap (#493 vs #496) resolved by the review fix on the PLAT-020 branch.

Conclusion: no lost work in any merge since the last deploy.

## Review findings on the open PRs

- **#495 ENG-007** — passes: matches plan (single shared CaseDetails projection, page-local selection, Confirmed→Fact→observation precedence, case-insensitive mileage units); one fewer query per Assessment GET.
- **#497 PLAT-021** — passes: one scheduled-query rule with three explicit branches (failed-operation 5m join, ≥3-distinct-operation persistence, ≥3-minute-bucket uncorrelated), PT15M window, Web 5xx alert untouched; alert-rule change deploys with the release provision.
- **#496 PLAT-020** — defect found and fixed in review: migration 20260821100623_GrantImageIntakeLifecycleUpdates (UPDATE on ImageIntakes to both roles) was missing from the applied-migrations census (IntakePersistenceIntegrationTests) and the bootstrap grant matrix (Invoke-AzureDatabaseBootstrap.ps1) — the matrix's own comment requires the extension. Fixed as review commit 3850a97b. No overlap with 20260821095500 (different table/permission). `changes` CI failure was the stale-merge-ref shape → close/reopen applied.
- **#473 MAIL-004** — passes with actions: dev merged in (census conflict resolved in migration-ID order — 20260820114412 sits between 100724 and 144004; capabilities.md resolved as dev rows + branch MAIL-13 row; bootstrap union). PR-026's outstanding visual gate (local rendered desktop/200%-zoom inspection of /Administration/MailCategories) performed by claude-code and recorded on MAIL-004 scratch; design/README.md gate wording closed (f9876cfe). Open questions all resolved/parked.
- **#470 AUTO-004/005** — passes: MCP tools delegate to existing Core ports with actor/operation-key/version discipline; no policy in Web; ingress remains composition-gated off in production; open questions parked with reasons; docs (capabilities, current-architecture, FRD-10) updated in-branch.

## Deploy inputs

- New migrations this release: 20260820114412_ApprovedOutlookCategoryCatalogue (schema — new ApprovedOutlookCategories table), 20260821095500_GrantWorkerVehicleLookupRequests, 20260821100623_GrantImageIntakeLifecycleUpdates (both grant-only). EF applies pending migrations regardless of already-applied later IDs; census asserts fresh-DB order.
- Route facts honored (release-9/15 memory + runbook): oras cp --from-oci-layout for the image (no docker); never azd env refresh (copy .azure from main checkout); efbundle from src/Pegasus.Web with the Production env set + AZURE_TOKEN_CREDENTIALS=AzureCliCredential; worker via az functionapp config-zip; App Insights capped 0.1 GB/day — verify worker via admin host status + ApprovedInboxPollStates.
- Production identity: sub e6076573-23a5-46a8-acef-7e22d264e5db, rg-pegasus-prod, ACR pegasusprodacr252ow37gij, web pegasus-prod-web-252ow37gij (FQDN ashymushroom-676209e5), worker pegasus-prod-worker-252ow37gij, KV pegasusprodkv252ow37g. PEGASUS_WORKER_ACTIVATION=approved-live-worker must be retained; absence is a stop condition.

## Ticket roster for post-deploy verification

Mine (remediation): CASE-010, INTK-023, INTK-024, INTK-025, MAIL-006, MAIL-007, MAIL-008, PLAT-019, DOCS-006. Codex mail set: TICK-047, TICK-049, TICK-050, TICK-051, TICK-052, TICK-053, TICK-056, TICK-057, TICK-064 + verifying children PR-013…PR-037. Newly merged: ENG-007, PLAT-021, PLAT-020, MAIL-004 (+PR-027/PR-028), AUTO-004, AUTO-005. Hygiene-only: PLAT-014 (already live since release 15). Out of scope: older verifying tickets (e.g. TICK-102/104). Post-alpha capabilities that are composition-gated off get honest deployed-and-gated notes and stay at verifying unless their own acceptance is met.
