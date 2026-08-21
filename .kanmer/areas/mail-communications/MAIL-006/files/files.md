# File map and integration method

Base: `origin/dev`. Every line number below is `origin/dev` as of 2026-08-21.

## Preconditions

1. `git fetch origin`, then branch `task/mail-006-inbox-message-record` from
   `origin/dev` into worktree `../pegasus-worktrees/mail-006`. Unset upstream
   after branching.
2. Land [[MAIL-008]] and [[PLAT-019]] **first**, or accept that this branch
   renders the classification slug and the dialog consequence copy until they
   land. Do not fix either here — both reach beyond this page.
3. Open `docs/design/references/mockups/inbox-message-page/preview/Main.html`
   and keep it beside the editor. It is the target, not a suggestion.

## Files

| File | Change |
| --- | --- |
| `docs/design/references/mockups/inbox-message-page/**` | Commit as-is — currently untracked |
| `src/Pegasus.Web/wwwroot/css/site.css` | Append `.decision`, `.mail-*`, `.form-column`, two `.facts` pins; edit `.record__head` |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Rewrite |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | `ActiveSection` gains `case`; add body-layout helper; carry `section` on association round-trips |
| `src/Pegasus.Core/Intake/StaffForwardBodyCleaner.cs` | Add a split that exposes the forwarded-header boundary |
| `tests/Pegasus.Core.Tests/Intake/StaffForwardBodyCleanerTests.cs` | Cover the split |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Update markup assertions; add absence assertions |

## Step 1 — CSS first, so the markup has something to land on

Append to `site.css` (end of file, the convention for ticket-scoped additions).
Copy the values verbatim from the artboard stylesheet; do not retype them.

- `.decision` — the right-hand card: `padding: var(--sp-4) var(--sp-5) var(--sp-5)`,
  `border: var(--border)`, `border-radius: var(--radius)`, `background: var(--paper)`;
  `.decision + .decision { margin-top: var(--sp-5) }`.
- `.decision .facts` and `.reason-dialog .facts` — pin
  `grid-template-columns: minmax(0, 1fr)`. **Required**: `.facts` is
  `repeat(auto-fit, minmax(230px, 1fr))` and would otherwise put the heading in
  one column and its list in the next. `.facts` has no other caller in the
  repo, so nothing else is affected.
- `.form-column` — `width: 100%; max-width: 680px`.
- `.mail-from`, `.mail-route`, `.mail-quoted`, `.mail-body` — the letter
  treatment, replacing today's `.mail-body` rule at site.css:1920.
- Edit `.record__head` (site.css:1550) to `align-items: flex-start` and
  `padding: var(--sp-4) var(--sp-6)`; edit `.record__head h1` (1559) to drop
  nothing and add `overflow-wrap: anywhere; text-wrap: pretty`. The subject must
  wrap, not truncate. Give `.record__head-end` `flex: 0 0 auto; padding-top: 2px`.

**No inline `style` attributes.** Production CSP is `default-src 'self'` with no
`style-src`, so they are discarded, and
`tests/…/Browser/AccessibilityTests.cs` asserts zero of them on `/Inbox`.

## Step 2 — Core: expose the forwarded-header boundary

`StaffForwardBodyCleaner` already owns that boundary via
`ForwardedHeaderRegex()`, and its own remarks warn the pattern is kept
byte-identical to `MimeKitPdfPigOpenXmlIntakeSourceReader.InlineForwardedHeaderRegex`
and "the two patterns must be changed together". **Do not write a third copy in
Web.** Add to the cleaner:

```
public static (string QuotedHeader, string Body) SplitForwardedHeader(string cleaned)
```

returning the matched `From:/Sent:/To:/Subject:` block and everything after it,
or `("", cleaned)` when there is no match. Pure, no new dependency, no change to
`Clean`'s existing behaviour or to the regex itself.

Tests: a forward splits into both parts; a non-forward returns an empty header
and the body unchanged; the existing five tests still pass.

## Step 3 — PageModel

`Message.cshtml.cs`:

1. `ActiveSection` (dev:~1010) — add `"case" => "case"` to the switch. Keep the
   fall-through to `"message"` for unknown values.
2. Add `public static IReadOnlyList<string> Paragraphs(string body)` — split on
   blank lines; a run of consecutive non-blank lines is one paragraph rendered
   with `.run-on` spacing. View formatting only, one caller, so it stays a
   static on this model rather than becoming a type.
3. **Carry `section=case` through every association round-trip.** The search
   form, the candidate links and `targetCaseId` are GET (dev:157-200); the
   link/unlink prepare and confirm handlers redirect (dev:199-447). Every one
   must preserve `section=case` or the operator is thrown back to the Message
   tab mid-flow. This is the easiest thing on the ticket to miss.
4. Leave every handler's Core call, expectation value and exception mapping
   exactly as it is. This is a view change; the six POST handlers keep their
   signatures and their optimistic-concurrency behaviour.

## Step 4 — The page

Rewrite `Message.cshtml` against the artboards. Old → new:

| dev lines | Was | Becomes |
| --- | --- | --- |
| 13-21 | `.back-link` | unchanged, above the record |
| 23-30 | `.page-heading` + `Open case` | `.record__head` — subject + `_StatusChip`. **`Open case` is deleted** |
| 32-57 | four `.status-card` notices | same cards, moved **inside** `.record__body`, above the tab content, as `Cases/Details.cshtml` does |
| 59-85 | `.evidence-row` of three figures | the Decision card's first three rows. The `Queue` figure is relabelled — `Accepted` is a routing disposition, not a work list |
| 87-266 | Case association panel | the **Case** tab, one `.form-column` |
| 268-292 | Classification evidence `.detail-list` | Decision card rows. Policy keys, both policy versions, `Decision version`, `Reason`, `Latest folder move` and the unavailable-folder rows are **deleted** |
| 294-317 | uncertain-move form | `Check move status` button in the Decision card; the row reads `Unconfirmed` |
| 318-353 | suggested move + `<h3>` + reason `<p>` | `Move to <Folder>` button; heading and paragraph **deleted** |
| 355-365 | Material evidence `<ul>` | **deleted** |
| 366-399 | Correct classification form | moves into the reason dialog; the two `(only when Other is selected)` labels lose the parenthetical |
| 401-423 | Permanent correction history `<ol>` | Corrections card beneath the Decision card, rendered only when non-empty |
| 425-432 | standalone Folder recommendation panel | **deleted** — `README:436` |
| 434-457 | `nav.tabs.section-gap` | `nav.tabs` **inside** the record; add the **Case** tab; use `<span class="count">` for the attachment count instead of baking it into the link text |
| 459-575 | Message / Attachments / Thread panels | Message becomes `.split-main`; Attachments and Thread keep their content, inside `.record__body` |

The Case tab renders only when `Model.AssociationReceipt` is not null, matching
dev's existing guard at line 87.

## Step 5 — Tests

`MailWorkspaceWebTests.cs` asserts on literal markup fragments, so the rewrite
breaks assertions by design. Update them to the new fragments and **add**:

- `Assert.DoesNotContain` for `qdos_mail_classification`, `version `,
  `subject.`, `Decision version`, `Read from the message`, `Open case`.
- The Case tab round-trip keeps `section=case` across a search and a link.
- `Filed to` renders the case link when filed.
- The existing read-only guard (`CountOccurrences(html, "method=\"post\"")`)
  still holds for a viewer.

Commands (`docs/runbook.md`):

```
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~StaffForwardBodyCleanerTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MailWorkspaceWebTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2
```

Then the canonical solution run before the PR.

## Step 6 — Before the PR

Run the simplification pass over the branch diff (`/simplify` plus the
`code-simplifier` agent) and record findings and dispositions in `plan.md`
under a dated "Simplification pass" heading. Refresh
`docs/current-architecture.md` only if the as-built shape changed — a view
rewrite normally does not.

## Traps

- **`section=case` on association round-trips** — step 3.3.
- **A third copy of the forwarded-header regex** — step 2.
- **`.facts` splitting into two columns** — step 1.
- **Inline styles** — the accessibility test fails, not the CSP, so it looks
  like a test problem rather than a rendering one.
- **`_ReasonDialog` is a `<div>`, not a native `<dialog>`.** `site.js:96-113`
  has a second `[data-dialog-open]` handler for native dialogs that bails on
  the `showModal` check. Do not convert the partial; both handlers would fire.
- **The move dialog is a confirmation.** No typed reason
  (`frd-08:243` — only the designated folder, no arbitrary choice). The reason
  posted is the derived one; `_ReasonDialog`'s textarea posts as `name="Reason"`,
  so the derived value goes in a hidden field of that name.
