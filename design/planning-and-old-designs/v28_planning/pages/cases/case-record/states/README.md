# Case record states

Every `cr*`-prefixed mockup-strip control (`pages/case_record.strip.html`)
and what it drives. All are read by `case_record.js`'s `frameState()`/
`updateFrame()` unless noted.

| Key | Values | Drives |
| --- | --- | --- |
| `crstate` | `notready`, `held`, `review`, `engineerprep` (Report preparation), `engineerpost` (Post report), `completed`, `query`, `closedcancelled`, `closedrejected`, `closederror`, `closedunlinked` | The ribbon state chip and its "review on" suffix (Held), the stepper stage, `CanEditCaseData`/`IsPostReportReadOnly`, the Actions-menu item set, Overview's Lifecycle actions and outstanding-requirements block, Notes' Add-note availability, the aside's Next action. |
| `crcasetype` | `inspection`, `audit`, `inspectionaudit` | The ribbon identity (switches between the fixture sheet's two Cases), the Case type chip, the Original case / Audit case links, Create audit's menu gate. |
| `crhasreport` | `no`, `yes` | Whether a report has ever been generated on an Inspection + Audit Case — the `HasGeneratedReport` half of Create audit's gate. |
| `credit` | `off`, `on`, `colleague` | The page-wide edit session: `off` is the claim-lease "Edit Case" control; `on` opens the session (`.is-editing` on `#case-record`, the Editing badge, Cancel/Save); `colleague` shows the colleague-editing lock chip and the "<name> is editing" availability sentence on every section. |
| `crarchived` | `no`, `yes` | Whether the Case is archived — locks `CanEditCaseData` even inside an open session, hides Archive from the menu, shows the Archived chip. |
| `crglass` | `none`, `active`, `awaiting`, `completed`, `failed`, `elsewhere` | The Estimate head's Glass's/Resume slot, the Glass's session line (state chip, guarded Close, Complete import), and the section's outcome notice. |
| `crproposal` | `off`, `on` | Whether Settlement's Decisions strip carries a reviewed AI proposal column (Awaiting/Accepted, Accept / Accept all). |
| `crreport` | `none`, `generated`, `stale`, `deliveryprepared`, `sent` | The Report section's generation card, the stale bar under the sticky block, Prepare delivery / Send prepared report, and the Report Actions-menu items' gate proxies. |
| `crcustody` | `confirmed`, `pending`, `failed` | The Files section head's custody chip. |
| `crscroll` | `scroll`, `tabs` | The section row's Scroll/Tabs switch: `data-layout` on the record, which section is `.is-active` (Tabs hides the rest via the live `.record[data-layout="tabs"] .record-section:not(.is-active)` rule), and whether section-nav links scroll or switch tabs. |
| `crview` | `record`, `evasend` | Which top-level block shows: the Case record itself, or the standalone `/Cases/{id}/Eva/Send` fallback page. |
| `role` (shared/global) | `Administrator`, `Engineer`, `User` | The signed-in operator name/initials in the shell; not Case-record-specific but affects "Assign to me" visibility on the Hand to Engineer dialog. |

## Not modelled as separate toggles

- **AssessmentCanOpen / AssessmentIsReadOnly** — folded into `crstate`
  (Engineer sections are treated as assessable from Review onward; see the
  page README's Notes).
- **OwnLeaseHeldElsewhere / Take over** — the live "Take over" wording for a
  second window holding the same operator's own lease is not a distinct
  strip state; only the three `credit` values are modelled.
- **CanAssignToMe** — read directly from `role` (Engineer only), matching
  `actor.IsInRole(StaffRole.Engineer)`.
