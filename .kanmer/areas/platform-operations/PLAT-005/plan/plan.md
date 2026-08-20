# Plan — PLAT-005: capture local visual evidence

## Approach

Use the repository-owned Offline lifecycle to start the authenticated local Pegasus host, then capture a small, reproducible visual set at the browser suite’s standard desktop viewport plus one constrained rail view. Retain only the screenshots and a manifest in the ticket’s assets, inspect them against the design authority, and stop the owned local stack. This is preferable to production screenshots, copied markup, or a new visual-regression harness: it exercises the real local caller while remaining proportional to an evidence-only task.

## Governing docs

- **Meets `docs/frd/frd-12-operator-experience.md`:** verify the rendered operator surfaces, their honest state presentation, primary navigation, and non-colour/accessible rail context through human-readable evidence. This ticket does not modify FRD-12 because it changes no behavior.
- **Meets `docs/runbook.md`:** use its supported Offline commands and local-only boundary. No cloud or vendor operation is authorized.
- **Meets `docs/design/README.md`:** inspect the current design authority’s rail, mark, layout, responsive, and decorative-image rules; record visual defects as follow-up tickets rather than silently changing the UI.

## Steps

1. **Prepare a reproducible Offline run.** Run the documented doctor/initialization lifecycle, record the repository revision and local run identity, start the owned stack, and verify it with `Status` and `Smoke`. Do not use an existing production/browser session.
2. **Define the capture matrix.** Record the localhost base URL, Chromium version, capture timestamp, 1280×720 standard viewport, and 1024×768 plus 512×768 constrained rail view. The required normal-view set is: Dashboard, Inbox, Queues, Cases, Case Details, Assessment, Administration, Upload, and the authenticated rail. Use only real available local records; an unavailable/empty state is evidence, not a reason to manufacture case data.
3. **Capture real rendered screens.** Navigate the authenticated `DevelopmentOffline` browser to each route, wait for stable rendering, and store lossless screenshots under the ticket assets with stable descriptive names. Capture the rail once at desktop and once at constrained/200%-equivalent width. Record any unavailable route/record and its actual rendered state in the manifest.
4. **Inspect and document evidence.** Check every image for the rail/header, visible navigation text, one real H1, marks beside their visible labels, no broken-image indicator, no horizontal clipping/overflow, and state labels that are not colour-only. Check the captured files and manifest for credentials, personal data, document text, and unnecessary sensitive material before retention.
5. **Compare against the automated baseline and close the run.** Run the existing Browser-tagged integration lane (or record its exact prerequisite failure), correlate its result with the screenshots, stop the owned local stack, and write the post-implementation report/proof with routes, viewport, commands, manifest, findings, and any follow-up ticket.

## Verification

- `pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline`
- `pwsh ./scripts/Initialize-LocalDevelopment.ps1`
- `pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start`, `Status`, `Smoke`, then `Stop`
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --no-restore --filter 'Category=Browser'`
- Inspect the retained screenshots and manifest at the evidence tier only: real local UI proof that supplements, not substitutes for, automated browser/accessibility evidence.

## Risks / open questions

- **Local fixture lacks a specific Case Details or Assessment record.** Capture its honest available/empty state, record the limitation, and do not fabricate one; create a follow-up only if the required journey cannot be evidenced.
- **Screenshots contain sensitive/irrelevant local data.** Inspect before retention, redact only through an explicitly documented, non-misleading method, or omit the artifact and record why.
- **A visual defect appears.** File a linked fix ticket with the screenshot/route as evidence; this ticket stays evidence-only.
- No operator question remains.
