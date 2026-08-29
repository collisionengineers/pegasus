## Checklist — PLAT-026

Ticked against the remediated branch at `1f67f027` (round 2, 2026-08-29). Each
tick names what was actually checked.

- [x] Mail settings area rendered on the `admin-layout` shell via `_AdminNav`
      (matches `docs/design/README.md` and EPIC-011 context.md §1.12).
      `Mailboxes.cshtml:14-16`.
- [x] Approved mailboxes table: Mailbox, Scope, Last update, State, Activated,
      Subscription, Review folders/Refresh — all present, all real handlers.
      `Mailboxes.cshtml:46-52`.
- [x] Mail categories table: Category, State, Review + Add category — all
      present, all real handlers. `Mailboxes.cshtml:216-218, 278-310`.
- [x] Activated and Subscription columns' data sources unchanged
      (MAIL-017/018/020/021 behaviour preserved) — `ActivatedAtUtc` and
      `IApprovedMailboxSubscriptionStore` still the only sources.
- [x] All literal copy centralized in `OperatorLabels.MailSettings`; no label
      duplicated elsewhere. **Round 2:** `MailSettings.Area` was a second
      constant holding "Mail settings" already owned by `Admin.Mail`, and
      `MailboxState`/`CategoryState` were a second copy of a two-value
      vocabulary `Humanise` already produced. Both closed in `1f67f027`;
      grepped — no `MailSettings.Area` reference remains.
- [x] `OperatorLabels.cs` edited append-only; no existing member reordered.
      `git diff --numstat origin/dev...HEAD` → `101  0`.
- [x] No explanatory copy introduced (labels/values/controls only). The panel
      description and meta line are the `h2 / description / meta` triple
      §1.12 specifies for the admin content panel; the restored
      `Review folders (N of 13)` is a label plus a count value.
- [x] Every control maps to a real handler; no inert control. Three handlers
      (`Update`, `ResolveFolders`, `SaveCategory`); grep for
      `disabled`/`aria-disabled`/`gated` on the page returns nothing.
- [x] `MailCategories` redirect route left intact, with
      `[Authorize(Policy = StaffRoleNames.Administrator)]` retained, and
      reported as the surface to delete — **to PLAT-029**, which `waves.md:9`
      assigns the `Administration/Index` deletion, not to UIIMP-009 as the
      round-1 wording said.
- [x] Web tests updated to assert new markup/handlers; no assertion weakened,
      skipped, deleted or inverted. **Round 2:** round 1 did weaken
      `AdministratorRefreshesOnlyServerResolvedLogicalFolderBindings`; the
      label-to-state pairing is restored in `7dc980bc` and its discriminating
      power proved by a mutation run that fails.
- [x] Non-administrator access denial still asserted — four `Forbidden`
      assertions across the two test files (`NonAdministratorCannotOpenMailSettings`,
      `NonAdministratorCannotPostMailSettingsHandlers`,
      `NonAdministratorCannotOpenMailSettings`,
      `NonAdministratorCannotPostCategoryChange`).
- [x] `catalogue.json` — **out of this lane's allocation.** `waves.md:9` gives
      its structural edits to PLAT-029. The round-1 edit is reverted
      (`aebe48ac`); the file is byte-identical to `origin/dev`. The stale
      `"visual"` classification of the `MailCategories` entry, and the
      superseded snapshot that must be deleted with it, are reported to
      PLAT-029 rather than fixed here.
- [x] `dotnet build ./Pegasus.slnx --configuration Release` run by this
      session: `Build succeeded. 0 Warning(s) 0 Error(s)`.
- [x] Focused test filter for both test classes run by this session:
      `Failed: 0, Passed: 17, Skipped: 0, Total: 17`. Plus the two other test
      classes that read `OperatorLabels`: `Passed: 8, Total: 8`.
- [x] Only PLAT-026's file set touched; anything else reverted.
      `git diff --name-only origin/dev...HEAD` returns exactly the seven files
      in the `files` doc.
- [x] Simplification pass run over the branch diff; findings dispositioned
      under a dated heading (round 1), and the external review findings
      dispositioned under "Review findings — dispositions (round 2)".
