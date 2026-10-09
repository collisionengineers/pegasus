# Case record: how it works today

Read from the live source on 9 October 2026 (`origin/dev` 37c4b96f5) and walked on the synthetic visual host the same day. Temporary review artifact; see the [round README](../../README.md).

## What the page does not show

- That entering the edit session grows some sections: Valuation draws its Value increases sub-panel and the Calculation line only while editing (`_CaseValuationCalculation.cshtml:54,81,202`), so the section is 590px reading and 913px editing at 1580 (measured).
- Why a section head offers Edit on a section the viewer cannot edit: `SectionOffersEdit` ignores `CanEditEngineering` (`Details.Frame.cs:133-138`), so a Case-data editor claims the lease and the Engineer sections lock with no label.
- Which fields a first save records: the first commit of a session wrote "Vehicle make, Inspection address treatment, Impacts, Disclose guide source, Include unrelated damage, Valuation commentary" to the timeline when one field was typed (walk, 9 October 2026).
- Where a Workflow or Closure action lands: `RedirectToDetails` carries the view and drops the section, so Hold, Release Hold, Return to Review, Assign Engineer, Correct principal, Close, Complete, Return to Engineer, Archive, Unlink report evidence and Create audit land at the top (`CaseMutationPageModel.cs:648-675`, `Details.Frame.cs:256`).
- That Refresh drops the selected repair spec (`Details.cshtml:99-104` carries `section` and `view` only).
- That the Scroll/Tabs choice is a session cookie while the fold and rail cookies last a year (`case-workspace.js:95-99`, `site.js:2051`).

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [Design authority](../../../../../docs/design/README.md) | Case record frame: ribbon, section row, sections, aside, read/edit one geometry, source tags, keyboard contract |
| [Case-workspace guardrails](../../../../../.agents/skills/pegasus-ui-guardrails/references/case-workspace.md) | Frame fixed by default, section order and ownership, availability, Actions menu, Valuation, Repair Spec, Damage, Files, aside |
| [FRD-16](../../../../../docs/frd/frd-16-case-record-workspace.md) | The record workspace's interactions |
| [FRD-13](../../../../../docs/frd/frd-13-case-lifecycle-and-workflow.md) | Lifecycle, readiness and blockers |
| [CONTEXT.md](../../../../../CONTEXT.md) | Reserved terms |

## Source by layer

| Layer | File | Owns |
| --- | --- | --- |
| Web | `src/Pegasus.Web/Pages/Cases/Details.cshtml` | the article, sticky block, section loop, lazy placeholders, aside and dialog roots |
| Web | `Details.cshtml.cs`, `Details.Frame.cs`, `Details.Views.cs`, `Details.*.cs` | page model: lease, editability, section visibility, redirects, Next action |
| Web | `Pages/Cases/Shared/_CaseRibbon.cshtml` | the 56px ribbon, its chips, Edit Case / Editing / Done and the Actions menu |
| Web | `_CaseAside.cshtml` | Views, Linked cases, Figures, Next action, Report not ready |
| Web | `_CaseOverview`, `_CaseClaim`, `_CaseOriginalReport`, `_CaseInspectionAddress`, `_CaseVehicle`, `_CaseDamage`, `_CaseValuation` (+ `Calculation`, `Opening`, `Preview`), `_CaseEstimate` (+ `_Glass*`, `_RepairSpecDiff`), `_CaseSettlement`, `_CaseReport`, `_CaseFiles` (+ `_CaseDocuments`, `_CaseImages`, `_CaseImageTagPicker`, `_CaseCorrespondence`), `_CaseHistory` | the sections |
| Web | `_CaseSectionHeadTools`, `_SubPanelToggle`, `_CaseNotices`, `_CaseCommitResult`, `_CaseSaveForm`, `_CaseDialogs`, `_CaseViewer` | head tools, folds, notices, the in-place commit answer, the one Save form, dialogs, the viewer |
| Web | `Presentation/CaseWorkspaceLabels.cs`, `Presentation/OperatorLabels.cs` | every label |
| Web | `wwwroot/css/site.css` (frame 1032-1091, cells 990-1030), `wwwroot/css/case-workspace.css` | geometry and look |
| Web | `wwwroot/js/case-workspace.js` (session 16-1830, damage 1879-2466, valuation 2499-2994, estimate 3004-3650, settlement 3945-4227, viewer 4383-5235), `wwwroot/js/site.js` (busy 128-413, dialogs 1006-1229, toasts 1843-1911, menus and folds 2116-2240) | behaviour |
| Core | `Pegasus.Core.Workflow` (`CaseEditAuthority`, `CaseLifecycleRules`), `Pegasus.Core.Assessment` (`AssessmentPolicy`, `AssessmentVocabulary`), `Pegasus.Core.Reports` (`CaseReportReadiness`) | lease authority, states, readiness and blockers |
| Infrastructure | `Persistence/EfCaseWorkflowStore.cs` | the lease on the workflow row; the section stores |

## Behaviours

### Frame

The sticky block is the 56px ribbon and the 40px section row under the 48px utility bar: 97px, measured into `--sticky-h` by `measure()` and re-measured by a ResizeObserver, so a second chip row grows it live. At 760px the utility bar is 96px and the ribbon 208px: 345px of sticky chrome (measured). Below 1441px the aside folds into a two-up strip above the sections; at 1441px and above it is sticky and capped at the viewport.

### Ribbon

Five facts (Case workspace / reference, Registration, Claimant, Principal, Engineer), chips (state with the Held review date, Case type, outcome, roadworthiness, repairs-of-value, Archived, "{Name} is editing"), then Original/Replacement case links, Edit Case or Take over (`btn--dark`), or while editing the lease line ("Saving…", "Saved HH:MM", a refusal), the Editing badge, a script-hidden Save now, Done; and one Actions menu. The menu's items follow the state (`_CaseRibbon.cshtml:26-58`); Export case is always listed; Create audit is listed on an Inspection + Audit Case and greyed in a `.menu-gated` span with its reason as `title`.

### Section row

One link per shown section (Damage and Valuation ride under Vehicle; Original report only on an Audit Case), Refresh (a GET form with `section` and `view`), and the Scroll/Tabs switch, hidden until script. `.section-nav` is `overflow-x:auto` with its scrollbar hidden: at 1440px with ten links the Notes link is clipped and its icon shows beside Refresh; at 760px the overflow is 380px (measured).

### Sections

Each is `section.record-section.panel` with a 51px head (title, optional meta, the head tools: primary, More, Edit, one availability label, fold chevron). Edit is read-mode only and claims the page-wide lease with `section` so the section stays in place (measured: a 16px scroll nudge, section top unchanged). Every field is a `.fc` cell: label line (with the one `src-tag` word), a greyed `.fv` value box reading and a white `.fi` control editing, `ro` where it cannot be edited. Lazy sections (Vehicle, Valuation, Files, Notes) render a 120px hatched placeholder and mount when within 2.5 viewports.

### Edit session and saving

Edit Case or a section Edit claims the lease and redraws the record `is-editing`. A plain control commits on change, a composite editor after a 1000ms idle or on leaving it; Ctrl S commits now. One commit at a time; a change during a commit queues. A landed commit swaps notices, ribbon halves, aside and dialogs at once, then a catch-up GET swaps them again and replaces every section holding nothing unsent. Focus is restored inside `#case-main` by id or position. Done posts ReleaseLease; leaving by a link beacons it; a heartbeat keeps the lease while the page is open.

### Aside

Views (once an Audit exists), Linked cases (while a Triage is linked), Figures (three figures, "—" when absent), Next action (AI drafts, Cancellation received, a stale notice, then one step: the first Case requirement, else the state's step, else the first blocker, drawn in full with a full-width control; "Held" is drawn as a word with a Case details button), Report not ready (everything outstanding, folded until opened, remembered by the fold cookie).

### Dialogs, viewer, toasts

Dialogs mount in the dialog root with a focus trap and `inert` siblings. The viewer is `position:fixed` at z 1200 (above the toast region's 1100) with Rotate, Zoom, Download, In report while editing, and crop on the stage. Toasts live 4200ms with no hover pause; a refusal is both a notice and a toast.

## Things the FRD does not settle

- Whether a section Edit should be offered on a section the viewer cannot edit, and what the locked section says.
- Where a Workflow or Closure action lands on return.
- How a first commit of the session should record untouched default fields.
- The empty value word: cells say "Not recorded", guide cards and Figures say "—", Repair Spec work-lists say "none", Decisions' Salvage says "Not applicable".
- Whether the Statement of truth (five paragraphs, 207px, the same on every Case) belongs on the Case page as a greyed box.
