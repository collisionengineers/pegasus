---
id: DELIV-037
type: ticket
title: >-
  Release 37 — EPIC-011 operations workspace, Provider API activation, and the
  API-01 residuals
status: done
area: delivery-repository
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-08-30T17:21:26.961Z'
  implementing: '2026-08-30T17:21:38.165Z'
  review: '2026-08-30T17:21:45.574Z'
  verifying: '2026-08-30T17:21:52.875Z'
  done: '2026-08-30T17:21:58.344Z'
labels:
  - release
  - requires-live-approval
groups:
  - EPIC-011
links:
  - TICK-058
  - AUTO-012
  - AUTO-013
archived: false
created: '2026-08-29T20:40:03.988Z'
updated: '2026-08-30T17:21:58.344Z'
---

## What

Promote `dev` to `main` and deploy release 37 to `rg-pegasus-prod`. This is a
**full release**, not a promotion-only pass: it carries application code,
infrastructure, runtime configuration and migrations.

Release 36 (2026-08-28) is what production serves today. Live state read
read-only on 2026-08-29 before any write:

- Web `pegasus-prod-web-252ow37gij`, active revision
  `pegasus-prod-web-252ow37gij--84132d01ccb0`, image digest
  `sha256:5ba65f61ad754639185764ed2c7795fc06938e6e397a3a9d5c7f7fe5c01bb032`,
  created 2026-08-28T02:54:27Z, 1 replica.
- Worker `pegasus-prod-worker-252ow37gij` carries exactly the expected seven
  functions: `DueWorkSweepFunction`, `InboxRecoveryFunction`,
  `PendingWorkRecoveryFunction`, `SentEvidencePollFunction`,
  `StagedArtifactReconciliationFunction`, `UnifiedWorkFunction`,
  `UnifiedWorkPoisonFunction`.
- The only `Features__*` app setting on Web is `Features__AutomationMcp=true`.
  `Features__ProviderApi` is absent, which is what closes it — `Program.cs:289`
  reads it with `GetValue<bool>` and a missing key is `false`.

## "Nothing gated off" — what that can and cannot mean

The operator asked for a deployment with nothing gated off. Five feature gates
exist. **Three of them throw at startup outside the `DevelopmentOffline`
runtime profile**, and the bicep sets `Runtime__Profile: 'Production'`, so
setting any of them would crash-loop the host rather than enable a feature:

| Gate | Release 37 | Why |
| --- | --- | --- |
| `Features:AutomationMcp` | **on** (already) | ADR-0026 |
| `Features:ProviderApi` | **on** (PR #632) | no ADR restricts it |
| `Features:SendToAi` | cannot | `src/Pegasus.Web/AiWork/SendToAi.cs:42` throws; ADR-0031 still needs a non-preview transport decision |
| `Features:LocalIntake` | cannot | `src/Pegasus.Web/Program.cs:124` throws |
| `Features:LocalDocumentCustody` | cannot | `src/Pegasus.Web/Program.cs:746` throws |

The bottom two are not features at all — they are the local development
substitutes for the real Graph and Box adapters. Enabling them in production
would *replace* real intake with a fake. So ungated ships as **both real gates
true**, which is what `infra/modules/platform.bicep:467-468` now carries.

**One activation is deliberately not in this release.** Issuing the first
Provider API credential is a separate exact-target approval
(`docs/capabilities.md:227`) and creates a live secret for an external party.
Enabling the route admits nobody on its own: every mapped request requires the
provider scheme and answers 401 without a credential. The credential is the
operator's call, separately, after this deploys.

## Route

Full release, per `.agents/skills/pegasus-release/SKILL.md`. Note the ordering
the skill mandates and which is easy to get backwards: **the promotion happens
before the artifact build**, and the artifacts are built in a disposable
detached worktree at the promoted SHA — not from the working checkout.

1. Full local gate on final `dev`.
2. Fresh `MERGE AUTH GRANTED`, then the atomic exact-SHA non-force promotion of
   `dev` to `main`; read both refs back and assert equality.
3. `Build-ReleaseArtifacts.ps1` in a detached worktree; `Test-AzureDeploymentPlan.ps1`
   in `Local`, `Artifact`, `PreUpload`; record the manifest SHA-256.
4. `oras cp` the image; the fetched digest must equal `manifest.webImage.digest`.
5. **Migrate before packaging** — `PreMigration` gate, `efbundle.exe` from
   `src/Pegasus.Web` with the full Production process environment, then
   `Invoke-AzureDatabaseBootstrap.ps1`, then verify the live head.
6. `azd provision -e pegasus-prod`; read the active revision and digest back.
7. Worker by `az functionapp deployment source config-zip`. **Never
   `azd deploy worker`** — it runs a remote Oryx build against the already
   published package and crash-loops the host.
8. `Invoke-ProductionSmoke.ps1`, then the Azure diagnostics review.
9. Docs by reviewed PR to `dev`, then a second freshly authorised
   promotion-only pass.

## Risks carried into this release

- **The 2026-08-14 Worker grant hotfix.** The Worker's SQL role lacked
  case-creation grants and was fixed live, never captured in a migration. If
  the bootstrap rewrites roles from a census that still omits them, this
  release regresses it and cases silently stop being created in production —
  the exact symptom that was originally reported. Verify before provisioning,
  not after.
- Nine pending migrations, four grant-carrying. Migration precedes packaging so
  a failure stops before any application code changes.
- A large release: the whole EPIC-011 UI replacement lands at once.
- `docs/operations.md:296` still says the estate serves release 35 while the
  table at `:314` records 36 — a release-36 prose miss to fix in the docs pass,
  along with `:121`, which claims the Provider API has "no endpoint, client,
  credential, or caller" and becomes actively false when this deploys.

## Verification

- [ ] Locked restore, Release build, both test filters green on the promoted SHA
- [ ] `origin/main` and `origin/dev` both read back equal to the approved SHA
- [ ] Uploaded image digest equals `manifest.webImage.digest`
- [ ] Live migration head equals the manifest `migrationIdentity`
- [ ] Worker grant census read back and recorded, including the 2026-08-14 grants
- [ ] Active Web revision serves the new digest
- [ ] Exactly seven Worker functions after deployment
- [ ] `Invoke-ProductionSmoke.ps1` passes, including intake liveness
- [ ] `Features__ProviderApi` observed `true` on the live Web app
- [ ] `docs/current-architecture.md` and `docs/operations.md` match what shipped
