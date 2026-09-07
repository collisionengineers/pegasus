# Post-implementation report — PLAT-065

## Status

Author implementation frozen for root's focused verification; not reviewed,
merged, deployed or activated. This ticket includes live activation: IaC and
local checks alone cannot establish completion.

Branch: `PLAT-065-document-intelligence`; worktree `.worktrees/plat-065`.
Exact base: `d367219669ad26d5f2b727bd10b582330febc906`, including merged
TICK-041/ADR-0040. Initial plan `c83c5e07629abf54` preceded the claim and edits. Root-authorized
validator amendment `60bf50e7ac0832ee` preceded its correction edits.
Seven expected files only; no source application, package, schema or convention
change. Commit/PR pending matching root PASS evidence.

## Implementation

- Existing `infra/modules/platform.bicep` declares one FormRecognizer S0/Standard
  account using existing prefix/suffix, location and tags. Documented ARM GA
  `2026-05-01`; custom subdomain equals account name; local auth disabled.
  No account identity or service-side storage access is needed: the existing
  Worker posts retained bytes.
- A deterministic account-scoped `Cognitive Services User` assignment targets
  the existing Worker identity only. Worker settings receive the account
  endpoint and depend on the role assignment. Web receives no role/endpoint.
  Existing activation inputs and seven-function census remain unchanged.
- The existing Local/PreProvision validator's obsolete blanket OCR ban now
  permits exactly the declared FormRecognizer/S0 keyless account, strips only
  that account before checking for any other Cognitive Services resource, and
  preserves Foundry/Maps/Vision/StaticWebApp rejection. The existing isolated
  Local mutation test retains the rogue Worker-setting case and adds six
  focused negatives; no new mode, flag or test fixture framework.
- Existing platform/main outputs expose non-secret account ID and endpoint for
  exact release readback. No new parameter, module, runtime, flag, secret,
  custom role or permission framework.
- Existing `WorkerActivationReleaseContractTests` gains one focused declaration
  test for account kind/SKU/auth/name, exact role/scope/Worker-only principal,
  dependency, unique Worker endpoint and output propagation. Existing host and
  template tests remain in the selected root cohort.
- Existing runbook, current architecture and operations documents record
  activation/readback/rollback, actual source state, exact dated target/cost
  and absent live evidence. TICK-085 remains the canonical Glass's PDF import
  caller owner; OCR output alone is not accepted import.

## Production caller and evidence boundary

The existing caller is `WorkerDependencyInjection` → `ProcessQueuedExternalWork`
→ `ProcessIntakeOcr` → `AzureDocumentIntelligenceOcr`. The existing credential
configuration excludes all non-managed-identity sources; no new client is
introduced. TICK-041 carries source-context, retained-result recovery and
prebuilt-layout GA data API `2024-11-30`. The ARM version is independent.

Intended exact resource: subscription
`e6076573-23a5-46a8-acef-7e22d264e5db`, tenant
`858cf5b3-aa0a-47a6-9b40-4851fd0afa94`, `rg-pegasus-prod`, `uksouth`, account
`pegasus-prod-ocr-252ow37gij`. Worker principal
`4f4d9606-3634-4c21-a1ee-3238351cfc69`; role
`a97b65f3-24c7-4388-baec-2e87135dc908`, at account scope. No Web grant.
Dated retail reference USD10/1,000 selected pages; six-page canary USD0.06.
No commitment, add-on, training, budget increase or assumed monthly volume.
Root's later exact-SHA release and fresh preview remain necessary before any
cloud write. This author performed none.

## Validation attempts

1. Author `git diff --check`: PASS, exit 0; only normal LF/CRLF worktree warnings.
2. Root installed Bicep compilation: PASS (resourceCount 3); no tool upgrade.
3. Root Local deployment-plan validation: FAIL, exit 1 at line 65,
   `Deferred Azure services are prohibited from the alpha deployment.` Build/
   tests did not start. The guard correction above is frozen for focused root
   rerun; no author build/test.
4. Root second Local deployment-plan attempt: FAIL, exit 1,
   `Database bootstrap must account for grant-carrying migration 20260907210000_ReportInputInvalidationPermissions.cs.`
   The corrected Document Intelligence guard no longer blocks. Build/tests
   again did not start. Root owns this separately tracked DOCS-020-derived
   bootstrap census correction; do not change bootstrap under PLAT-065.
5. Live provisioning, Worker canary/replay, readable-PDF no-OCR route and actual
   Web identity denial: NOT RUN. No provider request or endpoint activation.

The failed Local attempt remains recorded. A later pass does not erase it.
No production behavior, authorization rule or assertion was weakened to hide
it: the obsolete service exclusion now matches the approved ADR-0040 resource.

## Simplification and scope review

Reused the existing module, identities, Worker setting, role-assignment pattern,
output surface, contract-test class and canonical documentation. No competing
business policy, deployment route, data-plane client, OCR orchestrator or test
framework. No unrelated changes and no feature behind a new flag. Source
configuration is distinguished from deployed capability throughout.

## Remaining items and handoff

Root runs the plan's Bicep build, `Test-AzureDeploymentPlan.ps1 -Mode Local`
and architecture filter `WorkerActivationReleaseContractTests|WorkerCompositionTests`,
with any necessary incremental build owned by root. After matching PASS,
author commits/pushes this exact seven-file candidate and opens a dev PR for
independent review; no self-review or merge.

Root later owns authorized release/provisioning and live evidence after
TICK-085 integration. Preserve retained source/results and disabled local auth
on rollback; no account deletion or key fallback.

Additional ADR-0040 ticket `link_doc` was refused because the MCP's configured
shared repoRoot does not contain the newly merged file. The exact dev-based
worktree does; existing FRD refs and the plan/source ADR link remain. This is
not an excuse to mutate the user checkout or claim the additional ref exists.
