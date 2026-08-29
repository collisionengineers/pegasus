### 2026-08-29 — remediation round 2 (Claude; lane implemented by Codex)

Merged `origin/dev` first — already up to date at `b92cb9a7`, no conflicts.

Closed both `high` findings, disposed of all four `medium` and all three `low`.

- Restored the discriminating pairing in
  `AdministratorRefreshesOnlyServerResolvedLogicalFolderBindings` and proved it
  by mutation (swapped the reloaded block to the pre-refresh pairing → FAIL,
  `Not found: "<dt>Instructions</dt><dd>Configured</dd>"`; reverted → PASS).
  With the strong assertion back, `ResolveFolders` still behaves correctly.
- Reverted `docs/design/test-ui/catalogue.json` to `origin/dev` — it is
  PLAT-029's file per `waves.md:9`, and the edit had orphaned
  `pages/administration-mail-categories--default.html`. Branch is seven files
  now, all inside the allocation.
- Restored the folder-binding count (`Review folders (N of 13)`), deleted the
  duplicate `MailSettings.Area`, and made `MailboxState`/`CategoryState`
  delegate to `Humanise`.
- Corrected the false baseline in `research`, plan steps 5 and 7, the `files`
  ownership list, and the checklist (now honestly ticked); appended retractions
  to `post-implementation-report`.

Handed to PLAT-029, not fixed here: the now-stale `"visual"` classification of
the `MailCategories` catalogue entry plus its superseded snapshot, and the
duplicate "Outlook categories" card on `Administration/Index.cshtml`.

Left in `review`. No `proof` written, nothing merged.
