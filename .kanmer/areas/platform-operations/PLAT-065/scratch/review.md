---
kind: review-attestation
pr: "689"
head_sha: "fb00e457610535325de28d478c22eddef7c67705"
verdict: pass
reviewer: "/root/principal_delivery_audit"
independent: true
plan_hash: "1e53b90e34c01942"
ticket_updated: "2026-09-08T00:02:36.746Z"
board_sha: "f302c79cb2f217e4b2c005e2034d46a850e0f32e"
expected_reviewers: ["/root/principal_delivery_audit"]
threads_snapshot: []
findings: []
---

# PLAT-065 independent review

PASS for the approved seven-file infrastructure/configuration phase at the
exact head above. Author: /root/pack_reconcile; reviewer authored none of this
diff. Root retains merge authority. This is not a Done, provisioned, deployed,
live OCR, or Web-denial attestation.

## Inputs and scope

Read current ticket/plan, files 2a01b3a441157bc3, research
2965aedeaa0ce107, resolved questions 2f559090a25781ba, report
f8174b00fc9b7175, checklist 9cc1e06828d3dfb9 and root scratch/verify
93b1ed90987ae638; EPIC-011/014 context and current FRD-05, FRD-07,
ADR-0040. The current user/EPIC-014 authorization supersedes historical
exclusions. The plan explicitly stops this author phase at PR/Review; its
release and actual canary obligations remain unchecked.

Read all seven changed files against parent
32ce9544caa475e121ffb0f261ec045d9c04b46a: 202 additions/16 deletions.
Local and pushed head agree; author tree is clean. Later PLAT-072 source is
not falsely included in this tested parent.

## Acceptance and safety

- platform.bicep:299-321 adds one FormRecognizer S0/Standard account using the
  existing production prefix/suffix, location and tags. Its custom subdomain
  matches the account name; local auth is disabled. No account-side identity,
  storage role, secret, new module, package or runtime is introduced.
- The sole new assignment is deterministic and scoped to that account. It
  binds the existing Worker principal, not Web, to the approved built-in
  Cognitive Services User role. The built-in role also includes listkeys;
  this is expressly acknowledged in research, and disabled local auth prevents
  that from becoming a supported key-auth path. No Contributor/custom role.
- Worker alone gets DocumentIntelligence__Endpoint and depends on the
  assignment. Web gets neither endpoint nor role. Existing Worker identity,
  activation switches, function census and all other permissions are preserved.
  main outputs expose only the non-secret account ID and endpoint.
- Production callers remain WorkerDependencyInjection:45-57,105-110 through
  ProcessQueuedExternalWork/ProcessIntakeOcr/AzureDocumentIntelligenceOcr.
  WorkerAzureClientFactory:96-114 excludes non-managed-identity credentials.
  This diff activates configuration for that existing path, not another OCR
  implementation; DevelopmentOffline still rejects production settings.
- Test-AzureDeploymentPlan:297-311 replaces the obsolete blanket service ban
  with exactly the named ARM declaration, approved kind/SKU/local-auth shape;
  it rejects remaining Cognitive Services declarations. Existing
  Foundry/Maps/Vision/StaticWebApp and rogue Worker-setting guards stay intact.
  This is an existing source-template consistency guard, not an adversarial
  general Bicep parser or a substitute for the release preview.
- WorkerActivationReleaseContractTests checks exact name/subdomain, resource
  scope/principal, Worker dependency/unique setting and Web absence/output
  propagation. Its existing isolated validator test retains the original
  negative and adds six bounded forbidden-template cases.
- runbook/current-architecture/operations accurately separate the declared
  account and implemented OCR source context from absent deployment/live
  acceptance. The dated cost estimate is not a promised invoice or inferred
  monthly volume. Rollback removes Worker activation through the existing
  route, retaining the account, disabled local auth and durable evidence.

The ARM schema supports the declared custom-subdomain/auth/network properties.
Microsoft requires a custom endpoint for Entra authentication and documents
the selected role. Public primary-source checks only; no Azure estate call:
[ARM 2026-05-01](https://learn.microsoft.com/en-us/azure/templates/microsoft.cognitiveservices/2026-05-01/accounts),
[authentication](https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/versioning/sdk-overview-v4-0?view=doc-intel-4.0.0),
[built-in role](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/ai-machine-learning#cognitive-services-user).

## Verification evidence and retained attempts

Root's installed Bicep compilation passed. First Local validation failed at
the superseded service ban; the approved bounded guard correction followed.
Second Local validation failed at the DOCS permission-migration census; the
separate DELIV-050 correction landed in the exact tested parent. Both failed
attempts remain in the report and are not rewritten as passes.

Final root Local validation passed, followed by locked ArchitectureTests
restore and Release build (47.90 seconds, zero warnings/errors), then the
existing WorkerActivationReleaseContractTests|WorkerCompositionTests filter:
35/35 PASS, zero skips. Reviewer independently read TRX counters and hash:
artifacts/verification/plat-065-architecture.trx,
DA13856F70C992E13E698A85D346EB7FFAEEAB048E71D3FDA0A2AE0488CBEA97.
TRX UTC start 2026-09-07T23:56:43.4250527Z; finish 23:57:22.8795218Z.
Reviewer ran no build/test, snapshot capture, cloud write or provider analysis.

## GitHub and finding disposition

PR689 is OPEN, target dev, branch PLAT-065-document-intelligence, exact head
fb00e457610535325de28d478c22eddef7c67705. Live protection endpoint reports
Branch not protected (404); effective dev rules are []; check rollup is [].
No missing required check is bypassed. Root-directed skip-ci does not satisfy
the still-owed final converged release gate.

Complete review-thread query returned zero threads (hasNextPage false).
No external review findings exist. The bot's informational running-status
issue comment is not a defect or expected reviewer and is not a gate.
The sole assigned independent reviewer is settled by this exact-head record.
No findings require remediation.

## Remaining obligations

After root reads this whole verdict, root must freshly gather head, plan,
ticket, required policy/checks, threads and pushed board state before deciding
merge. Do not treat this attestation as authority for a later changed head.

Exact merged verification remains. TICK-085 canonical caller integration,
the authorized exact-SHA release/preview, fresh account/identity readback,
qualified canary and retained replay, readable-PDF non-OCR evidence, actual
Web-identity denial, and deployed operations/current-architecture updates
remain required before PLAT-065 completion. None is claimed by this PASS.
