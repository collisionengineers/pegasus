# Post-implementation report — MAIL-004

## Summary

Implemented the smallest global approved Outlook category catalogue: Administrators maintain exact display names as Active/Disabled, and MAIL-13's Core seam resolves an Active server-owned name from an internal id. Changes are versioned, reasoned, replay-safe and permanently audited. No Graph metadata/synchronization, message mutation, search/linking integration, generic rules editor, deployment or external write was added.

## Changes

| Area/files | Change | Why |
|---|---|---|
| `src/Pegasus.Core/Intake/ApprovedOutlookCategories.cs`, `StaffAuthorization.cs` | focused management/resolver contracts and named admin right | one Core owner; MAIL-13 cannot post arbitrary names |
| `EfApprovedOutlookCategoryStore.cs`, administration entities/configuration, DI | versioned global store, Active resolver and ActionHistory | reuse established administration transaction/replay convention |
| migration/snapshot and `Invoke-AzureDatabaseBootstrap.ps1` | one normalized unique table; Web SELECT/INSERT/UPDATE, DELETE denied; no Worker grant | exact least privilege and disable-not-delete |
| `MailCategories.cshtml(.cs)`, Administration index | dedicated accessible Administrator page | names/state only; no Graph id/color or generic rules UI |
| Core/integration/admin/browser/schema tests | authorization, normalization, active reload, duplicate/version/replay/history, no delete, route/accessibility evidence | prove the concrete MAIL-13 seam and real caller |
| FRD-08, design, capabilities | canonical catalogue behavior/UI/evidence | keep implementation and governing owners aligned |

## Governing docs

Meets FRD-08's configured-category requirement and FRD-12's Administrator-only UX boundary. FRD-08 now states the exact global allowlist/Active-resolution behavior; design names the narrow administration surface; capabilities records only the locally implemented prerequisite and keeps MAIL-13 mutation/Graph/deployment/live evidence undelivered. No ADR is required.

## Risks / follow-ups

TICK-054 remains the message-action owner. It must preserve unrelated Outlook categories and separately obtain any Graph permission/live-write approval. Search, linking, color/id storage and master-category synchronization remain excluded.

## Verification hand-off

- `dotnet restore ./Pegasus.slnx --locked-mode` — pass
- Release solution build — pass, 0 warnings/errors
- all Core — 831 pass; Architecture — 98 pass
- focused catalogue persistence/Web/schema and canonical administration — pass (including 9-test admin lane)
- authenticated accessibility lane — 22 pass
- Azure deployment-plan and migration grants (59 migrations) — pass
- documentation links (192 files), Markdown placement and diff check — pass

After merge, rerun the same focused tests against an exclusive LocalDB. No live Outlook/Azure check is required or authorized for this ticket.
