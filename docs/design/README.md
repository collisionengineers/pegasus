# Design authority

This file owns Pegasus visual presentation, assets, components and source-to-runtime
mappings. [FRD-12](../frd/frd-12-operator-experience.md), [FRD-15](../frd/frd-15-work-centre-queues-and-search.md), [FRD-16](../frd/frd-16-case-record-workspace.md) and [FRD-17](../frd/frd-17-administration-workspace.md) own functional interactions;
the [PRD](../prd/pegasus-product.md) owns scope. The [index](../index.md) routes
engineering, source architecture, operational observations and task workflow.
These are requirements, not a claim of deployment or operator acceptance.

## Evidence discipline

Use the accepted interface requirements and current FRD-12 interactions. One
Case record supports the refined Scroll/Tabs display choice. Native
engineering and reports remain independent of optional EVA. Prototype behavior
is reference evidence; it does not overrule current requirements.

Record the actual scope of rendered/operator evidence. A screenshot,
asset or planned interaction does not prove the implemented caller, deployment
or acceptance. Engineering defines those evidence tiers; do not repeat them here.

The 10 September pre-v1 correction uses the operator-supplied
Pegasus-v2-Refined-Pack as its visual reference: compact 13.5px body text,
36px controls, restrained surfaces, aligned bounded forms, and contextual
dialogs. The private prototype and its customer-derived fixtures must not be
published. Current operator requirements override conflicting prototype
behaviour: keep one Case Notes timeline, exactly one staff role, all engineering
powers for Administrators, and the agreed EVA retry behaviour. Prototype
handlers and illustrative figures are not application policy. Review actual
routed pages and their error/edit states against the reference at 1580×1000
and a smaller desktop width; shared-layout changes also need a 760px capture.
The presence of shared CSS or prototype screenshots is not visual acceptance.

## Product direction

The application is an operational, restrained, desktop-first internal
case-management tool for a small office of approximately eight users. It is
not a marketing site, document system, mobile product or general-purpose
command centre.

**The Integrated Operations Workspace was selected on 2026-08-28** as the
shell, route hierarchy and page family for the whole staff application. This
approves the contract transcribed in this file, not pixel-for-pixel
reproduction of the prototype and not any partial implementation.

The authenticated routes, in rail order, are:

1. Work Centre (`/`)
2. Inbox
3. Upload
4. Cases
5. Search
6. Operations
7. Administration, visible only to authorised Administrators
8. the account dialog and sign-out, from the rail foot

This order supersedes the 2026-08-04 order (`Dashboard → Inbox → Upload →
Queues → Cases → Administration`): the Dashboard becomes the Work Centre,
Queues becomes Cases, the former Cases search becomes Search, and Operations
returns as a routed workspace. `Triage`, `Unidentified`, `Audit`,
`Not ready`, `Review` and `Held` keep their settled meanings; Triage and
Unidentified are pre-Case records reached through the Cases rail, never Case
states.

The common hierarchy of every authenticated page is:

1. shell — rail, utility bar, working-set strip;
2. page header — eyebrow, title, freshness and the safe primary action;
3. operational panes, table, workbench or record;
4. named workflow, evidence, lease or exception state and consequential action;
5. provenance, external identity, permanent business history and limitations.

### Authenticated shell

The `.app-shell` grid is a 220px sticky `.app-rail` beside the `.app-column`.
The rail is the dark `--nav` gradient with a 3px `--red` top stripe. Its
content, top to bottom:

- **Brand** — the refined Pegasus mark (`images/pegasus-mark-refined-128.png`, no wordmark
  inside the image) at 52px beside "PEGASUS" and the line "Case management" (v28 P1).
- **Nav label "Work"**, then the links in order: Work Centre (`/`), Inbox
  [count], Upload, Cases (`/cases`) [count], Search (`/search`), Operations
  [count].
- **Nav label "Manage"**, then Administration — rendered for Administrators
  only, absent for everyone else.
- **Rail foot** — the health line (dot and "Current · HH:MM") and the user
  block (avatar initials, name, role, account-menu button).

The current route is signalled by a white background, a `--red` left border
and a red icon, with `aria-current="page"`; the border is the non-colour
cue. Below 980px the rail lies down into a horizontal bar, labels hide, and
the current-route border moves to the bottom edge, so the cue survives the
reflow; nothing is hidden at any width except the rail foot and nav labels,
whose functions move to the utility bar and account dialog.

**A rail count is a figure a page already queried**, never one the shell
invents. The Cases count is
`not_ready + review + with_engineer + query + held + triage + unidentified` (group
contract §1.1); the Inbox and Operations figures are composed by the wave-2
and wave-3 tickets that own those queries — the shell invents none. An absent
count renders nothing at all — a shell-level `0` would be exactly
the stale zero the operator-experience requirements forbid. Counts are
supplied by one page filter (`Presentation/RailCountsPageFilter.cs`), not by
each page.

The **account dialog** opens from the rail-foot button and shows Name, Role,
Session started (an `auth_time` claim) and Idle lock, with Close and Sign out.

**Rail collapse.** A Collapse control at the rail foot folds the rail to 64px:
icons and counts stay, labels become each link's `title`, and the control's
label flips between Collapse and Expand navigation. The choice is remembered
per browser in the `pegasus-rail` cookie (read by
`Presentation/ShellPreferences.cs`), so the collapsed rail is painted by the
server with no flash. Under 980px, where the rail lies down, the control is
hidden.

The **utility bar** is dark and sticky at the top of the column: freshness
text, the global search input with its "Ctrl K" hint (Enter or Ctrl K opens
the command palette dialog), the "New case" link to the real creation form
when intake is available, and the bell. Upload and Inbox retain their named
navigation destinations.

The **bell** is personal notifications and nothing else: never office-wide
work, attention rows or queue counts. Its `.bell-count` shows the signed-in
person's unread count and is absent at zero. The Notifications dialog lists
that person's notifications newest first — the Case reference with the
registration in mono, the cause in operator words ("Estimate draft ready",
"Assigned to you", "E-mail received", "Edited by …", "Query received"), when,
and an Unread chip until opened. Each row is a one-button form that marks it
read and opens its place (the Case, its Estimate or Notes section, the message
or the Unidentified item), working without script. **Mark all read** sits at the
head. With none the dialog says `No notifications`; a failed read says
`Notifications unavailable.` Notifications older than 30 days drop off.

The **working-set strip** sits under the utility bar and holds open records
only: Cases and the pre-Case records (Triage, Unidentified, image record, a
message) join it when opened and leave when closed. There is no Work Centre
tab and no "+ Open" tab; records open from Cases, Search, the Inbox or Ctrl K,
and with nothing open the strip is absent. The strip is fused with the record:
it sits on the page background over one hairline, and the active tab is white
with the red accent once and runs into the record card, which loses its top
edge. Each tab shows the kind's glyph, the reference in bold and the
registration in mono (a message shows its subject). State glyphs sit on the
tab: an amber dot for unsaved edits, a lightning glyph while a Glass's session
is open, a lock while a colleague holds the record. × shows on the active tab
and on hover, and middle-click closes; closing the current record lands on the
Work Centre. Six tabs are shown and the rest sit in an "N more" menu. The set
is kept per browser, at most twelve records, and another window's change
re-renders the strip.

A page that is a record announces itself: it sets
`ViewData["WorkingSetRecord"]` (`Presentation/WorkingSetRecord.cs`) and the
layout writes `main[data-record-href][data-record-kind][data-record-ref]
[data-record-reg][data-record-glyph]`, with no attribute on a page that is not
a record. A `pegasus:dirty` event marks the current tab as having unsaved
edits.

### Case record frame

The Case record (`/Cases/{id}`) has no page header. A sticky block sits under
the utility bar and working-set strip: the 56px **ribbon** — the reference as
the page's heading under "Case workspace · registration", Claimant, Principal
and Engineer; chips for state ("Held · review on 24 Sep"), Case type (Audit,
Inspection + Audit) and a colleague editing; links between an Audit Case and
its original; then Edit Case, or while editing the Editing badge, Cancel and
Save, and one **Actions** menu — and the 40px **section row** of section links,
Refresh and the Scroll/Tabs switch. Scroll is the default in every state; a
Tabs choice lasts for the browser session. The sections are Overview,
Inspection, Vehicle, Damage, Valuation, Estimate, Settlement, Report, Files and
Notes, each a foldable panel whose head carries its own Edit (entering the one
page-wide edit session), one availability label when the state does not allow
editing, and the fold chevron. The aside holds Figures and Next action and
folds into a two-up strip above the sections below 1441px.

The **Actions** menu holds exactly the progressions the state permits — Hand to
Engineer, Send to EVA, Mark report sent, Mark completed, Return to Review or
Engineer, Archive, Place on Hold or Release Hold, Correct
principal, Create audit — then, after a separator and in red, Close case.
Outside an edit session the menu appears only when Send to EVA is available.
Damage uses the **Plan** clicker only: a top-down silhouette drawn as the
panels, five graded severity fills with a legend, and numbered markers that
match the recorded-zones list. Images open in a full-screen **viewer** (title,
tag, position, Rotate, Zoom, Download, In report while editing, and a filmstrip
with excluded images greyed); crop happens on the viewer stage itself (drag,
handles, move, Aspect, Rotate left and right, Full frame, Reset, Save crop). A
crop is a stored rectangle: tiles and the report show the cropped region and
Download returns the original. Pre-Case records keep the simpler viewer with
Crop (Apply, Clear, Cancel) and the Tag select, and their tiles show the
cropped region the same way.

`main.app-main` holds `.content`, capped at 1580px and centred with the 18px
page padding, so a wide monitor shows equal margins either side rather than
every table pressed against the rail. Below the cap nothing moves. Every page
carries the skip link, the toast region and the dialog root.

`_LayoutAuth` remains the navless frame: sign-in, the signed-out
confirmation, access denied and the error family are not places in the
application (see
[Shell and routes](../frd/frd-12-operator-experience.md#shell-and-routes)).

### Keyboard and dialog contract

| Key | Action |
| --- | --- |
| Ctrl K | Command palette dialog (also Enter in the utility search) |
| Ctrl U | Upload (`/upload`) |
| Ctrl N | Create Case |
| Ctrl S | Save, only while a Case is in edit |
| F5 | Refresh — re-query the current page, never a browser reload |
| ArrowUp / ArrowDown | Move through a row list (`scope-list`, `row-button`, `work-item`, result rows) |
| Escape | Close the open dialog |

One accepted exception to keyboard parity: the Assessment whole-page raw
estimate drop is pointer-only, and it is a real gap — no staff keyboard
route performs this import. Every other action on the page stays
keyboard-reachable.

Dialogs are `.dialog` inside `.dialog-backdrop`, mounted in the dialog root:
initial focus on the first control, a focus trap, the rest of the document
`inert` while open, Escape closes where safe, and focus returns to the
invoking control. Toasts live in `.toast-region`, are announced restrainedly,
and never carry an action the page does not also offer.

### The Pegasus marks

**Commissioned by the operator; adopted 2026-08-17.** Ten purpose-drawn raster
marks live in `src/Pegasus.Web/wwwroot/images/marks/`. Four further marks —
`activity`, `brand`, `calendar` and `casefolder` — were supplied with the
design and are **not in the tree**: their bytes were never copied, so they have
no runtime mapping and no checksum row, and nothing may reference them until a
ticket puts them in the tree and records them below.

The marks are a second, deliberate class of imagery; the statement that no
imagery is needed for the internal application still holds for marketing
photography and for generated or substitute glyphs, neither of which these are.
They do not replace the Lucide sprite and do not compete with it:

- **A Lucide glyph names a thing inside a row** — an action, a state, a
  provenance word. It is 16px, inline, and one glyph means one thing everywhere.
- **A mark names a whole surface** — an administration area, an empty result,
  the product itself. It is 30–112px and sits beside text that already says
  the same thing.

Every mark is decorative: `aria-hidden`, empty `alt`, always beside text.
None is used for a semantic action or state.

Uses under the integrated design: the rail brand and the sign-in card
(`pegasus-lockup`); the Administration area panel heads (`accounts`,
`principals`, `configuration`, `mailboxes`, `automation`); the Cases rail
empty state (`checkmark`). `roles`, `access` and `organisations` lose their
surfaces when those areas fold into Staff accounts & roles and Principals
([Removed surfaces](../frd/frd-12-operator-experience.md)); their bytes stay registered and
their removal is proposed for the wave-5 removal ticket.

#### Pegasus marks source-to-runtime mapping

Upstream source: Claude Design project `710bb42f`, `assets/icons/` (1024×1024
RGBA PNGs). Runtime destination: `src/Pegasus.Web/wwwroot/images/marks/`
(128×128 Lanczos downscale, decorative `aria-hidden` with empty `alt`).

| Mark | Upstream source & SHA-256 | Runtime destination & SHA-256 | Mapping & usage |
| --- | --- | --- | --- |
| `pegasus-lockup.png` | `PegasusDesign/assets/icons/pegasus-lockup.png`<br>`C8F3551841AACA26AAE4F959B263DBB2409EB44A327207F8078D85A1F33668A7` | `src/Pegasus.Web/wwwroot/images/marks/pegasus-lockup.png`<br>`938C22B0F0FC621DC6FADD57748BA858CD1235292581AE47705A4ED336140EF0` | Retired by v28 P1: the rail and the sign-in card carry `images/pegasus-mark-refined-128.png` and `-256.png`. |
| `accounts.png` | `PegasusDesign/assets/icons/accounts.png`<br>`AFFA12B7C8609B253AAFB38304F503F83B868DD817902B53ADDFAE65A3E353A1` | `src/Pegasus.Web/wwwroot/images/marks/accounts.png`<br>`A8D467B827E0F19A6066640FA98A75D3673DA8A8C7642C4190D59BD5EDB718D5` | Administration → Staff accounts & roles. |
| `roles.png` | `PegasusDesign/assets/icons/roles.png`<br>`D3B970330A7DDFE1BE3BD92AF8C8B682B63E2270BF5537F3D5CE60EA6B0A97C0` | `src/Pegasus.Web/wwwroot/images/marks/roles.png`<br>`D942967041CFB7A7460015572B658AC483121272F7CFC0194F68A123B71BEBF0` | No integrated surface; proposed for the wave-5 removal ticket. |
| `access.png` | `PegasusDesign/assets/icons/access.png`<br>`371C4EF84A9E91F8E6509ACCFF045C68121147C22CDCD12D6D6509EF244CEC7F` | `src/Pegasus.Web/wwwroot/images/marks/access.png`<br>`70C98AE7591D467CA455BC481EA37963C67CBB1A8571A7EF823049054DB08C4D` | No integrated surface; proposed for the wave-5 removal ticket. |
| `organisations.png` | `PegasusDesign/assets/icons/organisations.png`<br>`ABAE832BE33CDEBFE1D80C8E47A1FFF4D1FEF644B02F2BD5D51FC9390C421204` | `src/Pegasus.Web/wwwroot/images/marks/organisations.png`<br>`804E77E33162BB09B0374058C6E6989B92A59224F813DDDA0BA6D410A69F6E8C` | No integrated surface; proposed for the wave-5 removal ticket. |
| `principals.png` | `PegasusDesign/assets/icons/principals.png`<br>`B85E82694474D92F3C15106699786B2081F8E2AFDE66D4A1A78E07071786C967` | `src/Pegasus.Web/wwwroot/images/marks/principals.png`<br>`879055AD9A973F05E2BE49F5EA00EDD43111D323BDC8C8952FCA727A7C9C0496` | Administration → Principals. |
| `configuration.png` | `PegasusDesign/assets/icons/configuration.png`<br>`B64DCBE7FD45B24A0D9BD687BF8E16BCB3E4E587ED16F93BF1BCE12370A6E921` | `src/Pegasus.Web/wwwroot/images/marks/configuration.png`<br>`86A311A3C1ACE78E5D5A407B289F901ED7C26860BCBBBDEF59EC93A71BAFA62E` | Administration → Workflow configuration. |
| `mailboxes.png` | `PegasusDesign/assets/icons/mailboxes.png`<br>`179A5677C4B73587601F0AF79162F87217C2035D096D90341281E23BFD87F688` | `src/Pegasus.Web/wwwroot/images/marks/mailboxes.png`<br>`1B727ACBE0DCC114370E0D620DCB74E20A12866C85187689ABDB8A249B61C019` | Administration → Mail settings. |
| `automation.png` | `PegasusDesign/assets/icons/automation.png`<br>`51F6970F9C0245E694D3562922A34AC5C3F2E762ACB5682FDF6DAA3FDFE10039` | `src/Pegasus.Web/wwwroot/images/marks/automation.png`<br>`1EABE2EF634065A1A76F78A6D520A366C49D469EBC3C92BA99F1DBA1A8F8B3FE` | Administration → Automation & AI. |
| `checkmark.png` | `PegasusDesign/assets/icons/checkmark.png`<br>`6ECC9917585A85D7B8C7EC62DB3C167689FD0F210D9838EC0B9959F1238471F3` | `src/Pegasus.Web/wwwroot/images/marks/checkmark.png`<br>`5531CC893A5C7A1137F049CF0D77A9D19B73EB30AC1036985A902FFC44A0C30F` | Cases rail empty state. |
| `activity`, `brand`, `calendar`, `casefolder` | Supplied with the design; not copied | **Not in the tree** — no destination, no checksum | Unplaced. A ticket that places one records both checksums here first. |

## Design principles

- Operational, restrained and border-led rather than decorative.
- Cool light-neutral ground, white panels, dark charcoal navigation and
  near-black text.
- Collision red is sparse: primary actions, the current route, visible focus
  and urgent emphasis.
- Product states are distinct: amber for incomplete/pending and the
  exceptions group, restrained navy for **Review**, blue for informational
  and in-progress external work, green for an operation or outcome that
  succeeded (Case created, Linked, Sent, Saved, Approved, Roadworthy), red for one that did not (Failed, Could not be read, Unavailable, Rejected, Unroadworthy), and red for
  danger and blocked, neutral for everything else.
- State is never conveyed by colour alone; every `.status` chip carries text.
- 3px corners, 1px hairline borders, `--shadow` only where the prototype
  raises a surface on hover or overlay, and a 12px gap rhythm.
- Inter Variable for all application text, Lucide line icons only.
- Controls communicate purpose without narrating obvious actions. Screens
  carry no lede or subtitle: eyebrow, one H1 and the content. Guidance appears
  only beside a control whose action has a consequence the operator must
  understand, and is one sentence.
- Do not expose Azure, OCR, AI mechanics, queue mechanics, extraction
  engines, deployment, adapter, lease/version, projection, ingress, or
  artifact terminology in operator copy. The word "intake" never appears in
  operator-facing text (operator decision 2026-08-04).
- Every state value shown to an operator passes through an explicit
  operator-label map — `Pegasus.Web.Presentation.OperatorLabels`. Raw
  `ToString()` of enums, snake_case event codes, GUIDs, hashes, storage paths,
  version integers and byte counts never reach markup. File sizes, where
  relevant, are megabytes to one decimal.
- Every date and time an operator reads renders Europe/London through that
  same map. `ToLocalTime()` is never correct: it resolves against the server
  clock, which is the office zone on a developer workstation and UTC on the
  deployed container.
- A composed query that returns zero renders `0`. A capability that is not
  composed in a deployment is absent from the interface — never an inert
  card or an "Unavailable" placeholder. The one permitted disabled control is
  a named, ticketed integration seam
  ([Absent versus disabled](README.md#absent-versus-disabled)). Genuine runtime
  failure renders the designed failure state with the last-good time.
- Every screen defines its empty, loading, and failure states in business
  language, and unknown-record URLs render the styled not-found screen, never
  a raw browser error.
- Screens are compact working surfaces: 40px rows, 12–16px panel padding,
  13–14px body text. A screen about a single record other than the Case
  record is one container — header, identity ribbon, action bar, sections as
  tabs — and the operator reaches its identity, its state, its available
  actions and its main content without scrolling.
- The Case record has Scroll and Tabs display modes using the same section
  hosts and one edit form. Scroll is the no-script fallback: its identity
  ribbon, action bar and section navigation are sticky, sections below the
  fold load lazily, and `?section=` reaches a section. Tabs hide inactive
  sections without removing their loaded fields or discarding unsaved edits.
  Retain the personal display preference and the single Case Notes timeline.
- Provenance is an icon with a one-word tooltip, shown on hover **and** on
  keyboard focus with a matching accessible name: Staff · Extracted · AI ·
  E-mail · Lookup · Principal · Automatic. Source labels, policy keys and
  provenance sentences do not appear in markup.
- A count query and a rendered time cannot be proved locally: an empty
  database returns the same zero as a correct query, and a Europe/London
  workstation clock matches the office by accident. Both need populated test
  data or the deployed instance.
- Every drawn control maps to a named handler or an approved disabled seam.
  An inert control is a defect.

Use the glossary's exact business terms and FRD-01's lifecycle labels.
Completed and Query are reversible work states; no formal Case is terminally
Closed. The single presentation map translates Core states to those labels;
no page defines its own map or generic Close action.

## Tokens

The token source is the prototype's `html[data-design="integrated"]` block,
verified value by value on 2026-08-28. The values below are the repository
authority; `site.css` declares them once on `:root` and no page declares a
literal colour, radius or size. Earlier token sets (the `collision-engineers-
design-dev` bundle, the 2026-07 warm palette) are superseded and not retained.

### Colour

| Token | Value | Role |
| --- | --- | --- |
| `--bg` | `#f1f3f4` | Page ground |
| `--surface` | `#fff` | Panels, rows, dialogs |
| `--surface-2` | `#f8f9f9` | Panel heads, fact cells, hover |
| `--surface-3` | `#e9edef` | Pressed, selected row, icon wells |
| `--ink` | `#202629` | Text |
| `--muted` | `#626b70` | Secondary text |
| `--quiet` | `#7d878c` | Tertiary text, eyebrows |
| `--line` | `#d2d8db` | Hairline borders |
| `--line-strong` | `#9da9ae` | Control borders |
| `--nav` | `#24282b` | Rail and utility bar |
| `--nav-2` | `#30363a` | Rail gradient end, dark buttons |
| `--nav-text` | `#f5f5f3` | Text on `--nav` |
| `--nav-muted` | `#bcc3c6` | Secondary text on `--nav` |
| `--red` | `#c9222b` | Primary action, current route, stripe |
| `--red-dark` | `#9e1720` | Pressed primary |
| `--amber` / `--amber-bg` | `#975b07` / `#fff4dc` | Incomplete, pending, Held, exceptions |
| `--navy` / `--navy-bg` | `#274f70` / `#eaf1f6` | Review |
| `--green` / `--green-bg` | `#2b643d` / `#e9f4ec` | Confirmed completion |
| `--blue` / `--blue-bg` | `#285f88` / `#eaf3f8` | Informational, With Engineer, external work in progress |
| `--danger` / `--danger-bg` | `#98272c` / `#fff0f1` | Danger actions, blocked, failed |
| `--focus` | `#d3232a` | Keyboard focus ring |
| `--shadow` | `0 8px 24px rgba(25,39,45,.09)` | Hover-raised cards, palette dropdown, toast |

Green must not represent progress, availability or a generic positive action;
it is reserved for confirmed completion. The prototype's `--polish-*`
properties are a layered overlay and their names are not carried: the
`--polish-shadow` and `--polish-shadow-raised` values are written into the
card rules that use them (resting and hover), and `--polish-red-soft` /
`--polish-blue-soft` are not adopted — the `--*-bg` tints are the only soft
fills.

State chips are `.status` with one tone modifier and always a text label:

| Modifier | Tone | Typical states |
| --- | --- | --- |
| `.status--amber` | `--amber` on `--amber-bg` | Not ready, Held, Unidentified, pending |
| `.status--navy` | `--navy` on `--navy-bg` | Review |
| `.status--blue` | `--blue` on `--blue-bg` | With Engineer, running, sent |
| `.status--green` | `--green` on `--green-bg` | Completed, confirmed |
| `.status--red` | `--danger` on `--danger-bg` | Failed, could not be read, correction required |
| `.status--neutral` | `--muted` on `--surface-3` | Closed, cancelled, unknown |

### Typography

Application text uses **Inter Variable** (upright and italic), vendored under
`src/Pegasus.Web/wwwroot/fonts/inter/` as woff2 with the SIL Open Font
License 1.1 text beside it. The face is self-hosted and declared with
`font-display: swap`; no external font stylesheet or CDN is referenced, and
the Content Security Policy permits fonts from `'self'` only.

```css
--font: "Inter Variable", Inter, ui-sans-serif, system-ui, -apple-system,
        "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
--mono: ui-monospace, SFMono-Regular, Consolas, "Liberation Mono", monospace;
```

The fallbacks after `"Segoe UI"` (Roboto, "Helvetica Neue", Arial) are
added beyond the prototype's stack so a workstation without the vendored
face degrades to a comparable sans-serif on every platform.

| File | Licence | SHA-256 |
| --- | --- | --- |
| `fonts/inter/InterVariable.woff2` | SIL OFL 1.1 (`fonts/inter/LICENSE.txt`, SHA-256 `262481E844521B326F5ECD053E59B98C8B2DA78C8EE1BDBB6E8174305E54935A`) | `693B77D4F32EE9B8BFC995589B5FAD5E99ADF2832738661F5402F9978429A8E3` |
| `fonts/inter/InterVariable-Italic.woff2` | SIL OFL 1.1 | `E564F652916DB6C139570FEFB9524A77C4D48F30C92928DE9DB19B6B5C7A262A` |

Rules:

- Body text is 13–14px; page titles 20px; eyebrows 10–11px uppercase with
  tracking; metric values and Case references may use weight 700+.
- Use semantic heading hierarchy: one H1 per page in `.page-title`.
- `--mono` is for references, registrations and technical handles only.
- Tw Cen MT and Futura are marketing, logo and document faces, not
  application fonts. No brand-font bundle is copied.

### Shape, borders and focus

| Token | Value |
| --- | --- |
| `--radius` | `3px` — controls, chips, rows |
| `--radius-lg` | `4px` — panels, dialogs, panes |
| Borders | `1px solid var(--line)`; controls `var(--line-strong)` |
| Keyboard focus ring | `3px solid var(--focus)`, `outline-offset: 2px` (the prototype base rule); the utility search takes `--navy` on its dark ground |
| Selection outline | Selected rows and tabs (`row-button`, `work-item`, `tab`) take `2px solid var(--navy)` |
| Depth | Border-first. `--shadow` is applied where the prototype applies it: `gallery-item` and `admin-card` hover, the command-palette dropdown and the toast; dialogs and the sign-in card carry their own deeper raised shadow; selected rows and metrics use an inset red bar, not a shadow |

There is no second radius pair. The 2px geometry of the previous design is
superseded.

### Spacing, layout and breakpoints

| Token | Value |
| --- | --- |
| `--rail` | `220px` |
| `--content-max` | `1580px` |
| `--gap` | `12px` |
| `--page-pad` | `18px` |
| `--row` | `40px` |

Spacing steps are 4, 8, 12, 18 and 24px. Panels and panes are `--gap` apart;
panel bodies pad 12–16px; page padding is `--page-pad`.

Layouts are desktop-first and reflow at these widths, each named by the
prototype's effective media queries:

| Max width | Reflow |
| --- | --- |
| 1360px | Work Centre panes narrow; `case-context` hides (`display: none`, as the prototype does); admin grids drop to two columns |
| 1180px | `queue-layout` rail narrows to 170px; the advanced search grid drops to three columns; `checks-grid` two columns; `case-overview-grid` stacks |
| 1100px | `pane-layout--3` drops its first pane; metric strips to three columns; the identity ribbon to three columns |
| 980px | The rail lies down into a horizontal bar; `admin-nav` becomes a horizontal scroller; `--content-max` is released |
| 900px | Workflow stepper stacks; estimate form two columns; `checks-grid` one column |
| 760px | Single column everywhere: panes stack with a top border, ribbons and fact grids one column, page actions full width, `--page-pad` 12px, dialogs pad 10px |

Mobile staff UI is **Not planned**. The reflow keeps a constrained desktop or
200% zoom usable; it does not create a mobile product. Reduced motion removes
every transition; forced colours outline the current route, selected tab and
primary/dark buttons in `CanvasText`/`ButtonText`.

### Motion

There is no product-wide motion system and no approved duration or easing
tokens. Hover and focus state transitions are 140ms (the prototype's
`.13–.14s ease`); dialog and toast
entrance is a single opacity/translate step; both are removed under reduced
motion. Marketing scroll reveals, staggered entrances, hover scaling and CTA
lift are excluded. Do not invent duration or easing tokens during
implementation.

## Assets

### Logo

The approved master is:

```text
docs/design/brand/logos/logo_no_margin.png
```

It is the red gear-C Collision Engineers lockup, copied exactly from
`assets/logo_no_margin.png` in the provided `collision-engineers-design-dev`
source bundle.

```text
SHA-256: E7247BE45911C46905343473E4C57B9F6ED7A450563D19C508C2D9652C2C63E2
```

Current consumers:

- embedded by `src/Pegasus.Infrastructure` for the integrated report renderer;
- the refined marks are copied byte-for-byte to the Web runtime and embedded by
  `src/Pegasus.Web/Pages/Shared/_LayoutAuth.cshtml` as the `auth-brand` of the
  sign-in card and by `_Layout.cshtml` in the authenticated rail
  (`pegasus-mark-refined-256.png` at 96px, `pegasus-mark-refined-128.png` at
  52px); the primary logo is not a Web shell consumer.

Rules:

- Never redraw the gear.
- Never extract it from a screenshot.
- Never recolour the master or invent another mark.
- Copy or optimise it for a runtime only through a reviewed source-to-runtime
  mapping with checksum proof.
- No ungoverned logo variant exists.

The upstream source directory may be absent from a clean checkout. The
checksum-pinned repository copy is the durable source.

#### Logo source-to-runtime mapping

| Asset | Upstream source & SHA-256 | Web runtime destination & SHA-256 | Mapping & usage |
| --- | --- | --- | --- |
| Primary logo | `docs/design/brand/logos/logo_no_margin.png`<br>`E7247BE45911C46905343473E4C57B9F6ED7A450563D19C508C2D9652C2C63E2` | `src/Pegasus.Web/wwwroot/images/logo_no_margin.png`<br>`E7247BE45911C46905343473E4C57B9F6ED7A450563D19C508C2D9652C2C63E2` | Byte-for-byte governed copy retained for the renderer boundary; it is not embedded by the Web shell. |
| Refined rail mark (128px) | `docs/design/brand/logos/pegasus-mark-refined-128.png`<br>`1D6758A5F9D90EA4539DBB498BF0171A93C23B85E9C3868BDDC7D6ABC724A1CB` | `src/Pegasus.Web/wwwroot/images/pegasus-mark-refined-128.png`<br>`1D6758A5F9D90EA4539DBB498BF0171A93C23B85E9C3868BDDC7D6ABC724A1CB` | Byte-for-byte copy embedded by `_Layout.cshtml` in the authenticated rail. |
| Refined auth mark (256px) | `docs/design/brand/logos/pegasus-mark-refined-256.png`<br>`E3C9712DE05E18D93BD21857F3961A8832941D91D942FA1E34F513944B545E1D` | `src/Pegasus.Web/wwwroot/images/pegasus-mark-refined-256.png`<br>`E3C9712DE05E18D93BD21857F3961A8832941D91D942FA1E34F513944B545E1D` | Byte-for-byte copy embedded by `_LayoutAuth.cshtml` as the `auth-brand` of the sign-in card. |

### Icons

Lucide is the only approved Web/UI icon system:

- 24×24 viewBox;
- 2px stroke;
- round caps and joins;
- rendered at 16–24px;
- `currentColor`.

Do not use emoji, Unicode dingbats, hand-drawn icons or infrastructure
symbols. The prototype's hand-drawn `iconPaths` are not adopted; every glyph
it names maps to the Lucide id below, and the prototype's inline sparkle on
Use the provider-neutral label `Send to AI`.

The checksummed sprite is `src/Pegasus.Web/wwwroot/images/lucide-sprite.svg`,
inlined once per page by `src/Pegasus.Web/Pages/Shared/_LucideSprite.cshtml`
as `<symbol viewBox="0 0 24 24">` elements; pages reference glyphs as
`<svg class="icon"><use href="#icon-…"/></svg>`. The `.icon` rule applies the
stroke and caps because a `<use>` clone does not inherit them. The sprite
holds the sixty glyphs below (the original seventeen plus the
forty-three later added from Lucide v0.344.0); each glyph checksum is the
SHA-256 of its `<g id="icon-…">…</g>` element in the sprite.

An icon paired with a visible text label is decorative and carries
`aria-hidden="true"`. An icon that is the whole control carries
`aria-hidden="true"` on the glyph and its accessible name on the button.
`src/Pegasus.Web/wwwroot/favicon.ico` has unrecorded provenance and is not
icon-system authority.

#### Lucide icons source-to-runtime mapping

Upstream source: Lucide official SVG vectors release (v0.344.0). Runtime
sprite: `src/Pegasus.Web/wwwroot/images/lucide-sprite.svg` (SHA-256 of the
committed LF blob `90FEB7AB7E40931DDE9B011CEC06F4E8B4DCD058695DEC09DB5E0965AC7A0992`,
sixty glyphs; the earlier seventeen-glyph sprite was
`24360787DB7A58F1B0ACA7E2F66405749C9D5742A2ADA91C07BDFF03202872D0`).

| Prototype name | Lucide id | Glyph SHA-256 | Usage |
| --- | --- | --- | --- |
| `dashboard` | `layout-dashboard` | `F8A9AFA8D2245E34D3DAEB88C9FF80A2AA546D1F8671212896E743E596F3752B` | Rail: Work Centre |
| `inbox` | `inbox` | `0817485BFAE1A740458AA3FC1E6E4542047FA890C547D35B17C771E6D352E901` | Rail: Inbox; Inbox scopes |
| `upload` | `upload` | `EE63E95EFECDAF141338475D367A54EF891E337491993DCDC1F3ED7936A42660` | Rail: Upload; dropzone |
| `queues` | `list` | `E7AF143D4992901731088F11F4AFDC0342361D5B85DB3841D252A9DCA5D97E45` | Rail: Cases (as the prototype draws it); Cases rail groups |
| `cases` | `folder-open` | `11EDC315700BAA321B840623A707A8571C28D511815EEB505516EAC795194BB9` | Rail: Search (as the prototype draws it); Case tabs |
| `image` | `image` | `309035AB9321F61F17336BD1B23E869BDE47EA07BA16CF72BE38762EF8922067` | Image record; gallery; image-initiated rows |
| `operations` | `loader` | `D606C955171E2BF83DA877BBC155127B0AB899007A3A16D0B90EE3C00C1926EF` | Rail: Operations; running jobs |
| `admin` | `layout-grid` | `DCF60CA3B7FC36D4C69ED1A6EBC4CAD464CE850786B7AEE5BD1AB0A542C0D0BE` | Rail: Administration |
| `search` | `search` | `832472670DB14C3420D64D80271A04FE90AE32D47F4834F4E70E9A8E2678EE7E` | Utility search; palette; Search buttons |
| `plus` | `plus` | `A1190965745A47ED26827784BBAE8B9291D5170501A02FB335D82247EA276108` | Add, Create, New estimate |
| `refresh` | `refresh-cw` | `C795E4B7F739E9CF2D5C5996CBDF8A0541734F0DC99EBE169BAE945FD04E2AA2` | Refresh, DVLA/DVSA refresh |
| `chevron-right` | `chevron-right` | `07C6F850908E2A9ABA2AD8B7B91AA8E525D463398D479DAD5EF10CB534FE3710` | Row affordance, stepper |
| `chevron-left` | `chevron-left` | `1E4CC2B6933AEDC73D77B080ABC988D9A4ED319191AC4AB2F0BD417C8E91BCE1` | Back, pagination |
| `chevron-down` | `chevron-down` | `07FA08D36ABFC560E7901833347764591406C71C2E8974BAF3EE518866D549C3` | Disclosure, select |
| `arrow` | `arrow-right` | `D8B246C7FDBAB41053F2016892C0664BB64C0C6D1ED4594C9D80470C1B219C70` | Open full record, transitions |
| `user` | `user` | `F12759D8CA6B092DCA70B2E265F4CD8921C6DC61B408C9DA3FFFC8650BE76AA2` | Rail user, account dialog |
| `more` | `more-horizontal` | `2124DA66776313BB29ED93D2CC06BBF1307EF8C8DBE672B3EE3AF4975F5E56D1` | Overflow menus |
| `clock` | `clock` | `EE847E37391A579398EA5CB111A4893642085DEA959EF3812F210ED69EABC5C6` | Freshness, due |
| `alert` | `alert-circle` | `69DA72930B08F89FA5C1AFDA3D5813BFAFA124D3E86F66B2100300F2B7DEB415` | Error summary, blocked |
| `warning` | `alert-triangle` | `40DEB35C6E3562DB12C1962989A7D9E24C758489247929C156DEDD8476DBE233` | Warning notice, exceptions group |
| `check` | `check` | `DE2A367F6B80B94E85E56CF01EFB198FB835039FEC3C0B4E643EAE54E9C857C6` | Save, confirm, checks |
| `check-circle` | `check-circle` | `CB9B89AA467B527393B51229F14E0314DB15D75792D2071C5FE599AB595C7678` | Confirmed completion |
| `file` | `file` | `1A3C36C8758354AA3FBE172B2F9AA864C898B425AEF310970A2A30C706899C4A` | Document rows |
| `mail` | `mail` | `1F2FF3622BA89D178DAC9BB0FBADE33862AF73E2AA627D7B8B682D8FA23B9C81` | Mail rows, Correspondence |
| `link` | `link` | `6D49DABEC5033468AD49114CA79422BC8CB2F1C1AE3696A34FD98EA5B1E93A1E` | Case link |
| `edit` | `pencil` | `63FD491D3A18940F7DDDD179F617729039871DBB01E42FCF6CEF81D6315A6C39` | Edit Case, inline edit |
| `save` | `save` | `BEEDEA57260C30DD0B222C239B536C1EFD8B8DD7BDD3BB5311CFB5DD325677A3` | Save |
| `close` | `x` | `FE4FAFDA78C537A7218FC7EDE65B1A01BE9BD5DF253C75805309D1EA2E6152DC` | Dialog close, tab close, remove |
| `hold` | `pause` | `1A4247062E4B9E29A38E9C6C0867F6D85351F8B058606EA1FC89768495734773` | Place on Hold, Held |
| `play` | `play` | `197D4369B982F7DBA348DCD5BD06F4C003A2C345C40CE2AC44719238463844D5` | Release Hold, Start automation |
| `report`, `document` | `file-text` | `A6AF7723E87920CF322C8C39F0A1080075BFA19B3E966A8E21D2D81A93772936` | Report, instruction evidence |
| `settings` | `settings` | `F6CE9F023EC1C2720723672887014349E1D3A68DF9555CA3795C5FEF95265B4A` | Principal settings, configuration |
| `filter` | `filter` | `C4319C676F5B160213319934EB2DEC6F60DD6F73C344C0D6C84AE1699430D45C` | Filter bars |
| `lock` | `lock` | `1F0A0861A3752428E1D5CABDAC22608E645A008229EF58415EC0C0E112F5BF2D` | Edit lease, idle lock |
| `external` | `external-link` | `27EB1A4F2FC62CA8E0B422442016854B17A5178EC7EE0DE23EB894BD5E5C5DF3` | Open Operations, retained source |
| `eye` | `eye` | `73D6B13F2AE0E9AA498E371618CF4CC6392C75F1A4C23FCDF1B981451368EF7A` | Preview, Show key |
| `bell` | `bell` | `5B315496E663ECA0E7465EDAD43FCD54BF00577C1737A5E8F9CC5352D185E79F` | Utility bar notifications |
| `signout` | `log-out` | `20B23EB0AF17FE443827B2E64EC23057092180CDE64B3FAC5F2A9DC210A70880` | Sign out |
| `calendar` | `calendar` | `9164C7178F10683EF0FB999F773149CD7AF5964875E6E896C6826F5A8988C67F` | Date filters, due |
| `history` | `history` | `ECC48B15E6A405F12C901A460C5D9745A09C84439AA1359EA3F846B8C28EF802` | Timeline, History panel |
| `copy` | `copy` | `10CBC775CD0ACEBBB15F863348821192DBD4A2858380CC295BEB020AB4144DCB` | Copy reference |
| `download` | `download` | `C5BB0DCFCE72DDFCD8BAC34C368CDE4E2013FF05C175318324D40776DF0C457C` | Save as, Download ZIP in the Send to EVA dialog |
| `folder` | `folder` | `6E9E30D6DB22DC0118AC8C8466659342AFAE90784EFD65B5E2929BE1BA7B0C16` | Folder scopes, Case Files |
| `info` | `info` | `9B266C26D53D1F6661CD45D11E5138FE00AF4289EA4EC8D4C320D41AB272CC3F` | Provenance, informational notice |
| `car` | `car` | `36AE3DC22866D02D1159AB8D6256BB09E91B2D98C03BC7126EE576437BECF0C5` | Vehicle section |
| `person` | `user` | (as `user`) | Claimant, parties |
| `task` | `check-square` | `D84CA64CC54CFF1C150D4D31618203F054470D80DFD59989B3EE52009574CE31` | Work items, checks |
| `archive` | `archive` | `37BA14C8285BE494749A4DA9E213B37048ABCEF5DB0D65B1C65DE959A135AD84` | Archive, Deleted Items |
| `send` | `send` | `63B04BD6FA6A68DEC5F9492B1D0926D00EE28C3F1332E10F47565AF49FB4649D` | Send, Send to EVA, **Send to AI** (the prototype's `btn(…,'primary','send')`) |
| `paperclip` | `paperclip` | `65E2F64F2264077A89E3D0DB428C3DF5E3C175BAB1A1C05561209B023EC2CED8` | Attachments |
| `home` | `home` | `7ABDB2720CEBD3A9AFBFAC581DCC7807C6E7A8E3229621EC5AA5B9583B060BDF` | Work Centre tab |
| `map` | `map-pin` | `93DF1DF4794C821825D59FC9550292FAB3504802B3EC767B9246E057CE499F29` | Inspection address |
| `key` | `key` | `9C4745F5604E0E32D887381FF5AE40DDA8BF4DF163D98025823CBBC0A06391C4` | API key |
| `shield` | `shield` | `456B29F0717F73785AE1CA5A492EF0B21693BDA13045B509E845BA38F08717AE` | Roles, access |
| — | `trash-2` | `2D59EB8F9393ABDFEE674BFC1A67A3ABD81146C1525F12DF7E753ACB40CB0773` | Delete estimate, remove line, Delete message |
| — | `rotate-cw` | `5DE57E248094872B06E8408E710E05E1D89BDEB2243DDF780254C8632FC6DDFB` | Rotate view |
| `activity` (undefined in the prototype) | `activity` | `8E33259DA8A236EBC5D6C96F27DFAB90CE1F69D78F9D935FA28A143443F2380B` | Service health, presence |
| `spark` (undefined) | `sparkles` | `D412CDDF7D44B1EED79ACB99F7D64A85E99BB77E9780FE49770883301EE63652` | Automation & AI nav entry, AI job rows |
| `reply` (undefined) | `reply` | `60A232864F635C41D9D82E6FDDB744EB8ABC8A1CAF369B1772B7F0CAF8C6D3FA` | Reply |
| `flag` (undefined) | `flag` | `A55F63EE07DFA4078A73AC54401544201065765B3DDB64C23B39CAC355A8AAE9` | Flag message |
| `sort` (undefined) | `arrow-up-down` | `9F9C9571C4A30B5642E7D6BBA19E58C836CC57F8ECDC5D044EB0819065C534BC` | Sort toggle |

The v26 shell and Case record glyphs below are inlined only by
`_LucideSprite.cshtml`, which now carries ninety-six symbols: the sixty above
plus these thirty-six. Each checksum is the SHA-256 of the glyph's UTF-8
`<symbol id="icon-…">…</symbol>` element in that partial. "No caller" marks a
glyph the v26 mockups reference that no current page draws.

| Prototype name | Lucide id | Glyph SHA-256 | Usage |
| --- | --- | --- | --- |
| — | `list-checks` | `3F03C3F128C5C21F348860C63561EB430BC7F3FD152F07B5499C4FADE643E13A` | Rail: Cases |
| — | `chevron-up` | `6176204E2A795D8FDC0E61565A475096A4F9FA09CF1CEBB091A57254D2034845` | No caller |
| — | `arrow-left` | `680C4FAE5D68949E00882E6C8A20D7AE0B201ADFFEC508E870E1F7FA02A9C2F3` | No caller |
| — | `arrow-up` | `F434D050FA5820052226CC0B907F1C949DE8163EDCCB3D2C2DEBB1397A11AC22` | No caller |
| — | `arrow-down` | `B9674C6E5CC4B78425D60908D272290AE7BC799A61A8B8842525A7ABECD5AA87` | No caller |
| — | `users` | `EEE334506429CAE176E7E392A686D8C504C856F11349B2D47D0450D772200FB6` | No caller |
| — | `images` | `3736526CF1887288E9D504986D739ADFE2BF83FA0A5411F37529ECD5E87F0285` | Cases Awaiting instruction; Search vehicle images |
| — | `forward` | `966C6DDDFF8EDD62B3F529C90209A061685034199BB8670F1FF6A0281979AD26` | Forward message |
| — | `unlink` | `FA6C00D4700751303B08AF8A35E90B3CE581A77DA80F428F585130BACB39DAE4` | Unlink a message from its Case |
| — | `file-output` | `C109CACE75A7483445BF22061B3141C5210AB2706012502180288644E3917F62` | No caller |
| — | `eye-off` | `56982F58FC81CBE4EB066E455259234DE2F2FE4B120631693BA0FA5544EB516F` | No caller |
| — | `key-round` | `5E7E3CD234E740B048195FBEF0B1C66E7CA85500AE1C7ACA7F35117496576E83` | No caller |
| — | `rotate-ccw` | `B734EDCBA8DF037C834EAB8A45DC181625008FEEBD20C4109D8F2486A33C27BE` | No caller |
| — | `crop` | `D6D10FFBF5D570C7F3CE47D5BA7C34C1ADA38EABD5C18F26164BB1A64A16A109` | Viewer Crop (Case record and pre-Case records) |
| — | `zap` | `998D4DC807CBE0CE4AF837D6282BCB925F8B169A1CE6DF4B1A2B06D6210474A0` | Working-set tab: Glass's session open |
| — | `grip-vertical` | `93D0BBD15AB6203E42D45A911F90B3BAB3D287E56AD4140804311B15FA062074` | No caller |
| — | `shield-check` | `1A0678C6E00913D6FFAB22299D518EC906BA4FB8AE3F17E8C5801C212FE21DBA` | No caller |
| — | `building` | `F8C777CE38931ABE01FAE9E46B1DC5527989F3152D9730EA941C7DA9D4DC9EFE` | No caller |
| — | `sliders` | `BC19EF5E6751EAE7634C7CA956BB16A0C0A6AB9ECCB8935811B63849FF7D9BFF` | No caller |
| — | `clipboard-list` | `F3B645C69B9060E6FA73E840EF5C864A2E4E4AB24750EB32A045D7DDBD4421C6` | Working-set tab: Triage; Open the Triage |
| — | `zoom-in` | `F32744E452483FCC60A24618A5C05630138A6C07806F647D510CE82726F0E8B2` | Case viewer Zoom |
| — | `corner-up-left` | `B7FDDB91FDBC7FDF2A1BFB36864024219751277B9C9AD0F4630306925778E08F` | No caller |
| — | `bar-chart` | `DB920FF9B7CE38B0D2703AE4B696B0682A3DCFE46B9BADE4260DFCF685BABB42` | Administration nav: Reports |
| — | `bot` | `91A0B1650FB3AE4CAC2615A264F8F023989CEFB119F21012937B322B5F941CCC` | Administration nav: Automation & AI |
| — | `circle` | `AB7E0A3948E0C6B703E06CAA4239196D6D9CFF245DBCC0C1E3C1BCD711EE19A8` | No caller |
| — | `circle-dot` | `D79F6EC939AEE821B5233C5346D93E70C02C61B8753C54F4469BFEB3712AD5C0` | No caller |
| — | `circle-slash` | `3183172285B4647F19E3945F408C9833806124D621159E99257BA3360501BA91` | No caller |
| — | `file-spreadsheet` | `16CF1ECDEF4DF9F53F2B22B78B633FBB9754BEC4ACF9D3BB9FEAEF3B0EB00DD9` | No caller |
| — | `heart-pulse` | `995A8F1F889A4EC78A7CD23E35DC50DAA9B7983AD78FDAA370D70F6608EC2718` | Administration nav: Service health |
| — | `move` | `DD97195C8B941325142543232DF562FFBD97461C7BCCAC06D21D2E971B880D88` | Case viewer crop: move handle |
| — | `phone` | `D3B43D179F6118EDF825342F9ED68F0AAF9840EA2DC63A13E72BE1B12567909F` | No caller |
| — | `pound-sterling` | `72454D0BBC72046B76787C89101DC263E6060D4F4AF8B2DDD47EF2BB9976171A` | Administration nav: Valuation presets |
| — | `scroll-text` | `9AD7578BF07319745EE8D157FDE09551A4AD21445317910C61AA569A2BB550F5` | Administration nav and hub: Logs |
| — | `square` | `7FA36224EAE826CF7CE1320F27BB389EF13B3FA5D359948A7A78720FEF80E8ED` | Case record: an unticked item |
| — | `square-check` | `05FC6728D20C44ACD31C6226C6B99396AD5B490B89460D8E018CC004655A7F41` | No caller |
| — | `undo` | `EE6DD129D3AD4ADD8FE1C87A3CDC8DE2E28B129C006C3F2AE52C5EB0C5FC2794` | Inbox Restore; Unidentified Reopen |

The v26 rail no longer draws the prototype's rail glyphs for three routes:
Cases uses `list-checks`, Operations `activity` and Administration `settings`
(`list`, `loader` and `layout-grid` remain in the sprite for their other
uses). A working-set tab draws its kind's glyph: `folder` (Case),
`clipboard-list` (Triage), `alert-circle` (Unidentified), `image` (image
record) and `mail` (message); `home` no longer has a Work Centre tab.

### Imagery and evidence

Upstream marketing photography is excluded, and no generated or substitute
glyph is used anywhere. The one class of imagery the internal Web application
carries is the [commissioned Pegasus marks](README.md#the-pegasus-marks): decorative,
naming a surface, always beside text that says the same thing.

Genuine case images, emails and documents are operational evidence, not
decorative assets. Use only authorised repository-provided evidence through
its owning workflow. Never generate placeholder cases, damage images, emails,
documents or people. The prototype's fixture data is not domain data and is
never copied, except the Case Workspace v2 fixture set
([engineering](../engineering.md#case-workspace-v2-fixture-values)).

### Web and renderer boundary

| Asset class | Approved consumer and boundary |
| --- | --- |
| Master logo | Embedded by the Infrastructure report adapter and copied byte-for-byte to Web for the sign-in frame |
| Report templates and document stylesheet | Embedded by `src/Pegasus.Infrastructure`; not Web shell assets |
| Supplied engineer signatures | The report snapshot carries the Case Sign-off Engineer account's printed name, optional qualifications and supplied signature image bytes/media type. No signature is embedded as an application resource. Supplied signature assets remain governed and are never Web decorative imagery. |
| Retired renderer workspace, prompt, model, skill and AI material | Historical source evidence only; not a separate runtime or policy owner |

The imported renderer can exercise its own assets without proving the planned
Pegasus report capability. Imported workspace material does not become UI,
report or design authority by existing in the repository. See the
[workspace boundary](../../workspaces/README.md).

## Voice, labels and necessary copy

Use concise, settled Collision Engineers language. Guidance is appropriate only when an operator must understand a consequence.

Approved necessary copy includes:

> Closed — a reason is required.

> No case or reference was created; review the missing or conflicting evidence.

> Created in error cannot be reopened. Create and link the replacement case.

> Unlinking this email cancels case <reference>.

> Error. Contact an administrator.

The last is the operator's own wording (18 September 2026) for Get valuation
on a source that answered with nothing. Received-mail chasing categories read
"Update Request", also the operator's wording; "provider" never appears in
operator copy (Principal is the word), except the Provider API's own name.

Permanent consequences must be visible without hover or colour alone. Illustrative text must not fabricate operational input.

These words are banned from operator-facing copy in
`src/Pegasus.Web/Pages/**/*.cshtml` and PageModel label maps, and a change
introducing one does not merge: `intake`, `bounded`, `projection`, `lease`,
`opaque`, `ingress`, `composed`, `artifact`, `durable`, `aggregate`,
`caller`, `correlation identifier`, `bytes`. This is a review rule, not an
automated check — nothing in CI enforces it today, and claiming otherwise
would be the kind of false assurance the evidence discipline above exists to
prevent. The words remain valid as internal code identifiers; the ban is on
what an operator reads.

## No explanatory copy and page economy

Operator direction, 2026-08-20: stop explaining pages. These are review rules
with the same force as the banned-words list above — a change violating one
does not merge.

- **A field is a label and a control, nothing more.** No hint sentence under a
  field, no "Required." or "Optional." text, no format guidance, no
  restatement of what the label already says. Required state is shown
  visually (the `required`-marker styling on the label plus `aria-required`),
  never as prose.
- **No how-it-works copy.** A page never describes its own mechanics,
  workings, derivations, or what will happen when a button is pressed. No
  worked-example tables, no "how this figure is calculated" prose, no
  introductory sentences under headings. The only exception is an individually
  approved consequence sentence from the closed necessary-copy list above.
- **Only populated, relevant sections render.** In read-only view, a section
  with nothing recorded and no available action is absent — not an
  empty-state panel. Edit-only sections render only in edit context. A long
  page of empty panels is a defect, not a layout choice.
- **Filters are dropdowns; tables sort newest first.** Table filtering uses
  labelled `select` controls (auto-submit with a no-script fallback), not
  rows of pill tabs. Tables default to newest first, and column headers are
  sort links that toggle direction server-side.



## Absent versus disabled

A capability that is not composed in this deployment is **absent** from the
interface. A control whose record does not yet satisfy a condition is present,
disabled, and states the condition on the control ("Available in Review").

**Amended 2026-08-28.** A disabled control for an *uncomposed
integration* is permitted only for a named integration seam drawn in the
approved design:

| Seam | Control |
| --- | --- |
| Experian | Vehicle checks → Run Experian check |
| Cazana | Valuation source |

**Narrowed 2026-09-01.** An excluded capability is absent, never drawn as
a disabled control. The direct Audatex service-launch control is removed on
that rule. By the operator's 15 September 2026 instruction the
Valuation section has one route to a guide card: while editing, Glass's,
Brego and Super CAP are each one card with editable month, mileage, retail
and trade boxes, a Get valuation button that looks the figures up and fills
the boxes (answering with a notice while that source has no connected
provider), and Save, which records the card; the boxes are typed by hand
just as well. There is no separate Add valuation dialog. This does not
remove the Estimate section's selected configured-Engineer Glass's
repair-estimate launch; a connected Glass's valuation provider
supersedes the earlier rule that Pegasus "records no Glass's valuation". Glass's and Audatex file
import stays in scope through the whole-page drop; Cazana remains the
disabled seam.

Every other uncomposed capability stays absent: no inert card, no
"Unavailable" placeholder, no unlinked route. A disabled seam carries its
accessible name, `aria-disabled`, and no handler; the ticket that composes
the integration enables it.



## Component map

The class vocabulary is the one `site.css` declares under the integrated
design. Every page composes these classes; a page-specific class is a defect
unless it is listed here. The shell delivery carries the
previous vocabulary in a delimited legacy block for pages not yet ported,
deleted in wave 5.

### Shell

| Class | Role |
| --- | --- |
| `app-shell`, `app-rail`, `app-column`, `app-main`, `content` | Grid, rail, column, main landmark, 1580px content |
| `brand`, `brand-copy` | Rail lockup |
| `primary-nav`, `nav-label`, `nav-link`, `nav-count` | Rail navigation; `nav-count` is absent when there is no figure |
| `rail-health`, `rail-user` | Rail foot |
| `utility-bar`, `utility-freshness`, `utility-search` | Dark bar |
| `rail-collapsed` (on `app-shell`), `[data-rail-toggle]` | Collapsed 64px rail and its Collapse/Expand control |
| `bell-wrap`, `bell-count`, `row-list`, `row-form`, `row-button`, `row-button--unread` | The bell with its unread count, and the Notifications dialog's one-button rows |
| `workspace-tabs` (`[data-working-set]`), `workspace-tab`, `workspace-tab-link`, `tab-close`, `tabs-more` | Working-set strip of open records: six tabs, the rest in "N more"; `body.has-working-set` while anything is open |
| `external-shell`, `auth-card`, `auth-brand` | Navless frames |
| `skip-link`, `sr-only` | Accessibility |

### Page

| Class | Role |
| --- | --- |
| `page-header`, `page-title`, `eyebrow`, `page-actions` | Header row |
| `btn`, `btn--primary`, `btn--dark`, `btn--danger`, `btn--ghost`, `btn--small`, `btn--icon` | The one button family; `--primary` is `--red`, `--dark` is `--nav-2`, `--danger` is `--danger`; `--icon` is a compact icon-only button (Refresh, dismiss, section fold) |
| `metric-strip`, `metric-strip--3`, `metric-strip--4`, `metric` | Count buttons linking to `/Cases?tab=` (the Work Centre's four) |
| `panel`, `panel-head`, `panel-body`, `panel-body--compact`, `panel-body--tight` | Bordered section |
| `notice`, `notice--success`, `notice--warning`, `notice--danger` | Inline notice: label plus value only |
| `status` and its tone modifiers | State chip ([Colour](README.md#colour)) |
| `tabs`, `tab` | `tablist` / `tab` with `aria-selected` |
| `pane-layout`, `pane-layout--2`, `pane-layout--3`, `pane`, `pane-head`, `pane-body`, `pane-scroll` | Multi-pane workspaces |
| `queue-layout` | The Cases three-pane variant |
| `scope-list`, `scope-button` | Left-pane scopes with icon well and count |
| `row-button`, `work-item` | Selectable rows (arrow navigation) |
| `fact-grid`, `fact`, `definition-list`, `definition` | Label/value cells |
| `pagination` | Bounded pagination with current-page context |
| `empty` | Empty result; renders only where an action exists |
| `table-wrap` | Horizontal scroll container for tables |

### Record

| Class | Role |
| --- | --- |
| `record`, `record-head`, `record-accent`, `record-bar`, `record-body` | Single-record container |
| `sticky-block` (`[data-sticky-block]`) | The record's sticky block under the utility bar and working-set strip, measured at runtime into `--sticky-h` |
| `ribbon`, `ribbon-facts`, `ribbon-item`, `ribbon-ref`, `ribbon-value`, `ribbon-chips`, `ribbon-actions` | The 56px identity ribbon: the reference as the page's `h1` (`ribbon-value`) under "Case workspace · registration", Claimant, Principal, Engineer; state, Case type and colleague-editing chips; then the edit controls and the one **Actions** menu |
| `section-row`, `section-nav`, `section-link`, `section-tools`, `layout-switch` | The 40px section row: section links (the one in view carries `aria-current`), Refresh and the Scroll/Tabs switch |
| `workspace`, `workspace-aside` | The record grid: sections beside a 285px aside (Figures, Next action) that folds above the sections below 1441px |
| `record-section`, `panel[data-collapse]`, `panel-collapse`, `is-collapsed`, `is-editing`, `is-locked` | One section panel, foldable and remembered per browser; the record's edit and read-only states |
| `fg`, `fc`, `fv`, `fi`, `ro`, `idn` | One-geometry cells: a value that becomes its input while editing; `ro` never edits; `idn` reads with a lock while the rest edits |
| `menu`, `menu-body`, `menu-sep` | A `details` menu (the Actions menu, head menus); one open at a time |
| `gated`, `avail` | The dashed availability label, stated once per section head |
| `damage-workbench`, `damage-markers`, `stepper`, `figures`, `figure` | The Damage Plan clicker and its numbered markers, the workflow stepper and the aside figures |
| `damage-diagram`, `impact` | The clickable damage diagram and its zone markers; `impact` marks a zone with recorded damage |
| `tyre-card` | Tyre and seat belt per corner, spare tyre, centre belt |
| `valuation-card` | One valuation entry: source, date, time, mileage, guide month, retail, trade |
| `outcome-option` | Settlement outcome choice |
| `derived` | A value derived, never entered: impact location and severity, equity, and a permitted ratio line where one is shown |
| `report-image`, `cropper` | Report-image preparation on the Report section: designated Close-up and Overview, supporting images in order, non-destructive crop |
| `workflow-stepper`, `workflow-step` | Not ready → Review → With Engineer → Completed ⇄ Query; Held badge |
| `case-overview-grid`, `overview-facts`, `accident-card`, `checks-grid` | Overview and Vehicle sections |
| `blocker-list`, `blocker` | Outstanding requirements |
| `timeline`, `notes-list`, `note-entry` | History and Notes |
| `document-list`, `document-row`, `gallery`, `viewer-stage` | Case Files |
| `mail-preview`, `decision-card` | Inbox preview and message decision |
| `assessment-v3`, `estimate-tabs`, `estimate-tab`, `estimate-editor`, `estimate-form-grid`, `estimate-table`, `estimate-totals` | Estimate section of the Case record (formerly the Assessment page, D30) |
| `ai-jobs-panel` | Operations AI Job List |
| `admin-layout`, `admin-nav` | Administration |
| `dropzone`, `file-list`, `file-row`, `upload-outcome` | Upload |
| `report-preview` | Report draft preview dialog body |

### Dialogs and feedback

| Class | Role |
| --- | --- |
| `dialog-backdrop`, `dialog`, `dialog-head`, `dialog-body`, `dialog-foot` | Modal dialog ([contract](README.md#keyboard-and-dialog-contract)) |
| `command-dialog` | Command palette |
| `toast-region`, `toast` | Toasts |

### Utility classes

The Content Security Policy forbids inline styles, so the prototype's
`style=""` attributes become these utilities and nothing else:

`mt-0`, `mt-1`, `mb-2`, `ml-auto`, `cluster--between`, `cluster--start`,
`panel-body--compact`, `panel-body--tight`, `field--narrow`, `no-border`,
`viewer-stage`, `metric-strip--3`, `metric-strip--4`.

The names are fixed here and delivered with the shell; a new utility needs a
second caller and a recorded reason.

### Shared partials

| Partial | Role |
| --- | --- |
| `_Layout`, `_LayoutAuth` | Frames |
| `_LucideSprite` | The inlined sprite |
| `_ShellDialogs` | Account, Notifications, command palette |
| `_AdminNav` | Administration panel nav |
| `_StatusChip`, `_PageHeader`, `_ReasonDialog`, `_ErrorSummary`, `_EvidenceViewer`, `_ImageGallery`, `_UploadOutcome`, `_Provenance` | Retained, restyled to the vocabulary |
| `Presentation/OperatorLabels.cs` | The one label map |
| `Presentation/RailCountsPageFilter.cs` | Rail counts |









## Accessibility

The planned UI supports keyboard and pointer operation, screen readers, 200% zoom, forced colours and reduced motion on supported desktop layouts.

Required behavior:

- skip link;
- semantic landmarks and headings;
- labelled navigation;
- semantic tables with captions, headers and sort state;
- keyboard-operable queue selection;
- explicit pane and tab relationships;
- associated field errors and error summaries;
- visible focus;
- practical 44px targets;
- restrained live announcements;
- non-colour state cues;
- safe modal focus handling;
- permanent consequences visible without hover;
- server authorisation regardless of route visibility.

Each visible capability/state also needs authenticated Web-caller and named
Core-owner evidence. Generated imagery or synthetic operational material cannot
prove acceptance.

## Source and runtime map

| Concern | Durable owner or source | Runtime consumer or evidence |
| --- | --- | --- |
| Product capability and horizon | [Requirements](../prd/README.md), [capabilities](../capabilities.md) | Routed staff pages as each wave lands |
| Open policy questions | [Open decisions](../open-decisions.md) | No implementation inference until resolved |
| Architecture and caller boundaries | [Architecture](../current-architecture.md) | Core, Web, Worker, MCP and external adapters |
| Production, release, monitoring, and recovery state | [Operations](../operations.md) | No deployment claim from design or source presence |
| Setup, testing, release, and recovery procedure | [Runbook](../runbook.md) | Procedure is not execution evidence |
| Engineering procedure | [Engineering](../engineering.md) | Reviewed implementation and verification |
| Design authority | This file | Approved tokens, assets, class vocabulary and page contracts |
| Interaction contract | Current operator instructions and [FRD-12](../frd/frd-12-operator-experience.md) | Earlier imported design decisions provide context only where consistent with current requirements |
| Shell | This file | `src/Pegasus.Web/Pages/Shared/_Layout.cshtml`, `_ShellDialogs`, `RailCountsPageFilter.cs` |
| Tokens and vocabulary | This file | `src/Pegasus.Web/wwwroot/css/site.css`, `site.js` |
| Font | This file | `src/Pegasus.Web/wwwroot/fonts/inter/` |
| Master logo | `docs/design/brand/logos/logo_no_margin.png`, checksum above | Embedded by the Infrastructure report adapter; the Web shell uses the refined mark mappings above |
| Renderer templates/style | Repository renderer asset sources | Embedded by `src/Pegasus.Infrastructure`; Core owns report policy and accepted presentation values |
| Engineer signatures | Repository renderer signature sources; the Sign-off Engineer account setting holds the signature image | Rendered as the Case's sign-off tuple by the renderer; none is Web decorative imagery |
| Retired renderer/skills/AI source | Git history and accepted integration records | No separate caller, runtime, or policy owner |
| Decision rationale | [Decision records](../adr/README.md) | Does not itself prove implementation |
| Change evidence | Git history | Does not replace caller, deployment or acceptance evidence |
| External reference qualification | [Reference index](../../reference/README.md) | Reference presence never creates authority |

The similarly named logo and signature files under `reference/rendererref1/`
are retained supplied evidence. The logo and all three signature pairs are
byte-identical to the governed assets under `docs/design/brand/`, but are not
deduplicated: `reference/` preserves the supplied evidence grouping while
`docs/design/` owns runtime use. Equal bytes do not transfer either role and the
evidence copies do not replace this design authority.

## Change and verification rule

Change approved design authority, source/runtime mapping and affected implementation in one reviewed change.

A conforming change must:

1. identify whether it is planned, implemented, caller-proved, deployed or accepted;
2. preserve exact business labels, consequences and authorisation boundaries;
3. use approved tokens and assets or explicitly record a reviewed divergence;
4. verify the real caller rather than imported or unused source;
5. update accessibility evidence for affected states and routes;
6. use genuine authorised material for operator review;
7. preserve checksum proof for copied or optimised logo assets;
8. avoid synthetic brand assets, operational examples, copy or duplicated generated output;
9. avoid a parallel runtime token file until one selected implementation can make a single source directly consumable; and
10. return every `Next` or `Later` UI capability to complete design approval before adding any route, control, workflow or placeholder.



## UI specification

Specification for the Integrated Operations Workspace. Runtime evidence is recorded separately.
The per-page contract is [FRD-12](../frd/frd-12-operator-experience.md#operator-experience) with [FRD-15](../frd/frd-15-work-centre-queues-and-search.md), [FRD-16](../frd/frd-16-case-record-workspace.md) and [FRD-17](../frd/frd-17-administration-workspace.md);
this section holds the cross-cutting rules every page is held to.

### Shared shell and hierarchy

1. Shell: rail, utility bar with the bell, working-set strip, account dialog.
2. Page header: eyebrow, title, freshness and a safe primary action.
3. Operational panes, table, workbench or record.
4. Named workflow/evidence/lease/exception state and consequential action.
5. Provenance, external identity, permanent business history and limitation.

### Contracts

| Component | Required contract |
| --- | --- |
| Shell/access | Sign-in and disabled/stale-role/denied outcomes; permitted-route visibility plus server authorisation; rail counts absent, never zero. |
| Metric/queue | Label, value or unavailable state, last-good time, current refresh state, and exact destination filter. `0`, loading, current, stale, partial, unavailable, and failed remain distinct. |
| Row | One keyboard-focusable full-row button with visible affordance; all row text contributes to its accessible name; arrow-key navigation within the list. |
| Field provenance | Every editable or source-derived Case datum shows its current origin marker. Origin and status remain distinct. |
| Supporting detail navigation | Opening evidence or supporting detail preserves list/detail position, the current context, and every unsaved edit; returning never silently discards or replaces proposed values. |
| State action | Permitted transition, prerequisite, consequence, required reason, recovery and history link; never generic Close. |
| Readiness blocker | Every unmet requirement names its exact field or material, source, reason, and permitted resolution; no opaque aggregate blocker. |
| Identity ribbon | Read-only Case/PO, registration, claimant, principal, state, with Engineer and Sign-off Engineer beside it; sticky on the single-scroll Case record. There is no separate Assessment ribbon. |
| Inspection address | Provider-determined default; reasoned per-Case override; previous values selectable. |
| Estimates | Each estimate has its own VAT percentage (default 20) and selected VAT categories; VAT applies to selected discounted Labour, Parts, Materials and Specialist categories. Unknown repairer VAT blocks Use as Current until an explicit status or categories are recorded; totals compute once in Core. A saved version's actions row carries **Estimate PDF** in read and edit modes. |
| Evidence/document panel | The stored case files themselves — name, type, size, source, custody chip, preview, download; a reasoned removal recorded on the timeline; exact Sent evidence with separate discovery, link and sent times. |
| Evidence image preview | Loading and source-preserving enlarged-image states are explicit; Rotate view is a viewer-local transform. |
| Mail preview | Keyboard and pointer intent exposes an accessible preview that changes no message or Case state; when intent moves away the pane restores the selected message and stays visible with its navigation links. |
| Mail refresh | No automatic refresh while an operator is reading or acting. Manual refresh retains scope, page and open message where available. |
| Lease/conflict | Holder/expiry/recovery, read-only alternative, current conflict and preserved proposed values. |
| History | Business mutation/accepted evidence/export/material business failure only; no routine views, polling, retry, lease heartbeat or telemetry. |
| Reason dialog | Named requirement/consequence, labelled reason, confirmation/cancel, initial focus, focus containment, Escape where safe and focus return to the invoking control. Used by Case, Triage, Mail, Image Intake and Operations; Administration actions post on the click with no confirmation dialog, except Delete account, which confirms in a native dialog because the row is removed. |

### Presentation responsibilities

Product requirements own business gates and outcomes; this specification owns
how they are presented and operated. Lists expose identity, state, freshness,
filter, provenance, and permitted action. Records expose source evidence,
accepted facts, missing/conflicting values, history, leases, external status,
and reasoned transitions without duplicating Core policy. The shell and Work
Centre own navigation and exact counts; Administration areas own authorised
configuration journeys; error, empty, loading, denied, stale, partial,
conflict, and unavailable states are explicit.

#### Enforced presentation rules

These are the rules every operator surface is held to.

1. **Words, never codes.** No persisted enum, snake_case code, hash, storage
   key, path, byte count or version integer appears as operator text. One
   place — `Pegasus.Web.Presentation.OperatorLabels` — turns a persisted code
   into words, and every surface goes through it. Where a code carries a
   distinction the operator must act on, the distinction is kept and only the
   spelling changes.
2. **No raw identifiers.** GUIDs, correlation ids, sequence-lineage ids and
   external transport handles are internal. Where an operator genuinely needs
   a stable handle, show the business reference — Case/PO, Image reference,
   registration.
3. **One clock.** Every date and time renders Europe/London through
   `OperatorLabels`. `ToLocalTime()` is never correct: it resolves against the
   server clock, which is the office zone on a developer workstation and UTC
   on the deployed container, so it looks right exactly where it is tested and
   is wrong through British Summer Time where it runs.
4. **Sizes in MB**, one decimal, and only where the size is something the
   operator can act on. Never bytes.
5. **Every screen has designed empty, loading and failure states**, written as
   business statements rather than as descriptions of the query that returned
   nothing. An unknown-record URL renders the styled not-found surface, never a
   raw browser 404.
6. **Absent versus disabled.** A capability that is not composed in this
   deployment is absent. A capability whose record does not yet satisfy a
   condition is present, disabled, and states the condition. A named, ticketed
   frontend preview may be visible disabled and inert before its backend exists;
   it makes no delivery claim and has no production handler. Implemented
   behaviour behind a closed composition gate is not delivered
   ([Absent versus disabled](README.md#absent-versus-disabled)).
7. **Counts and times cannot be proved locally.** A count query against an
   empty database returns the same zero as a correct one, and a rendered time
   against a Europe/London workstation clock matches the office by accident.
   Both need evidence from populated data and a non-London clock — a test that
   stores rows, or the deployed instance.

### Freshness and reconciliation

Every query keeps the last successful value/time visible when a later refresh
is stale, partial, unavailable, or failed. Refresh — the page button or F5 —
reruns the same query; it never substitutes zero, marks an external action
complete, or changes a business fact. Show start/completion feedback and a
safe retry.

Routine refresh audit belongs to content-safe telemetry. When staff accept,
reject, link, or change an external fact during reconciliation, show the
source/version, prior and new value, actor, time, outcome, and required
reason in permanent history.

### Exceptions and necessary copy

Use guidance only where the operator must understand a consequence:

- "Closed — a reason is required."
- "No case or reference was created; review the missing or conflicting evidence."
- "Created in error cannot be reopened. Create and link the replacement case."
- "Unlinking this email cancels case <reference>."

Illustrative text must not fabricate operational input. Loading, empty,
stale/partial, retryable error, denied/unauthenticated, validation, conflict,
external-unknown and reopened behavior follows the state matrix. Permanent
consequences remain visible without hover or colour alone.

### Accessibility and acceptance

Use skip link, labelled navigation, semantic tables/captions/header/sort
state, keyboard row selection, pane/tab relationships, associated error
summary, restrained live announcements, visible focus and safe modal focus
handling. The desktop layouts cover 1580, 1100 and 760px. Mobile is `Not
planned`.

When implemented:

- each visible row and state needs authenticated Web-caller and named
  Core-owner evidence;
- screen-reader-compatible semantics remain required behavior;
- operator review uses approved genuine local immutable material only;
  generated imagery or synthetic operational material cannot prove
  acceptance; and
- every UI capability beyond this contract re-enters specification,
  independent review and explicit approval before its route, control or
  workflow is added.

## Functional interaction owner

[FRD-12](../frd/frd-12-operator-experience.md) owns routes, account actions,
Case states, permitted interactions and freshness behavior. This guide owns
presentation, components, tokens and assets. Do not copy the route/action matrix
here. The accepted single-scroll Case layout is specific to that record; it
does not impose a global tab/scrolling prohibition on every other screen.

## Business language

Functions should be apparent from labels and actions. Do not narrate the
application or expose internal service names, GUIDs, hashes, storage paths,
enum names, event codes or version integers. Use Evidence for files, images
and mail. Display source with the accepted icon/short-label treatment.

Do not display the internal word intake (the Administrator's Intake log tab is
the one exception); use Inbox, Upload, received files or vehicle images. Where
size matters, show
megabytes rather than bytes. Show a known count of zero as 0; omit a metric
whose query does not exist instead of inventing a number or placeholder count.
Read FRD-12 for record-specific layout and visible-disabled action exceptions.
