---
name: Pegasus Operator Interface
colors:
  surface: '#f7f6f4'
  surface-bright: '#ffffff'
  surface-container-lowest: '#ffffff'
  surface-container: '#f7f6f4'
  on-surface: '#16191d'
  on-surface-variant: '#6b6b6b'
  inverse-surface: '#1b1e23'
  inverse-on-surface: '#ededee'
  outline: '#d8d5d1'
  outline-variant: '#e6e4e1'
  primary: '#db0816'
  on-primary: '#ffffff'
  primary-container: '#fceeef'
  on-primary-container: '#8f1422'
  secondary: '#143a5e'
  on-secondary: '#ffffff'
  secondary-container: '#e7f0f8'
  on-secondary-container: '#143a5e'
  tertiary: '#7a3e00'
  on-tertiary: '#ffffff'
  tertiary-container: '#fff4d6'
  on-tertiary-container: '#7a3e00'
  error: '#db0816'
  on-error: '#ffffff'
  error-container: '#fceeef'
  on-error-container: '#8f1422'
  success: '#16833b'
  success-container: '#e8f3ec'
  plate-yellow: '#fcd116'
  on-plate: '#16191d'
  plate-border: '#d9b012'
  background: '#f7f6f4'
  on-background: '#16191d'
typography:
  metric-numeral:
    fontFamily: system-ui
    fontSize: 28px
    fontWeight: '700'
    lineHeight: 31px
    letterSpacing: '0'
  page-title:
    fontFamily: system-ui
    fontSize: 20px
    fontWeight: '700'
    lineHeight: 26px
    letterSpacing: '0'
  section-heading:
    fontFamily: system-ui
    fontSize: 15px
    fontWeight: '700'
    lineHeight: 20px
    letterSpacing: '0'
  body-base:
    fontFamily: system-ui
    fontSize: 13.5px
    fontWeight: '400'
    lineHeight: 20px
    letterSpacing: '0'
  body-small:
    fontFamily: system-ui
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 18px
    letterSpacing: '0'
  eyebrow-caps:
    fontFamily: system-ui
    fontSize: 11px
    fontWeight: '700'
    lineHeight: 14px
    letterSpacing: 0.08em
  plate-ref:
    fontFamily: ui-monospace
    fontSize: 13px
    fontWeight: '700'
    lineHeight: 16px
    letterSpacing: 0.04em
rounded:
  sm: 5px
  DEFAULT: 6px
  plate: 4px
  full: 9999px
spacing:
  unit: 4px
  xs: 4px
  sm: 8px
  md: 12px
  lg: 16px
  xl: 24px
  gutter: 24px
  table-row: 32px
  fact-row: 28px
---

# Design System: Pegasus Operator Interface

This is historical comparison material. Current interface requirements and
component rules are owned by [the design authority](../docs/design/README.md).
The accepted v3 design and later operator decisions supersede conflicting
layout, signatory, account-review and Triage rules in this captured guide.

Pegasus is Collision Engineers' case-management system for independent vehicle
assessors. Its users are trained staff working queues of email intake, vehicle
cases, triage and engineer assessments all day on desktop monitors. This is a
**cockpit, not a brochure**: every design decision serves scanning speed,
state clarity and evidential trust.

## 1. Visual Theme & Atmosphere

Warm-neutral operational calm with one disciplined jolt of signal red. The
canvas is a warm paper grey (#f7f6f4) carrying white panels lifted by a
hairline border and a very low shadow — depth is whispered, never dramatic.
Density is high ("cockpit dense", 8/10): 32px table rows, 28px fact rows,
13.5px body text, and a screen's identity, state, actions and primary content
fit above the fold at 1280×800. Variance is low (3/10): predictable,
grid-aligned, symmetric layouts are correct here — operators build muscle
memory, so surprise is a defect. Motion is near-static (1/10).

The single moment of theatre is the **record band**: a near-black charcoal
header band (#1b1e23) on case-record screens, so a screen about one record
reads as "a record being worked". It is the only filled dark surface in the
light theme and it never uses the brand red. Collision red (#db0816) is spent
sparingly on exactly four things: primary actions, active navigation, blocked
states, and the focus ring — scarcity is what keeps it loud.

A dark left navigation rail (inverse surface #1b1e23, red active item, white
text) is an approved shell direction for dense worklist screens: dark rail,
light content, generous top toolbar with global search.

## 2. Color Palette & Roles

### Primary Foundation
- **Warm Paper** (#f7f6f4) — page background; recessed grounds and read-only zones
- **Panel White** (#ffffff) — cards, panels, table bodies, form surfaces
- **Hairline** (#e6e4e1) — 1px structural borders everywhere; **Strong Hairline** (#d8d5d1) for control edges
- **Record Band** (#1b1e23) — the one dark surface: record headers and the optional dark nav rail; text on it is **Band Bright** (#ededee), secondary text **Band Mute** (#a7a9ad)

### Accent & Interactive
- **Collision Red** (#db0816) — the single accent. Primary buttons, active navigation (always paired with weight + underline, never hue alone), blocked states, focus ring (3px at 38% opacity). **Pressed Red** (#8f1422) for hover/active. **Red Tint** (rgba(219,8,22,.07)) for blocked backgrounds.
- Saturated but disciplined: red never decorates, never gradients, never fills large areas.

### Typography & Text Hierarchy
- **Ink** (#16191d) — primary text (never pure black)
- **Charcoal** (#2c2a27) — button labels, secondary headings
- **Muted** (#6b6b6b) — metadata, captions, table headers, sender domains

### Functional States (tinted chip triads: fg / bg / 1px border)
- **Pending Amber** — #7a3e00 on #fff4d6, border #a15c00 — incomplete, not-ready, "New"
- **Review Navy** — #143a5e on #e7f0f8, border #365f87 — review states, informational classifications, table links
- **Blocked Red** — #8f1422 on red tint, border #db0816 — blocked, failed
- **Confirmed Green** — #16833b on #e8f3ec — **reserved exclusively for confirmed completion.** Green never means progress, availability, or generic positivity.
- **Neutral** — muted on paper — unavailable, loading, cancelled, historic

### Signature: VRM Plate
- **Plate Yellow** (#fcd116) with **Ink** (#16191d) text and a 1px **#d9b012** border — the UK-number-plate badge for vehicle registrations (see §4).

## 3. Typography Rules

- **One face**: the system UI sans stack (`ui-sans-serif, system-ui, "Segoe UI", Roboto, Arial`). No webfonts are loaded — this is a deliberate, recorded decision of the design authority (fast, native, zero FOUT), not an oversight. Do not substitute Inter, Geist, or any loaded font.
- **Operational scale, not marketing scale**: page title 20px/700 · section 15px/700 · sub 14px/650 · body 13.5px · small 13px · caption 12.5px · eyebrow 11px/700 uppercase with 0.08em tracking. Nothing larger than 28px exists except nothing — there are no hero sizes.
- **Numbers are data**: all counts, metrics, dates and references render with tabular numerals (`font-variant-numeric: tabular-nums`) so columns align. Vehicle registrations and internal references may use the monospace stack, bold, slightly tracked.
- Hierarchy is carried by weight, case and colour — not size jumps. Table headers: 11px, 700, uppercase, muted, on paper.
- Line length for prose (email bodies, notes) capped near 65ch; truncate list-row subjects with ellipsis after two lines.

## 4. Component Stylings

- **Buttons**: compact (5px × 10–12px padding, 34–40px tall), 5px radius, 1px strong-hairline border, white fill, charcoal 600-weight label, optional 14px leading icon. **Primary**: solid Collision Red, white text, darkens to Pressed Red on hover — flat, no glow, no gradient. **On-band variant**: transparent with 30% white border. **Disabled actions stay visible** (greyed with stated condition) — removing an action falsely says it can never be done.
- **Status Chips**: pill (999px), 1px state-colour border, tinted background, 12.5px 650-weight label, optional 12px icon. Exactly the five sanctioned tones in §2. A chip is the *only* way state is communicated in rows and cards.
- **VRM Plate Badge** (signature component): a small rounded rectangle (4px radius) in Plate Yellow #fcd116 with bold, uppercase, slightly-tracked ink lettering in the monospace stack, 1px #d9b012 border, sized like a real UK plate cut-down (approx 84×22px in table rows). An optional 6px navy-blue leading band may echo the plate's country strip, kept purely decorative. Used wherever a vehicle registration identifies a row or record. Internal references (Case/PO, Image reference) use a **white** plate variant: same shape, panel background, hairline border, ink monospace text — so vehicle identity and internal identity read as siblings, yellow vs white.
- **Queue Cards**: white card, 1px hairline, **3px state-coloured top border**, 84px min-height; large 28px tabular count, 13px 650-weight label below, status chip beneath; grid-packed with shared borders (-1px overlap). Amber/blue/green/red/unavailable variants follow the state rules.
- **Metric Tiles**: like queue cards but with a 34px rounded icon square in the state tint; attention variant borders in amber.
- **Panels**: 16px padding, 6px radius, 1px hairline, very low shadow (0 1px 2px at 6%). Cards only where elevation communicates hierarchy — inside dense tables and lists, hairline dividers do the work instead.
- **Tables** (the workhorse): 32px rows, hairline row borders, header row on paper in 11px uppercase muted, hover row washes to paper, links in Review Navy 700. Every actionable row is one full-row click target with a visible affordance at the row end. Status chips keep their pill shape inside cells. Column count earns its keep — no decorative columns.
- **Forms**: label above input, hint below, error in red below that; inputs 1px strong-hairline border, 5px radius, white fill, red 3px focus ring at 38% opacity; checkboxes/radios accent in red; file inputs dashed-border drop zones. Validation summaries list one unmet requirement per line, each naming its field and resolution.
- **Dialogs**: centred, 6px radius, deep soft shadow, scrim of ink at 62%; a reason textarea and an explicit action pair — never a bare confirm.
- **Freshness Banner**: full-width quiet strip above worklists announcing data age (loading / stale / failed variants) — evidential trust is a feature.
- **Empty States**: composed and instructive — an icon, one sentence naming what would appear here, and the action that populates it. Never a lone "No data".
- **Loading**: skeleton rows matching the exact table/card dimensions. No spinners.

## 5. Layout Principles

- Desktop application, 1280px minimum design width; shell content constrained to 1440px. Dense multi-pane layouts at ≥1280px. At 1024–1279px and 200% zoom, secondary panes reflow into labelled tabs or stacked sections without losing state, labels or actions. **There is no mobile product** — do not generate phone layouts.
- Grid-first: dashboard 2-column; worklist screens split 2fr/1fr (the list leads, the form or detail follows — an even split starves the table); record screens use a full-width band header then a fact-grid body.
- Shell: either the current light top bar (white, hairline bottom border, brand left, nav links right, red active underline) or the approved dark left rail (see §1) with grouped, eyebrow-labelled sections (OVERVIEW / TRIAGE / INTAKE / QUEUES / ADMIN), count badges right-aligned, red active item.
- Spacing rhythm: 4px base — 4/8/10/12/14/16/20/24/40. Primary gutters 24px. Section gaps 24px, never more than 40px — vertical generosity is marketing, not operations.
- Above-the-fold contract: identity, state, primary content and actions visible at 1280×800 without scroll.
- No overlapping elements, no decorative absolute positioning.

## 6. Motion & Interaction

- Near-static. The product's one animation is the refresh feedback pulse; everything else moves only as instant state change or ≤150ms opacity/transform ease.
- No perpetual micro-loops, no shimmer, no floating, no staggered cascade reveals — operators read these as instability. (Deliberate override of generic "premium motion" advice.)
- `prefers-reduced-motion` removes even the refresh pulse.
- Interactive feedback is tactile and instant: hover darkens, active presses 1px, focus rings appear immediately.

## 7. Voice & Vocabulary (strict)

UI copy is quiet, factual, and uses the operator vocabulary — never internal domain names:

- "Received item" (never "Intake receipt") · "E-mail activity" (never "Intake queues") · "Blocked" (never "Blocked intake") · "Vehicle images" (never "Image intake") · "Image reference" (never "Image Intake Reference") · "Case stage" (never "State")
- The word **"intake" never appears** in operator-facing text.
- Say "Case", never "Job". "Principal", never "Client". "Case/PO", never "Claim number". "Image Based Assessment" always written out in full.
- Buttons name the action's object: "Create case", "Add evidence", "Record finding" — no "Submit", "OK", "Learn more".
- Sample data must be plausible UK casework: VRMs like "EJ17 NBZ", references like "576059", principals like solicitors' and claims firms, dates DD/MM/YYYY, times Europe/London. Counts are believable and irregular (77, 36, 3 — never 100, 50%).

## 8. Anti-Patterns (Banned)

- Green for anything but confirmed completion
- Red used decoratively, or state signalled by colour alone (always pair with icon/label/weight)
- Gradients of any kind; neon or outer glows; purple/blue "AI" aesthetics
- Pure black (#000000); cool slate greys mixed into the warm neutral set
- Marketing patterns: heroes, oversized display type, 96px section padding, three-equal-card feature rows, centred landing layouts, "Learn more" links
- Webfonts, Inter, serif faces anywhere
- Emojis; spinners; skeleton-less loading; toasts for errors that belong inline
- Invented metrics or fake percentages; "John Doe"/"Acme" sample data
- Mobile/hamburger layouts; horizontal scroll on content
- More than one primary button per view region

## 9. Design System Notes for Stitch Generation

- Lead prompts with role and density: "dense desktop operator worklist for a vehicle-assessment case system, warm paper ground, white panels, hairline borders, 32px table rows".
- Name the state language explicitly: "amber pending chips, navy review chips, red blocked chips, green only for completed".
- Ask for the signature identifiers: "yellow UK-numberplate VRM badges in monospace, white plate-style badges for internal references".
- For record screens: "near-black charcoal record band header with white title, metadata row and on-band outline buttons; light fact-grid body below".
- For shells: "dark charcoal left rail with eyebrow-labelled nav groups, right-aligned count badges, red active item" or "white top bar with red active underline".
- Iterate one region at a time (toolbar, then table, then rail) — whole-screen re-rolls lose the density contract.
