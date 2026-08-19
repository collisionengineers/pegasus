# Design authority

This file is the durable authority for Pegasus visual design, Web interaction contracts, approved assets, component and pattern boundaries, and source-to-runtime mappings. Product scope and business capability remain owned by [requirements](../prd/README.md) and [capabilities](../capabilities.md); architecture and deployed state remain with [architecture](../current-architecture.md) and [operations](../operations.md), procedures with the [runbook](../runbook.md), operator truth with [operator notes](../operator-notes.md), and repository workflow with [engineering](../engineering.md).

## Evidence discipline

Intended, planned, implemented, caller-proved, deployed and accepted are distinct:

- **Planned `0.1.0-alpha.1`** describes the approved target contract. It does not prove an authenticated Web caller, deployment or operator acceptance.
- **Implemented** means code or an asset exists. Imported workspace code is not automatically a Pegasus caller.
- **Caller-proved** requires a real route or other named caller exercising the behavior.
- **Deployed** requires deployment evidence; none is inferred from implementation.
- **Accepted** requires the specified accessibility and operator review evidence.
- The three retained comparison rasters record the shell-selection comparison. Operations-first is the selected strategy; raster pixels and details are not design approval or runtime evidence.

The implemented offline QDOS-alpha surface assigns authenticated manual receipt/list/detail/source work to `/Intake`, `/Intake/{id}`, and `/Intake/{id}/Source`, and exposes token-bound public request submission only at `/Uploads/{token}`; the desktop evaluator is separately owned ([ADR-0016](../adr/0016-standalone-desktop-email-evaluator.md)). Implementation is not deployment, accessibility acceptance, or operator acceptance evidence.

Detailed durable product-design owners are the
[operator-experience requirements](README.md#operator-experience-requirements) and
[UI specification](README.md#ui-specification). Per-capability ownership and
activation boundaries are owned by the
[capability inventory](../capabilities.md#capabilities) alone.


## Product direction

The application is an operational, restrained, desktop-first internal case-management tool for a small office of approximately eight users. It is not a marketing site, document system, mobile product or general-purpose command centre.

**Operations-first was selected on 2026-07-27 for the planned `0.1.0-alpha.1` shell and landing strategy.** This approves the route hierarchy and operating model, not pixel-for-pixel reproduction of a comparison raster and not a partial implementation.

The authenticated routes are:

1. Dashboard
2. Inbox
3. Upload
4. Queues
5. Cases
6. Operations
7. Administration, visible only to authorised Administrators
8. authenticated user/sign-out controls

This order was settled by the operator on 2026-08-04 and shipped in releases
6 and 7. It supersedes the earlier planned order
(`Operations → Intake → Triage → Cases → Administration → Search`): Search
merged into Cases, which has the identical backing query; the combined intake
screen split into Inbox and Upload; and `Triage` stopped naming a route while
keeping its settled meaning as a pre-case entity inside Queues.

Operations is the staff-wide information workspace. It contains three sections:
retryable external work, active internally generated upload links, and
informational AI operations copy. Empty and bounded-result states are explicit;
Operations has no general receipt filter or email ledger. The separately planned
principal-scoped provider API is not represented by the staff Automation/MCP
ingress. Safe upload-link withdrawal and existing retry feedback retain
antiforgery, reason, lease, version, actor, idempotency and PRG behaviour.

The common hierarchy is:

1. authenticated identity, role, navigation and sign out;
2. surface title, exact queue or filter, freshness and safe primary action;
3. operational table, workbench or record;
4. named workflow, evidence, lease or exception state and consequential action;
5. provenance, external identity, permanent business history and limitations.

### Authenticated shell: the operator rail

**Adopted 2026-08-17.** The authenticated shell is a 236px left rail, not a top
bar. The routes keep the settled order and the rail carries their outstanding
counts, so an operator sees where the work is without opening anything. The
route list, the conditional Inbox item and the Administrator-only Administration
item are unchanged; only their placement is.

Two consequences are recorded rather than assumed:

- **The current route's non-colour signal is a left border, not an underline.**
  `aria-current="page"` and the weight change are unchanged, so the route is
  still identifiable without hue; the underline moved to the rail's leading
  edge. Under 1024px the rail lies down into a horizontal bar and the border
  moves to the bottom edge, so the signal survives the reflow. Nothing is
  hidden at any width.
- **A rail count is a figure a page already queried**, never one the shell
  invents. An absent count renders nothing at all — a shell-level `0` would be
  exactly the stale zero placeholder the operator-experience requirements
  forbid.
- **The content region is bounded and centred** (operator decision,
  2026-08-19). `main` is capped at 1280px, carries the 24px gutters, and sits
  centred in the space beside the rail, so a wide monitor shows equal margins
  either side rather than every screen pressed against the rail with the right
  of the display empty. It is not stretched to fill: a table's far column
  belongs within a glance of its first. Below the cap nothing moves.

`_LayoutAuth` and `_LayoutExternal` are unaffected: sign-in, the signed-out
confirmation, access denied, the error family and the one screen a third party
sees are not places in the application and keep their navless or brand-only
frame.

### The Pegasus marks

**Commissioned by the operator; adopted 2026-08-17.** Fourteen purpose-drawn
raster marks, supplied with the design, live in
`src/Pegasus.Web/wwwroot/images/marks/`. They are a second, deliberate class of
imagery, and the earlier blanket statement that no imagery is needed for the
internal application is narrowed to exclude them: it still holds for marketing
photography and for generated or substitute glyphs, neither of which these are.

They do not replace the Lucide sprite and do not compete with it. The division
is by job:

- **A Lucide glyph names a thing inside a row** — an action, a state, a
  provenance word. It is 16px, inline, and one glyph means one thing everywhere.
  The sixteen registered below remain the only glyphs used that way.
- **A mark names a whole surface** — an administration workspace, an empty
  result, the product itself. It is 30–112px, sits beside a heading or above a
  sentence, and carries detail no line glyph holds at that size.

Every mark is decorative: `aria-hidden`, empty `alt`, always beside text that
already says the same thing, so nothing is lost with images off. None is used
for a semantic action or state, so the one-icon-per-meaning rule is untouched.

Current uses: the eight administration workspace cards (`accounts`, `roles`,
`access`, `organisations`, `principals`, `configuration`, `mailboxes`,
`automation`); the Inbox and Queues empty states (`mailboxes`, `checkmark`); and
the product lockup in the rail and on the forced password-change card
(`pegasus-lockup`). `activity`, `brand`, `calendar` and `casefolder` are
supplied and not yet placed.

They are approved assets and belong in the register below once their bytes are
in the tree, on the same terms as the sprite: recorded name and SHA-256, and no
silent substitution.
#### Pegasus marks source-to-runtime mapping

Upstream source: Claude Design project `710bb42f`, `assets/icons/` (1024×1024
RGBA PNGs). Runtime destination: `src/Pegasus.Web/wwwroot/images/marks/`
(128×128 Lanczos downscale, decorative `aria-hidden` with empty `alt`).

| Mark | Upstream source & SHA-256 | Runtime destination & SHA-256 | Mapping & usage |
| --- | --- | --- | --- |
| `pegasus-lockup.png` | `PegasusDesign/assets/icons/pegasus-lockup.png`<br>`C8F3551841AACA26AAE4F959B263DBB2409EB44A327207F8078D85A1F33668A7` | `src/Pegasus.Web/wwwroot/images/marks/pegasus-lockup.png`<br>`938C22B0F0FC621DC6FADD57748BA858CD1235292581AE47705A4ED336140EF0` | Lanczos downscale to 128×128. Rail brand lockup and forced password-change card. |
| `accounts.png` | `PegasusDesign/assets/icons/accounts.png`<br>`AFFA12B7C8609B253AAFB38304F503F83B868DD817902B53ADDFAE65A3E353A1` | `src/Pegasus.Web/wwwroot/images/marks/accounts.png`<br>`A8D467B827E0F19A6066640FA98A75D3673DA8A8C7642C4190D59BD5EDB718D5` | Lanczos downscale to 128×128. Administration → Staff accounts. |
| `roles.png` | `PegasusDesign/assets/icons/roles.png`<br>`D3B970330A7DDFE1BE3BD92AF8C8B682B63E2270BF5537F3D5CE60EA6B0A97C0` | `src/Pegasus.Web/wwwroot/images/marks/roles.png`<br>`D942967041CFB7A7460015572B658AC483121272F7CFC0194F68A123B71BEBF0` | Lanczos downscale to 128×128. Administration → Staff roles. |
| `access.png` | `PegasusDesign/assets/icons/access.png`<br>`371C4EF84A9E91F8E6509ACCFF045C68121147C22CDCD12D6D6509EF244CEC7F` | `src/Pegasus.Web/wwwroot/images/marks/access.png`<br>`70C98AE7591D467CA455BC481EA37963C67CBB1A8571A7EF823049054DB08C4D` | Lanczos downscale to 128×128. Administration → Access review. |
| `organisations.png` | `PegasusDesign/assets/icons/organisations.png`<br>`ABAE832BE33CDEBFE1D80C8E47A1FFF4D1FEF644B02F2BD5D51FC9390C421204` | `src/Pegasus.Web/wwwroot/images/marks/organisations.png`<br>`804E77E33162BB09B0374058C6E6989B92A59224F813DDDA0BA6D410A69F6E8C` | Lanczos downscale to 128×128. Administration → Organisations. |
| `principals.png` | `PegasusDesign/assets/icons/principals.png`<br>`B85E82694474D92F3C15106699786B2081F8E2AFDE66D4A1A78E07071786C967` | `src/Pegasus.Web/wwwroot/images/marks/principals.png`<br>`879055AD9A973F05E2BE49F5EA00EDD43111D323BDC8C8952FCA727A7C9C0496` | Lanczos downscale to 128×128. Administration → Principals. |
| `configuration.png` | `PegasusDesign/assets/icons/configuration.png`<br>`B64DCBE7FD45B24A0D9BD687BF8E16BCB3E4E587ED16F93BF1BCE12370A6E921` | `src/Pegasus.Web/wwwroot/images/marks/configuration.png`<br>`86A311A3C1ACE78E5D5A407B289F901ED7C26860BCBBBDEF59EC93A71BAFA62E` | Lanczos downscale to 128×128. Administration → Workflow configuration. |
| `mailboxes.png` | `PegasusDesign/assets/icons/mailboxes.png`<br>`179A5677C4B73587601F0AF79162F87217C2035D096D90341281E23BFD87F688` | `src/Pegasus.Web/wwwroot/images/marks/mailboxes.png`<br>`1B727ACBE0DCC114370E0D620DCB74E20A12866C85187689ABDB8A249B61C019` | Lanczos downscale to 128×128. Administration → Approved mailboxes; Inbox empty state. |
| `automation.png` | `PegasusDesign/assets/icons/automation.png`<br>`51F6970F9C0245E694D3562922A34AC5C3F2E762ACB5682FDF6DAA3FDFE10039` | `src/Pegasus.Web/wwwroot/images/marks/automation.png`<br>`1EABE2EF634065A1A76F78A6D520A366C49D469EBC3C92BA99F1DBA1A8F8B3FE` | Lanczos downscale to 128×128. Administration → Automation. |
| `checkmark.png` | `PegasusDesign/assets/icons/checkmark.png`<br>`6ECC9917585A85D7B8C7EC62DB3C167689FD0F210D9838EC0B9959F1238471F3` | `src/Pegasus.Web/wwwroot/images/marks/checkmark.png`<br>`5531CC893A5C7A1137F049CF0D77A9D19B73EB30AC1036985A902FFC44A0C30F` | Lanczos downscale to 128×128. Queues empty states. |

Capabilities allocated beyond `0.1.0-alpha.1` have no alpha navigation, control, workflow or placeholder. Their exact first-introduction releases remain owned by the [capability inventory](../capabilities.md#capabilities). Every deferred UI capability must re-enter specification, alternatives, independent review, explicit approval, visual generation and manual visual review before implementation.

## Design principles

- Operational, restrained and border-led rather than decorative.
- White or light-neutral ground, white panels, warm-charcoal navigation and near-black text.
- Collision red is sparse: primary actions, active navigation, visible focus and urgent emphasis.
- Product states are distinct: amber for incomplete/pending, restrained navy for **Review**, and green only for confirmed completion.
- State is never conveyed by colour alone.
- Use 2px corners, 1px hairline borders, rare soft shadows and a 4px spacing rhythm.
- Use system UI text and Lucide line icons only.
- Controls communicate purpose without narrating obvious actions. Screens carry no lede or subtitle: one H1 and the content. Guidance appears only beside a control whose action has a consequence the operator must understand, and is one sentence.
- Do not expose Azure, OCR, AI, queue mechanics, extraction engines, deployment, adapter, lease/version, projection, ingress, or artifact terminology in operator copy. The word “intake” never appears in operator-facing text (operator decision 2026-08-04).
- Every state value shown to an operator passes through an explicit operator-label map — `Pegasus.Web.Presentation.OperatorLabels`. Raw `ToString()` of enums, snake_case event codes, GUIDs, hashes, storage paths, version integers and byte counts never reach markup. File sizes, where relevant, are megabytes to one decimal.
- Every date and time an operator reads renders Europe/London through that same map. `ToLocalTime()` is never correct: it resolves against the server clock, which is the office zone on a developer workstation and UTC on the deployed container, so it looks right exactly where it is tested and is wrong through British Summer Time where it runs.
- A composed query that returns zero renders `0`. A capability that is not composed in a deployment is absent from the interface — never a disabled item, inert card, or “Unavailable” placeholder. Genuine runtime failure renders the designed failure state with the last-good time.
  - This applies to capabilities, not to conditions. An action the record in front of the operator will genuinely offer once a condition is met stays visible and disabled with the condition named on the control (“Available in Review”); removing it would assert the action is impossible, which is false.
- Every screen defines its empty, loading, and failure states in business language, and unknown-record URLs render the styled not-found screen, never a raw browser error.
- Screens are compact working surfaces, not marketing pages: 4px base rhythm with 8/12/16 steps, 32px table rows, 12–16px panel padding, 13.5–14px body text. A screen about a single record is one container — header, action bar, tabs — and the operator reaches its identity, its state, its available actions and its main content without scrolling.
- Provenance is an icon with a one-word tooltip, shown on hover **and** on keyboard focus with a matching accessible name: Staff · Extracted · AI · E-mail · Lookup · Principal · Automatic. Source labels, policy keys and provenance sentences do not appear in markup.
- A count query and a rendered time cannot be proved locally: an empty database returns the same zero as a correct query, and a Europe/London workstation clock matches the office by accident. Both need populated test data or the deployed instance.

Settled terms retain their exact meanings and casing, including `Audit`, `Triage`, `Unidentified`, `Blocked`, `Not ready`, `Review` and `Held` (`Blocked` supersedes the earlier interface wording `Blocked intake`, operator decision 2026-08-04; the pre-case failure boundary it names is unchanged). Never substitute a generic **Close** action for a named lifecycle outcome.

## Tokens

The upstream token source was `styles/colors_and_type.css` in the provided `collision-engineers-design-dev` bundle. That source pack is not retained. The values below are the adapted repository-owned authority; no website stylesheet or generated token file is copied.

### Colour

| Role | Approved value or rule |
| --- | --- |
| Collision red | `#DB0816` |
| Pressed/dark red | `#8F1422` |
| Red tint | `rgba(219,8,22,.07)` |
| Warm charcoal | `#2C2A27` |
| Near-black ink | `#16191D` |
| White | `#FFFFFF` |
| Light neutral | `#F5F4F2` |
| Border | `#E6E4E1` |
| Muted text | `#6B6B6B` |
| Confirmed-success green | `#16833B` |
 | Incomplete/pending amber | `#7A3E00` (fg) / `#FFF4D6` (bg) / `#A15C00` (border) |
 | Review navy | `#143A5E` (fg) / `#EAF1F8` (bg) / `#365F87` (border) |

 Amber incomplete/pending (`#7A3E00`/`#FFF4D6`/`#A15C00`) and navy **Review** (`#143A5E`/`#EAF1F8`/`#365F87`) are approved Pegasus state tokens implemented across `site.css` and status partials. Green must not represent progress, availability or a generic positive action; it is reserved for confirmed completion.
Excluded marketing tokens include WhatsApp green/pills, large display scales, CTA shadows, document red and brand-font declarations.

#### Reviewed divergence: the `Send to Claude` control

Authorised by the operator on 2026-08-03 and scoped to the single
`.send-action` control on the Engineer assessment surface. The action carries
the provider's own identity so it reads as Claude on sight and is never
mistaken for a Collision Engineers action.

Every value below is declared as a local custom property on the control
itself, not added to `:root`, so no other surface can inherit it. The rest of
the application keeps the approved palette, the 2px radius and the red focus
ring.

| Divergence | Value | Why it is confined here |
| --- | --- | --- |
| Terracotta gradient | `#E8956D` → `#D97757` → `#B85F3D` at 135° | The provider's accent. Not added to the colour table and used by no other rule. |
| Corner radius | `12px` | Matches the provider's control shape. The approved `2px` remains the only radius elsewhere. |
| Type | `Poppins` first, falling back to the approved system stack | The face is requested, never loaded. No font bundle, file or external stylesheet is added, so a workstation without Poppins renders the approved stack. |
| Raised shadow and hover lift | `0 2px 5px` at rest, `0 12px 28px -8px` and `translateY(-2px)` on hover | The one lift in the product. Removed under reduced motion. |
| Focus ring | `2px solid #6A9BCC` | The approved red ring all but disappears on terracotta. This is the only control that does not use it. |
| Sparkle glyph | Inline four-point star | Not a Lucide glyph and deliberately **not** added to the checksummed sprite, which is unchanged at 16 glyphs. |
| Ember canvas | Ten particles from a seeded generator | Decoration drawn beneath the label. It reads no page data, starts only when motion is welcome, and stops when the control leaves the document. |

Reduced motion removes the lift, the sparkle animation and the canvas; forced
colours discards the gradient and restores a `ButtonText` border. The control
keeps its 44px target and its accessible name in every mode.

**Known shortfall, recorded rather than hidden:** `#FAF9F5` on this gradient
measures about 2.3:1 at the light stop, 3.0:1 at the middle and 4.2:1 at the
deep stop, against the 4.5:1 this size and weight require. The gradient is the
provider's own and was adopted as given. Resolving it means either deepening
the ramp or taking dark text, and that is an operator decision, not a
design-pass one.

### Typography

Use this system stack for all application text:

```css
ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif
```

Rules:

- Body text is 14–16px.
- Use semantic heading hierarchy.
- Compact uppercase eyebrows may be used where useful.
- Queue and metric values may use stronger weight.
- Tw Cen MT and Futura are marketing, logo and document faces, not application body or UI fonts.
- Do not copy or load an application brand-font bundle.

The shorter fallback stack currently used by `src/Pegasus.Web/wwwroot/css/site.css` is compatible but is not a separate authority.

### Shape, borders and focus

| Token | Approved value |
| --- | --- |
| Primary radius | `2px` |
| Borders | `1px` |
| Keyboard focus ring | `3px rgba(219,8,22,.38)` |
| Depth | Border-first; rare soft shadows |

The 3px geometry that previously appeared in the Development CSS is resolved: `src/Pegasus.Web/wwwroot/css/site.css` now uses the approved 2px radius throughout. There is no second approved radius.

### Spacing and layout

Approved spacing steps are:

```text
4, 8, 12, 14, 18, 24, 32, 40, 64px
```

Use only steps exercised by the selected UI. Primary gutters are 24px.

At 1280px and wider, use dense desktop multi-pane layouts. At 1024–1279px and at 200% zoom, reorder secondary content into labelled tabs, drawers or ordered sections without losing identity, state, labels, focus or actions. The upstream marketing 1200px/96px section rhythm is not imported.

Mobile staff UI is **Not planned**. CSS reflow does not create a mobile product, and a supported-device notice is only for genuinely unsupported devices, never a substitute for responsive desktop behavior.

### Motion

There is no product-wide motion system and no approved duration or easing tokens.

A basic, non-essential refresh or loading animation is permitted if:

- the feedback remains understandable without motion;
- reduced-motion preferences receive a static equivalent; and
- the behavior is verified through the real approved route.

Marketing scroll reveals, staggered entrances, hover scaling and CTA lift are excluded. Do not invent duration or easing tokens during implementation.

## Assets

### Logo

The approved master is:

```text
docs/design/brand/logos/logo_no_margin.png
```

It is the red gear-C Collision Engineers lockup, copied exactly from `assets/logo_no_margin.png` in the provided `collision-engineers-design-dev` source bundle.

```text
SHA-256: E7247BE45911C46905343473E4C57B9F6ED7A450563D19C508C2D9652C2C63E2
```

Current consumers:

- embedded by `src/Pegasus.Infrastructure` for the integrated report renderer;
- copied byte-for-byte to the Web runtime and embedded by `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` (see the source-to-runtime mapping below).

Rules:

- Never redraw the gear.
- Never extract it from a screenshot.
- Never recolour the master or invent another mark.
- Copy or optimise it for a runtime only through a reviewed source-to-runtime mapping with checksum proof.
- The former `CE` text mark was replaced by the mapped logo in `_Layout.cshtml`; the leftover `.brand-mark` CSS rule has been deleted from `site.css`. No second logo variant exists.

The upstream source directory may be absent from a clean checkout. The checksum-pinned repository copy is the durable source.


 #### Logo source-to-runtime mapping

 | Asset | Upstream source & SHA-256 | Web runtime destination & SHA-256 | Mapping & usage |
 | --- | --- | --- | --- |
 | Primary logo | `docs/design/brand/logos/logo_no_margin.png`<br>`E7247BE45911C46905343473E4C57B9F6ED7A450563D19C508C2D9652C2C63E2` | `src/Pegasus.Web/wwwroot/images/logo_no_margin.png`<br>`E7247BE45911C46905343473E4C57B9F6ED7A450563D19C508C2D9652C2C63E2` | Byte-for-byte copy embedded in `_Layout.cshtml` header navbar link. Replaces fake `CE` mark and unproven favicon link. |
 | Primary logo (design-system preview copy) | `docs/design/brand/logos/logo_no_margin.png`<br>`E7247BE45911C46905343473E4C57B9F6ED7A450563D19C508C2D9652C2C63E2` | `docs/design/system/src/logo.png`<br>`7A6BD1CE2A57EB47BA9C7BA011935596D64ADE655FE02FFF1854497B962A33AA` | Bicubic downscale to 416×232 (27 KB) of the master, inlined as a data URI into the Claude Design bundle's `AppNav` so previews are self-contained. Not a Web runtime asset; the Web keeps the byte-for-byte copy above. |

### Icons

Lucide is the only approved Web/UI icon system:

- 24×24 viewBox;
- 2px stroke;
- round caps and joins;
- rendered at 16–24px;
- `currentColor`.

Do not use emoji, Unicode dingbats, hand-drawn icons or infrastructure symbols.

The checksummed Lucide sprite is delivered at `src/Pegasus.Web/wwwroot/images/lucide-sprite.svg` (see the mapping below). Glyph usage is now exercised: `src/Pegasus.Web/Pages/Shared/_LucideSprite.cshtml` inlines the same sixteen glyph vectors once per page from `_Layout.cshtml`, and pages reference them as `<svg class="icon"><use href="#icon-…"/></svg>`.

The inline partial is the runtime delivery of the checksummed asset, not a second icon set. It carries the identical glyph vectors and differs only in wrapper element: each glyph is a `<symbol viewBox="0 0 24 24">` rather than a `<g>`, because `<use>` does not inherit a `viewBox` from a `<g>` and consuming elements would render clipped. The approved 2px stroke and round caps are applied by the `.icon` rule in `site.css`, since a `<use>` clone does not inherit presentation attributes from the sprite root. No glyph was added, removed, or redrawn.

Every icon rendered today is decorative and paired with a visible text label, so each carries `aria-hidden="true"`; any future icon that is not decorative needs its own accessible label. `src/Pegasus.Web/wwwroot/favicon.ico` has unrecorded provenance and is not icon-system authority.


 #### Lucide icons source-to-runtime mapping

 Upstream source: Lucide official SVG vectors release (v0.344.0).
 Runtime sprite: `src/Pegasus.Web/wwwroot/images/lucide-sprite.svg` (SHA-256 `C81F067708B5EF1C2CEDABF4A38BADC175A11DEFE7919DA69192100EE6922BF1`).

 | Icon glyph | Glyph SHA-256 | Usage & accessibility mapping |
 | --- | --- | --- |
 | `search` | `832472670DB14C3420D64D80271A04FE90AE32D47F4834F4E70E9A8E2678EE7E` | Global search action and input field prefix icon (`aria-hidden="true"`) |
 | `user` | `F12759D8CA6B092DCA70B2E265F4CD8921C6DC61B408C9DA3FFFC8650BE76AA2` | Authenticated user menu / profile identity icon |
 | `refresh-cw` | `C795E4B7F739E9CF2D5C5996CBDF8A0541734F0DC99EBE169BAE945FD04E2AA2` | Manual refresh action / freshness banner status icon |
 | `clock` | `EE847E37391A579398EA5CB111A4893642085DEA959EF3812F210ED69EABC5C6` | Freshness timestamp and due date indicator icon |
 | `calendar` | `9164C7178F10683EF0FB999F773149CD7AF5964875E6E896C6826F5A8988C67F` | Date range and instruction date filter icon |
 | `check-circle` | `CB9B89AA467B527393B51229F14E0314DB15D75792D2071C5FE599AB595C7678` | Confirmed success status badge icon |
 | `alert-triangle` | `40DEB35C6E3562DB12C1962989A7D9E24C758489247929C156DEDD8476DBE233` | Incomplete / pending / warning status chip icon |
 | `alert-circle` | `69DA72930B08F89FA5C1AFDA3D5813BFAFA124D3E86F66B2100300F2B7DEB415` | Error summary / blocked intake status icon |
 | `info` | `9B266C26D53D1F6661CD45D11E5138FE00AF4289EA4EC8D4C320D41AB272CC3F` | Provenance panel and field detail information icon |
 | `file-text` | `A6AF7723E87920CF322C8C39F0A1080075BFA19B3E966A8E21D2D81A93772936` | Source document and instruction evidence icon |
 | `filter` | `C4319C676F5B160213319934EB2DEC6F60DD6F73C344C0D6C84AE1699430D45C` | Table and workbench queue filter control icon |
 | `shield` | `456B29F0717F73785AE1CA5A492EF0B21693BDA13045B509E845BA38F08717AE` | Administration workspace navigation icon |
 | `chevron-right` | `07C6F850908E2A9ABA2AD8B7B91AA8E525D463398D479DAD5EF10CB534FE3710` | Interactive table row expansion indicator |
 | `arrow-right` | `D8B246C7FDBAB41053F2016892C0664BB64C0C6D1ED4594C9D80470C1B219C70` | Action transition and external link indicator |
 | `upload` | `EE63E95EFECDAF141338475D367A54EF891E337491993DCDC1F3ED7936A42660` | Intake manual upload action icon |
 | `lock` | `1F0A0861A3752428E1D5CABDAC22608E645A008229EF58415EC0C0E112F5BF2D` | Case edit lease indicator icon |

### Imagery and evidence

Upstream marketing photography is excluded, and no generated or substitute glyph is used anywhere. The one class of imagery the internal Web application does carry is the [commissioned Pegasus marks](#the-pegasus-marks), adopted 2026-08-17: fourteen operator-supplied raster marks that name a surface, always decorative and always beside text that says the same thing.

Genuine case images, emails and documents are operational evidence, not decorative assets. Use only authorised repository-provided evidence through its owning workflow. Never generate placeholder cases, damage images, emails, documents or people.

The retained comparison rasters are selection evidence only. The Operations-first shell strategy is approved; no raster is pixel-level authority, runtime payload or implementation proof.

### Web and renderer boundary

| Asset class | Approved consumer and boundary |
| --- | --- |
| Master logo | Embedded by the Infrastructure report adapter and copied byte-for-byte to Web |
| Report templates and document stylesheet | Embedded by `src/Pegasus.Infrastructure`; not Web shell assets |
| Supplied engineer signatures | Andy Patterson's approved exact tuple is embedded by Infrastructure; other supplied assets remain governed but inactive; never Web decorative imagery |
| Retired renderer workspace, prompt, model, skill and AI material | Historical source evidence only; not a separate runtime or policy owner |

The imported renderer can exercise its own assets without proving the planned Pegasus report capability. Imported workspace material does not become UI, report or design authority by existing in the repository. See the [workspace boundary](../../workspaces/README.md).

## Voice, labels and necessary copy

Use concise, settled Collision Engineers language. Guidance is appropriate only when an operator must understand a consequence.

Approved necessary copy includes:

> Blocked — a reason is required.

> No case or reference was created; review the missing or conflicting evidence.

> Created in error cannot be reopened. Create and link the replacement case.

Permanent consequences must be visible without hover or colour alone. Illustrative text must not fabricate operational input.

These words are banned from operator-facing copy in
`src/Pegasus.Web/Pages/**/*.cshtml` and PageModel label maps, and a change
introducing one does not merge: `intake`, `bounded`, `projection`, `lease`,
`opaque`, `ingress`, `composed`, `artifact`, `durable`, `aggregate`, `queue`,
`caller`, `correlation identifier`, `bytes`. This is a review rule, not an
automated check — nothing in CI enforces it today, and claiming otherwise
would be the kind of false assurance the evidence discipline above exists to
prevent. The words remain valid as internal code identifiers; the ban is on
what an operator reads.

## Access and permissions

Staff accounts, authentication, and authorisation are implemented and enforced through authenticated Web callers ([architecture](../current-architecture.md)). Accounts use Pegasus-managed usernames and passwords. Core owns the exact [staff role access matrix](../frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix), automated-actor boundary, and [case edit authority](../frd/frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery); this section owns only how those decisions appear in the planned UI.

| Actor | Planned UI boundary |
| --- | --- |
| Administrator | Staff shell plus Administration surfaces for accounts/access/roles, principals, configuration, approved mailbox allowlist, and the Automation client registration and activity review (enable/disable kill switch and permanent activity records addressable by correlation identifier; no secret display). |
| Engineer, User | Staff shell without Administration surfaces. Their ordinary Intake, Triage, Case, document, evidence, and lifecycle controls are identical. |
| Automated processing | No UI account or interactive control. |
| Provider API client ([API-01–API-04, `Next / 0.4.0`](../capabilities.md#capabilities)) | No staff shell, Case workspace, or Administration surface. |
| External/customer | No application account; the only external surface is the isolated request-scoped `/Uploads/{token}` upload page (INT-31), which exposes no case or request state. |

Every protected route and action must handle unauthenticated, disabled-session, stale-role, denied, loading, and successful outcomes. Hiding a route or control never replaces server authorisation. Administration has no generic rules editor, credential/cloud/release operation, bulk predecessor import, or bulk Case-edit tool. No surface permits permanent deletion or direct external/customer Case editing.

## Operations-first shell

Operations is the landing route.

```text
CE logo | Dashboard | Inbox | Upload | Queues | Cases | Administration | User
Dashboard
Not ready | Review | Held        (active cases)
Received today | Unidentified | Blocked        (e-mail activity)
New cases today | Sent to Engineer: today / week | Reports sent: today / week
Last updated | Refresh
```

The approved route order is `Dashboard → Inbox → Upload → Queues → Cases →
Administration (admin-only) + user controls` (operator decision 2026-08-04).
Search merged into Cases, which has the identical backing query; the former
combined intake screen split into Inbox and Upload; Queues is the
pre-engineer-assignment work viewer carrying Not ready, Review, Held and
Triage — the first three Case stages, the fourth a separate pre-case entity.
`Triage` no longer names a screen, nav item, title or route, and its settled
meaning is unchanged.

Rules:

- Every metric is an exact query link to its corresponding filtered queue.
- `Blocked` is exact wording and remains pre-case.
- Every metric shows its last-good time and one current refresh state: loading,
  current, stale, partial, unavailable, or failed.
- `0` is a current result, never a substitute for stale, partial, unavailable,
  failed, or not-yet-loaded data, and no shipped tile may render a placeholder
  for a query that does not exist.
- Manual refresh reruns the same filter, gives start/completion feedback, keeps
  last-good data visible, and never claims an external action succeeded.
- Refresh remains telemetry; accepting, rejecting, linking, or changing an
  external fact during reconciliation is a permanent, attributable business
  event.
- Day boundaries use Europe/London midnight.
- Week boundaries begin Monday.
- At constrained desktop width or 200% zoom, the selected summary becomes an ordered, labelled section after the results without losing identity, state or action context.
- Receiving work, Queries and Other are `Next / 0.3.0` in the [capability inventory](../capabilities.md#capabilities), with no `0.1.0-alpha.1` surface.
- There are no `0.1.0-alpha.1` saved views, bulk actions, inline mutation, calendar, personal assignments or general email queues.

The selection rationale is strongest shared-office awareness and truthful day/week visibility. Its risk is density and dependence on independent, accurate queries.

### Rejected alternatives retained as evidence

| Direction | Rationale and boundary |
| --- | --- |
| Worklist-first | Highest repeated case-queue throughput, initially focused on `Not ready`, with a selector limited to `Not ready`, `Review` and `Held`. It weakens whole-office day/week visibility. It must not become a generic cross-feature list; Intake and Triage remain dedicated, the summary is read-only, and consequential actions open focused flows. No bulk actions, saved personal queues, inline lifecycle mutation or speculative email work. |
| Case-first | Clearest auditability and deep case context, with Cases/search as the landing and Operations retained as a full named route. It makes shared queue scanning less immediate and cannot be the earliest implementation. No generic Close, notes substitute, percentage completeness, named Engineer assignment, inline external editing, estimator, valuation, finance, AI or mobile controls. |

The comparison rasters remain selection evidence: [Operations-first](references/mockups/candidate-a-operations-first.png), [Worklist-first](references/mockups/candidate-b-worklist-first.png), and [Case-first](references/mockups/candidate-c-case-first.png). Their styling and details are not automatically approved.

## Current Development caller

The exercised Development journey is:

```text
upload one supported local source
→ Core ProcessIntake, fail closed
→ persisted receipt/outcome
→ queue
→ receipt review
→ retained source/evidence/draft/assets
→ authorised retained-asset download
```

It runs under authenticated staff identity (the DevelopmentOffline profile's server-derived actor); it does not create a case, allocate a reference or prove the planned shell.

### Core outcome to operator label and persistence

The received-items caller combines the processing decision with the actual
Case link and durable allocation state. A decision is never case-existence
authority on its own.

| Core intake decision | Exact operator label | Receipt persisted | Case/reference persisted |
| --- | --- | --- | --- |
| `CaseCreated` | Ready for case allocation, Creating case, Case not created, or Case created according to allocation/link state | Yes | Only when the Case intake link exists |
| `NeedsSorting` | Unidentified | Yes | No |
| `BlockedIntake` | Blocked | Yes | No |
| `OcrRequired` | Needs text extraction | Yes | No |
| `TechnicalFailure` | Failed | Yes | No |
| `Unsupported` | Unsupported | Yes | No |
| `ImageIntakeRegistered` | Vehicle images registered | Yes | No formal Case/PO; allocates the Image-initiated VRM reference |

`CaseCreated` is the processing eligibility code; typed allocation records the
separate attempt and the Case intake link is the only proof that a reference
exists. Ambiguous or unidentified material is `Unidentified`.

`Needs text extraction` records a fail-closed outcome; it does not prove that deferred OCR capability is implemented. The intake list also derives the display outcome `Associated with Case` for receipts holding an active case association.

Validation or refusal before an accepted intake receipt must not be described as formal Case creation. An `ImageIntakeRegistered` receipt allocates the separate Image-initiated Image Intake Reference.

There is no decision meaning "a human has not pressed the button yet":
[FRD-02](../frd/frd-02-intake-and-source-identity.md#matching-conflicts-and-reversible-association) is explicit that definitive authorised intake creates
exactly one instructed Case idempotently and that "the allocation decision adds no universal
manual acceptance gate", and the [operator notes](../operator-notes.md) send only ambiguous
provider, instruction-type, or case evidence — and any unidentified e-mail — to `Unidentified`.
Every definitive typed instruction attempts allocation at processing time. An Audit is definitive
only when its separate original report supplies one literal outcome: `repairable` or `total loss`.
An Audit instruction with no separate report, a conflicting report, or an unclear outcome is
`Unidentified`; it allocates neither a Case/PO nor an Audit reference and carries a U-reference.
Incomplete ordinary detail is never a bar to allocation. A failure is retained separately and requires a reasoned staff
retry after correction.
`Review` and `Ready to review` denote the Case stage before the report is with an Engineer and
must never name an intake state.

### Planned case-creation mapping

| Intake decision | Operator state | Persisted case/reference consequence |
| --- | --- | --- |
| Definitive authorised instruction with instruction and image completeness satisfied | `Review` | Create exactly one case/reference through shared fail-closed acceptance |
| Definitive authorised instruction without both completeness requirements | `Not ready` | Create exactly one incomplete case/reference |
| Audit instruction without a separate original report carrying one literal outcome | `Unidentified` | No Case/PO or reference; preserve the received evidence under its U-reference |
| Staff-resolved intake with both completeness requirements recorded | `Review` | Create exactly one case/reference |
| Staff-resolved intake without both completeness requirements recorded | `Not ready` | Create exactly one incomplete case/reference |
| Explicit confirmation of both requirements on an existing `Not ready` case | `Review` | Transition the existing case; do not create another case/reference |
| `Blocked intake` | Shown as `Blocked`, with required reason | Persist pre-case intake work only; no case/reference |
| Unidentified, unsupported/incomplete source, identity-critical ambiguity, custody/integrity/replay/occurrence conflict or missing identity evidence | Unidentified or named pre-case failure | No case/reference |
| Resolve/retry of blocked or failed intake | Re-enter ordinary fail-closed intake | Create exactly one case/reference only if the ordinary gates then pass |

## Component map

Only the first table describes exercised components. Planned contracts do not create a speculative component library.

### Exercised components

| Component | Purpose and states | Runtime owner |
| --- | --- | --- |
| Development shell/navigation | Identify the current proof and reach Development routes; normal, hover and focus; the current route carries `aria-current="page"` with a weight change **and a 2px Collision-red left border** so it is not signalled by colour alone; the Inbox item is conditional and is **absent**, never a disabled span, where the capability is not composed | `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` |
| Navless shells | The screens that are not a place in the application. `_LayoutAuth` carries sign in, the signed-out confirmation, access denied and the error/not-found family; `_LayoutExternal` carries the one screen a third party sees and states the company, never the product | `src/Pegasus.Web/Pages/Shared/_LayoutAuth.cshtml`, `_LayoutExternal.cshtml` |
| Status-code page | The designed answer to a status code with no exception behind it: unknown record, dead external upload link, oversized upload, rate-limited sign-in. Scoped away from the health, version and automation surfaces, whose callers want a parsable body | `src/Pegasus.Web/Pages/StatusCode.cshtml(.cs)` |
| Operator label map | The single place a persisted code becomes words: stage, case type, document role and origin, custody, upload-link state, history event, file size. Raw `enum.ToString()`, snake_case event codes and PascalCase compounds never reach markup | `src/Pegasus.Web/Presentation/OperatorLabels.cs` |
| Queue/metric card | Show persisted Development intake counts and open the exact list; value and unavailable states are both exercised, an unavailable tile stating its absence rather than substituting a zero; stale and partial remain planned | `src/Pegasus.Web/Pages/Index.cshtml`, `src/Pegasus.Web/wwwroot/css/site.css` |
| Status chip | The single place a business or query state selects its tone and Lucide glyph; always paired with its text label | `src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml` |
| Freshness and manual refresh | Last-good Europe/London time, current refresh state, and a manual refresh that reruns the same filter with start feedback and double-submit protection | `src/Pegasus.Web/Pages/Shared/_FreshnessBanner.cshtml` |
| Page header | Eyebrow, title, lede and optional primary action, shared by the Operations, Cases, Intake, Triage and Administration surfaces | `src/Pegasus.Web/Pages/Shared/_PageHeader.cshtml` |
| Administration workspaces | Authorised Administration entry cards; one accessible link per card with the whole card as the pointer target | `src/Pegasus.Web/Pages/Administration/Index.cshtml` |
| Intake receipt and upload | Submit one bounded authenticated source through `ReceiveIntake`; redirect to the staged receipt status; show Received, Processing, Complete or Failed; refresh nonterminal status every two seconds with a manual refresh available; link completion to the resulting case or retained receipt; list and inspect retained receipts and download the retained source only as an authorised safe attachment | `src/Pegasus.Web/Pages/{Upload,UploadStatus}.cshtml(.cs)`, `src/Pegasus.Web/Pages/Intake/{Index,Details,Source}.cshtml(.cs)` |
| Image-initiated Case list/detail | List searchable Image-initiated records by lifecycle state and exact Image Intake Reference; detail presents VRM suggestions, preserved group evidence, merge history, custody, and reasoned staff closure | `src/Pegasus.Web/Pages/ImageIntake/{Index,Details}.cshtml(.cs)` |
| Triage queue/detail | List and filter triage records and execute the Core-owned detail commands without adding due/chaser controls | `src/Pegasus.Web/Pages/Triage/{Index,Details}.cshtml(.cs)` |
| Anonymous request upload | Token-bound `/Uploads/{token}` form and immediate result; antiforgery, idempotent operation key, generic non-disclosing outcomes | `src/Pegasus.Web/Pages/Uploads/Request.cshtml(.cs)` |

### Planned component contracts

| Component | Required contract |
| --- | --- |
| Shell/access | Sign-in; disabled, stale-role and denied outcomes; visibility derived from the [Core-owned role matrix](../frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix) plus server authorisation |
| Metric/queue | Label, value or unavailable state, last-good time, current refresh state and exact destination filter; `0`, loading, current, stale, partial, unavailable and failed remain distinct; each Case row has a read-only latest attributable activity/evidence summary with its timestamp, using operator language and never implying an external delivery or rewriting permanent history; bounded pagination has accessible current-page context and keyboard-operable next/previous controls, with page size determined during surface design |
| Intake workbench | Immutable source occurrence and evidence beside the distinct editable candidate/accepted Case projection; source/dispatch identity; `All`/`Instructions`/`Images` filter; fact versus suggestion versus confirmed value; provenance, ambiguity/conflict, association history, acceptance path and no-case consequence |
| Request-scoped upload | Bound upload fields and immediate request-local result only; expired, revoked, limit, custody, replay and cross-request failures disclose no case/reference, request history or other material |
| State action | One current Case and one named Core action; prerequisites, consequence, reason where required, recovery and history link; never a generic Close, bulk edit or external edit |
| Identity header | Read-only Case/PO, principal, registration, type/secondary Audit identity, workflow state, `Due by`/overdue state and EVA proxy limitation |
| Due/chaser panel | Missing-material reason, next chase, most recent recorded channel/outcome, optional note and next permitted action together; preparation/copy is not sent or delivered |
| Inspection address | Provider-determined default: the Principal's inspection-mode setting autofills exact `Image Based Assessment` or requires an explicit physical vehicle/repairer location; reasoned per-Case staff override; physical address fields appear only for the physical mode and never imply attendance |
| Engineering findings | Separate Roadworthiness and Assessment controls; accepted and superseded versions, reasoned correction, reopen requirement and no inferred fee/invoice mutation |
| Evidence/document panel | Original/source/version, logical removal and closed lock; Box/external state; issued report versions; exact Outlook evidence with separate discovery, link and sent times |
| Lease/conflict | One current Case; holder, expiry, renew/release/reacquire state and read-only alternative; current conflict and preserved proposed values; no forced Administrator takeover |
| History | Read-only presentation of the Core-owned [permanent action history](../frd/frd-04-parties-accounts-and-access.md#permanent-action-history), including actor/caller/time and one-Case scope without message bodies or telemetry noise |
| Reason dialog | Named requirement and consequence; labelled reason; confirmation/cancel; initial focus, focus containment, Escape where safe and focus return to the invoking control |

Opening source evidence or other supporting detail preserves the current list/detail position and every unsaved edit; returning never silently discards or replaces the operator’s proposed values.

## Planned workflow patterns

### Intake

The Intake workbench presents the immutable [source occurrence and durable dispatch identity](../frd/frd-02-intake-and-source-identity.md#source-occurrence-and-dispatch-identity), provenance, original custody, attachments/images, facts, and derivations separately from an editable candidate or accepted Case projection. A source never becomes the Case record merely because a candidate is accepted.

The planned alpha surface includes:

- manual upload;
- automatic ingestion from `instructions@collisionengineers.co.uk`;
- correct treatment of staff-forwarded email as real intake;
- stable source-occurrence and dispatch identity, duplicate delivery, pending/retry state, and idempotent result;
- EML and freehand email-body extraction;
- PDF embedded text and embedded images;
- DOCX text and every visible image placement without deduplicating repeated appearances;
- JPEG and PNG image-led intake;
- reviewed vehicle-registration suggestions from ordinary vehicle images;
- bounded, fail-closed handling for unreadable, oversized or incomplete sources;
- typed, editable, operator-reviewable drafts;
- field provenance, validation, missing values and contradictions;
- principal/provider identification;
- `Needs sorting` and reasoned `Blocked intake`;
- definitive and staff-resolved acceptance through the same business rules;
- registration-based provisional identity for image-led work;
- ambiguous/conflicting association review and reasoned manual link, unlink, reversal, or reassociation while preserving every prior relationship and original origin under the [Core association contract](../frd/frd-02-intake-and-source-identity.md#matching-conflicts-and-reversible-association);
- missing, integrity, replay, retention, custody and persistence failures.

A staff-created in-house upload request is permitted only through a temporary
token bound to exactly one request, its allowed operation, and a server-enforced
expiry. Staff can revoke it, and the isolated unauthenticated surface exposes
only that request's upload fields and immediate structured result. It exposes no
case/reference, request state/history, other document, token-management function,
external account, or cross-request lookup. Success proves only request-local
custody, not case creation, Box custody, EVA handoff, report generation, or
external delivery. File type/count/size limits, expiry, revocation, idempotent
retry, abuse handling, cross-request isolation, durable custody, and
non-disclosing errors are acceptance gates.

Policy-specific email predicates and acceptance evidence remain open gates for only their named automatic paths. They do not weaken manual or shared fail-closed acceptance.

### Triage

Triage is a distinct inbox classification/label and separate pre-case record, never a case state. The UI implements the [Core-owned normal workflow and completion evidence](../frd/frd-03-triage.md#normal-workflow-and-completion-evidence) rather than defining another transition policy.

The detail workspace presents the normal sequence from registration-gated `Needs sorting`, through `Open`, missing-information correspondence, and an accepted finding, to exact reply-chain evidence and `Completed`. It must show acknowledgement, information request, or other ordinary correspondence as non-completing activity; display missing, ambiguous, unapproved, or technically failed reply evidence; and expose `Cancelled` as the separately named end without finding/reply.

Finding correction/replacement, new response, reasoned reopen, and optional later Case link/unlink/relink remain visible in permanent history. The Case link is reference-only: Triage findings do not alter Case/PO, reference, lifecycle, final outcome, Engineer report, or Audit identity. Assignee remains optional, with no due date or chaser UI.

### Case

The Case workspace visibly preserves the immutable [Case/PO and principal identity](../frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity), registration, [Inspection, standalone Audit, or Inspection + Audit type](../frd/frd-01-case-identity-and-lifecycle.md#case-types), secondary Audit identity where applicable, workflow state, `Due by`/overdue state, and EVA proxy limitation. It presents accepted case-party functions and the inspection-address snapshot for that Case without allowing later reusable organisation/repairer edits to rewrite historical case evidence.

A wrong-principal repair is presented as the Core-owned `Created in error` original and its linked replacement, never as an editable Case/PO or principal field. Both references remain visible and the original has no reopen control.

Case work includes:

- source, provenance, and typed case data;
- documents and images;
- suggestion-first ordinary-image VRM with source-image/confirmed/no-result
  distinction;
- DVLA/DVSA and MOT/mileage observations with source/version/age and
  supplied/external/estimated classification;
- provider-determined inspection mode: explicit physical vehicle/repairer
  location or exact `Image Based Assessment` autofilled from the Principal's
  setting, with reasoned per-Case override;
- separate Roadworthiness and Assessment findings plus correction history;
- tasks and reminders;
- `Due by`, missing-material reason, next chase, last channel/outcome, optional
  note, and next permitted action in one work area;
- seven-calendar-day missing-information chasers and `Held` behavior that
  preserves the interval;
- request-scoped upload-link creation and copyable manual chasers;
- manual WhatsApp material;
- successful deterministic EVA JSON/image/manifest generation as the
  once-per-case `First sent to Engineer` proxy, with later revisions distinct;
- issued report/addendum versions and exact report-Sent evidence;
- lease/conflict recovery; and
- permanent action history.

EVA owns actual named-Engineer assignment. Pegasus must not describe the export proxy as replacing EVA’s engineering workflow.

No-result, unknown, stale, partial, unavailable, and failed vehicle/external
states are distinct from a confirmed value. Refresh retains last-good data and
never overwrites a staff-confirmed value. The UI shows source/version, prior and
new value, actor, time, outcome, and reason when reconciliation changes business
truth.

Roadworthiness and Assessment are independent professional findings. Correction
retains the earlier accepted finding and displays the reasoned superseding
version; a closed Case must be reasonedly reopened before revision. A finding or
report correction never implies a fee/invoice change.

Report generation, PDF custody, Outlook Sent evidence, and external receipt are
separate. Report sent enters post-report work rather than closing the Case.
`CASE-23` query/dispute controls are `Next / 0.4.0` in the [capability inventory](../capabilities.md#capabilities); the alpha UI invents no reply state machine.

Lifecycle actions use only the named [Core lifecycle and correspondence contract](../frd/frd-01-case-identity-and-lifecycle.md#lifecycle-closure-and-correspondence): Post-report completion, Provider cancellation, Collision Engineers rejection, and Created in error remain distinct from acknowledgements, information requests, report-Sent evidence, queries, and other correspondence. The interface never substitutes a generic Close action. A closed Case is read-only; only a permitted reasoned reopen to a valid nonterminal state restores mutation controls, and `Created in error` offers only its linked-replacement route.

Each Case has at most one authorised staff editor at a time through the [Core lease and mutation guard](../frd/frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery). Other authorised staff see the holder and that Case read-only. `Enter edit mode`, renewal, `Leave editing`, authoritative expiry, reload/compare, and reacquire are the only recovery interactions: lease loss or a stale version disables every mutation, preserves proposed values for comparison, and never overwrites the newer Case. There is no forced Administrator takeover, bulk Case edit, direct external edit, or collaborative merge control.

### Documents and external evidence

- Create the Box case folder using the immutable Case/PO name.
- Retain source emails, instruction documents, images, correspondence, and reports.
- Preserve document and issued report/addendum versions.
- Use logical removal; never physically delete files through the workflow.
- Closed-case documents are read-only until the Case is validly reopened.
- Show Box unavailable, pending, retry, and unknown states rather than implying success.
- Provide authorised staff upload, view, download, and export actions.
- Treat request-scoped public upload as request-local receipt only, not Case creation, Box custody, EVA handoff, report generation, or delivery.
- Private transient Worker staging is not a staff surface or downloadable area.
- Keep picture upload, report-with-PDF handoff, PDF generation/custody, and external delivery as distinct evidence states.
- Report evidence uses the exact Outlook Sent item and keeps discovery, link, and sent times distinct.
- Manual link, unlink, or relink requires a reason and deterministically recomputes dependent events/counts.
- Preserve the final accepted Sent association even if Outlook later moves or deletes the item.
- Ambiguous or absent evidence remains visible.
- Triage reply evidence and Case report-Sent evidence are separate contracts.
- Chasers are copyable for manual sending; automated outbound messages are deferred.

### Search and filters

The exact UI-07 fields are:

- Case/PO;
- Image Intake Reference;
- registration;
- claimant;
- claim number;
- principal;
- state;
- Engineer;
- received date;
- instruction date;
- date range;
- origin.

### Permanent history

The History panel is a read-only presentation of the [Core-owned permanent action history](../frd/frd-04-parties-accounts-and-access.md#permanent-action-history). It shows the attributable staff or automated actor, caller, time, one affected Case or pre-case record, action/outcome, reason where required, and before/after or evidence reference needed to understand each business event. It does not render message bodies, routine views, refresh/polling, retries, lease heartbeats, or adapter/Worker mechanics; those remain telemetry or security evidence outside the operational UI.

## Complete UI state contract

| Scope | Required states |
| --- | --- |
| Queries | Loading; empty; current success; stale with last-good time; partial; unavailable; failed/retry; unauthenticated; disabled; stale-role; denied |
| Mutations | Validation; confirmation; success; denied; stale version; lease lost; dependency unavailable; idempotent/replayed result; conflict and recovery |
| Intake | Empty/oversize; replay; retention/custody failure; Ready for case allocation; Needs sorting; Unsupported; missing/integrity asset; evidence missing/contradictory; reasoned Blocked intake/resolve/retry; every acceptance path; refusal with no case/reference; upload token expired/revoked/cross-request/limit/abuse result |
| Triage | Registration missing; unassigned/assigned; every named state; missing/ambiguous/unapproved/technical reply evidence; finding replacement/correction/new response; cancel/reopen/link/unlink/relink |
| Case | Not ready/chasing; Review; Held/preserved interval; due/overdue; chaser last-outcome/next-action; gate refusal; physical address/Image Based Assessment; VRM and vehicle/MOT suggestion/no-result/stale/unavailable/failure; independent finding correction; documents locked; Box/external-effect states; EVA proxy/revision limitation; report generated/custodied/sent/externally received distinction; report evidence absent/ambiguous/manual/exact; every terminal outcome; archive; reopened; Created-in-error nonreopenable; lease held/expired/lost/stale |

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

When a planned surface has a real caller, record:

1. keyboard-only traversal;
2. screen-reader and semantic inspection;
3. focus and error behavior;
4. 1280px-and-wider desktop review;
5. 1024–1279px constrained-desktop review;
6. 200% zoom review;
7. forced-colours review;
8. reduced-motion review;
9. contrast review;
10. automated accessibility scanning through the real caller.

Each visible capability/state also needs authenticated Web-caller and named Core-owner evidence. Generated imagery or synthetic operational material cannot prove acceptance. Operator review uses approved, genuine, local immutable material only.

## Deferred and absent UI seams

Exact horizon and first-introduction release remain owned by the [capability inventory](../capabilities.md#capabilities). No future allocation creates an alpha route, control, workflow, placeholder or dormant implementation.

### Deferred integration and intake surfaces

There is no alpha control, route or placeholder for:

- additional provider activation beyond the alpha source policy;
- `desk@`, `engineers@` or `info@` automatic ingestion;
- legacy DOC, MSG or scan-like PDF OCR extraction;
- automatic matching beyond the operator-directed INT-28/INT-32 image/instruction pairing at the accepted ADR-0019 bar;
- broader mailbox identity, taxonomy mapping, folder recommendation/move, suggested actions, case association or mailbox browsing;
- Receiving work, Queries, Other or a full email-management workspace;
- post-report query/dispute work;
- provider submission/status/result APIs;
- broader classified-email MCP actions;
- AI/vision assistance for vehicle images or damage evidence;
- spreadsheet preparation of future inspection-address/repairer reference data.

Provider APIs and MCP are non-browser boundaries and do not create staff-shell destinations.

### Deferred casework and advanced surfaces

There is no alpha control, route or placeholder for:

- automatic chaser or report sending;
- authenticated compose/reply/forward/send in Pegasus;
- Diminution or Commercial case workflows;
- automated WhatsApp ingestion;
- an in-app AI assistant or AI-assisted identification, action, extraction or address suggestion;
- replacing EVA assignment, estimating, valuation, report preparation or engineering workflow;
- direct EVA, Audatex, valuation, finance or invoicing integrations;
- guided mobile image capture or third-party guided-capture integration;
- a custom application domain;
- a canonical Engineer workbench, repair specification, valuation, salvage or deterministic report-output workflow;
- AI-generated query-response proposals or durable `Send to AI` work;
- management information for Engineer throughput, query rates, Audit uplift, principal report/invoice measures or turnaround;
- `AI Assessor`.

Deferred AI may propose but must not mutate, accept or send autonomously. Future deterministic outputs must use one accepted structured case/engineering record, validate accepted data, calculate once and avoid duplicate truth owners or output-specific source forks.

One recorded exception (operator widening, 2026-08-03; extended by the
operator-approved AI-09 specification —
[ADR-0021](../adr/0021-automation-actor-direct-write-assessment-contract.md) and
[FRD-11](../frd/frd-11-reports-correspondence-and-reviewed-proposals.md), the
re-entry specification for exactly this slice): the Engineer assessment
workbench (UI-15) exists as design markup under
`src/Pegasus.Web/Pages/Cases/Assessment/`, and the AI-09 wiring task
restored its route (`/Cases/{id}/Assessment`, unlinked from every
navigation and case surface) to bind only the case-identity header, the
readiness rail, the `Send to Claude` panel, and the PAV sensitivity
slider. Every section form stays empty and unbound; the staff save paths
and the review presentation of unconfirmed automation values remain
forbidden until the full UI-15 re-entry approval.

**Unbound markup proves nothing (recorded 2026-08-17).** Where a supplied
design shows a capability the inventory allocates beyond this release, the
markup may exist on this surface but is *implemented* only in the weakest sense
of the evidence tiers above: no caller, no deployment, no acceptance. Three
rules keep it from reading as more than that.

- It carries **no model binding and no handler**. An unbound section that
  posted somewhere would be the capability, not a picture of it.
- It shows **no fabricated operator data**. Inputs render empty and read-only
  figures render as an em dash or a named empty state — never a plausible
  number. A convincing valuation nobody calculated is worse than no valuation.
- Where the control is one that will genuinely arrive, it **stays visible and
  states its condition** rather than vanishing, using the disabled-with-
  condition idiom. The assessment's estimating-service links and assessment
  import are the current instances: EXT-12 and EXT-13, both `Later / 1.0.0`,
  each requiring its own accepted contract. A control that disappeared would
  say the work is not coming; one that looked live would say it had arrived.

This is a presentation allowance inside an already-recorded exception. It
allocates nothing, and every deferred capability still re-enters the complete
design route before it is wired.

The `Send to Claude` panel states are server-rendered: `available` (the
confirm dialog then a real POST), `sent` — "Sent. Changes will appear on
this case for your review." with a `Check for completion` reconcile
control (manual refresh, no auto-poll), `completed` — "Claude has
finished" linking the case history, `failed` — "Nothing was sent" with a
retry, and `unavailable` naming its reasons in text. The PAV slider on the
Valuation section is a review aid only: a labelled `input type="range"`
paired with a numeric input, tabular numerals, ranges only from recorded
valuation figures, the indicative settlement (PAV − salvage) as text on a
recorded total-loss outcome, named missing-evidence states for every
absent input (a ratio without a costed repair total is not evidence), no
animation, no new colour tokens, and it writes nothing.

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
- predecessor data import, predecessor availability after cutover or predecessor code reuse;
- SMS or Microsoft Teams integration;
- customer/claimant portal (request-scoped upload links under INT-31 remain permitted; a link exposes no case or request state and creates no account);
- independent Engineer accounts;
- solicitor, insurer, repairer or vehicle-owner accounts.

A supported desktop reflow does not alter the permanent mobile-product boundary.

## Source and runtime map

| Concern | Durable owner or source | Runtime consumer or evidence |
| --- | --- | --- |
| Product capability and horizon | [Requirements](../prd/README.md), [capabilities](../capabilities.md) | Planned staff routes; current caller is narrower |
| Open policy and token questions | [Open decisions](../open-decisions.md) | No implementation inference until resolved |
| Architecture and caller boundaries | [Architecture](../current-architecture.md) | Core, Web, Worker, MCP and external adapters |
| Production, release, monitoring, and recovery state | [Operations](../operations.md) | No deployment claim from design or source presence |
| Setup, testing, release, and recovery procedure | [Runbook](../runbook.md) | Procedure is not execution evidence |
| Engineering procedure | [Engineering](../engineering.md) | Reviewed implementation and verification; `.agents/skills/` routes remain subject to it |
| Design authority | This file | Approved Web tokens, assets, components and patterns |
| Current Web shell | This file’s approved direction; current code is evidence only | `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` |
| Current Web tokens/layout | This file | `src/Pegasus.Web/wwwroot/css/site.css`, conforming: approved colour, spacing, 2px radius, 1px border and focus ring, with no unapproved literal and no new token |
| Current dashboard | Current exercised component map | `src/Pegasus.Web/Pages/Index.cshtml` |
| Current intake caller | Current Development pattern | `src/Pegasus.Web/Pages/Intake/` → Core `ProcessIntake` |
| Master logo | `docs/design/brand/logos/logo_no_margin.png`, checksum above | Renderer Core and the checksummed Web copy embedded by `_Layout.cshtml` |
| Renderer templates/style | Repository renderer asset sources | Embedded by `src/Pegasus.Infrastructure`; Core owns report policy and accepted presentation values |
| Engineer signatures | Repository renderer signature sources | Infrastructure embeds only the active Andy Patterson asset; excluded from Web decorative imagery |
| Retired renderer/skills/AI source | Git history and accepted integration records | No separate caller, runtime, or policy owner |
| Decision rationale | [Decision records](../adr/README.md) | Does not itself prove implementation |
| Change evidence | Git history | Does not replace caller, deployment or acceptance evidence |
| External reference qualification | [Reference index](../../reference/README.md) | Reference presence never creates authority |
| Claude Design system (design-tool bindings) | `docs/design/system/` — React bindings that render the classes of `site.css` byte-for-byte (`dist/styles.css` is a build-time copy, never a second token file); `.design-sync/` holds the sync config, per-component previews and conventions | claude.ai/design project “Pegasus Design System”. Design-tool output only: not referenced by `Pegasus.slnx`, the Web runtime, or any deployment; not a caller. Refresh after any `site.css` change (`cd docs/design/system && npm run build`, then `/design-sync`) |

The original `collision-engineers-design-dev` bundle supplied the shared logo, colour, type and icon foundation but explicitly did not define this internal command-centre application. The repository imports only approved shared essentials and renderer assets. Marketing layouts, imagery, fonts, WhatsApp styling, scroll reveals and mobile navigation are excluded. The source bundle is not retained as a second design system.

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

Status: **Planned `0.1.0-alpha.1` requirements with Operations-first shell selected.** This is the canonical publication of the reviewed `0.1.0-alpha.1` inventory. Shell selection does not approve every comparison-raster detail or prove a staff caller.

### Evidence state and scope

The implemented route set is owned by [architecture — current callers](../current-architecture.md); the alpha shell spans intake, image intake, cases, triage, search, operations, and administration; the desktop evaluator is separately owned ([ADR-0016](../adr/0016-standalone-desktop-email-evaluator.md)). This implementation state does not by itself prove deployment or operator acceptance.

The intended setting is a small office of approximately eight users. Staff accounts use Pegasus-managed usernames and passwords; authenticated Web callers derive the actor and roles server-side, while implementation does not itself prove deployed session behavior. Core owns the exact [staff role access matrix](../frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix), automated-actor boundary, and [case edit authority and recovery](../frd/frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery); this design must not create broader permissions or a second role policy.

| Actor | Planned UI boundary |
| --- | --- |
| Administrator | Staff shell plus Administration surfaces for accounts/access/roles, principals, configuration, approved mailbox allowlist, and the Automation client registration and activity review (enable/disable kill switch and permanent activity records addressable by correlation identifier; no secret display). |
| Engineer, User | Staff shell without Administration surfaces. The ordinary case/action controls are the same for both roles. |
| Automated processing | No UI account or interactive control. |
| Provider API client ([API-01–API-04, `Next / 0.4.0`](../capabilities.md#capabilities)) | No staff shell or Administration surface. |
| External/customer | No application account. A capability-bearing `/Uploads/{token}` link exposes only bounded document submission and generic terminal outcomes, with no case or request identity disclosure. |

Every protected route and action visibly handles unauthenticated, disabled-session, stale-role, denied, loading, and successful outcomes. Route or control hiding is never authorisation. The UI offers neither permanent deletion, credential/cloud/release administration, a generic mailbox-rule editor, bulk case editing, nor external direct Case editing.

### `0.1.0-alpha.1` flows

**Intake** presents the immutable source occurrence and its derived evidence separately from the editable candidate and accepted Case projection; matching conflict, ambiguity, manual association, reversal, and reassociation remain visible rather than rewriting the source. The evidence pane retains the exact `All`/`Instructions`/`Images` filters. Opening source evidence or supporting detail preserves the current list/detail position and every unsaved candidate edit; returning restores the Intake or Case-detail context without silently discarding or replacing proposed values. Controls invoke the Core-owned [source and Case association](../frd/frd-02-intake-and-source-identity.md#matching-conflicts-and-reversible-association) and [mandatory pre-case gate](../frd/frd-02-intake-and-source-identity.md#mandatory-pre-case-gates) contracts. The result view shows provenance, attachments/images, suggestions, validation, conflicts, origin, dispatch/retry state, the accepted `Review` or incomplete `Not ready` Case, or the explicit reason no case/reference exists.

**Triage** remains visually and navigationally distinct from a Case and from generic inbox sorting. Its list/detail workspace presents the registration gate, assignee, named findings and states, missing/ambiguous reply evidence, replacement history, completion/cancellation, reopen, and optional later Case association. Core owns the [normal Triage workflow and completion evidence](../frd/frd-03-triage.md#normal-workflow-and-completion-evidence); the design must distinguish ordinary acknowledgement or information correspondence from the exact reply-chain evidence required to complete the workflow.

**Case** keeps Case/PO, principal, registration, [Inspection, standalone Audit, or Inspection + Audit identity](../frd/frd-01-case-identity-and-lifecycle.md#case-types), workflow state, due date, and EVA proxy limitation visible. It presents the accepted Case projection alongside source/provenance, data, documents/images, parties and inspection address, vehicle/MOT, tasks/reminders, outbound evidence, external-work states, and permanent history. Core owns [principal and historical case-party identity](../frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity), [lifecycle closure and correspondence](../frd/frd-01-case-identity-and-lifecycle.md#lifecycle-closure-and-correspondence), [outbound correspondence evidence](../frd/frd-08-email-mailbox-and-background-processing.md#outbound-correspondence-evidence), and one-case [edit authority and recovery](../frd/frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery). The workspace identifies the active editor and stale version, becomes read-only after lease loss or named closure, and offers only the authorised retry/reopen/reacquire routes; one control mutates one current Case at a time.

**Administration** is an Administrator-only surface implementing the linked role matrix. It exposes account/access/role, principal successor, configuration, and approved-mailbox-allowlist controls, but no generic rules editor, credential/cloud operation, bulk predecessor import, bulk Case edit, or direct external Case-edit surface.

### UI-07 search and filters

Case/PO, Image Intake Reference, registration, claimant, claim number, principal, state, Engineer, received/instruction dates and range, and origin. Each result is one keyboard-focusable full-row link or button with a visible affordance.

### Operations and state boundaries

Operations shows Not ready, Review, Held, Unidentified, exact `Blocked intake`, separate Triage, Due today, New cases today, Sent to Engineer today/week, and Reports sent today/week. It uses Europe/London midnight days and Monday-week boundaries. `New cases today` has the exact Case-creation definition in the [requirements](../frd/frd-12-operator-experience.md#dashboard-freshness-and-reconciliation). Counts open their exact filtered queues; zero is distinct from stale/unavailable; last updated and manual refresh are visible. Receiving work, Queries and Other are `Next / 0.3.0` in the [capability inventory](../capabilities.md#capabilities), with no `0.1.0-alpha.1` surface.

An intake row always presents received date above received time and its precise processing outcome. At constrained desktop width, long Case/PO or Image Intake Reference text moves to a labelled second line; it must not overlap the received timestamp or another row field.

#### `0.1.0-alpha.1` surface inventory

- Intake includes manual upload; definitive/staff-resolved paths; immutable [source occurrence/dispatch](../frd/frd-02-intake-and-source-identity.md#source-occurrence-and-dispatch-identity) beside the Case projection; origin/custody; extraction and reviewed VRM suggestion; Image-initiated registration with its Image Intake Reference, lifecycle/merge/closure outcome, and no formal Case/PO state; field provenance, validation, ambiguity/conflict, association history, duplicate/retry, and missing/integrity asset/source failures. Each row identifies its exact outcome rather than a generic `New`.
- Case identity presents the Core-owned [Inspection, standalone Audit, and Inspection + Audit](../frd/frd-01-case-identity-and-lifecycle.md#case-types) distinctions, secondary Audit identity, immutable [Case/PO and principal](../frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity), and linked `Created in error` replacement without offering identity rewrite.
- Case work covers Not ready, Review and Held; separate mandatory instruction-completeness, image-completeness, and staff-review decisions before Engineers-queue eligibility, with no Pegasus named-Engineer assignment in alpha; due/overdue; seven-calendar-day chasers with the Held interval preserved; the Core-owned [staff-created temporary, revocable, expiring, request-scoped in-house upload-token](../frd/frd-02-intake-and-source-identity.md#request-scoped-upload-links) isolation, non-disclosure, and request-local custody contract; [copyable manual chasers](../frd/frd-01-case-identity-and-lifecycle.md#due-work-chasing-and-action-history); tasks/reminders; manual WhatsApp material; DVLA/DVSA and MOT/mileage; provider-determined inspection mode (physical address, or exact `Image Based Assessment` autofilled from the Principal's setting with reasoned override); and successful EVA JSON/image export only as the Sent-to-Engineer proxy.

- Case evidence shows retained source images, their provenance, category, staff-confirmed third-party exclusions, and advisory findings. It does not contain EVA or report-image selection/order controls; the focused alpha exports every eligible Case-vehicle image, EVA owns downstream ordering, and the accepted future Engineers screen owns those decisions after EVA replacement.
- Documents/evidence covers automatic Box folder, upload/version, logical removal, closed-case lock/reopen-before-change, Box unavailable/pending/retry/unknown, exact report-Sent evidence and reasoned manual link/unlink/relink.
- Terminal/aftercare presents the exact [Core-owned lifecycle and correspondence](../frd/frd-01-case-identity-and-lifecycle.md#lifecycle-closure-and-correspondence) outcomes and reasoned recovery paths. It must not turn acknowledgement, report-Sent evidence, or other correspondence into a generic completion action.

#### Complete state matrix

| Scope | Explicit states |
| --- | --- |
| Queries | loading; empty; success; stale/partial with last-good time; transient error/retry; unauthenticated/disabled/stale-role/denied |
| Mutations | validation; confirmation; success; denied; stale version; lease lost; dependency unavailable; idempotent/replayed result; conflict and recovery |
| Intake | empty/oversize; replay; retention/custody failure; Ready for case allocation; Unidentified; Unsupported; missing/integrity asset; evidence missing/contradictory; Blocked intake reason/resolve/retry; every acceptance path; refusal with no case/reference |
| Triage | registration missing; unassigned/assigned; every named state; missing/ambiguous/unapproved/technical reply evidence; finding replacement/correction/new response; cancel/reopen/link/unlink/relink |
| Case | Not ready/chasing; Review; Held/preserved interval; due/overdue; gate refusal; documents locked; Box/external-effect states; EVA proxy limitation; report evidence absent/ambiguous/manual/exact; every terminal outcome; archive; reopened; Created-in-error nonreopenable; lease held/expired/lost/stale |

The UI presents the [Core-owned permanent action history](../frd/frd-04-parties-accounts-and-access.md#permanent-action-history) with enough actor, time, outcome, reason, and before/after context to understand each business event. Routine views, refresh/polling, retries, leases/heartbeats, and adapter/Worker mechanics stay out of the operational history panel.

### Accessibility, desktop and data boundary

Use semantic landmarks/headings/tables, labels and associated errors, keyboard operation, visible focus, screen-reader announcements, practical 44px targets, forced-colours and reduced-motion support; state is never colour-only. At 1280px+ use dense multi-pane desktop. At 1024–1279px and 200% zoom, reorder essential desktop content into labelled tabs/drawers/sections without loss. Mobile staff UI is **Not planned**; a supported-device notice is only for genuinely unsupported devices, never a CSS-width substitute.

The contained visual boundary is warm off-white ground, white panels, warm-charcoal navigation, near-black text, CE-red primary/urgent accents, amber incomplete/pending, restrained navy Review and green only confirmed completion. Use system-sans 14–16px body text, sharp 2px corners, rare shadows and Lucide-style line icons. Each semantic action or state uses one consistent icon everywhere, drawn from the sixteen registered Lucide glyphs; generated or substitute replacement glyphs are prohibited. The [commissioned Pegasus marks](#the-pegasus-marks) are a separate, approved class: they name a surface rather than an action or a state, and never stand in for a glyph. Do not expose Azure, OCR, AI, queues or implementation mechanics in operator copy.

Evaluation and operator review use approved genuine local immutable material only. Do not invent operational inputs. Every deferred `Next` or `Later` capability carries its exact target in the [capability inventory](../capabilities.md#capabilities) and has no `0.1.0-alpha.1` control, navigation, workflow, or placeholder — except the recorded routeless UI-15/AI-09 review artifacts, owned by [design § Deferred casework and advanced surfaces](README.md#deferred-casework-and-advanced-surfaces). Every later UI change must re-enter the complete design route.

### Selected shell and open gates

Operations-first is selected for the `0.1.0-alpha.1` landing and navigation strategy. The three retained comparison rasters are selection evidence; Direction A's shell strategy is approved, but no raster is pixel-level authority or runtime proof. Policy-specific email predicates and acceptance evidence still block only their named automatic paths. Deferred `Next` and `Later` UI remains outside this selection regardless of its exact allocated target.

### Historical material

The selected Operations-first direction and the rejected Worklist-first and Case-first comparisons are recorded in the [design authority](README.md) (product direction, and rejected alternatives retained as evidence). Their obsolete planning files are retired; the [Operations-first](references/mockups/candidate-a-operations-first.png), [Worklist-first](references/mockups/candidate-b-worklist-first.png), and [Case-first](references/mockups/candidate-c-case-first.png) rasters remain immutable selection evidence. The current design route is [design](README.md), with interaction detail in [ui-spec.md](README.md#ui-specification).

## UI specification

Status: **Specification for the shipped `0.1.0-alpha.1` interface. The shell and landing strategy are Operations-first as selected; the routes, presentation rules and vocabulary settled by the operator on 2026-08-04 shipped in releases 6 and 7. Detailed raster styling remains subject to this specification and the design system.**

### Shared shell and hierarchy

1. Authenticated identity/role, navigation and sign out.
2. Surface title, the exact queue/filter, freshness and a safe primary action.
3. Operational table, workbench or record.
4. Named workflow/evidence/lease/exception state and consequential action.
5. Provenance, external identity, permanent business history and limitation.

The routes shipped in releases 6 and 7 are Dashboard, Inbox, Upload, Queues,
Cases and authorised Administration (operator decision 2026-08-04). Operations
is a scoped staff workspace in the implementation; its documentation does not
prove a deployed or released route. Search merged into Cases, which has the
identical backing query; the combined intake screen split into Inbox and Upload;
`Triage` no longer names a route while keeping its settled meaning as a pre-case
entity inside Queues. Each comparison direction uses the same focused-flow set.
Production email allocated `Next / 0.3.0` appears only after its gates; every
deferred `Next` or `Later` capability carries its exact target in the [capability
inventory](../capabilities.md#capabilities). Deferred capabilities have no alpha
placeholder route or control — except the recorded routeless UI-15/AI-09 review
artifacts, owned by [design § Deferred casework and advanced surfaces](README.md#deferred-casework-and-advanced-surfaces).

The Development/local email evaluator is separately owned and has no QDOS-alpha
route, navigation, control, `unchecked`/`checked` workbench, review-report
mechanic, or UI acceptance checkpoint. This does not remove the shared mail
policy, production-intake surfaces, Graph replay/live adapters, or the genuine
evidence required to activate them.

### Contracts

| Component | Required contract |
|---|---|
| Shell/access | Sign-in and disabled/stale-role/denied outcomes; permitted-route visibility plus server authorisation. |
| Metric/queue | Label, value or unavailable state, last-good time, current refresh state, and exact destination filter. `0`, loading, current, stale, partial, unavailable, and failed remain distinct. Dashboard includes exact `Blocked`, Due today, New cases today, and day/week Sent to Engineer and Reports sent. |
| Inbox/intake row | Received date above time; exact processing outcome rather than generic `New`; long Case/PO or Image Intake Reference moves to a labelled second line at constrained desktop width and never overlaps the timestamp. |
| Intake workbench | Persistent source identity; `All`/`Instructions`/`Images` evidence filter; evidence/candidate; fact versus suggestion versus confirmed value; provenance/missing/conflict; acceptance path and no-case failure consequence. |
| Search result | One full-row keyboard-focusable link or button with visible action affordance; all result text contributes to its accessible name without obscuring its identity fields. |
| Field provenance | Every editable or source-derived Case datum shows its current origin marker. Direct values identify staff entry, extraction, AI, provider API, or external vehicle/estimate origin; derived values identify accepted inputs and calculation. Origin and status remain distinct. |
| Supporting detail navigation | Opening source evidence or other supporting detail preserves list/detail position, the current Intake or Case-detail context, and every unsaved edit; returning never silently discards or replaces proposed values. |
| Request-scoped in-house upload | Authenticated staff create a temporary token bound to one request/operation and server-enforced expiry. The isolated public edge exposes bound upload fields and an immediate request-local result only; expiry, revocation, cross-request isolation, limits, custody, retry, abuse, and non-disclosing failures are explicit. |
| State action | Permitted transition, prerequisite, consequence, required reason, recovery and history link; never generic Close. |
| Readiness blocker | Every unmet requirement names its exact field or material, source/provenance, reason, and permitted resolution. The UI has no opaque aggregate blocker; actions enable only from their explicit current prerequisites and no unrelated save resets state. |
| Identity header | Read-only Case/PO/principal, registration, type/secondary Audit identity, workflow state, `Due by`/overdue state, and EVA proxy limitation. |
| Due/chaser panel | Missing-material reason, next chase, last recorded channel/outcome, optional note, and next permitted action together. Copy/preparation is not sent or delivered; Triage has no such panel. |
| Inspection address | Provider-determined default from the Principal's inspection-mode setting (exact `Image Based Assessment` autofilled, or physical vehicle/repairer address); reasoned per-Case staff override; address fields appear only for the physical mode and never imply attendance. |
| Engineering findings | Separate Roadworthiness and Assessment controls; accepted and superseded versions; correction reason/history; reopen requirement; no inferred fee/invoice mutation. |
| Evidence/document panel | Original/source/version/logical removal/closed lock; Box/external state; issued report versions; exact Outlook evidence with separate discovery/link/sent times. |
| Evidence image preview | Loading and source-preserving enlarged-image states are explicit; opening or closing a preview preserves Case context and does not alter source, category, advisory, or report-image selection. |
| Email quick preview | At allocated mailbox-workspace activation, keyboard and pointer intent exposes an accessible preview that neither clips/obscures adjacent controls nor changes message or Case state; focus departure dismisses it. It shows sender, subject, timestamp, excerpt, classification, association and attachment names, but no mutation controls. |
| Mailbox refresh | No automatic refresh while an operator is reading or acting. Manual refresh retains active list context and an open message where available. If it leaves the active scope or becomes unavailable, keep detail visible with explicit no-longer-in-this-view state and return-to-list action. |
| Email-management workspace | Planned `Next / 0.3.0`: land on the incoming Inbox across all approved mailboxes, newest received message first. Mailbox, folder, queue and search filters narrow that view, remain visible and are preserved on return from message or Case detail; a fresh visit resets to the default all-Inboxes view. The workspace provides manual refresh, last successful update time and distinct stale/unavailable state, preserving active filters, page and open message where still available. Sent and read-only Deleted Items search are explicit folder scopes. General search includes retained message bodies, attachment filenames and searchable attachment content; unavailable content is explicit. Search remains in the current mailbox/folder scope unless explicitly broadened, and results are individual messages rather than collapsed conversations. Each result identifies body, attachment-name or attachment-content hits and names a matching attachment. Inbox and search-result lists use accessible pagination, not infinite scrolling. Inbox rows include a short body excerpt beneath sender and subject, and display read/unread state without changing it. Opened messages preserve list context and show full message, retained attachments and a chronological independently openable thread limited to retained messages in approved mailbox/folder scope. Show classification, queue, processing outcome and Case association before actions. Classification, linking and folder-move actions exist only in message detail and only for one exact message; no bulk actions. A folder move follows a saved classification as a separate confirmation to the policy-designated folder only; reclassification to a new designated folder requires another separate confirmation; failure preserves classification, remains visible and allows staff-initiated retry; success removes the message from Inbox without duplication and preserves it in destination-folder/search scope. Linking uses Case search, target summary, reason and confirmation; no `View in Outlook` action. Selecting a Case opens it in the same tab; Back restores the message detail and list context. Each Case has one newest-first chronological history of its associated received and Sent correspondence, with explicit oldest-first ordering; this workspace remains the cross-mailbox browsing and reconciliation surface. |
| Report-image selection | Future Engineers-screen surface; no `0.1.0-alpha.1` surface. The Engineer report-generation section, not Case evidence, selects and orders report images. It requires a human-confirmed readable registration for the first overview, excludes reflections, and distinguishes an advisory from a human decision. |
| Lease/conflict | Holder/expiry/recovery, read-only alternative, current conflict and preserved proposed values. |
| History | Business mutation/accepted evidence/export/material business failure only; no routine views, polling, retry, lease heartbeat or telemetry. |
| Reason dialog | Named requirement/consequence, labelled reason, confirmation/cancel, initial focus, focus containment, Escape where safe and focus return to the invoking control. |

### Presentation responsibilities

Product requirements own business gates and outcomes; this specification owns
how they are presented and operated. Lists expose identity, state, freshness,
filter, provenance, and permitted action. Detail pages expose source evidence,
accepted facts, missing/conflicting values, history, leases, external status,
and reasoned transitions without duplicating Core policy. The shell and
dashboard own navigation and exact queue metrics; administration surfaces own
authorised configuration journeys; error, empty, loading, denied, stale,
partial, conflict, and unavailable states are explicit.

#### Enforced presentation rules

These are the rules every operator surface is held to. They were the
presentation contract of the 2026-08 UI implementation programme and outlived
it; the programme's own review folder was deleted once its work landed, and
these are what remained true.

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
   condition is present, disabled, and states the condition.
7. **Counts and times cannot be proved locally.** A count query against an
   empty database returns the same zero as a correct one, and a rendered time
   against a Europe/London workstation clock matches the office by accident.
   Both need evidence from populated data and a non-London clock — a test that
   stores rows, or the deployed instance.

### Focused flows

**Intake:** source -> `All`/`Instructions`/`Images` evidence filter ->
evidence/candidate -> safe processing plus deterministic Principal and Case type
creates exactly one Case/reference. Incomplete ordinary data, images, or
applicable progression requirements yield **Not ready**; **Review** follows
only when the explicit route policy permits it. `Blocked intake` with a
required reason creates no Case/reference when an identity-critical gate fails;
fail-closed `Needs sorting` remains Unidentified work. Resolve/retry re-enters the same
path and may create exactly one Case/reference only after it establishes the
identity-critical facts. Manual image/instruction link and reasoned reversal
retain original origins.

Opening evidence or supporting detail from Intake preserves the active `All`/`Instructions`/`Images` filter, selected record, scroll/list-detail position, and every unsaved candidate edit. Return restores the originating Intake or Case-detail context without reloading over proposed values.

The request-scoped in-house upload route is a distinct public edge of that
intake flow. Authenticated staff create a temporary token bound to one request,
its allowed operation, and a server-enforced expiry; staff can revoke it. The
isolated unauthenticated surface uploads only to that request and returns an
immediate structured result. Expired, revoked, cross-request, type/count/size
limit, custody, retry, and abuse outcomes reveal no case, reference, request
history, other upload, token-management function, or external account. Success
proves request-local custody only, not case creation, Box custody, EVA handoff,
report generation, or external delivery.

**Triage:** distinct inbox classification/label plus dedicated pre-case
list/detail; never a case state. Missing registration goes to `Needs sorting`;
Open/Awaiting information/Finding recorded/Completed/Cancelled; two
independently optional findings, with at least one required before Finding
recorded/Completed: Roadworthiness = Roadworthy/Unroadworthy and Assessment =
Repairable/Total loss. A case's `has Triage` is Boolean/reference-only. Triage
findings are reference-only and do not affect Case/PO/reference, workflow,
professional findings, final outcome, Engineer report, Audit suffix/allocation,
fee, invoice, or any other case decision. Exact reply-chain evidence;
reasoned pre-send replacement and post-send superseding finding/new response;
optional assignee; reasoned case link. Reopen returns to Open and preserves the
prior finding/reply. No due/chaser UI.

**Case:** read-only until an explicit edit lease. The persistent header keeps
Case/PO, principal, registration, type/secondary Audit identity, state,
`Due by`/overdue, and EVA proxy limitation visible. The work area keeps the
missing-material reason, next chase, last recorded channel/outcome, optional
note, and next action together; due/chaser work is separate from `New cases today`.
Overview, data, provenance, documents/images, vehicle/MOT, tasks/reminders,
request-scoped in-house upload token, EVA export, report evidence, and history remain
focused sections.

Inspection address defaults from the Principal's inspection-mode setting:
exact `Image Based Assessment` without fabricated address fields, or physical
vehicle/repairer address with address fields; staff may override the default
on a Case with a reason. Ordinary-image VRM and vehicle/MOT results show suggestion,
confirmed, unknown/no-result, stale, unavailable, and failed distinctions with
source/version/age; refresh never overwrites confirmed or last-good data.

Image readiness display is a future surface: the advisory (registration overview, damage close-up, and the applicable reflection criterion, refreshed whenever current Case images change, with no Case-state, eligibility, or chase effect) is owned by [AI-05, `Later / 1.0.0`](../capabilities.md#capabilities) and has no `0.1.0-alpha.1` surface.

Roadworthiness and Assessment are separate professional findings. A correction
shows the retained earlier version and reasoned superseding version; a closed
case requires reasoned reopen before revision. Issued report/addendum versions
and exact Sent evidence remain distinct; report sent enters post-report work
and does not close the case. A Box PDF, upload, export, or queue result is not
delivery evidence, and correction never implies a fee/invoice change.

Named actions cover Not ready, Review, Held, terminal outcomes, archive/reopen.
Held preserves the chase interval; Created in error offers only linked
replacement and never Reopen.

**Administration:** account creation/disable/access review/roles, principal
successor cutover, configuration and mailbox allowlist. No generic rules editor
or cloud/credential operation.

The complete per-scope query, mutation, Intake, Triage and Case state contract
is the [requirements state
matrix](README.md#complete-state-matrix); this specification does not
compress or replace it.

### Freshness and reconciliation

Every query keeps the last successful value/time visible when a later refresh
is stale, partial, unavailable, or failed. Manual refresh reruns the same
filter; it never substitutes zero, marks an external action complete, or
changes a business fact. Show start/completion feedback and a safe retry.

Routine refresh audit belongs to content-safe telemetry. When staff accept,
reject, link, or change an external fact during reconciliation, show the
source/version, prior and new value, actor, time, outcome, and required reason
in permanent history.

### UI-07 exact search and filters

Case/PO, Image Intake Reference, registration, claimant, claim number, principal, Case stage, Engineer, received/instruction dates and range, and origin.

These are the Cases filters. There is no separate Search route: the former
Search screen ran the identical backing query, so it merged into Cases
(operator decision 2026-08-04). The common filters sit on one line and the
rest behind a `More filters` disclosure.

### Exceptions and necessary copy

Use guidance only where the operator must understand a consequence:

- “Blocked — a reason is required.”
- “No case or reference was created; review the missing or conflicting evidence.”
- “Created in error cannot be reopened. Create and link the replacement case.”

Illustrative text must not fabricate operational input. Loading, empty, stale/partial, retryable error, denied/unauthenticated, validation, conflict, external-unknown and reopened behavior follows the full state matrix. Permanent consequences remain visible without hover or colour alone.

### Accessibility and acceptance

Use skip link, labelled navigation, semantic tables/captions/header/sort state, keyboard queue selection, pane/tab relationships, associated error summary, restrained live announcements, visible focus and safe modal focus handling. At 1280+ use dense panes; at 1024–1279 and 200% zoom, turn secondary panes into labelled tabs/drawers/ordered sections while identity/state/actions remain first. Mobile is `Not planned`.

When implemented:

- each visible trace row and state needs authenticated Web-caller and named Core-owner evidence;
- keyboard, screen-reader, focus/error, forced-colours, reduced-motion, 1280+ desktop, constrained desktop and 200%-zoom inspection must be recorded;
- operator review uses approved genuine local immutable material only; generated imagery or synthetic operational material cannot prove acceptance; and
- every UI capability allocated after `0.1.0-alpha.1` re-enters inventory, specification, alternatives, independent review, explicit approval, visual generation and manual visual review before its exact target can be implemented.

### Image-initiated Case surface

Vehicle-image arrivals with one usable VRM use the Image-initiated Case route.
List/detail surfaces show the immutable VRM reference, preserved group and
filenames, and the lifecycle state — Awaiting definitive instruction, Merged
into Instruction-initiated Case, or Staff-closed. Search accepts the exact
reference or VRM. Merge links both histories; staff closure requires a reason
and makes the record read-only. Conflicting or unreadable groups use
Unidentified instead. There is no dedicated Box custody surface for the
Image-initiated Case in this slice (see FRD-05); preserved source files remain
reachable through the origin receipt.
