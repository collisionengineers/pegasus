# Collision Engineers — Renderer Design System

This document describes how the Collision Engineers house style is encoded in the
renderer: the colour palette and where each colour is used, the two typographic
registers, the anatomy of the letterhead and running footer, the component classes
defined in `report.css`, and the rule that the logo and signatures are embedded
assets. It also records the constraint that no "AI" theming is permitted anywhere in
the output.

The Pegasus top-level design system has a single stylesheet source of truth:
`design/assets/report-renderer/templates/report.css`. The Core project links and embeds
that stylesheet, then inlines it into every document, so the look is identical whether
a page is produced from the CLI, the WinUI 3 desktop app or the cloud API.

The system is faithful to two prior bodies of work: the CSS-native
`collision-engineers-design` system, and the proven `report-renderer`
(Python/WeasyPrint) whose preferred sample outputs the data register reproduces
verbatim.

## How the brand maps into the renderer

The brand is expressed as plain HTML and CSS, rendered to PDF by headless Chromium.
Three things carry the brand into each document:

1. **The stylesheet** (`report.css`) — colours, typography, table styling, the value
   box, the fee table, the signature block, and the media slots.
2. **The letterhead shell** — a small amount of C#-built HTML in `HtmlComposer`
   (the logo, the Our/Your Ref/Date block, the centred title, and the running footer).
3. **The embedded brand assets** — the master logo and the engineer signature images,
   inlined as data URIs from `BrandAssets`.

Adding a template never touches the brand: a new document type reuses the same
stylesheet, the same letterhead shell, and the same embedded assets.

## Palette

The palette is defined directly in `report.css` (and in the C#-built footer template).
Colours are referenced by literal hex value rather than CSS custom properties.

| Colour | Hex | Where it is used |
| --- | --- | --- |
| Documents red | `#C80A32` | Section-heading rules (the red rule under each `h2`/`.sec-h`); table header row backgrounds (`th`, `.fee-table th`); the value figure in the value box (`.value-box .value`); the red border and cell borders of the summary `data-table`; the red title variant (`.title.red`); the fee-note total rule (`.fee-totals .r.total`); the thin red rule in the running footer. |
| Warm charcoal | `#222` | Body text colour and most headings. (The brand reference charcoal is `#2C2A27`; the printed body resolves to `#222`.) |
| Grey label cells | `#F2F2F2` | Key cells in the value box (`.value-box td:first-child`), the `data-table` key column (`td.k`), and the fee-note total row background. |
| Zebra | `#F5F5F5` | Even-row striping in data-register tables (`tbody tr:nth-child(even) td`). |
| Grid | `#BEBEBE` | Cell borders in data tables, the value box, the key/value table, and media placeholders. |
| Link blue | `#0057B8` | Hyperlinks (`a`), e.g. linked advert references. The header `Our/Your Ref` link styling is not applied; refs are plain text. |
| Footer / muted grey | `#555` | Footer strapline text, fee-note detail and notes, signature role line, media notes. |
| Media placeholder fill | `#F4F4F3` | Empty photo slots (`.media-ph`) where no image is supplied. |

`print-color-adjust: exact` is set on the footer so the red rule prints rather than
being dropped by the print pipeline; `PrintBackground` is enabled on the PDF engine so
red header rows and grey cells render in the output.

## Typographic registers

Two registers share one letterhead. The choice of register is fixed per template type,
not per document.

| Register | Base size | Body line-height | Applies to | Body treatment |
| --- | --- | --- | --- | --- |
| Data | 8.8pt | 1.22 | Market valuation evidence, advert evidence pack, fee note | Dense tabular layout |
| Letter | 10pt | 1.5 | Expert reports | Justified prose paragraphs (`.doc-p`) |

The data register is the document `body` default (`font-size: 8.8pt`). It is ported
verbatim from the WeasyPrint renderer so output matches the preferred style examples.
The reference table, advert tables, key/value table and value box all sit at or near
this size.

The letter register is layered on top for expert reports through dedicated classes:
`.doc-p`, `.doc-ul`, `.salutation`, `.re-line`, `.sec-h` and the signature block are
all 10pt (with headings slightly larger). Expert-report paragraphs are justified;
data-register tables are not.

Both registers use **Arial / Helvetica**, A4 portrait. The Dockerfile adds
`fonts-liberation` so the Linux container reproduces Arial metrics for the body text.

### Headings and titles

| Element | Class | Size | Notes |
| --- | --- | --- | --- |
| Document title | `.title` | 14pt, uppercase, centred | `.title.red` for newer red titles; `.title.underlined` optional. Expert-report titles are upper-cased and may take the red variant. |
| Subtitle | `.subtitle` | 9.6pt, centred, bold | Optional. |
| Section heading (data) | `.section h2` | 10.6pt, uppercase intent, red rule under | `1.5pt` red bottom border. |
| Section heading (letter) | `.sec-h` | 10.5pt, uppercase, red rule under | `1.5pt` red bottom border. |

## Letterhead and footer anatomy

Every document is wrapped by the same shell, built in
`HtmlComposer.Letterhead(...)` and `HtmlComposer.FooterTemplate(...)`. The page
geometry is supplied by the PDF engine, not the stylesheet: A4 portrait with margins
top `1mm`, left/right `12mm`, bottom `22mm` (the deeper bottom margin reserves space
for the running footer).

### Letterhead (page 1 banner)

```
.document-header  (flex, logo left / reference table right)
├── img.logo                 master gear-"C" logo, 53mm × 30.3mm, inlined data URI
└── table.reference-table    Our Ref / Your Ref / Date
        Our Ref:   <value>     (always present; falls back to a per-document ref)
        Your Ref:  <value>     (rendered only when supplied)
        Date:      <value>     (falls back to today's date)
```

`Our Ref` falls back to a sensible per-template value when not set in the payload —
the subject registration for a valuation, the fee-note number for a fee note, or
`REPORT` for an expert report. `Your Ref` is omitted entirely when blank.

### Title and sections

Below the letterhead sits a centred uppercase title (`MARKET VALUATION EVIDENCE`,
`ADVERT EVIDENCE PACK`, `FEE NOTE`, or the expert report's own title), followed by the
document body. Each section heading carries a red rule beneath it.

### Running footer (every page)

The footer is a Chromium running-footer template, so it repeats on every page with
live page numbers. Its anatomy:

```
┌ thin red rule (0.6pt #C80A32, inset 17mm left/right) ────────────────┐
│  <strapline>                                       — n of N —        │
└──────────────────────────────────────────────────────────────────────┘
```

The standard strapline is:

```
Collision Engineers Ltd | www.CollisionEngineers.co.uk | engineers@collisionengineers.co.uk
```

Fee notes swap the email for the VAT registration number:

```
Collision Engineers Ltd | www.CollisionEngineers.co.uk | VAT Reg No. <number>
```

(When a fee note has no VAT number, the footer drops the third segment.) The page
marker on the right uses Chromium's `pageNumber` / `totalPages` placeholders rendered
as `— n of N —`.

## Component classes in `report.css`

These are the reusable building blocks. The data register supplies the table base;
specialised classes layer brand styling on top.

### Table base (data register)

```css
table  { border-collapse: collapse; width: 100%; }
thead  { display: table-header-group; }   /* header repeats across pages */
tr     { break-inside: avoid; }           /* rows never split across a page break */
th     { background: #c80a32; color: #fff; }
td     { border: 0.4pt solid #bebebe; }
tbody tr:nth-child(even) td { background: #f5f5f5; }   /* zebra */
```

The repeating header group and `break-inside: avoid` are what keep long tables legible
across pages: a 36-row valuation flows to multiple pages with the red header row
repeating and no split rows.

### `kv-table` — key/value pairs (data register)

Two-up label/value grid used for subject-vehicle details on valuations. Labels render
as white-background header cells (`background: #fff`, grid border, regular weight, 20%
width); values sit in adjacent cells. Zebra striping is suppressed (even rows stay
white).

### `data-table` — red-bordered summary table (letter register)

The vehicle/matter summary table inside expert reports. Distinguished by a `1.2pt`
solid red outer border with `0.6pt` red internal cell borders. The key column
(`td.k`) is grey (`#F2F2F2`, 18% width); the value column (`td.v`) is bold (32% width).

### `value-box` — headline figure (data register)

The boxed retail-value figure on the valuation. A grey label cell
(`td:first-child`, `#F2F2F2`) sits beside the figure cell; the figure
(`.value-box .value`) is rendered in documents red at 12pt, centred, no-wrap. The whole
box carries `break-inside: avoid` so it is never split across a page.

### Evidence / advert tables (data register)

Two fixed-layout advert tables share the table base but set explicit column widths so
the columns line up consistently:

- `.report-advert-table` — the comparable advert table on the **valuation**:
  number, vehicle, year, mileage, seller, price, comment. Comment text uses the
  `.small` class (7.6pt) to fit.
- `.evidence-advert-table` — the linked reference table on the **advert evidence
  pack**: advert id, vehicle, year, mileage, price, link cell.

Both build on `.advert-table { table-layout: fixed; }` and use the shared `.number`,
`.price`, `.year`, `.mileage` and `.small` helpers.

### `fee-table` and fee totals (fee note)

The fee note has its own components:

- `.fee-meta` — two-column bill-to / fee metadata block, with uppercase `.lab`
  labels.
- `.fee-table` — line items: red `th` header, bottom-bordered rows, right-aligned
  `.amt` amount column, muted `.fee-detail` sub-text.
- `.fee-totals` — right-aligned subtotal / VAT / total stack. The `.r.total` row has a
  `1.2pt` red top rule, a grey background, and a larger bold figure (10.5pt).
- `.pay-grid` and `.fee-notes` — payment details and trailing notes.

### Signature block (letter / valuation)

```
.sig-block            break-inside: avoid
├── .sig-closing      "Yours faithfully," (10pt)
├── img.sig-img       embedded signature image (≤ 14mm tall)
├── .sig-name         engineer name (bold, 10pt)
├── .sig-org          "Collision Engineers Ltd" (bold, 9pt)
└── .sig-role         role / qualifications (9pt, muted)
```

The closing, role and organisation fall back to sensible defaults
(`Yours faithfully,`, `Independent Automotive Engineer`, `Collision Engineers Ltd`)
when not supplied. An explicit empty string for `name`, `role` or `org` suppresses that
line entirely — this is how the firm-only rebuttal sign-off (`Yours faithfully,` /
`Collision Engineers Ltd`) is expressed. The whole block avoids being split across a page.

### Media slots (expert report)

```
.media-row            two-column grid, break-inside: avoid
└── .media-col
     ├── h4           caption (centred, 9pt bold)
     ├── img.media-img  supplied image, OR
     ├── .media-ph    grey placeholder (#F4F4F3) when no image is supplied
     └── .media-note  optional caption note (8pt, muted)
```

A media row is a two-up photo grid. When an image path is supplied it is inlined; when
absent the slot shows a bordered grey placeholder so the layout still reads cleanly.

### Density variants (valuation auto-fit)

Three density tiers are applied as a body class, ported verbatim from the prior
renderer:

| Density | Body class | Base size |
| --- | --- | --- |
| Normal | (none) | 8.8pt |
| Compact | `report-compact` | 8pt |
| Ultra-compact | `report-ultra-compact` | 7.5pt |

Each variant scales the logo, reference table, title, section spacing, table padding,
value box and advert text down proportionally. Fit-to-page templates render Normal,
then Compact, then Ultra-compact, re-counting PDF pages until they hit the page target
— the sample valuation auto-fits to Compact to stay on one page.

## Embedded assets rule

The logo and the engineer signatures are governed by top-level `design/` sources and
embedded into Core at build time, not read from disk at render time. `BrandAssets`
reads them from the Core assembly and inlines them as base64 data URIs:

- The master red gear-"C" logo (`design/brand/logos/logo_no_margin.png`) is loaded
  once into `LogoDataUri` and placed in the letterhead `img.logo`.
- Engineer signatures resolve by key to `design/brand/signatures/{key}.png`. The
  bundled keys are `andy_patterson`, `ed_mawdsley` and `neil_oreilly`. An unknown
  or missing key
  resolves to `null` and the signature block simply omits the image.

Because these assets travel inside the assembly and are inlined as data URIs, a render
needs no external file lookups, no network fetches, and no installed fonts beyond the
Arial-metric body text. This is what lets the same renderer produce byte-faithful
output on a Windows desktop and inside a Linux container. (Payload-supplied media in
expert reports may additionally reference `data:`, `http(s):` or a local file path;
the brand logo and signatures are never sourced that way.)

## No-AI-theming constraint

The output carries no "AI" theming of any kind: no sparkle or magic icons, no emoji,
and no decorative gradients. The visual language is calm and factual, matching documents
that go to courts, solicitors and insurers. The palette is limited to documents red,
charcoal, the greys and the link blue listed above; ornamentation is restricted to the
red section rules, the red header rows, and the single boxed value figure. This is a
hard constraint on every template and on any future template added to the system.
