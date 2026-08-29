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

### What existed before this ticket (pre-port baseline, `origin/dev` at
`b92cb9a7`)

- `Pages/Administration/Mailboxes.cshtml` / `.cshtml.cs` — the Approved
  mailboxes table: Mailbox, Scope, Last update (poll status), State
  (`_StatusChip`), Activated, Subscription, Review folders/Refresh
  (`<details>` disclosure with the edit form), plus an "Add mailbox"
  disclosure and, in the same file, a second section for Mail categories
  (Category, State, Review) with its own "Add category" disclosure.
- `Pages/Administration/MailCategories.cshtml.cs` — already a bare
  `RedirectToPagePermanent("/Administration/Mailboxes")`; `MailCategories.cshtml`
  a placeholder page for the same redirect.

So the two tables were **already co-located on one physical page**
(`Mailboxes.cshtml`), with `MailCategories` already a redirect stub — the
prior ticket (pre-EPIC-011, `0d2be937`/`4d00c3b7` lineage) had already done
the functional consolidation. What was missing against the EPIC-011 contract
was the **admin-layout shell**: the page rendered its own bespoke header
instead of `<div class="admin-layout">` + `<partial name="Shared/_AdminNav" />`
+ the `panel`/`panel-head` h2/description/meta convention that PLAT-029
(wave 1) established and that `Pages/Operations/Index.cshtml` (PLAT-023,
merged) already demonstrates for a multi-table admin/ops page.

### Load-bearing state that must not regress (MAIL-017/018/020/021)

- **Activated** column — `OperatorLabels.OfficeTime(mailbox.ActivatedAtUtc, …)`
  — real state: whether Graph has confirmed mailbox identity/activation, not
  decoration.
- **Subscription** column — `Model.SubscriptionStatusFor(mailbox)`, backed by
  `IApprovedMailboxSubscriptionStore` and `ApprovedMailboxSubscription`
  (`LifecycleState`, `ExpiresAtUtc`, `LastMaintenanceFailureCode`,
  App-Insights-capped maintenance) — real Graph subscription health, not
  decoration.
- Both columns must survive the re-skin with their existing data sources
  unchanged; only presentation/labels move.

### Design authority

`docs/design/README.md` §"No explanatory copy and page economy" (line 638)
binds: labels/values/controls only, no hint text, no how-it-works prose, no
empty-state paragraphs beyond a single "No approved mailboxes" /
"No mail categories" row. The pre-existing page already followed this; the
port preserves it.

### Consolidation decision (recorded, not newly made)

The contract folds Mailboxes + MailCategories into one "Mail settings" area.
Deletion of the superseded `/Administration/MailCategories` route belongs to
UIIMP-009 (wave 5), per the epic's greenfield-but-staged-deletion rule. This
ticket keeps the existing `MailCategories` redirect-stub route intact (it
already forwards to Mailboxes) and reports it as the surface for UIIMP-009's
deletion list — no new seam was needed since the redirect already existed.

### Reuse

- `Pages/Shared/_AdminNav.cshtml` (PLAT-029) — read only, not modified.
- `Pages/Shared/_StatusChip.cshtml` — reused for mailbox/category state chips.
- `Pages/Operations/Index.cshtml` house style — multi-table panel-per-area
  pattern followed for the Mail settings panel head (h2 + description + meta
  line).
- `OperatorLabels` — a new nested `MailSettings` static class appended at the
  end of the file (existing members untouched, per the shared-file rule for
  `OperatorLabels.cs`).
- Existing Core ports (`ListApprovedMailboxes`, `UpdateApprovedMailbox`,
  `IApprovedMailboxPollStatusQueries`, `IApprovedMailboxSubscriptionStore`,
  `IResolveApprovedMailboxIdentity`, `ListApprovedOutlookCategories`,
  `UpdateApprovedOutlookCategory`) — unchanged; this is a presentation port,
  not a business-rule change.
