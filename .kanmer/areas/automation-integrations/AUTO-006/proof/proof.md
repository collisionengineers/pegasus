# Proof — AUTO-006: Redesign the Automation & AI administration area

## Scope of this proof (decision D15)

Written against **merged `dev` at `450b9234a6f5626f21adea3c4da244550a3bdace`**
(2026-08-29 18:03:20 +0100, "Merge pull request #621 from
collisionengineers/task/eng-027-case-valuations").

This is **dev-merged evidence, pending the single wave-5 `dev` → `main`
promotion**. `main` currently serves release 36 (`783b4b88`); nothing in this
ticket is deployed. Per D15 the ticket walks to Done on this evidence; the
exact-SHA, non-force promotion to `main` happens once, at wave 5, under
explicit `MERGE AUTH GRANTED`.

## The work is on `dev`

PR [#618](https://github.com/collisionengineers/pegasus/pull/618) merged as
`cba29a4f` ("Merge pull request #618 from
collisionengineers/task/auto-006-automation-admin", 2026-08-29 15:26:06 +0100).

```
git merge-base --is-ancestor cba29a4f 450b9234   -> exit 0 (ancestor)
```

`git show --stat cba29a4f` — 7 files, 803 insertions, 305 deletions:

| File | Role |
| --- | --- |
| `Pages/Administration/Automation/Index.cshtml(.cs)` | the redesigned area |
| `Pages/Administration/Automation/Activity.cshtml(.cs)` | inherited PLAT-015 scope |
| `Presentation/OperatorLabels.cs` | +43 lines, this lane's nested class |
| `tests/…/AutomationAdministrationWebTests.cs` | +429 lines, new |
| `tests/…/SendToAiConnectorAdministrationTests.cs` | retargeted |

Content confirmed landed, not merely claimed by the PR: every `file:line`
quoted below was read with `git show 450b9234:<path>`.

## Capability → production caller

Capabilities enumerated from this ticket's own **What / Approach /
Verification / Inherited scope** sections.

| Capability the ticket names | Production caller | Evidence |
| --- | --- | --- |
| The Automation & AI administration area exists as an operator surface | Route `@page` at `Pages/Administration/Automation/Index.cshtml:1`, linked from the admin rail | `Pages/Administration/Shared/_AdminNav.cshtml:35` `<a asp-page="/Administration/Automation/Index" …>` |
| Automation status / registered clients / active + failed job counts | `OnGetAsync` at `Index.cshtml.cs:75` | model bound and rendered in `Index.cshtml` |
| Stop / Start automation (danger, reason-gated) | `OnPostSetEnabledAsync` at `Index.cshtml.cs:88` | `Index.cshtml:72` opens `automation-kill-switch-dialog`; the dialog's `DialogActionUrl` is `Url.Page("/Administration/Automation/Index", "SetEnabled")` (`Index.cshtml:180`) with hidden `TargetEnabled` + `OperationKey` |
| AI settings (proposal, timeout, enabled) with Save | `OnPostSaveAiSettingsAsync` at `Index.cshtml.cs:130` | `Index.cshtml:114` `<form method="post" asp-page-handler="SaveAiSettings">`, submit at `:150` |
| Remove the AI channel token | `OnPostClearChannelTokenAsync` at `Index.cshtml.cs:207` | `Index.cshtml:156` opens `ai-remove-token-dialog`; `DialogActionUrl` = `Url.Page(…, "ClearChannelToken")` (`Index.cshtml:196`) |
| **Inherited PLAT-015:** resolve a raw `AggregateId` to a business reference, or omit it | `Activity.cshtml.cs:83` `TargetReference(record)` → `_caseReferences` populated by `ResolveCaseReferencesAsync` (`:89`–`:104`, reading `details.Summary.Reference`) | rendered at `Activity.cshtml:57` `@(Model.TargetReference(record) ?? "—")` under the `Reference` column header (`:44`) |
| **Inherited PLAT-015:** remove the "you can filter by" narration | Absent | `git grep -n -i "filter by" 450b9234 -- src/Pegasus.Web/Pages/Administration/Automation/` returns nothing |

### No inert control

Every `<button>` on the redesigned area either submits a form or opens a
dialog whose action URL resolves to a handler on this page model. There is no
`disabled` control and no `data-dialog-open` without a matching dialog
partial. The two dialogs are conditionally rendered
(`Index.cshtml:171` `@if (registration is not null)`, `:190`
`@if (connector is { TokenHeld: true })`) — conditional state, not a D7 seam.

### The composition gate is OPEN in the deployed estate (D21)

The area is listed only when `AdminAutomationComposed` is true
(`_AdminNav.cshtml:34`), which follows the `Features:AutomationMcp`
composition gate. Per the D21 table this qualifies as delivered only if that
gate is open in the deployed estate and `docs/operations.md` records it. It
is:

- `infra/modules/platform.bicep:467` — `{ name: 'Features__AutomationMcp', value: 'true' }`
- `docs/operations.md:131` — heading "Automation MCP is implemented and
  **enabled in production**"; `:138` — "since release 9 (2026-08-18) the
  production Web revision renders `Features__AutomationMcp=true` from Bicep".

## Commands run, with exit codes

Run in the main checkout on `dev` at `450b9234`, Windows + PowerShell 7.

```
dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false
  -> Build succeeded. 0 Warning(s), 0 Error(s).   exit 0

dotnet test ./Pegasus.slnx --configuration Release --no-build
  --filter "FullyQualifiedName~AutomationAdministrationWebTests"
  -> see the run record appended below
```

CI on the branch head `7e5cf00c` (run 33254724609): **success**, with all four
`sql-integration` shards green.

## What this evidence does NOT prove

- **Nothing here is deployed.** `main` is at release 36 and does not carry
  this code. This is tier-2 (build/test + caller-backed source) evidence, not
  tier-3 deployed-and-exercised evidence.
- **No browser or layout walk.** No claim is made about clipped text or
  overflow at 1580/1100/760 for this area. **UIIMP-010** owns the merged walk.
- **No Test UI snapshot claim.** The catalogue/snapshot corpus is regenerated
  once per merge on the merging branch (EPIC-011 decisions, "Two shared
  files"); the gate that consumes it is **UIIMP-005**, still unmerged. This
  proof asserts nothing about snapshot currency for this area.
- **No live automation was started or stopped.** The kill switch was not
  exercised against any deployed environment; per D26 no lane performs a live
  activation.
- **The `Features:AutomationMcp` production evidence predates this code.**
  Release 9 proves the gate is open; it does not prove this revision of the
  page ran in production, because this revision has never been deployed.
- **`AutomationAdministrationWebTests` is a test, not a caller.** It is cited
  as regression cover for the handlers, not as the production caller; the
  production callers are the routes and forms in the table above.
