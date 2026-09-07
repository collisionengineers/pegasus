# Design authority

This file is the durable authority for Pegasus visual design, Web interaction
contracts, approved assets, component and pattern boundaries, and
source-to-runtime mappings. Product scope and business capability remain owned
by [requirements](../prd/README.md) and [capabilities](../capabilities.md);
architecture and deployed state remain with
[architecture](../current-architecture.md) and [operations](../operations.md),
procedures with the [runbook](../runbook.md), operator truth with
[operator notes](../operator-notes.md), and repository workflow with
[engineering](../engineering.md).

The design it describes is the **Integrated Operations Workspace**, approved by
the operator on 2026-08-28 from the effective render layer of the reviewed
prototype and transcribed into the EPIC-011 group contract (`context.md` §1,
decisions D1–D13). This file restates that contract as the durable authority.
Delivery is planned: the design system and shell are delivered by PLAT-029
(EPIC-011 wave 1) and the pages by EPIC-011 waves 2–5. Nothing here is a claim
that a surface exists, is deployed, or is accepted; the evidence discipline
below governs every such claim.

## Evidence discipline

The accepted v3 interface and the 6 September 2026 operator answers are the
current target. Older prototype/layout descriptions are comparison evidence
where they conflict. Use one scrolling Case record with section jumps and
Core-owned editing authority; complete engineering and reports within Pegasus.
The supplied v3 verification JSON is missing, so no claim is made that its
65 checks or 240 vectors were executed. Current routed-page snapshots and
browser acceptance require their own recorded runs.

Repository FRDs and this design guide own UI requirements. The adjacent
work-pack is supporting comparison evidence, not a canonical execution source
(D15, amended 2026-09-02). A visual conflict with a delivered surface pauses only the affected
lane and its dependants; it never pauses the programme.

Intended, planned, implemented, caller-proved, deployed and accepted are distinct:

- **Planned** describes the approved target contract — the Integrated
  Operations Workspace below. It does not prove an authenticated Web caller,
  deployment or operator acceptance.
- **Implemented** means code or an asset exists. Imported workspace code is not automatically a Pegasus caller.
- **Caller-proved** requires a real route or other named caller exercising the behavior.
- **Deployed** requires deployment evidence; none is inferred from implementation.
- **Accepted** requires the specified accessibility and operator review evidence.
- The reviewed prototype records the design selection. Its fixture data,
  dead layers and defects are not design approval or runtime evidence; only
  its effective render layer, as transcribed here, is the contract.

Detailed durable product-design owners are the
[operator-experience requirements](README.md#operator-experience-requirements) and
[UI specification](README.md#ui-specification). Per-capability ownership and
activation boundaries are owned by the
[capability inventory](../capabilities.md#capabilities) alone.

## Test UI

[The Test UI catalogue](test-ui/index.html) is the disposable, offline
catalogue of current routed Web surfaces. Open it directly from the repository
to inspect generated static HTML snapshots without .NET, authentication, a
database, or external services. State files use the
pages/route-key--state.html convention; `catalogue.json` is the single
route-classification and state list.

The catalogue is design evidence only. It does not implement, approve, deploy,
or simulate server behaviour, and it is never an application or publish input.
Its markup is captured from the current Razor pages and PageModels, with only
volatile security/operation values and local URLs normalized. Do not edit the
generated HTML by hand. Approval of a
Test UI experiment is separate from implementation in the Live Razor pages.
Each visual state names the current Razor/PageModel branch it represents in the
catalogue manifest. Regenerate and verify after changing a routed Razor page:
`scripts/Update-TestUiSnapshots.ps1`, then
`scripts/Update-TestUiSnapshots.ps1 -Verify` and
`scripts/Test-UiCatalogue.ps1`.

The [route map](#routes) below states what the catalogue's route keys become
as each wave lands: PLAT-029 makes the structural edits to `catalogue.json`
(moved routes, 301 stubs, the removed `/VehicleImages` list), and each page
ticket re-snapshots its own routes.

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
returns as a routed workspace. `Triage`, `Unidentified`, `Audit`, `Blocked`,
`Not ready`, `Review` and `Held` keep their settled meanings; Triage and
Unidentified are pre-Case records reached through the Cases rail, never Case
states.

The common hierarchy of every authenticated page is:

1. shell — rail, utility bar, workspace tabs;
2. page header — eyebrow, title, freshness and the safe primary action;
3. operational panes, table, workbench or record;
4. named workflow, evidence, lease or exception state and consequential action;
5. provenance, external identity, permanent business history and limitations.

### Authenticated shell

The `.app-shell` grid is a 220px sticky `.app-rail` beside the `.app-column`.
The rail is the dark `--nav` gradient with a 3px `--red` top stripe. Its
content, top to bottom:

- **Brand** — the `pegasus-lockup` mark at 48px beside "PEGASUS" and the line
  "Case management".
- **Nav label "Work"**, then the links in order: Work Centre (`/`), Inbox
  [count], Upload, Cases (`/cases`) [count], Search (`/search`), Operations
  [count].
- **Nav label "Manage"**, then Administration — rendered for Administrators
  only, absent for everyone else.
- **Rail foot** — the health line (dot and "Current · HH:MM") and the user
  block (avatar initials, name, role, account-menu button).

The current route is signalled by a white background, a `--red` left border
and a red icon well, with `aria-current="page"`; the border is the non-colour
cue. Below 980px the rail lies down into a horizontal bar, labels hide, and
the current-route border moves to the bottom edge, so the cue survives the
reflow; nothing is hidden at any width except the rail foot and nav labels,
whose functions move to the utility bar and account dialog.

**A rail count is a figure a page already queried**, never one the shell
invents. The Cases count is
`not_ready + review + with_engineer + held + triage + unidentified` (group
contract §1.1); the Inbox and Operations figures are composed by the wave-2
and wave-3 tickets that own those queries — the shell invents none. An absent
count renders nothing at all — a shell-level `0` would be exactly
the stale zero the operator-experience requirements forbid. Counts are
supplied by one page filter (`Presentation/RailCountsPageFilter.cs`,
PLAT-029), not by each page.

The **account dialog** opens from the rail-foot button and shows Name, Role,
Session started (an `auth_time` claim) and Idle lock, with Close and Sign out.

The **utility bar** is dark and sticky at the top of the column: freshness
text, the global search input with its "Ctrl K" hint (Enter or Ctrl K opens
the command palette dialog), the "Add" primary button (dialog: Upload files,
Create Case, Create upload request, Review Inbox) and the bell (Notifications
dialog).

The **workspace-tab strip** sits under the utility bar: a "Work Centre" tab,
one closable tab per open Case record — at most four, least-recently-used
evicted — and an "Open" button that raises the command palette.

`main.app-main` holds `.content`, capped at 1580px and centred with the 18px
page padding, so a wide monitor shows equal margins either side rather than
every table pressed against the rail. Below the cap nothing moves. Every page
carries the skip link, the toast region and the dialog root.

`_LayoutAuth` and `_LayoutExternal` remain the navless frames: sign-in, the
signed-out confirmation, access denied, the error family and the one screen a
third party sees are not places in the application (see
[External frames](#external-frames)).

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
estimate drop is pointer-only (D16), and it is a real gap — no staff keyboard
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
(D2, [Removed surfaces](#removed-surfaces)); their bytes stay registered and
their removal is proposed for the wave-5 removal ticket.

#### Pegasus marks source-to-runtime mapping

Upstream source: Claude Design project `710bb42f`, `assets/icons/` (1024×1024
RGBA PNGs). Runtime destination: `src/Pegasus.Web/wwwroot/images/marks/`
(128×128 Lanczos downscale, decorative `aria-hidden` with empty `alt`).

| Mark | Upstream source & SHA-256 | Runtime destination & SHA-256 | Mapping & usage |
| --- | --- | --- | --- |
| `pegasus-lockup.png` | `PegasusDesign/assets/icons/pegasus-lockup.png`<br>`C8F3551841AACA26AAE4F959B263DBB2409EB44A327207F8078D85A1F33668A7` | `src/Pegasus.Web/wwwroot/images/marks/pegasus-lockup.png`<br>`938C22B0F0FC621DC6FADD57748BA858CD1235292581AE47705A4ED336140EF0` | Rail brand and sign-in card. |
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
  and in-progress external work, green only for confirmed completion, red for
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
  ([Absent versus disabled](#absent-versus-disabled)). Genuine runtime
  failure renders the designed failure state with the last-good time.
- Every screen defines its empty, loading, and failure states in business
  language, and unknown-record URLs render the styled not-found screen, never
  a raw browser error.
- Screens are compact working surfaces: 40px rows, 12–16px panel padding,
  13–14px body text. A screen about a single record other than the Case
  record is one container — header, identity ribbon, action bar, sections as
  tabs — and the operator reaches its identity, its state, its available
  actions and its main content without scrolling.
- The Case record is one scrolling page (D29, 2026-09-02): its identity
  ribbon, action bar and section jump-nav are sticky, the jump-nav marks the
  section in view, sections below the fold render lazily, and `?section=`
  jumps to a section. No layout switch exists. The "sections as tabs" rule
  above is superseded for the Case record only.
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

Settled terms retain their exact meanings and casing, including `Audit`,
`Triage`, `Unidentified`, `Blocked`, `Not ready`, `Review`, `With Engineer`,
`Complete` and `Held`. `With Engineer` and `Complete` are display labels only
(D3): `ReportPreparation` and `PostReport` render as "With Engineer",
`PostReportComplete` as "Complete", and the Core enum is untouched; other
terminal outcomes render as "Closed · <outcome>" in Search and are excluded
from the Cases rail. Never substitute a generic **Close** action for a named
lifecycle outcome.

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
| `.status--green` | `--green` on `--green-bg` | Complete, confirmed |
| `.status--red` | `--danger` on `--danger-bg` | Blocked, failed, closed in error |
| `.status--neutral` | `--muted` on `--surface-3` | Closed, cancelled, unknown |

### Typography

Application text uses **Inter Variable** (upright and italic), vendored under
`src/Pegasus.Web/wwwroot/fonts/inter/` as woff2 with the SIL Open Font
License 1.1 text beside it (D13). The face is self-hosted and declared with
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
- copied byte-for-byte to the Web runtime and embedded by
  `src/Pegasus.Web/Pages/Shared/_LayoutExternal.cshtml` — the external frame
  states the company, never the product. `_Layout.cshtml` does **not** use
  it; the authenticated rail carries the `pegasus-lockup` mark.

Rules:

- Never redraw the gear.
- Never extract it from a screenshot.
- Never recolour the master or invent another mark.
- Copy or optimise it for a runtime only through a reviewed source-to-runtime
  mapping with checksum proof.
- No second logo variant exists.

The upstream source directory may be absent from a clean checkout. The
checksum-pinned repository copy is the durable source.

#### Logo source-to-runtime mapping

| Asset | Upstream source & SHA-256 | Web runtime destination & SHA-256 | Mapping & usage |
| --- | --- | --- | --- |
| Primary logo | `docs/design/brand/logos/logo_no_margin.png`<br>`E7247BE45911C46905343473E4C57B9F6ED7A450563D19C508C2D9652C2C63E2` | `src/Pegasus.Web/wwwroot/images/logo_no_margin.png`<br>`E7247BE45911C46905343473E4C57B9F6ED7A450563D19C508C2D9652C2C63E2` | Byte-for-byte copy embedded by `_LayoutExternal.cshtml` (public upload frame) and, under the integrated design, the `auth-brand` of the sign-in card. |

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
`Send to Claude` is retired ([Reviewed divergences](#reviewed-divergences)).

The checksummed sprite is `src/Pegasus.Web/wwwroot/images/lucide-sprite.svg`,
inlined once per page by `src/Pegasus.Web/Pages/Shared/_LucideSprite.cshtml`
as `<symbol viewBox="0 0 24 24">` elements; pages reference glyphs as
`<svg class="icon"><use href="#icon-…"/></svg>`. The `.icon` rule applies the
stroke and caps because a `<use>` clone does not inherit them. The sprite
holds the sixty glyphs below (the original seventeen plus the
forty-three PLAT-029 added from Lucide v0.344.0); each glyph checksum is the
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
sixty glyphs; the pre-PLAT-029 seventeen-glyph sprite was
`24360787DB7A58F1B0ACA7E2F66405749C9D5742A2ADA91C07BDFF03202872D0`).

| Prototype name | Lucide id | Glyph SHA-256 | Usage |
| --- | --- | --- | --- |
| `dashboard` | `layout-dashboard` | `F8A9AFA8D2245E34D3DAEB88C9FF80A2AA546D1F8671212896E743E596F3752B` | Rail: Work Centre |
| `inbox` | `inbox` | `0817485BFAE1A740458AA3FC1E6E4542047FA890C547D35B17C771E6D352E901` | Rail: Inbox; Inbox scopes |
| `upload` | `upload` | `EE63E95EFECDAF141338475D367A54EF891E337491993DCDC1F3ED7936A42660` | Rail: Upload; dropzone; Add dialog |
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
| `link` | `link` | `6D49DABEC5033468AD49114CA79422BC8CB2F1C1AE3696A34FD98EA5B1E93A1E` | Case link, upload link |
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
| `download` | `download` | `C5BB0DCFCE72DDFCD8BAC34C368CDE4E2013FF05C175318324D40776DF0C457C` | Save as, Download ZIP in the Send to EVA dialog (D36) |
| `folder` | `folder` | `6E9E30D6DB22DC0118AC8C8466659342AFAE90784EFD65B5E2929BE1BA7B0C16` | Folder scopes, Case Files |
| `info` | `info` | `9B266C26D53D1F6661CD45D11E5138FE00AF4289EA4EC8D4C320D41AB272CC3F` | Provenance, informational notice |
| `car` | `car` | `36AE3DC22866D02D1159AB8D6256BB09E91B2D98C03BC7126EE576437BECF0C5` | Vehicle section |
| `person` | `user` | (as `user`) | Claimant, parties |
| `task` | `check-square` | `D84CA64CC54CFF1C150D4D31618203F054470D80DFD59989B3EE52009574CE31` | Work items, checks |
| `archive` | `archive` | `37BA14C8285BE494749A4DA9E213B37048ABCEF5DB0D65B1C65DE959A135AD84` | Archive, Deleted Items |
| `send` | `send` | `63B04BD6FA6A68DEC5F9492B1D0926D00EE28C3F1332E10F47565AF49FB4649D` | Send, Send to EVA, **Send to Claude** (the prototype's `btn(…,'primary','send')`) |
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

### Imagery and evidence

Upstream marketing photography is excluded, and no generated or substitute
glyph is used anywhere. The one class of imagery the internal Web application
carries is the [commissioned Pegasus marks](#the-pegasus-marks): decorative,
naming a surface, always beside text that says the same thing.

Genuine case images, emails and documents are operational evidence, not
decorative assets. Use only authorised repository-provided evidence through
its owning workflow. Never generate placeholder cases, damage images, emails,
documents or people. The prototype's fixture data is not domain data and is
never copied, except the Case Workspace v2 fixture set permitted by D43
([engineering](../engineering.md#case-workspace-v2-fixture-values-d43)).

### Web and renderer boundary

| Asset class | Approved consumer and boundary |
| --- | --- |
| Master logo | Embedded by the Infrastructure report adapter and copied byte-for-byte to Web for the external and sign-in frames |
| Report templates and document stylesheet | Embedded by `src/Pegasus.Infrastructure`; not Web shell assets |
| Supplied engineer signatures | Andy Patterson's approved exact tuple is embedded by Infrastructure; other supplied assets remain governed; never Web decorative imagery. The signatory policy is D31 (the Case's Sign-off Engineer tuple), delivered by `DOCS-017`. |
| Retired renderer workspace, prompt, model, skill and AI material | Historical source evidence only; not a separate runtime or policy owner |

The imported renderer can exercise its own assets without proving the planned
Pegasus report capability. Imported workspace material does not become UI,
report or design authority by existing in the repository. See the
[workspace boundary](../../workspaces/README.md).

## Voice, labels and necessary copy

Use concise, settled Collision Engineers language. Guidance is appropriate only when an operator must understand a consequence.

Approved necessary copy includes:

> Blocked — a reason is required.

> No case or reference was created; review the missing or conflicting evidence.

> Created in error cannot be reopened. Create and link the replacement case.

> Unlinking this email cancels case <reference>.

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

## Access and permissions

Staff accounts, authentication, and authorisation are implemented and
enforced through authenticated Web callers
([architecture](../current-architecture.md)). Accounts use Pegasus-managed
usernames and passwords. Core owns the exact
[staff role access matrix](../frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix),
automated-actor boundary, and
[case edit authority](../frd/frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery);
this section owns only how those decisions appear in the UI.

| Actor | UI boundary |
| --- | --- |
| Administrator | Staff shell plus the Administration areas: Staff accounts & roles, Principals, Workflow configuration, Mail settings, Automation & AI, Service health, Action Logs, Reports. No secret display beyond the masked Principal API key. |
| Engineer, User | Staff shell without Administration. Their ordinary Inbox, Cases, Search, Case record (including its Engineer sections, D30), Upload and Operations controls are identical. |
| Automated processing | No UI account or interactive control; the Automation Actor appears only as `SYSTEM` / `AI` in notes and Action Logs. |
| Provider API client | No staff shell, Case workspace, or Administration surface; its credential is the Principal "Pegasus API key" (D8). |
| External/customer | No application account; the only external surface is the request-scoped `/Uploads/{token}` page, which exposes no case or request state. |

Every protected route and action must handle unauthenticated,
disabled-session, stale-role, denied, loading, and successful outcomes. Hiding
a route or control never replaces server authorisation. Administration has no
generic rules editor, cloud/release operation, bulk predecessor
import, or bulk Case-edit tool. No surface permits permanent deletion or
direct external/customer Case editing.

## Absent versus disabled

A capability that is not composed in this deployment is **absent** from the
interface. A control whose record does not yet satisfy a condition is present,
disabled, and states the condition on the control ("Available in Review").

**Amended 2026-08-28 (D7).** A disabled control for an *uncomposed
integration* is permitted only for a named, ticketed integration seam drawn
in the approved design:

| Seam | Control | Ticket |
| --- | --- | --- |
| Experian | Vehicle checks → Run Experian check | ENG-001 |
| Cazana | Valuation source | ENG-008 / ENG-009 |

**Narrowed 2026-09-01 (D21).** An excluded capability is absent, never drawn as
a disabled control. The direct Glass's valuation-service and Audatex
service-launch controls are removed on that rule (ENG-030). This does not
remove the Estimate section's selected configured-Engineer Glass's
repair-estimate launch (D03). Glass's and Audatex file import stays in scope
through the whole-page drop, and manual valuation records include Glass's,
Brego, Super CAP and Engineer's Value; Cazana remains the disabled seam.

Every other uncomposed capability stays absent: no inert card, no
"Unavailable" placeholder, no unlinked route. A disabled seam carries its
accessible name, `aria-disabled`, and no handler; the ticket that composes
the integration enables it.

## Routes

| Route | Page | Notes |
| --- | --- | --- |
| `/` | Work Centre | Metric strip, Needs attention, Today pane |
| `/Inbox` | Inbox | Scope, messages, preview panes |
| `/Inbox/{id}` | Inbox message | Record, tabs, decision card |
| `/Upload` | Upload | Dropzone and file rows |
| `/Cases` | Cases | `queue-layout`; `?tab=` selects the rail group: `not-ready`, `review`, `with-engineer`, `complete`, `triage`, `awaiting-instruction` (D38), `held`, `unidentified` |
| `/Triage/{id}` | Triage record | Pre-Case |
| `/Unidentified/{id}` | Unidentified record | Pre-Case |
| `/Search` | Search | Advanced grid, results, Selected Case |
| `/Cases/{id}` | Case record | One scrolling page (D29); `?section=` jumps to `overview`, `engineer-notes`, `inspection`, `vehicle`, `damage`, `valuation`, `estimate`, `settlement`, `report`, `files`, `notes` (D30) |
| `/Cases/{id}/Assessment` | — | 301 to `/Cases/{id}?section=estimate` (D30) |
| `/Operations` | Operations | AI Job List, Attention required, upload links, EVA handoffs; one-line partial-data notice linking to Administration Service health (D37) |
| `/Administration/{area}` | Administration | `accounts`, `principals`, `configuration`, `mail`, `automation`, `service-health`, `action-logs`, `reports`; bare `/Administration` opens `accounts` |
| `/VehicleImages/{id}` | Image record | The list page is removed (D1) |
| `/Uploads/{token}` | Public upload | External frame |

Route moves are 301 stubs delivered by PLAT-029 and deleted in wave 5:
`/Triage` → `/Cases?tab=triage`; `/Unidentified` →
`/Cases?tab=unidentified`; `/Cases?query…` (the former search) →
`/Search` with the same query string. `/VehicleImages` (list) is removed
outright — no stub — and `ImageIntake/Index` is deleted; the detail page
stays as the image record. Test UI catalogue route keys follow these moves
in the same PLAT-029 change set. `/Cases/{id}/Assessment` →
`/Cases/{id}?section=estimate` is a permanent 301 delivered with the
sections move (D30, ENG-034).

## Component map

The class vocabulary is the one `site.css` declares under the integrated
design. Every page composes these classes; a page-specific class is a defect
unless it is listed here. PLAT-029 delivers the vocabulary and carries the
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
| `workspace-tabs`, `workspace-tab` | Tab strip (max 4 Case tabs, LRU) |
| `external-shell`, `auth-card`, `auth-brand` | Navless frames |
| `skip-link`, `sr-only` | Accessibility |

### Page

| Class | Role |
| --- | --- |
| `page-header`, `page-title`, `eyebrow`, `page-actions` | Header row |
| `btn`, `btn--primary`, `btn--dark`, `btn--danger`, `btn--ghost`, `btn--small` | The one button family; `--primary` is `--red`, `--dark` is `--nav-2`, `--danger` is `--danger` |
| `metric-strip`, `metric-strip--3`, `metric-strip--5`, `metric` | Count buttons linking to `/Cases?tab=` |
| `panel`, `panel-head`, `panel-body`, `panel-body--compact`, `panel-body--tight` | Bordered section |
| `notice`, `notice--success`, `notice--warning`, `notice--danger` | Inline notice: label plus value only |
| `status` and its tone modifiers | State chip ([Colour](#colour)) |
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
| `record-ribbon`, `ribbon-item` | Identity ribbon (Case/PO, Registration, Claimant, Principal, State; Engineer and Sign-off Engineer beside it — D31) |
| `edit-bar` | Sticky edit bar: lease text, Unsaved chip, Discard, Save |
| `case-sticky` | The Case record's sticky block: identity ribbon, action bar and section jump-nav (D29) |
| `section-nav`, `section-link` | Section jump-nav and its links; the link for the section in view carries `aria-current` (D29) |
| `case-workspace`, `case-context` | Case record grid and its context column; the side nav they carried is superseded by `case-sticky` and `section-nav` (D29) |
| `suggest-btn` | Per-field suggestion chip that fills its field when chosen (D34) |
| `damage-diagram`, `impact` | The clickable damage diagram and its zone markers; `impact` marks a zone with recorded damage (D39) |
| `tyre-card` | Tyre and seat belt per corner, spare tyre, centre belt (D39) |
| `valuation-card` | One valuation entry: source, date, time, mileage, guide month (CASE-029), retail, trade (D40) |
| `outcome-option` | Settlement outcome choice (D41) |
| `derived` | A value derived, never entered: impact location and severity (D39), equity, and a permitted ratio line where one is shown (D41) |
| `report-image`, `cropper` | Report-image preparation on the Report section: designated Close-up and Overview, supporting images in order, non-destructive crop (D19, ENG-031) |
| `workflow-stepper`, `workflow-step` | Not ready → Review → With Engineer → Complete; Held badge |
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
| `dialog-backdrop`, `dialog`, `dialog-head`, `dialog-body`, `dialog-foot` | Modal dialog ([contract](#keyboard-and-dialog-contract)) |
| `command-dialog` | Command palette |
| `toast-region`, `toast` | Toasts |

### Utility classes

The Content Security Policy forbids inline styles, so the prototype's
`style=""` attributes become these utilities and nothing else:

`mt-0`, `mt-1`, `mb-2`, `ml-auto`, `cluster--between`, `cluster--start`,
`panel-body--compact`, `panel-body--tight`, `field--narrow`, `no-border`,
`viewer-stage`, `metric-strip--3`, `metric-strip--5`.

The names are fixed here and delivered by PLAT-029; a new utility needs a
second caller and a reason in the ticket plan.

### Shared partials

| Partial | Role |
| --- | --- |
| `_Layout`, `_LayoutAuth`, `_LayoutExternal` | Frames |
| `_LucideSprite` | The inlined sprite |
| `_ShellDialogs` | Account, Add, Notifications, command palette |
| `_AdminNav` | Administration panel nav |
| `_StatusChip`, `_PageHeader`, `_ReasonDialog`, `_ErrorSummary`, `_EvidenceViewer`, `_ImageGallery`, `_UploadOutcome`, `_Provenance` | Retained, restyled to the vocabulary |
| `Presentation/OperatorLabels.cs` | The one label map |
| `Presentation/RailCountsPageFilter.cs` | Rail counts |

## Workspace contract

The per-page contract is the EPIC-011 group `context.md` §1, transcribed
here as the durable statement. Each page is delivered by its wave-2 ticket
against the PLAT-029 shell; wave 4 adds the feature controls that need
wave-3 backend.

### Work Centre `/`

Header "Work Centre", eyebrow "Office-wide work", freshness, Refresh and
Create Case (primary). A five-metric strip (Not ready, Review, Held,
Unidentified, Blocked), each a button to `/Cases?tab=…`. Two panes: left
"Needs attention" — `work-item` buttons carrying kind · reference, title,
priority chip, detail, owner and due; right "Today" / "Selected work" with
"Open full record", the selected item's eyebrow, heading, lead, chip, a
notice "Why this needs attention" (label and Core-derived value only), a fact
grid (Source, Owner, Last recorded outcome, Due) and a "Next permitted action"
panel (Open Case / Triage / Operations / Review source, Copy reference).
Work-item kinds: Case (due chase, blockers, readiness), Mail (Unidentified),
Triage (no finding), External work (retryable failure), Held decision. The
Blocked metric is a real query, never a fixture.

### Inbox `/Inbox`, message `/Inbox/{id}`

List: header "Inbox" / "Retained mail"; filter bar (Mailbox, Folder, Queue
selects, search input, Search dark, Refresh); three panes — Scope (All
incoming, Unread, Receiving work, Case updates, Pre-instructions,
Unidentified, Sent Items, each with icon well and count), Messages (sort
toggle "Received ↓/↑", rows with unread dot, sender, date/time, subject,
excerpt, outcome chip, Case reference or queue · attachments; bounded
pagination), Preview (subject, route, chip, excerpt, attachment chips, fact
grid Classification / Case association / Folder / Search match; Open full
message, Open linked Case).

Message: header subject / "Inbox message" / Back to Inbox; record head;
record bar **Reply (dark), Forward, Compose, Flag, Delete (danger)**. Reply,
Forward and Compose open the composer dialog (To, Subject, Message, Case,
From) and Send creates Sent-Items evidence linked to the Case; Delete asks a
reason and moves to Deleted Items (D4 — gated, production activation approved
separately). Tabs Message / Attachments (n) / Thread / Case. Decision card
(Classification, Destination, Filed to, Folder, Decided · Automatic; Correct
classification; Move to X / Check move status) and a Corrections timeline.
Attachments table (File, Type, Size, Search content, Custody, Preview). Case
tab: summary card, Open Case, Change association.

### Cases `/Cases`

Header "Cases"; filters Principal select and, on Not ready only, a Missing
select (All / Instructions / Images / Both missing), Clear. `queue-layout`:
rail "Case workflow" with groups **Workflow** (Not ready, Review, With
Engineer, Complete), **Pre-Case work** (Triage, Awaiting instruction — the
Image-initiated Cases still awaiting an instruction, D38) and **Exceptions**
in amber (Held, Unidentified), each an icon well and count. Middle pane rows
by kind: case (reference · registration, chip, claimant · principal, origin ·
received, due); image-initiated (reference · registration, files · custody);
triage (reference · registration, provider · assignee); unidentified
(reference · kind, handle, received · reason). Right "Quick detail": a Case
shows eyebrow origin, heading, compact workflow stepper, Outstanding
requirements and Current work (Due, Engineer, Next action, Open full Case);
Triage, Unidentified and image records show a definition list and an open
button. Cases in other terminal outcomes are excluded from this rail (D3).

### Triage `/Triage/{id}`

Header reference / "Triage" / Back to Cases (`/Cases?tab=triage`) and
Refresh. Record head (reference; registration, provider; state chip). Record
bar: eyebrow "Triage" and assignee. Body: Determinations panel
(Roadworthiness select, Repair outcome select, Save determinations primary),
Source panel (Material, Received, Case link), Notes panel (Date / Time / ID
and text; Add note — append-only, no edit and no delete, D25), Files panel
(Name, Kind, Received; View / Download — retained sources, their attachments
and the linked vehicle images, with no upload action, D25). The Notes panel and
the record's durable events read as one chronological History. The existing
server-side transitions — await information, link, complete, cancel, reopen —
stay available through the determinations flow and dialogs where a handler
exists.

### Unidentified `/Unidentified/{id}`

Header reference / "Unidentified" / Back to Cases and Refresh. Record head;
warning notice (the reason); Retained source panel (Permanent reference,
Kind, Operator handle, Received, Source, Canonical reason; View retained
source, Resolve destination dark); History panel. Resolve dialog: Destination
select (Add to existing Case / Create Case from accepted instruction /
Register Image-initiated Case / Close with reason), Case picker, reason.

### Search `/Search`

Header "Search" / freshness and Create Case. Advanced grid: Case/PO or image
reference, Registration, Claimant, Claim/provider reference, Principal, State,
Engineer, Received from/to, Origin, Search (dark), Clear. Two panes: results
table (Case/PO and provider reference, Vehicle with make/model, Claimant,
Principal, Type, State, Due; rows selectable by hover, focus, click or Enter)
and "Selected Case" preview (eyebrow type, heading, chip, Accident
circumstances, fact grid Provider ref / Engineer / Due / Next action,
Outstanding (n), Open Case, Copy Case/PO). Closed cases show
"Closed · <outcome>".

### Case workspace `/Cases/{id}`

The Case record is one scrolling page (D29, 2026-09-02; EPIC-012
`context.md` §Shared decisions governs where it differs from EPIC-011 §1.8
and §1.9). Header reference / "Case record · registration" / Back to Cases
and Refresh. `case-sticky`: identity ribbon (Case/PO, Registration,
Claimant, Principal, State chip; Engineer and Sign-off Engineer beside it —
D31), presence strip, action bar and `section-nav` with scroll-spy;
`?section=` jumps; sections below the fold render lazily; no layout switch.
Action bar: Edit Case | Finish editing and Renew editing | "Editing held by
X until T." | Reopen Case (closed); Place on Hold / Release Hold; Create
upload link; **Send to EVA** (Review; With Engineer as a re-send — D36) →
the EVA handoff dialog (Engineer, Sign-off Engineer; Download ZIP / Send via
API, API disabled unless the Principal enables it); **Report sent** (primary,
With Engineer — confirms detected Sent evidence, D10) / **Return to
Engineer** (Complete); right: Close Case (danger, not Complete). There is no
Download EVA package action (D36) and no Open Assessment action (D30).
Sticky edit bar while editing; one **Edit Case** / **Save** / **Discard** flow
over one lease covers every section, including Files preparation edits.

Sections in order (D30): **Overview, Engineer notes, Inspection, Vehicle,
Damage, Valuation, Estimate, Settlement, Report, Files, Notes**. Every
section is always viewable; Damage, Valuation, Estimate, Settlement and
Report are read-only once Complete.

- **Overview:** workflow stepper (Held exception badge); Outstanding
  requirements (title, Source, Why, Resolve); the edit form when editing
  (Claimant, Provider reference, Registration, Make, Model, Accident
  circumstances); "Case overview" panel — Work facts (Case type, Provider
  reference, Inspection, Engineer, Received, Due), Parties (Principal,
  Claimant, Repairer/holder, Intermediary, Image source, Origin), accident
  card (circumstances, Incident detail, Vehicle).
- **Engineer notes:** Add note (editing only); entries Date, Time, ID and
  text, append-only, no edit and no delete (D32).
- **Inspection:** Inspect at as a fast-update choice — Image Based
  Assessment, Claimant address, Repairer location, Storage location,
  previous addresses used for this principal, Manual entry; an option
  without a value is disabled — and Storage location (D33).
- **Vehicle:** Registration, Make, Model, Year, Mileage, Mileage source;
  one **Look up DVLA & MOT** action; looked-up values render as
  `suggest-btn` chips beside their fields and fill the field when chosen; no
  checks panel and no suggestion table; Run Experian check (disabled seam,
  ENG-001) (D34); "Vehicle History" textarea (`narrative.history_check`).
- **Damage:** `damage-diagram` with a marker per zone — front, left/right
  front, left/right side, left/right rear, rear, roof, four wheels,
  underside, interior, mechanical — each with Severity and Note, no damage
  type (D39; D45); `tyre-card` per corner (tyre, seat belt), spare tyre,
  centre belt; Unrelated damage with Deduction; Paint or material transfer;
  Impact location and Impact severity as `derived` values (D39).
- **Valuation:** source cards, presets, Preview and explicit Apply. Sources
  include Glass's, Brego and Super CAP manual entries, Cazana as a disabled
  seam, Engineer's Value, and AI market research for automation only.
- **Estimate:** shall carry named tabs, discounts, VAT categories, a rate
  snapshot and a Glass's launch button for a configured signed-in Engineer,
  plus whole-page raw import.
- **Settlement:** `outcome-option` (Total loss / Repairable / Cash in lieu /
  Contract repair), Category, Salvage value, Excess, Betterment, Claimant
  VAT registered, Reserve, Equity (`derived`), Repair duration, Delays,
  Report delay, Storage per day, Recovery, Hire start, Hire daily cost,
  Diminution, Salvage logistics; ratio lines are permitted, not required
  (D41).
- **Report:** content switches, report-date override, fee-note preview, image
  preparation (Close-up, Overview, Supporting, Not used), and delivery
  preparation; it also prints the marked damage diagram (D39).
- **Files:** Documents (Add evidence → `/Upload`; rows name, type ·
  size · source, custody chip, Preview, Save as | Open Operations), Vehicle
  images gallery (viewer dialog: Rotate view, Save as), Correspondence
  (Compose, Reply, Forward, Open Inbox; linked message rows).
- **Notes:** Add Case note / Record chase (editing only); entries Date,
  Time, ID (staff username / SYSTEM / AI) and text, newest first — Case
  notes, business events, chase outcomes and AI events merged.

Dialogs: reason (hold, release, close, reopen); Create upload request
(Recipient, Reason; expiry, max files and max size shown as read-only policy
values → one-time secret toast); Record chase (Recipient, Channel, Prepared
content, Disposition, Reason); Case note; Engineer note; Add valuation;
finish-edit; stale-version conflict (current versus proposed); save-in-Review
warning.

### Assessment `/Cases/{id}/Assessment`

The route is a 301 to `/Cases/{id}?section=estimate` (D30, 2026-09-02):
the workbench is the Damage, Valuation, Estimate, Settlement and Report
sections of the Case record, always viewable and read-only once Complete.
The Estimate section carries what follows. Section head: New estimate
(dark), **Send to Claude** (primary); Generate report draft / Preview report
draft sit on the Report section. There
shall have a whole-page raw estimate import and a Glass's launch button for a
configured signed-in Engineer. The parser is auto-detected and fails closed on
ambiguity; Drafts are named by provider plus sequence, and the same Case with
the same hash replays.
`assessment-v3`: "Estimates" pane — estimate tabs (tablist) and editor
(Delete estimate danger, Duplicate, Use estimate / Current chip, Save
estimate dark; fields
Estimate name, Source, Repair days, Labour-rate card, VAT categories, VAT %;
lines table Operation (Replace / Repair / R&I /
Paint / Other), Description, Part number, Qty, Labour h, Paint h, Part £,
remove; notes; totals Parts / Labour / Paint / Other / Subtotal / VAT /
Total). The Labour-rate card select offers the enabled global versioned cards
and prices panel and paint hours. Parts, Materials and Specialist explicit
amounts use their selected VAT categories; Other costs have no VAT category.
VAT % is per estimate (default 20) and applies only to the selected discounted
taxable categories. No comparison
or savings figure appears in the totals or
on the report (D17). Dialogs: Send to Claude (direction textarea, Target
Estimate % slider of Engineer's Value — optional, 0–80 %, no default, and the
derived Target amount shown beside it as proposal guidance only (D24) — Case
Valuation, Target amount; disabled without an Engineer's Value); Delete estimate; Report draft preview;
image viewer.

### Upload `/Upload`, public `/Uploads/{token}`

Upload: header only; dropzone ("Drag files here or choose files" · "EML, MSG,
PDF, DOC, DOCX, JPG or PNG · up to 10 MiB each" · Choose files dark); file rows
(status chip, progress, per-file outcome with Open X / Add to existing Case /
Create Case / Cancel) beneath one submission decision (D20); Upload (primary)
and Clear. Public: external shell, company logo, "Secure file request",
heading "Upload files for REF", request reference and expiry, dropzone, Submit
files; the first successful file starts a fixed non-sliding 15-minute session
for additions or replacements, closed by explicit finalisation or expiry
without naming the Case (D20).

### Operations `/Operations`

Header "Operations"; one-line partial-data notice linking to Administration
Service health — Operations carries no Service health table (D37); **AI Job
List** panel (meta "n jobs", "Send Unidentified to AI" dark; table Job (kind
and detail), Record, Started by, Created, State, Action: Review estimate /
Open query / Review | Complete job | —); **Attention required** (retryable
external work: Case, Work, Item, Attempts, Failure, Retry this work);
**Active upload links** (Case, Recipient, Last activity, Accepted, Expires,
State, Withdraw link); **EVA handoffs** (Case, Route, Engineer, State,
Result). AI job kinds are Estimate, Unidentified resolution, Query response,
Unidentified-queue pass (D5) and MarketResearch (D35); scheduled passes
arrive through the Automation Actor under the `automation.jobs` scope (D6).

### Administration `/Administration/{area}`

`admin-layout`: panel nav — **Staff accounts & roles, Principals, Workflow
configuration, Mail settings, Valuation presets, Automation & AI, Service
health, Action Logs, Reports** | content panel (heading, area label, meta).
Administration shall provide Valuation presets and the per-engineer Glass's
page at `/Administration/Glass/{staffId:guid}`.

- **Accounts:** table Name, Username, Role (inline select), State, Save
  (disabled until changed; reason prompt), Account (Disable danger / Review /
  Reset password → Temporary password, Confirm temporary password, Reason;
  each Engineer row shall link to that engineer's Glass's page.
  forced change at next sign-in, never emailed — D28); Create staff account.
- **Principals:** table Name, Principal Code, Roles, State, Settings; Create
  Principal (creates the backing Organisation inline, D2); Settings dialog —
  route e-mail addresses (read-only), the two independent ADR-0034 EVA
  toggles — Manual API submission, Automatic API submission (ZIP export
  needs no setting) — Pegasus API key (masked, Show / Hide), Generate new
  key (danger → reason), Save.
- **Workflow configuration:** Instruction completeness and Image completeness
  (required / not-required item rules with exact blockers, never a percentage
  — D23), no staff review panel (D44), Due work (Chase interval: whole
  calendar days, 1–365, default 7, Europe/London — D23), Labour-rate cards
  (Name, panel-and-paint hourly rate, State; Create card, Disable card — D17);
  Save configuration.
- **Mail settings:** Approved mailboxes table (Mailbox, Scope, Last update,
  State, Review folders / Refresh) and Mail categories table (Add category).
- **Automation & AI:** Automation panel (status, Registered clients, Active
  jobs, Failed jobs, Stop / Start automation danger → reason) and AI settings
  (Proposal, Timeout, enabled checkbox, Save).
- **Service health:** the only service health table (Area, Service, State,
  Latest evidence, Dependency, Retry / View); Administration-only, Operations
  links to it (D37).
- **Action Logs:** filters (Search, Area, Actor, Result, From, To, sort
  toggle, Clear) and table Time, Actor, Area, Action, Reference, Result.
- **Reports:** From, To, Engineer; Generate / Preview / Export; "Engineer
  Report" table (Engineer, Queries received, Reports). Queries received are
  retained messages classified as post-report e-mails associated with the
  Engineer's cases in the period (D12).

### External frames

Sign in: dark `external-shell`, `auth-card` with the company logo and
"PEGASUS", heading "Sign in to Pegasus", Username, Password, Sign in. The
signed-out, access-denied and error family keep the same card frame. The
public upload page uses the external shell and the company logo.

## Removed surfaces

Removed by the integrated design and deleted by their wave tickets; none is
stubbed, documented as delivered, or kept behind a flag:

- the `/VehicleImages` list page (`ImageIntake/Index`) — the detail page
  stays as the image record (D1);
- Organisations, Staff accounts and Roles as separate Administration areas —
  folded into Staff accounts & roles and Principals (D2);
- the Administration index card page (`Administration/Index`);
- the Automation Activity page — replaced by Action Logs;
- the old Assessment section tabs, the old Triage action bar and the
  Additional case section;
- the Assessment Import estimate dialog and its file picker — replaced by the
  whole-page drop; still shipped, removal owed by ENG-033 (D16);
- the Assessment page `/Cases/{id}/Assessment` — a 301 to
  `/Cases/{id}?section=estimate`; the workbench becomes Case record sections
  (D30, ENG-034);
- the Case workspace side nav and context column, the Open Assessment action
  and the Download EVA package action — superseded by the single-scroll Case
  record and the Send to EVA re-send (D29, D30, D36; CASE-038, CASE-040);
- the Operations Service health table — Service health is
  Administration-only (D37, PLAT-069);
- **Superseded by D21:** direct Glass's valuation-service and Audatex
  service-launch controls are absent; the selected configured-Engineer
  Glass's repair-estimate launch remains on the Estimate section, and file
  import stays in scope;
- a standalone Images list, runtime-managed email or document templates, and
  any autonomous-send control — never built (D21); staff-initiated outbound
  delivery stays in scope under ADR-0036;
- the Dashboard, Queues and combined Cases-search routes, which the 301 stubs
  cover until wave 5.

## Reviewed divergences

Divergences from the prototype are reviewed and recorded, never silent.

### Prototype defects, not reproduced

Recorded from the effective render layer (group `context.md` §1.15):

| Defect | Resolution |
| --- | --- |
| Undefined icons `activity`, `spark`, `reply`, `flag`, `sort` | Lucide `activity`, `sparkles`, `reply`, `flag`, `arrow-up-down` |
| "Create organisation" button | "Create Principal" (D2) |
| Mixed casing of "Eva" | `EVA` |
| Open Assessment offered on Review | The Engineer sections are always viewable and read-only once Complete (D11 as amended by D30); there is no Open Assessment action |
| Work Centre "Filter" button with no handler | Absent |
| Unbounded Inbox "Next" | Bounded pagination with current-page context |
| Fixture-driven "Blocked" metric | A real Core query |
| Unused `.work-today-summary`, `.prototype-note`, `.console-status`, analyst, baseline and `assessment-v2` rules | Not ported |
| Inline `style=""` attributes | The [utility classes](#utility-classes) under CSP |
| Hand-drawn `iconPaths` | The Lucide sprite |

### Retired: the `Send to Claude` flourish

The 2026-08-03 reviewed divergence — a terracotta gradient, 12px radius,
Poppins request, hover lift, blue focus ring, inline sparkle and ember canvas
confined to the `.send-action` control — is retired. Under the integrated
design **Send to Claude** is a `btn--primary` with the Lucide `send` glyph,
the approved red focus ring and no local custom properties, so the recorded
contrast shortfall no longer exists.

## Deferred and absent UI seams

Exact horizon and first-introduction release remain owned by the
[capability inventory](../capabilities.md#capabilities). No future allocation
creates a route, control, workflow, placeholder or dormant implementation
beyond the seams named under [Absent versus disabled](#absent-versus-disabled).

### Deferred integration and intake surfaces

There is no control, route or placeholder for:

- additional provider activation beyond the current source policy;
- `desk@`, `engineers@` or `info@` automatic ingestion;
- legacy DOC, MSG or scan-like PDF OCR extraction;
- automatic matching beyond the operator-directed INT-28/INT-32
  image/instruction pairing at the accepted ADR-0019 bar;
- broader mailbox taxonomy mapping, folder recommendation or suggested
  actions beyond the decision card;
- post-report query/dispute work beyond the AI query-response job;
- AI/vision assistance for vehicle images or damage evidence;
- spreadsheet preparation of future inspection-address/repairer reference
  data;
- direct Glass's valuation-service or Audatex service launch, a standalone
  Images list, runtime-managed templates or autonomous outbound sending —
  each absent, not disabled (D21); the selected configured-Engineer Glass's
  repair-estimate launch remains in the Estimate section (D03);
- AutoTrader scraping or any AutoTrader integration inside Pegasus — the
  `MarketResearch` job is researched by the operator's external connector
  and comes back as Case evidence and a valuation entry (D35);
- a Scroll/Tabs layout switch on the Case record (D29).

Provider APIs and MCP are non-browser boundaries and do not create staff-shell
destinations. The Provider API credential is administered through the
Principal settings dialog only (D8).

### Deferred casework and advanced surfaces

There is no control, route or placeholder for:

- automatic chaser or report sending;
- Diminution or Commercial case workflows;
- automated WhatsApp ingestion;
- replacing EVA assignment or engineering workflow;
- direct Experian, Glass's valuation-service, Audatex, Cazana, finance or
  invoicing integrations — Experian and Cazana keep their named disabled seams,
  while the Glass's valuation-service and Audatex service-launch controls are
  absent (D21); the selected configured-Engineer Glass's repair-estimate
  launch remains in scope (D03);
- guided mobile image capture or third-party guided-capture integration;
- a custom application domain;
- management information beyond the Engineer Report.

AI may propose but must not mutate, accept or send autonomously: every AI
job ends in a staff review action on Operations. Future deterministic outputs
must use one accepted structured case/engineering record, validate accepted
data, calculate once and avoid duplicate truth owners.

### Not planned

The following are permanent absences, not backlog placeholders:

- external/customer accounts;
- public registration;
- staff multi-factor authentication;
- mobile/responsive staff product;
- automated malware scanning;
- document redaction;
- digital signatures;
- automated retention/deletion;
- legal hold;
- subject-access/correction/export/erasure workflow;
- dedicated DPIA/compliance workflow;
- GitHub Actions deployment with scoped OIDC;
- separate staging, QA, UAT, training or demo environments;
- deployment slots/Standard S1;
- private networking, zone redundancy or multi-region failover;
- quarterly restore exercises;
- predecessor data import, predecessor availability after cutover or
  predecessor code reuse;
- SMS or Microsoft Teams integration;
- customer/claimant portal (request-scoped upload links remain permitted; a
  link exposes no case or request state and creates no account);
- independent Engineer accounts;
- solicitor, insurer, repairer or vehicle-owner accounts.

A supported desktop reflow does not alter the permanent mobile-product
boundary.

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

When a planned surface has a real caller, the package-pinned Playwright
Chromium Browser lane records:

1. keyboard-only traversal;
2. semantic structure and accessible-name inspection;
3. focus and error behavior;
4. 1280px-and-wider desktop review;
5. 1024–1279px constrained-desktop review;
6. 200% zoom review;
7. forced-colours review;
8. reduced-motion review;
9. contrast review;
10. automated accessibility scanning through the real caller.

These checks are the selected release accessibility evidence. They do not
simulate Narrator or another screen reader and do not establish screen-reader
interoperability, complete WCAG conformance, subjective usability, or operator
acceptance. Each visible capability/state also needs authenticated Web-caller
and named Core-owner evidence. Generated imagery or synthetic operational
material cannot prove acceptance.

## Source and runtime map

| Concern | Durable owner or source | Runtime consumer or evidence |
| --- | --- | --- |
| Product capability and horizon | [Requirements](../prd/README.md), [capabilities](../capabilities.md) | Routed staff pages as each wave lands |
| Open policy and token questions | [Open decisions](../open-decisions.md) | No implementation inference until resolved |
| Architecture and caller boundaries | [Architecture](../current-architecture.md) | Core, Web, Worker, MCP and external adapters |
| Production, release, monitoring, and recovery state | [Operations](../operations.md) | No deployment claim from design or source presence |
| Setup, testing, release, and recovery procedure | [Runbook](../runbook.md) | Procedure is not execution evidence |
| Engineering procedure | [Engineering](../engineering.md) | Reviewed implementation and verification |
| Design authority | This file | Approved tokens, assets, class vocabulary and page contracts |
| Design contract of record | EPIC-011 group `context.md` §1 and D1–D28; EPIC-012 group `context.md` §Shared decisions D29–D43 for the Case record (Kanmer board) | Transcribed here; the board record is the batch constraint |
| Shell | This file | `src/Pegasus.Web/Pages/Shared/_Layout.cshtml`, `_ShellDialogs`, `RailCountsPageFilter.cs` (PLAT-029) |
| Tokens and vocabulary | This file | `src/Pegasus.Web/wwwroot/css/site.css`, `site.js` (PLAT-029) |
| Font | This file | `src/Pegasus.Web/wwwroot/fonts/inter/` (PLAT-029) |
| Master logo | `docs/design/brand/logos/logo_no_margin.png`, checksum above | Renderer Core and the checksummed Web copy embedded by `_LayoutExternal.cshtml` |
| Renderer templates/style | Repository renderer asset sources | Embedded by `src/Pegasus.Infrastructure`; Core owns report policy and accepted presentation values |
| Engineer signatures | Repository renderer signature sources; the Sign-off Engineer account setting holds the signature image (D31, superseding D18) | Rendered as the Case's sign-off tuple by the renderer (DOCS-017); none is Web decorative imagery |
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

## Operator experience requirements

Status: **Planned — the Integrated Operations Workspace, to be delivered by
PLAT-029 (wave 1) and EPIC-011 waves 2–5.** This is the canonical publication
of the reviewed contract. Selection does not prove a staff caller, deployment
or acceptance.

### Evidence state and scope

The implemented route set is owned by
[architecture — current callers](../current-architecture.md); the desktop
evaluator is separately owned
([ADR-0016](../adr/0016-standalone-desktop-email-evaluator.md)).
Implementation state does not by itself prove deployment or operator
acceptance.

The intended setting is a small office of approximately eight users. Staff
accounts use Pegasus-managed usernames and passwords; authenticated Web
callers derive the actor and roles server-side. Core owns the exact
[staff role access matrix](../frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix),
automated-actor boundary, and
[case edit authority and recovery](../frd/frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery);
this design must not create broader permissions or a second role policy. The
actor boundary table is under [Access and permissions](#access-and-permissions).

### Flows

**Work** starts on the Work Centre: office-wide counts and the items that
need attention, each opening its exact record or `/Cases?tab=` group.

**Inbox** is the retained-mail workspace: scoped browsing, bounded
pagination, a preview that changes no state, and a message record whose
decision card is the only place classification, association and folder moves
happen — one exact message, never bulk. Outbound Reply, Forward and Compose
retain Sent evidence linked to the Case (D4).

**Cases** is the workflow viewer: Not ready, Review, With Engineer and
Complete as Case stages; Triage and Awaiting instruction as pre-Case work
(D38); Held and Unidentified as exceptions. Triage and Unidentified records
open on their own routes and never become Case states.

**Case** is read-only until an explicit edit lease, and is one scrolling
page (D29): the sticky identity ribbon keeps Case/PO, registration,
claimant, principal, state, Engineer and Sign-off Engineer visible, and the
section jump-nav marks the section in view. Outstanding requirements name
their field, source, reason and resolution. Lifecycle actions are the named
Core outcomes: hold, release, close with reason, reopen with reason;
`Created in error` offers only its linked replacement. Report sent is
evidence-driven (D10); the Engineer sections are always viewable and
read-only once Complete, and `/Cases/{id}/Assessment` is a permanent 301
(D30); Send to EVA is offered in Review and re-sent from With Engineer with
Download ZIP or Send via API (D36).

**Search** runs the advanced query and previews the selected Case; closed
cases show their outcome.

**Operations** is the staff-wide work ledger: AI jobs ending in staff
review, retryable external work, upload links and EVA handoffs; Service
health is Administration-only and Operations links to it (D37).

**Administration** is Administrator-only and implements the linked role
matrix through its eight areas. No generic rules editor or cloud
operation, bulk import or bulk Case edit.

### State matrix

| Scope | Explicit states |
| --- | --- |
| Queries | loading; empty; success; stale/partial with last-good time; transient error/retry; unauthenticated/disabled/stale-role/denied |
| Mutations | validation; confirmation; success; denied; stale version; lease lost; dependency unavailable; idempotent/replayed result; conflict and recovery |
| Upload and received material | empty/oversize; replay; retention/custody failure; per-file outcome (Open / Add to existing Case / Create Case); Unidentified; Unsupported; Blocked with reason; refusal with no case/reference; public-link expired/revoked/cross-request/limit/abuse |
| Triage | registration missing; unassigned/assigned; every named state; missing/ambiguous/unapproved/technical reply evidence; finding replacement/correction/new response; cancel/reopen/link/unlink/relink |
| Case | Not ready/chasing; Review; With Engineer; Complete; Held/preserved interval; due/overdue; gate refusal; documents locked; Box/external-effect states; EVA exported/sent/detected; report evidence absent/ambiguous/detected; every terminal outcome; reopened; Created-in-error nonreopenable; lease held/expired/lost/stale |
| AI jobs | created; running; awaiting review; completed; failed; cancelled |

The UI presents the
[Core-owned permanent action history](../frd/frd-04-parties-accounts-and-access.md#permanent-action-history)
with enough actor, time, outcome, reason, and before/after context to
understand each business event. Routine views, refresh/polling, retries,
leases/heartbeats, and adapter/Worker mechanics stay out of the Notes and
Action Logs panels.

### Accessibility, desktop and data boundary

Use semantic landmarks/headings/tables, labels and associated errors, keyboard
operation, visible focus, screen-reader announcements, practical 44px targets,
forced-colours and reduced-motion support; state is never colour-only. At
1580px the content is centred; the [breakpoints](#spacing-layout-and-breakpoints)
reflow down to a single column at 760px. Mobile staff UI is **Not planned**.

The visual boundary is the [token table](#tokens): cool light ground, white
panels, dark navigation, red primary, amber exceptions, navy Review, blue
in-progress, green completion, Inter Variable, 3px corners, Lucide glyphs
and the commissioned marks. Do not expose Azure, OCR, AI mechanics, queues or
implementation mechanics in operator copy.

Evaluation and operator review use approved genuine local immutable material
only. Do not invent operational inputs. Every deferred `Next` or `Later`
capability carries its exact target in the
[capability inventory](../capabilities.md#capabilities) and has no control,
navigation, workflow, or placeholder beyond the named disabled seams.

## UI specification

Status: **Specification for the Integrated Operations Workspace, planned.**
The per-page contract is the [workspace contract](#workspace-contract) above;
this section holds the cross-cutting rules every page is held to.

### Shared shell and hierarchy

1. Shell: rail, utility bar, workspace tabs, account dialog.
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
| Request-scoped upload | Staff create a temporary token bound to one request and server-enforced expiry. The public page exposes bound upload fields and an immediate request-local result only. |
| State action | Permitted transition, prerequisite, consequence, required reason, recovery and history link; never generic Close. |
| Readiness blocker | Every unmet requirement names its exact field or material, source, reason, and permitted resolution; no opaque aggregate blocker. |
| Identity ribbon | Read-only Case/PO, registration, claimant, principal, state, with Engineer and Sign-off Engineer beside it (D31); sticky on the single-scroll Case record (D29). There is no separate Assessment ribbon (D30). |
| Inspection address | Provider-determined default; reasoned per-Case override; previous values selectable. |
| Estimates | Each estimate has its own VAT percentage (default 20) and selected VAT categories; VAT applies to selected discounted Labour, Parts, Materials and Specialist categories. Unknown repairer VAT blocks Use as Current until an explicit status or categories are recorded; totals compute once in Core. |
| Evidence/document panel | The stored case files themselves — name, type, size, source, custody chip, preview, download; a reasoned removal recorded on the timeline; exact Sent evidence with separate discovery, link and sent times. |
| Evidence image preview | Loading and source-preserving enlarged-image states are explicit; Rotate view is a viewer-local transform. |
| Mail preview | Keyboard and pointer intent exposes an accessible preview that changes no message or Case state; when intent moves away the pane restores the selected message and stays visible with its navigation links. |
| Mail refresh | No automatic refresh while an operator is reading or acting. Manual refresh retains scope, page and open message where available. |
| Lease/conflict | Holder/expiry/recovery, read-only alternative, current conflict and preserved proposed values. |
| History | Business mutation/accepted evidence/export/material business failure only; no routine views, polling, retry, lease heartbeat or telemetry. |
| Reason dialog | Named requirement/consequence, labelled reason, confirmation/cancel, initial focus, focus containment, Escape where safe and focus return to the invoking control. |

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
   ([Absent versus disabled](#absent-versus-disabled), D7).
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

- "Blocked — a reason is required."
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
handling. The wave-5 final walk records the browser proof at 1580, 1100 and
760px. Mobile is `Not planned`.

When implemented:

- each visible row and state needs authenticated Web-caller and named
  Core-owner evidence;
- the package-pinned Chromium Browser lane records keyboard, focus/error,
  forced-colours, reduced-motion and the three widths; screen-reader-compatible
  semantics remain required behavior, but screen-reader interoperability is
  not part of the selected evidence;
- operator review uses approved genuine local immutable material only;
  generated imagery or synthetic operational material cannot prove
  acceptance; and
- every UI capability beyond this contract re-enters specification,
  independent review and explicit approval before its route, control or
  workflow is added.
