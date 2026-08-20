# Proof — PLAT-017 (command-log)

Executed 2026-08-20 ~20:00 UTC, operator-approved targets exactly as planned.

## SQL — pegasus-prod-sql-252ow37gij / pegasus
- 66 data tables classified for wipe, 30 preserved; the run asserts every `sys.tables` entry is classified (codex's new tables included) before touching anything.
- `NOCHECK CONSTRAINT ALL` → `DELETE` per table → `WITH CHECK CHECK CONSTRAINT ALL` (revalidated on empty tables — no orphan risk).
- **1,211 rows deleted** across 35 non-empty data tables (largest: IntakeAssets 606, IntakeReceiptEvents 57, RetainedMailboxAttachments 49, CaseDataFields 43, ActionHistory 41, IntakeReceipts/StagedReceipts/WorkItems/Evaluations 40 each; Cases 4).
- Post-wipe non-empty tables are exactly the preserve list: `__EFMigrationsHistory` 58, AspNet* (2 users/3 roles), ApprovedMailboxes 1 + poll states, OpenIddict* (1 automation client + tokens), Organizations/Principals/lineages 1 each, ProviderDomain*/ProviderReferences, SecurityEvents 102, WorkflowConfigurations 1, and the three sequence tables (references never reused — invariant held).

## Storage
- `pegcustody252ow37gij/transient-intake`: 159 blobs → `az storage blob delete-batch` → **0 blobs**.
- `authentication-ring` (data-protection keys) and `box-links` untouched (configuration).
- `pegtrans252ow37gij` queues (intake-work, external-work, both poisons): verified empty before and untouched.

## Untouched by design
Outlook mailbox, Box content (DOCS-005's release step deletes the legacy binding JSONs), all identity/config/reference tables.

Live-app spot check happens with the release-15 post-deploy verification (empty Queues/Inbox, staff login working).
