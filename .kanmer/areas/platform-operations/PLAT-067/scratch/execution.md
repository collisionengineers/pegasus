## Transitions

- 2026-09-02T12:06:54.362Z lease-phase implementing → running-command (lease e74166af-f738-4d01-844d-9f13461f8311 rev 2; expires 2026-09-02T12:36:54.349Z)

2026-09-02T12:07Z — Fresh production wipe dry run PASS (exit 0). Inventory: 36 blobs / 3,932,690 bytes in pegcustody252ow37gij/transient-intake; 70 non-preserved SQL tables / 147 rows in pegasus on pegasus-prod-sql-252ow37gij; preserve list 31/31, effective preserved 32; sequences Case 31, Image 7, Unidentified 1. Paused before -Execute for exact operator approval.

2026-09-02 — Approved production wipe PASS: 36 blobs removed; SQL transaction committed 147 deletions across 70 targeted tables; zero blobs and zero targeted-table rows remain; 354 preserved rows remain; sequence values unchanged (31/7/1); excluded systems untouched; operator confirmed authenticated UI empty.

Release preflight then stopped on a retained failure: Invoke-ProductionSmoke.ps1 against current release 37 exited 1. Worker activation PASS (approved-live-worker), but newest inbound poll completed 1,662 minutes ago and the script reported the recovery timer is not running. Candidate refs and PR/check census passed. No merge authorization requested, no Git promotion, artifact build, or Azure deployment write performed. Resume by diagnosing the Worker/poll condition through the release troubleshooting route; do not merely rerun the failed smoke.

2026-09-02 Azure read-only diagnosis: Function App pegasus-prod-worker-252ow37gij is Running with Normal availability; all seven expected functions are present; other timer functions execute successfully. App Insights shows System.IO.InvalidDataException 'Graph Inbox message omitted receivedDateTime' recurring every five minutes. This matches release-37 behavior and the reviewed release-38 fix in 712bfcf3/c6842a8c: genuinely absent sparse-delta timestamps are skipped so the cursor advances, while present-but-unparseable values still fail. PR 641 CI passed. Live migration head remains 20260829212237_GrantProviderSubmissionAcceptRecovery. Operator explicitly confirmed release 38 is intended to resolve this condition. Disposition: proceed to exact-SHA promotion boundary; retain the failed preflight and require post-deploy smoke.

- 2026-09-02T12:25:38.998Z lease-phase running-command → implementing (lease e74166af-f738-4d01-844d-9f13461f8311 rev 4; expires 2026-09-02T12:55:38.981Z)

2026-09-02 exact-SHA promotion PASS: after fresh literal MERGE AUTH GRANTED, canonical atomic two-ref push advanced main fb3f07acc8cca8d9d8b57db8a431b607772436dc → 0f0e90ae44ffda7339ca2a460310deeb98121afa while lease-checking dev at the same SHA. Fresh fetch read back both origin/main and origin/dev equal to 0f0e90ae44ffda7339ca2a460310deeb98121afa.

- 2026-09-02T12:27:09.233Z lease-phase implementing → running-command (lease e74166af-f738-4d01-844d-9f13461f8311 rev 5; expires 2026-09-02T14:27:09.016Z)
