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

### Correction (2026-08-29)

An earlier draft of this section was written after this session's own file
reads landed mid-way through the implementing agent's in-progress edit, and
wrongly described the already-ported markup as the pre-existing baseline.
Corrected against `git show origin/dev:<path>` (the true `b92cb9a7` baseline):

- `Mailboxes.cshtml` / `.cshtml.cs` were the **existing bespoke layout**: a
  `back-link` to `/Administration/Index`, a `<partial name="Shared/_PageHeader" />`,
  a raw `status-card` notice, no `admin-layout`/`_AdminNav` shell. It held
  only the Approved mailboxes table (Mailbox, Scope, Last update, State,
  Activated, Subscription, Review folders/Refresh) with its own per-mailbox
  edit form, keyed by top-level bound properties (`MailboxId`,
  `ExpectedVersion`, `OperationKey`, `Address`, …) — no `MailboxForm` wrapper.
- `MailCategories.cshtml` / `.cshtml.cs` was a **separate, fully independent
  page** (its own bespoke layout, its own `Save` handler, its own
  `ListApprovedOutlookCategories`/`UpdateApprovedOutlookCategory` calls, its
  own top-level bound properties `CategoryId`/`DisplayName`/`SelectedState`/
  `ExpectedVersion`/`Reason`/`OperationKey`) — **not** a redirect stub. It was
  reachable at its own route and rendered its own "Current categories" /
  "Add an approved category" sections.

So this ticket's actual job was two things at once: (1) re-skin onto the
`admin-layout` shell, and (2) physically **fold the two pages into one**
(Mailboxes hosts both tables; `MailCategories` becomes a permanent redirect
to `/Administration/Mailboxes`) — not just a shell re-skin of an
already-combined page.

### Load-bearing state that must not regress (MAIL-017/018/020/021)

- **Activated** column — `OperatorLabels.OfficeTime(mailbox.ActivatedAtUtc, …)`
  — real state: whether Graph has confirmed mailbox identity/activation, not
  decoration.
- **Subscription** column — `Model.SubscriptionStatusFor(mailbox)`, backed by
  `IApprovedMailboxSubscriptionStore` and `ApprovedMailboxSubscription`
  (`LifecycleState`, `ExpiresAtUtc`, `LastMaintenanceFailureCode`,
  App-Insights-capped maintenance) — real Graph subscription health, not
  decoration.
- Both columns must survive the re-skin/merge with their existing data
  sources unchanged; only presentation/labels/binding-property-prefix moved.
  Verified in the merged diff: both columns and their backing calls are
  present unchanged in `Mailboxes.cshtml.cs`.

### Design authority

`docs/design/README.md` §"No explanatory copy and page economy" (line 638)
binds: labels/values/controls only, no hint text, no how-it-works prose, no
empty-state paragraphs beyond a single "No approved mailboxes" /
"No mail categories" row.

### Consolidation decision (recorded)

The contract folds Mailboxes + MailCategories into one "Mail settings" area.
Deletion of the superseded `/Administration/MailCategories` route (now a
`RedirectToPagePermanent`) and the still-standing separate card on
`Administration/Index.cshtml` belong to **UIIMP-009** (wave 5), per the
epic's greenfield-but-staged-deletion rule. This ticket leaves the redirect
stub as the smallest honest seam and reports both surfaces for UIIMP-009's
deletion list.

### Reuse

- `Pages/Shared/_AdminNav.cshtml` (PLAT-029) — read only, not modified.
- `Pages/Shared/_StatusChip.cshtml` — reused for mailbox/category state chips.
- `Pages/Operations/Index.cshtml` house style — multi-table panel-per-area
  pattern followed for the Mail settings panel head (h2 + description + meta
  line).
- `MailLogicalFolders.All` — reused for the folder-bindings disclosure.
- `OperatorLabels` — a new nested `MailSettings` static class appended at the
  end of the file (existing members untouched, per the shared-file rule for
  `OperatorLabels.cs`).
- Existing Core ports (`ListApprovedMailboxes`, `UpdateApprovedMailbox`,
  `IApprovedMailboxPollStatusQueries`, `IApprovedMailboxSubscriptionStore`,
  `IResolveApprovedMailboxIdentity`, `ListApprovedOutlookCategories`,
  `UpdateApprovedOutlookCategory`) — unchanged; this is a presentation port,
  not a business-rule change.
