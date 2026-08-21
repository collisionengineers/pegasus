# Release 16 deployment proof (2026-08-21)

Type: command-log + visual. This is the shared evidence bundle cited by every ticket verified under release 16.

## Promotion

- PR #503 (CI vehicle) green: fail=0 across all lanes.
- Operator granted `MERGE AUTH GRANTED`; exact-SHA non-force fast-forward `f0b01f39 → 4111ad29` pushed to `refs/heads/main`.
- Second `MERGE AUTH GRANTED`; docs refresh `adf0237e` (PR #504, green) fast-forwarded to main. End state main = dev = `adf0237e`, deployed SHA `4111ad29` (release-15 pattern).

## Build and validation

- `Build-ReleaseArtifacts.ps1 -Version 0.1.0-alpha.1 -SourceRevision 4111ad29…` from a clean tree: web.zip (106,428,117 B), web-image.tar.gz (1,423,188,480 B), worker.zip (107,003,452 B), efbundle.exe; manifest SHA-256 `D89EDF32…`; sourceStatus clean.
- `Test-AzureDeploymentPlan.ps1` passed in `Artifact` and `PreProvision` (env pegasus-prod; six `*_SECRET_URI` → `pegasusprodkv252ow37g`; `PEGASUS_WORKER_ACTIVATION=approved-live-worker` retained; Worker Disabled settings render `false`).

## Deploy

- Image: `oras cp --from-oci-layout` → `pegasusprodacr252ow37gij.azurecr.io/pegasus/web:4111ad29…`, digest preserved `sha256:3b891b45f6bef5f559b86f144a546c00f886c80f38f8f054352738affa002aab`.
- `azd provision` succeeded (1m47s); web Container App `pegasus-prod-web-252ow37gij` image = that digest, revision `--4111ad291779` at 100% traffic.
- Migrations: efbundle applied `20260820114412_ApprovedOutlookCategoryCatalogue`, `20260821095500_GrantWorkerVehicleLookupRequests`, `20260821100623_GrantImageIntakeLifecycleUpdates`; live head readback = `20260821100623_GrantImageIntakeLifecycleUpdates`.
- Worker: `az functionapp deployment source config-zip` → "Deployment was successful."
- Smoke: `Invoke-ProductionSmoke.ps1` **passed** (health, exact version/SHA `4111ad29`, anonymous-denial, https redirect, Worker `approved-live-worker`).

## Live verification (read-only SQL + production UI via operator's Chrome)

- Grant readback (`sys.database_permissions`): worker INSERT+SELECT / DELETE-denied on `VehicleLookupRequests`; both roles SELECT+INSERT+UPDATE / DELETE-denied on `ImageIntakes`; web SELECT+INSERT+UPDATE / DELETE-denied on `ApprovedOutlookCategories` — exactly the census.
- Automatic vehicle lookup end-to-end: within one reconcile tick of the deploy, `VehicleLookupRequests` = 3 (V2MTM, MD22DDU, DE23XKP, RequestedAt 14:34:34Z) and `VehicleLookupObservations` = 3 with real DVSA/DVLA data (MERCEDES-BENZ / FORD / AUDI) — the release-15 silent-zero regression is gone.
- Worker inbox poll fresh: `instructions@collisionengineers.co.uk` LastCompletedAtUtc 46 s before readback.
- Production Inbox: bold effective original senders (nduncombe@/jfleming@qdosassist.co.uk), no desk subline, clean excerpts (sender's words, no wrapper junk), family · subtype labels, queue/classification selector, search, Inbox/Sent/Deleted Items scopes, honest "Case not created · Unclassified · Unidentified" on EREF24-shape mail.
- Production message page (EREF6): record container, Message/Attachments/Thread/Case tabs, Classified pill, structured From/Sent/To/Subject forwarded block, body trimmed to the sender's own words ("Julie Fleming / Existing Claims Handler"), Decision card (New instruction · Inspection → Receiving work → QDOS26006), and the **Correct classification dialog opens under the deployed CSP** (dead on release 15).
- `/Administration/MailCategories` live: standard Administration pattern, empty-catalogue state + add form.
- Assessment page (QDOS26006): Make MERCEDES-BENZ / Year 2018 / Engine 1950 / Fuel DIESEL prefilled from the new lookup observation through the shared Case projection.
- `/mcp` anonymous GET → 302 to sign-in (fail-closed ingress unchanged).
- Alert rule `pegasus-prod-application-exceptions` live with 15-minute window, enabled.

## Limits (honest tiers)

The four live test emails were received 08:02–09:18, before this deploy, so extraction Version 4 (claimant/vehicle/date/circumstances/report facts) and embedded-photograph custody promotion have no live caller yet — they are proven by the real-corpus test suite at the deployed SHA and await the operator's fresh post-wipe test mail for live-tier proof. The operator wipes test data after this deployment (their action).
