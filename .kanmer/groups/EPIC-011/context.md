# EPIC-011 context — binding for every member ticket

Source of record: `C:\Users\PC\Downloads\Pegasus_UI_Assessment_Refined.html`
(layered prototype; ~15 monkey-patch passes). **Only the effective final
render layer is the contract** — it is transcribed in §1 below. Earlier layers
(baseline/analyst designs, "Work streams", the old Triage action bar, the
`assessment-v2` layout, the Additional case section) are dead and must not be
ported. The prototype's fixture data is not domain data — never copy it.

Rules (in addition to AGENTS.md and EPIC-008's context):

- No explanatory copy: port labels, values and controls; never hint sentences,
  empty-state prose, "why this matters" text or how-it-works copy from the
  prototype. `docs/design/README.md` §Voice and §No explanatory copy bind.
- Every drawn control maps to a named handler or an approved disabled seam
  (D7). Never render an inert control.
- One list per concept: labels live in `Presentation/OperatorLabels.cs`.
- A ticket owns whole files; never touch a neighbour lane's files. Report
  what belongs to another ticket; do not fix it.
- Subagents do not run tests, snapshot scripts or browser tests; the
  orchestrator runs the wave loop. Build only for compiler feedback.
- Fixtures use the documented estate; no fabricated domain data.

## 1. Effective design contract

### 1.1 Shell (every authenticated page)
- `.app-shell` grid: 220px `.app-rail` (sticky, dark gradient, 3px red top stripe) + `.app-column`.
- Rail: brand (logo 48px scaled, "PEGASUS" / "Case management"), nav-label "Work", links in order **Work Centre (/), Inbox [count], Upload, Cases (/cases) [count = not_ready+review+with_engineer+held+triage+unidentified], Search (/search), Operations [count]**, nav-label "Manage", **Administration** (admins only). Current route: white bg, red left border, red icon well. Counts are page-queried figures; absent count renders nothing.
- Rail foot: health line (dot + "Current · HH:MM"), user block (avatar initials, name, role, account menu button → dialog: Name/Role/Session started/Idle lock, Close, Sign out).
- Utility bar (dark, sticky): freshness text, global search input with "Ctrl K" hint (Enter/Ctrl K → command palette dialog), "Add" primary button (dialog: Upload files / Create Case / Create upload request / Review Inbox), bell → Notifications dialog.
- Workspace tabs strip: "Work Centre" tab + one closable tab per open Case record (max 4, LRU) + "Open" button (command palette).
- `main.app-main` → `.content` max 1580px centred; skip link; toast region; dialog root with focus trap + `inert`.
- Keyboard: Ctrl K palette, Ctrl U upload, Ctrl N new case, Ctrl S save while editing, F5 refresh (re-query, not reload), ArrowUp/Down through row lists.
- `<980px`: rail lies down to horizontal bar (labels hidden), current = bottom border. `<760px`: single column everywhere.

### 1.2 Work Centre `/`
Header "Work Centre" / eyebrow "Office-wide work" / freshness + Refresh + Create Case (primary). Metric strip (5, buttons → `/cases?tab=…`): Not ready, Review, Held, Unidentified, Blocked. Two-pane `integrated-home--expanded`: left "Needs attention" (work-item buttons: kind·ref, title, priority chip, detail, owner, due); right head "Today"/"Selected work" + "Open full record", detail: eyebrow, h2, lead, chip, notice "Why this needs attention" (label + Core-derived value only), fact grid (Source, Owner, Last recorded outcome, Due), panel "Next permitted action" (Open Case/Triage/Operations/Review source + Copy reference). Work-item kinds: Case (due chase, blockers, readiness), Mail (Unidentified), Triage (no finding), External work (retryable failure), Held decision.

### 1.3 Inbox `/inbox`, message `/inbox/{id}`
List: header "Inbox"/"Retained mail"; filter bar (Mailbox, Folder, Queue selects, search, Search dark, Refresh); 3 panes: Scope (All incoming, Unread, Receiving work, Case updates, Pre-instructions, Unidentified, Sent Items — each with icon well + count), Messages (sort toggle "Received ↓/↑", rows with unread dot, sender, date/time, subject, excerpt, outcome chip, caseRef/queue · attachments; pagination), Message preview (subject, route, chip, excerpt, attachment chips, fact grid Classification/Case association/Folder/Search match; Open full message, Open linked Case).
Message: header subject/"Inbox message"/Back to Inbox; record head; record bar **Reply (dark), Forward, Compose, Flag, Delete (danger)** → composer dialog (To, Subject, Message, Case, From) → Send creates Sent-Items evidence linked to Case; Delete → reason dialog → Deleted Items. Tabs Message / Attachments (n) / Thread / Case. Decision card (Classification, Destination, Filed to, Folder, Decided·Automatic; Correct classification; Move to X / Check move status), Corrections timeline. Attachments table (File, Type, Size, Search content, Custody, Preview). Case tab: summary card, Open Case, Change association.

### 1.4 Cases `/cases` (was Queues)
Header "Cases"; filters: Principal select, (Not ready only) Missing select (All/Instructions/Images/Both missing), Clear. 3-pane `queue-layout`: rail "Case workflow" groups — **Workflow:** Not ready, Review, With Engineer, Complete · **Pre-Case work:** Triage · **Exceptions (amber):** Held, Unidentified — each with icon well + count. Middle: rows per kind (case: ref·reg, chip, claimant·principal, origin·received, due; image-initiated: ref·reg, files·custody; triage: ref·reg, provider·assignee; unidentified: ref·kind, handle, received·reason). Right "Quick detail": case → eyebrow origin, h2, compact workflow stepper, Outstanding requirements, Current work (Due, Engineer, Next action, Open full Case); triage/unidentified/image → definition list + open button.

### 1.5 Triage `/triage/{id}`
Header ref/"Triage"/Back to Cases (`/cases?tab=triage`) + Refresh. Record head (h1 ref; reg, provider; state chip). Record bar: eyebrow "Triage" + assignee. Body: Determinations panel (Roadworthiness select, Repair outcome select, Save determinations primary) · Source panel (Material, Received, Case link) · Notes panel (entries Date/Time/ID + text). Existing server-side transitions (await information, link, complete, cancel, reopen) stay available through the determinations flow / dialogs where a handler exists.

### 1.6 Unidentified `/unidentified/{id}`
Header ref/"Unidentified"/Back to Cases + Refresh. Record head; warning notice (reason); Retained source panel (Permanent reference, Kind, Operator handle, Received, Source, Canonical reason; View retained source, Resolve destination dark); History panel. Resolve dialog: Destination select (Add to existing Case / Create Case from accepted instruction / Register Image-initiated Case / Close with reason), case picker, reason.

### 1.7 Search `/search` (was Cases)
Header "Search"/freshness + Create Case. Advanced grid: Case/PO or image reference, Registration, Claimant, Claim/provider reference, Principal, State, Engineer, Received from/to, Origin, Search (dark), Clear. Two panes: results table (Case/PO + provider ref, Vehicle + make/model, Claimant, Principal, Type, State, Due; rows selectable via hover/focus/click/Enter) + "Selected Case" preview (eyebrow type, h2, chip, Accident circumstances, fact grid Provider ref/Engineer/Due/Next action, Outstanding (2), Open Case, Copy Case/PO).

### 1.8 Case workspace `/cases/{id}`
Header ref / "Case workspace · reg" / Back to Cases + Refresh. Identity ribbon (Case/PO, Registration, Claimant, Principal, State chip). Presence strip. Action bar: Edit Case | Finish editing + Renew editing | "Editing held by X until T." | Reopen Case (closed); Place on Hold / Release Hold; Create upload link; **Send to EVA** (Review) / **Download EVA package** (With Engineer or Complete, exported) — both open the EVA handoff dialog (Engineer assignment; Export ZIP / Send via API); **Report sent** (primary, With Engineer — confirm detected Sent evidence, D10; enters post-report work, does not close) / **Return to Engineer** (Complete); right: Open Assessment (dark, With Engineer/Complete only — D11), Close Case (danger, not Complete). Sticky edit bar when editing (lease text, Unsaved chip, Discard, Save).
Workspace: side nav **Case Overview, Vehicle, Valuations, Inspection address, Case Files, Notes** | main | context (Current position: State, Version, Due, Engineer, Edit authority; Next action card).
- Overview: workflow stepper (Not ready → Review → With Engineer → Complete; Held exception badge); Outstanding requirements (blockers: title, Source, Why, Resolve); edit form when editing (Claimant, Provider reference, Registration, Make, Model, Accident circumstances); "Case overview" panel: Work facts (Case type, Provider reference, Inspection, Engineer, Received, Due) · Parties (Principal, Claimant, Repairer/holder, Intermediary, Image source, Origin) · accident card (circumstances, Incident detail, Vehicle).
- Vehicle: Registration, Make, Model, Year, Mileage, Mileage source; "Vehicle checks" panel: Refresh DVLA / Refresh DVSA/MOT (same lookup) / Run Experian check (disabled seam, ENG-001), state list, "Vehicle History" textarea (= `narrative.history_check`).
- Valuations: table (Source, Date, Time, Mileage, Retail value, Trade value, Edit) + Add valuation; dialog Source (Glass's / Cazana / Engineer's Value), Date, Time, Mileage, Retail, Trade.
- Inspection address: Recorded value, Provider default, Previous values select; Edit → input + Cancel/Save.
- Case Files: Documents (Add evidence → /upload; rows name/type·size·source, custody chip, Preview, Save as | Open Operations), Vehicle images gallery (viewer dialog: Rotate view, Save as), Correspondence (Compose, Reply, Forward, Open Inbox; linked message rows).
- Notes: Add Case note / Record chase (editing only); entries with Date, Time, ID (staff username / SYSTEM / AI) + text, newest first (merges case notes, business events, chase outcomes, AI events).
Dialogs: reason (hold/release/close/reopen), Create upload request (Recipient, Reason; expiry/max files/max size read-only policy values → one-time secret toast), Record chase (Recipient, Channel, Prepared content, Disposition, Reason), Case note, finish-edit, stale-version conflict (current vs proposed), save-in-Review warning.

### 1.9 Assessment `/cases/{id}/assessment`
Access: With Engineer or onwards (D11); read-only once Complete. Header "Assessment"/"ref · reg"; 7-item identity ribbon (+Mileage, Vehicle); record bar: New estimate (dark), Import estimate, Glass's (disabled seam), Audatex (disabled seam), **Send to Claude** (primary); right: Generate report draft / Preview report draft. `assessment-v3`: collapsible Evidence rail (Instruction + images) | "Estimates" pane: estimate tabs (tablist) + editor (Delete estimate danger, Duplicate, Use estimate / Current chip, Save estimate dark; fields Estimate name, Source, Repair days, Labour rate, Paint labour rate, Paint materials, Other costs, VAT %; lines table Operation (Replace/Repair/R&I/Paint/Other), Description, Part number, Qty, Labour h, Paint h, Part £, remove; notes; totals Parts/Labour/Paint/Other/Subtotal/VAT/Total). Dialogs: Import estimate (name, source Audatex PDF/JSON/Other, file); Send to Claude (direction textarea, Target Estimate % slider of Engineer's Value, Case Valuation, Target amount; disabled without Engineer's Value); Delete estimate; Report draft preview; image viewer.

### 1.10 Upload `/upload`, public `/Uploads/{token}`
Upload: header only; dropzone ("Drag files here or choose files" · "EML, MSG, PDF, DOC, DOCX, JPG or PNG · up to 25 MB each · 10 files" · Choose files dark); file rows (status chip, progress, per-file outcome with Open X / Add to existing Case / Create Case / Cancel); Upload (primary) + Clear. Public: external shell, company logo, "Secure file request", h1 "Upload files for REF", request ref + expiry, dropzone, Submit files.

### 1.11 Operations `/operations`
Header "Operations"; partial-data notice; **AI Job List** panel (meta "n jobs", "Send Unidentified to AI" dark; table Job(kind+detail), Record, Started by, Created, State, Action: Review estimate/Open query/Review | Complete job | —); **Service health** table (Area, Service, State, Latest evidence, Dependency, Retry/View); **Attention required** (retryable external work: Case, Work, Item, Attempts, Failure, Retry this work); **Active upload links** (Case, Recipient, Last activity, Accepted, Expires, State, Withdraw link); **EVA handoffs** (Case, Route, Engineer, State, Result).

### 1.12 Administration `/admin[/{area}]`
Admin-layout: panel nav — **Staff accounts & roles, Principals, Workflow configuration, Mail settings, Automation & AI, Service health, Action Logs, Reports** | content panel (h2 area label, description, meta).
- Accounts: table Name, Username, Role (inline select), State, Save (disabled until changed; reason prompt), Account (Disable danger / Review); Create staff account.
- Principals: table Name, Principal Code, Roles, State, Settings; Create Principal; Settings dialog: route e-mail addresses (read-only), EVA API submission settings (the two independent toggles owned by FRD-07/ADR-0034: Manual API submission, Automatic API submission; ZIP export needs no setting), Pegasus API key (masked, Show/Hide), Generate new key (danger → reason), Save.
- Workflow configuration: Instruction completeness (2 checkboxes), Review (2 checkboxes), Due work (chase interval); Save configuration.
- Mail settings: Approved mailboxes table (Mailbox, Scope, Last update, State, Review folders/Refresh) + Mail categories table (+ Add category).
- Automation & AI: Automation panel (status, Registered clients, Active jobs, Failed jobs, Stop/Start automation danger → reason) + AI settings (Proposal, Timeout, enabled checkbox, Save).
- Service health: same table as Operations.
- Action Logs: filters (Search, Area, Actor, Result, From, To, sort toggle, Clear) + table Time, Actor, Area, Action, Reference, Result.
- Reports: From, To, Engineer; Generate / Preview / Export; "Engineer Report" table (Engineer, Queries received, Reports).

### 1.13 External frames
Sign in: dark external shell, auth card (company logo + "PEGASUS", h1 "Sign in to Pegasus", Username, Password, Sign in). Signed-out / access denied / error family keep the same card frame.

### 1.14 Removed
`/VehicleImages` list page (detail page stays as the image record); Organisations, Access review, Roles as separate admin areas (folded per §1.12); Automation Activity page (→ Action Logs); old Assessment section tabs; old Triage action bar; Additional case section.

### 1.15 Prototype defects (do not reproduce)
Undefined icons `activity/spark/reply/flag/sort` (use Lucide activity/sparkles/reply/flag/arrow-up-down); "Create organisation" → "Create Principal"; casing → "EVA"; Open Assessment on Review; Work Centre "Filter" no-op; unbounded Inbox "Next"; fixture-driven "Blocked" metric; unused `.work-today-summary/.prototype-note/.console-status/analyst/baseline/assessment-v2` rules.

## 2. Operator decisions (2026-08-28)

| # | Decision |
| --- | --- |
| D1 | Vehicle images: delete `/VehicleImages` list only; keep the detail page as the image record. |
| D2 | One "Principals" admin area; Organisation stays a backing Core entity created inline by "Create Principal". |
| D3 | Case states: display mapping only — ReportPreparation + PostReport → "With Engineer", PostReportComplete → "Complete"; Core enum untouched; other terminals "Closed · <outcome>" in Search, excluded from the Cases rail. |
| D4 | Outbound mail is built: staff Reply/Forward/Compose via Graph from an approved mailbox, retained as Sent evidence linked to the Case; Flag/Delete are Outlook mutations; gated, production activation approved separately. |
| D5 | AI jobs: Estimate, Unidentified resolution, Query response, Unidentified-queue pass; scheduled passes are created by external crons through the Automation Actor. |
| D6 | New scope `automation.jobs` with a consent line. |
| D7 | Uncomposed integrations (Experian, Glass's, Audatex, Cazana) render disabled as drawn; a disabled control is permitted only for a named, ticketed integration seam. |
| D8 | Principal "Pegasus API key" is the Provider API credential — delivered with the submission endpoint (TICK-058/061). |
| D9 | Free VAT % per estimate; the Current estimate's VAT % overrides the report's built-in rule. |
| D10 | "Report sent" is evidence-driven, never asserted: a report sent from Pegasus auto-links its Sent evidence; a report sent through EVA is detected (Case reference + PDF report mail), the PDF attached as the report document and the Sent evidence linked. Per protected operator-notes (~line 202) a linked Sent item enters **post-report work** (still displayed "With Engineer"); `Post-report complete` stays the separate reasoned closure via "Close Case". The Case action only confirms detected evidence. (Corrected 2026-08-28 by PLAT-047.) |
| D11 | Assessment opens for With Engineer or onwards, never Review; read-only once Complete. |
| D12 | Engineer Report "Queries received" = retained messages classified post-report-emails associated with the Engineer's cases in the period. |
| D13 | Vendor Inter Variable (+Italic) woff2 with OFL licence + SHA-256 in the design README. |
| D14 | Work Centre "Blocked" metric links to `/Cases?tab=unidentified`; Blocked intake rows are listed in that Exceptions tab uncounted, with their own `Blocked intake` chip; the Unidentified count (tab and rail sum) counts Unidentified items only — the two meanings stay distinct (recorded 2026-08-28, UIIMP-007 review). |

Routine calls: EVA note dropped; "No EVA" policy dropped; the dialog exposes the two ADR-0034 toggles rather than a three-value select (PLAT-047 review, 2026-08-28); route e-mail addresses read-only; account dialog "Session started" via an `auth_time` claim; wave-1 `site.css` carries a delimited legacy block for not-yet-ported page classes, deleted in wave 5; `main` is promoted only after wave 5.
