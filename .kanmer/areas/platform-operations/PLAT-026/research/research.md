## PLAT-026 — Mail settings administration port

### Scope source

`.kanmer/groups/EPIC-011/context.md` §1.12 (Administration) is the binding
contract:

> Mail settings: Approved mailboxes table (Mailbox, Scope, Last update,
> State, Review folders/Refresh) + Mail categories table (+ Add category).

EPIC-011 `decisions-2026-08-29.md` D16 pulled this ticket in from EPIC-008 as
wave 2 lane I3, alongside PLAT-025 (Configuration) and PLAT-027
(Accounts/Access/Roles). All three run in parallel this wave, each owning a
disjoint file set under `Pages/Administration/**`.

### Correction (2026-08-29, superseded — see the second correction below)

An earlier draft of this section was written after this session's own file
reads landed mid-way through the implementing agent's in-progress edit, and
wrongly described the already-ported markup as the pre-existing baseline. That
first correction fixed the framing but still copied the **new** column list
into the sentence describing the **old** page. The accurate baseline is below.

### Correction 2 (2026-08-29, remediation round 2)

Checked against `git show origin/dev:<path>` at `b92cb9a7`, line by line:

- `Mailboxes.cshtml` was the **existing bespoke layout**: a `back-link` to
  `/Administration/Index` (line 10), a `<partial name="Shared/_PageHeader" />`
  (line 15), no `admin-layout`/`_AdminNav` shell. It carried **three** stacked
  `section`s, not one table:
  1. `Current policies` (line 28) — the approved-mailbox table, whose six
     columns were **`Address`, `Route scope`, `State`, `Activated`,
     `Polling`, `Subscription`** (lines 40–45). There was **no**
     Review-folders column and no per-row disclosure.
  2. `Update policies` (line 69) — one `panel form-panel` per mailbox, each
     with an `<h3>` of the address, a `<details>` whose summary read
     `Logical folders (N of 13 configured)` (line 79) over a `<ul>` of
     `Label — Configured / Not configured` items, the per-mailbox edit form,
     and the `Resolve logical folders` button.
  3. `Add an approved address` (line 140).
  Bindings were top-level (`MailboxId`, `ExpectedVersion`, `OperationKey`,
  `Address`, …) — no `MailboxForm` wrapper.
- `MailCategories.cshtml` / `.cshtml.cs` was a **separate, fully independent
  34-line page** (its own bespoke layout, its own `Save` handler, its own
  `ListApprovedOutlookCategories`/`UpdateApprovedOutlookCategory` calls, its
  own top-level bound properties `CategoryId`/`DisplayName`/`SelectedState`/
  `ExpectedVersion`/`Reason`/`OperationKey`) — **not** a redirect stub. It was
  reachable at its own route and rendered its own "Current categories" /
  "Add an approved category" sections with two working forms.

Column mapping old → new, for the record: `Address` → `Mailbox`,
`Route scope` → `Scope`, `Polling` → `Last update` (rename only; the cell
still renders `Model.PollStatusFor(mailbox)`), and `State` / `Activated` /
`Subscription` unchanged. `Review folders / Refresh` is a **new** column that
absorbs the old `Update policies` section, so no column was dropped.

So this ticket's actual job was two things at once: (1) re-skin onto the
`admin-layout` shell and collapse three stacked sections into one panel, and
(2) physically **fold the two pages into one** (Mailboxes hosts both tables;
`MailCategories` becomes a permanent redirect to `/Administration/Mailboxes`)
— not just a shell re-skin of an already-combined page.

### Load-bearing state that must not regress (MAIL-017/018/020/021)

- **Activated** column — `OperatorLabels.OfficeTime(mailbox.ActivatedAtUtc, …)`
  — real state: whether Graph has confirmed mailbox identity/activation, not
  decoration.
- **Subscription** column — `Model.SubscriptionStatusFor(mailbox)`, backed by
  `IApprovedMailboxSubscriptionStore` and `ApprovedMailboxSubscription`
  (`LifecycleState`, `ExpiresAtUtc`, `LastMaintenanceFailureCode`,
  App-Insights-capped maintenance) — real Graph subscription health, not
  decoration.
- **Folder-binding progress count** — the old summary's `N of 13 configured`
  is the at-a-glance answer to "is this mailbox wired up yet". Dropped by the
  first implementation pass and restored in round 2 as
  `Review folders (N of 13)` via `MailSettings.ReviewFoldersProgress`.
- All three must survive the re-skin/merge with their existing data sources
  unchanged; only presentation/labels/binding-property-prefix moved. Verified
  in the merged diff.

### Design authority

`docs/design/README.md` §"No explanatory copy and page economy" (line 638)
binds: labels/values/controls only, no hint text, no how-it-works prose, no
empty-state paragraphs beyond a single "No approved mailboxes" /
"No mail categories" row.

### Consolidation decision (recorded)

The contract folds Mailboxes + MailCategories into one "Mail settings" area.
Deletion of the superseded `/Administration/MailCategories` route (which this
ticket reduced to a `RedirectToPagePermanent`) and of the still-standing
separate "Outlook categories" card on `Administration/Index.cshtml` belong to
**PLAT-029** — `waves.md:9` assigns "delete … `Administration/Index`" and the
`catalogue.json` structural edits to that lane, not to UIIMP-009 as an earlier
draft of this document said. This ticket leaves the redirect stub as the
smallest honest seam and reports both surfaces to PLAT-029.

### Reuse

- `Pages/Administration/Shared/_AdminNav.cshtml` (PLAT-029) — read only, not
  modified.
- `Pages/Shared/_StatusChip.cshtml` — reused for mailbox/category state chips.
- `Pages/Operations/Index.cshtml` house style — multi-table panel-per-area
  pattern followed for the Mail settings panel head (h2 + description + meta
  line).
- `MailLogicalFolders.All` — reused for the folder-bindings disclosure.
- `OperatorLabels.Admin.Mail` — the one owner of the string "Mail settings";
  the panel `h2` reads it rather than defining a second constant.
- `OperatorLabels.Humanise` — the one owner of the mailbox/category state
  vocabulary; `MailSettings.MailboxState`/`CategoryState` delegate to it.
- `OperatorLabels` — a new nested `MailSettings` static class appended at the
  end of the file (existing members untouched, per the shared-file rule for
  `OperatorLabels.cs`); verified `101 added / 0 deleted` against `origin/dev`.
- Existing Core ports (`ListApprovedMailboxes`, `UpdateApprovedMailbox`,
  `IApprovedMailboxPollStatusQueries`, `IApprovedMailboxSubscriptionStore`,
  `IResolveApprovedMailboxIdentity`, `ListApprovedOutlookCategories`,
  `UpdateApprovedOutlookCategory`) — unchanged; this is a presentation port,
  not a business-rule change.
