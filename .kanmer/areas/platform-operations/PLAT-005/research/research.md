# Research — PLAT-005: local visual screenshot evidence

## Question

How can PLAT-005 add human-readable visual evidence for the shipped Pegasus operator interface without claiming that screenshots replace browser/accessibility coverage or accessing live systems?

## Findings

- [[PLAT-001]] verified the implemented interface with the Browser-tagged integration lane, but its proof explicitly states that local visual screenshots were not captured.
- `docs/runbook.md` defines the supported Offline lifecycle: `Invoke-Doctor.ps1 -Profile Offline`, `Initialize-LocalDevelopment.ps1`, then `Invoke-LocalDevelopment.ps1 -Action Start|Status|Smoke|Stop`. It uses the authenticated `DevelopmentOffline` staff profile and does not call Azure, Graph, Box, or any vendor.
- `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs` establishes the current route baseline: fixed headless Chromium, authenticated staff routes, axe checks, no inline styles, one H1, the rail/top-bar position guard, 1024×768 constrained desktop, and a 512×768 200%-equivalent view.
- The existing visual obligation is the rail plus Dashboard, Inbox, Queues, Cases, Case Details, Assessment, Administration, and Upload. Some detail screens require a known local seeded record; the plan must capture an honest empty/unavailable state if the local fixture does not expose one, rather than fabricate data.
- `docs/runbook.md` requires evidence artifacts to be reviewed for credentials, document text, and unnecessary personal data before retention. The Offline profile supplies local fixture data only.

## Implications

- Use only the supported Offline scripts and package-pinned Chromium/browser workflow; do not start ad-hoc services or use any production session.
- Capture each route at a documented viewport, including one constrained/zoom-equivalent rail image, then inspect the visual result for rails, marks, overflow, broken images, and honest UI states.
- Store a manifest with route, viewport, local runtime/profile, capture time, and checks performed. Screenshots supplement—never replace—the existing Browser suite.
- No production read/write, application code change, design change, or new visual-regression framework is justified.
