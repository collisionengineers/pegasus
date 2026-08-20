# Checklist — PLAT-005

- [x] Start a documented Offline run and record the repository revision, run identity, base URL, and successful Status/Smoke checks.
- [ ] Create the capture manifest with the route matrix and fixed viewport settings.
- [ ] Capture the authenticated rail at 1280×720 and constrained 1024×768 / 512×768 views.
- [ ] Capture Dashboard, Inbox, Queues, Cases, Case Details, Assessment, Administration, and Upload as real local rendered routes.
- [ ] Inspect every screenshot for rail/navigation, H1, marks beside text, broken-image indicators, overflow/clipping, and non-colour state labels; record honest unavailable/empty states.
- [ ] Review screenshots and manifest for credentials, document text, personal data, and other unnecessary sensitive material before retention.
- [x] Run the Browser-tagged integration lane, or record its exact prerequisite failure.
- [ ] Stop the local stack and write the post-implementation report/proof with artifacts, routes, commands, findings, and any linked follow-up.

## Progress notes

- [x] **Resolved 2026-08-20:** [[PLAT-014]] completed independent review, merged to `dev`, and reached Verifying; this ticket resumed against merge commit `2688e9c3f06d1db3c85b8c8bc69a41bc4696b5f8`.

- [x] **Offline lifecycle — 2026-08-20:** `pwsh Invoke-Doctor -Profile Offline` and `pwsh Initialize-DevelopmentEnvironment.ps1 -Profile Offline` passed. Owned run `efb229edfa284deca9b06359c3cd8df2` started at `https://localhost:51461`; its Status was Running with Azurite, Web, Worker, and Functions healthy. Smoke passed at 2026-08-20T12:04:01Z with source SHA `2688e9c3f06d1db3c85b8c8bc69a41bc4696b5f8`, initialized identity, validated HTTPS, and validated the administrator route. The exact run was stopped and confirmed Stopped after evidence collection.

- [x] **Browser integration lane — 2026-08-20:** `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore --filter "Category=Browser" --logger "console;verbosity=normal"` passed (exit code 0, 440.2 seconds).

- [ ] **Blocked 2026-08-20:** no controllable browser instance is available in this workspace (browser discovery returned an empty set) while the Offline Web run is healthy. The requested real rendered screenshot set, capture manifest, visual inspection, and final report/proof cannot be honestly completed without a browser surface. No images or manifest were fabricated or retained. Resume the remaining visual capture steps once a browser is available.
