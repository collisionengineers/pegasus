---
name: pegasus-wipe-intake-data
description: Sterilize the Pegasus production estate between test rounds — clear the Azure Blob artifacts and SQL rows that any form of intake (email, image, manual upload) generates or stores, leaving identity, mailbox configuration and sequence state intact. Use whenever an operator asks to wipe, clear, reset or sterilize test/intake data.
---

# Wiping Pegasus intake-generated data

Intake (Outlook mail, image intake, manual upload) writes to exactly two
places: Azure Blob Storage (the artifact bytes) and Azure SQL (everything the
Web UI actually renders — cases, receipts, retained mail, documents). Both
must be cleared together, or cases and emails keep showing in the UI even
after the blobs are gone. This has been run five times
(`docs/operations.md`); use the script below rather than re-deriving the
preserve list or account names by hand.

## The estate

| Thing | Exact name |
| --- | --- |
| Resource group | `rg-pegasus-prod` |
| Subscription | `e6076573-23a5-46a8-acef-7e22d264e5db` |
| Custody storage account | `pegcustody252ow37gij` |
| Intake artifact container (wiped) | `transient-intake` |
| SQL | `pegasus-prod-sql-252ow37gij` / database `pegasus` |

Same estate as [`pegasus-release`](../pegasus-release/SKILL.md) — this skill
touches a subset of it.

## What gets wiped

- **All blobs in `pegcustody252ow37gij/transient-intake`** — both the
  content-addressed `sha256/*` store and in-flight `staging/*` blobs. There
  is no case/date prefix in the path, so the whole container is always the
  target.
- **~70 non-preserved SQL tables** — everything intake and case-handling
  writes: `Cases`, `CaseDocuments`, `CaseHistory`, `IntakeReceipts`,
  `IntakeStagedReceipts`, `IntakeAssets`, `RetainedMailboxMessages`,
  `RetainedMailboxAttachments`, `DocumentVersions`/`DocumentOccurrences`,
  `Triage`/`UnidentifiedItems`, `ActionHistory`, and the rest of the
  intake/case pipeline.

## What never gets touched, and why

- **`authentication-ring`** — ASP.NET Core Data Protection key ring
  (`keys.xml`, see `src/Pegasus.Web/Program.cs:191-195`). Deleting it would
  invalidate every live auth cookie. Not test data.
- **`box-links`** — provisioned but currently unreferenced by any
  application code; not part of the intake artifact path.
- **`pegtrans252ow37gij`** (separate storage account) — Azure Functions
  runtime storage (`app-package`, `azure-webjobs-*`, work queues). Nothing
  intake-related lives here.
- **The SQL preserve list** (31 tables + `ApprovedMailbox*`) — identity/auth
  (`AspNet*`, `OpenIddict*`), mailbox poll cursors and Graph subscriptions
  (clearing these would make the Worker re-ingest every message still in the
  mailbox), `Organizations*`/`Principals*`, `ProviderDomain*`/
  `ProviderReferences`, `WorkflowConfigurations`, `SendToAiControl`,
  `SecurityEvents`, and the three sequence tables (so no case/image/
  unidentified reference is ever reused).
- **Outlook and Box themselves** — the script only touches Azure Blob and
  Azure SQL; no Graph or Box API call exists in it.

## Procedure

1. **Dry run (read-only, no approval needed):**
   ```powershell
   pwsh ./scripts/Invoke-IntakeDataWipe.ps1
   ```
   Prints the current blob count/size and the SQL table/row breakdown to be
   wiped. This *is* the fresh inventory the live-operation approval matrix
   requires — always re-run it immediately before executing, never reuse a
   stale count.

2. **Get explicit approval** naming the exact targets — *clear all blobs in
   `pegcustody252ow37gij/transient-intake` and delete rows from the N
   reported non-preserved tables in database `pegasus` on
   `pegasus-prod-sql-252ow37gij`.* Both are production writes; per
   `docs/runbook.md`'s live-operation approval matrix ("Change or use an
   Azure service") this needs approval before running with `-Execute` —
   plan approval alone is not enough.

3. **Execute (only after approval):**
   ```powershell
   pwsh ./scripts/Invoke-IntakeDataWipe.ps1 -Execute
   ```
   Deletes the blobs first, then the SQL rows in one transaction (`NOCHECK
   CONSTRAINT` → `DELETE` → `WITH CHECK CHECK CONSTRAINT`, so a preserved
   table referencing a deleted row would fail the check).

4. **Verify:** the script's own post-run output reports blobs remaining
   (expect 0) and "Wiped tables still holding rows" (expect 0), plus
   preserved-row totals and sequence values (`CaseSequences`/
   `ImageIntakeSequences`/`UnidentifiedSequences`) unchanged. Reload the
   Pegasus Web UI and confirm no cases/emails remain — that's the actual
   end-to-end signal an operator cares about.

5. **Record it** in `docs/operations.md`, following the phrasing convention
   of prior wipe entries: date, exact blob and row/table counts, preserve
   count, confirmation that `authentication-ring`, `box-links`,
   `pegtrans252ow37gij`, Outlook and Box were untouched, and the unchanged
   sequence values. Nest it in the current release's bullet if one was just
   deployed; otherwise add it as a standalone dated bullet.

## Never

- Run `-Execute` without a fresh dry run immediately before it — state may
  have changed since the last check.
- Target any container/account other than `pegcustody252ow37gij/transient-intake`.
- Skip the preserve-list self-check — if it throws ("Preserve list has
  missing tables"), stop and investigate schema drift rather than editing
  the list to make it pass.
- Call Outlook, Graph, or Box from this process — the wipe is Azure-only by
  design; mailbox/Box cleanup is a separate, differently-approved operation.
