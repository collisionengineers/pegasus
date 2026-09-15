---
name: pegasus-wipe-intake-data
description: Sterilize the Pegasus production estate between test rounds — clear intake-generated Blob and SQL data, with an explicit full test-estate option that retains only alex and restarts the current QDOS counter. Use whenever an operator asks to wipe, clear, reset or sterilize test/intake data.
---

# Wiping Pegasus intake-generated data

Intake (Outlook mail, image intake, manual upload) writes to exactly two
places: Azure Blob Storage (the artifact bytes) and Azure SQL (everything the
Web UI actually renders — cases, receipts, retained mail, documents). Both
must be cleared together, or cases and emails keep showing in the UI even
after the blobs are gone. Prior runs are recorded in `docs/operations.md`; use
the script below rather than re-deriving the preserve list or account names by
hand.

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
- **Every non-preserved SQL table reported by the fresh dry run** — everything
  intake and case-handling writes: `Cases`, `CaseDocuments`, `CaseHistory`,
  `IntakeReceipts`,
  `IntakeStagedReceipts`, `IntakeAssets`, `RetainedMailboxMessages`,
  `RetainedMailboxAttachments`, `DocumentVersions`/`DocumentOccurrences`,
  `Triage`/`UnidentifiedItems`, `ActionHistory`, and the rest of the
  intake/case pipeline.

With `-ResetTestEstate`, the same transaction also removes every account except
the single `alex` Administrator, including its Identity children, OpenIddict
authorizations/tokens and `SecurityEvents` where the removed account is the
subject or actor. It resets only the current London-year QDOS case sequence to
zero, so the next allocation is `QDOSyy001`.

## What never gets touched, and why

- **`authentication-ring`** — ASP.NET Core Data Protection key ring
  (`keys.xml`, see `src/Pegasus.Web/Program.cs:191-195`). Deleting it would
  invalidate every live auth cookie. Not test data.
- **`box-links`** — provisioned but currently unreferenced by any
  application code; not part of the intake artifact path.
- **`pegtrans252ow37gij`** (separate storage account) — Azure Functions
  runtime storage (`app-package`, `azure-webjobs-*`, work queues). Intake wake
  and work identifiers do live here; the wipe leaves them intact. On resume,
  queued mail notifications must use the new persisted receive-time cutoff.
- **For an ordinary wipe, the SQL preserve list** — identity/auth
  (`AspNet*`, `OpenIddict*`), mailbox configuration and Graph subscriptions,
  `Organizations*`/`Principals*`, `ProviderDomain*`/
  `ProviderReferences`, `WorkflowConfigurations`, `SendToAiControl`,
  `SecurityEvents`, `ValuationPresets` (administrator-managed configuration),
  and the four sequence tables, including `TriageSequences` (so no
  case/image/Triage/unidentified reference is ever reused).
- **Outlook and Box themselves** — the script only touches Azure Blob and
  Azure SQL; no Graph or Box API call exists in it.

Inbox poll rows survive, but their cursor/lease is cleared and their existing
`StartBoundaryUtc` advances to the wipe cutoff. Missing poll rows are seeded
at that cutoff. Original mailbox activation times remain unchanged. Preserving
an old cursor alone is insufficient: Graph can expire it and enumerate old
messages whose occurrence identities the wipe removed.

## Procedure

1. **Dry run (read-only, no approval needed):**
   ```powershell
   pwsh ./scripts/Invoke-IntakeDataWipe.ps1
   ```
   Prints the current blob count/size and the SQL table/row breakdown to be
   wiped. This *is* the fresh inventory the live-operation approval matrix
   requires — always re-run it immediately before executing, never reuse a
   stale count.

   For the full test-estate reset, use:
   ```powershell
   pwsh ./scripts/Invoke-IntakeDataWipe.ps1 -ResetTestEstate
   ```
   It additionally prints the retained `alex` account, every account and trace
   count to remove, and the QDOS sequence that would restart. It refuses unless
   exactly one `alex` account exists with the Administrator role and QDOS has
   exactly one sequence lineage.

2. **Get explicit approval** naming the exact targets — *clear all blobs in
   `pegcustody252ow37gij/transient-intake` and delete rows from the N
   reported non-preserved tables in database `pegasus` on
   `pegasus-prod-sql-252ow37gij`.* Both are production writes; per
   `docs/runbook.md`'s live-operation approval matrix ("Change or use an
   Azure service") this needs approval before running with `-Execute` —
   plan approval alone is not enough.

   Approval for `-ResetTestEstate` must also name every reported non-`alex`
   account, its attributable trace counts, and the reported current-year QDOS
   sequence reset. Approval for an ordinary wipe does not authorize those
   additional writes.

3. **Execute (only after approval):** stop the exact Worker app for the
   approved maintenance window and exclude application writes. The script
   refuses `-Execute` unless the Worker is already stopped; it does not
   change service state itself.
   ```powershell
   pwsh ./scripts/Invoke-IntakeDataWipe.ps1 -Execute
   ```
   Deletes the blobs first, then the SQL rows in one transaction (`NOCHECK
   CONSTRAINT` → `DELETE` → `WITH CHECK CHECK CONSTRAINT`, so a preserved
   table referencing a deleted row would fail the check). The same SQL
   transaction records the cutoff captured before blob deletion. On success,
   old notifications and delta resets cannot re-ingest pre-cutoff mail;
   newly received or forwarded mail remains eligible.

   Use `-ResetTestEstate -Execute` only when the expanded targets were included
   in the immediately preceding approval.

4. **Verify:** the script's own post-run output reports blobs remaining
   (expect 0) and "Wiped tables still holding rows" (expect 0), plus an
   exact before/after comparison of every value in the four reference-sequence
   tables (`CaseSequences`/`ImageIntakeSequences`/`TriageSequences`/
   `UnidentifiedSequences`) and the `ValuationPresets` row count (expect 0
   changes). The expanded reset instead expects only its inventoried QDOS row
   to become zero and verifies that `alex` is the sole remaining account with
   no attributable OpenIddict or security-event rows for deleted account IDs.
   Reload the
   Pegasus Web UI and confirm no cases/emails remain — that's the actual
   end-to-end signal an operator cares about.

5. **Record it** in `docs/operations.md`, following the phrasing convention
   of prior wipe entries: date, exact blob and row/table counts, preserve
   count, confirmation that `authentication-ring`, `box-links`,
   `pegtrans252ow37gij`, Outlook and Box were untouched, and the unchanged
   sequence values and committed mail cutoff. Resume the previously approved
   Worker only after successful verification. Nest it in the current release's bullet if one was just
   deployed; otherwise add it as a standalone dated bullet.

## Never

- Run `-Execute` without a fresh dry run immediately before it — state may
  have changed since the last check.
- Target any container/account other than `pegcustody252ow37gij/transient-intake`.
- Skip the preserve-list self-check — if it throws ("Preserve list has
  missing tables"), stop and investigate schema drift rather than editing
  the list to make it pass.
- Use `-ResetTestEstate` to select arbitrary retained users or principals. It
  has one bounded meaning: retain `alex` and reset current-year QDOS only.
- Call Outlook, Graph, or Box from this process — the wipe is Azure-only by
  design; mailbox/Box cleanup is a separate, differently-approved operation.
