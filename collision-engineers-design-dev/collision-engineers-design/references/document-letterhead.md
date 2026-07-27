# Document & Report Letterhead Spec

The canonical A4 letterhead for all CE expert reports, valuations, addenda, rebuttals, and fee notes. Read this before building any document template.

---

## Page setup

- **Format:** A4 portrait
- **Margins:** generous (~2cm / ~20mm sides, ~22mm bottom, ~12mm top clearance after header)
- **Body typeface:** `Arial, Helvetica, sans-serif` — system-locked sans. Not brand faces for body copy.
- **Body size:** ~13.5px (10pt), line-height 1.6, justified alignment
- **Doc red:** `#C80A32` — used for section-heading rules, table header rows, and the headline value figure

---

## Header (repeats on every page)

```
[Logo — master gear-C, top-left, ~53mm wide]     [Our Ref:  XXXXX  ]
                                                  [Your Ref: XXXXX  ]
                                                  [Date:     DD/MM/YY]
```

- Logo: `assets/logo_no_margin.png` (red gear-C lockup)
- Reference block: right-aligned, bold labels, regular values. 54mm min-width.
- Continuation pages: carry a one-line running head with the same three refs.

---

## Title block (centred, below header)

```
TOTAL LOSS REPORT               ← bold, UPPERCASE, centred
RE: [Vehicle] — [Registration]  ← optional centred subtitle
```

On newer rendered outputs the title is in **doc red** (`#C80A32`). On letter-style documents it may be black + underlined. Either is acceptable.

Opening lines (`Dear Sirs` / `FAO …` / `RE: …` matter line) are bold; on some templates set in doc red.

---

## Section headings

```
NATURE OF INCIDENT              ← uppercase, bold
──────────────────────────────  ← 1.5–2pt red rule (#C80A32) immediately below
```

The red rule beneath every section heading is the signature document motif.

---

## Summary data table (vehicle / matter details)

- Thick red border (grid lines in `#C80A32`)
- Grey label cells (`#F2F2F2`, normal weight)
- Bold values
- Four-up key/value layout

---

## Evidence / comparables tables

| Column | Style |
|---|---|
| Header row | `#C80A32` background, white bold text |
| Even body rows | `#F5F5F5` zebra |
| Grid | Thin grey `#BEBEBE` |
| Currency columns | Right-aligned |

---

## Value box (assessed value / settlement figure)

```
┌─────────────────────────────────────────┐
│  Engineer's assessed retail value       │
│                        £12,500.00       │ ← large, bold, doc red
└─────────────────────────────────────────┘
```

Bordered box; grey label cell; headline figure in **large doc red** (`#C80A32`), centred.

---

## Footer (every page)

```
─────────────────────────────────────────── ← thin red rule (#C80A32)
Collision Engineers Ltd  |  www.CollisionEngineers.co.uk  |  engineers@collisionengineers.co.uk
                                                           — n of N —
```

Fee notes swap the email for the VAT registration number. Page marker `— n of N —` is optional but present on multi-page reports.

---

## Signatures

Transparent PNG files placed above the engineer's typed name:

```
assets/signatures/andy_patterson.png
assets/signatures/ed_mawdsley.png
assets/signatures/neil_oreilly.png
```

Below signature: typed name + *"Independent Automotive Engineer, Collision Engineers Ltd"* + qualifications (e.g. M.Inst.AEA) + AQP number.

---

## Case imagery

Impact area diagrams and vehicle inspection photos are inserted per report as content slots — they are not brand assets. The document layout provides space for them; treat as placeholder blocks.

---

## Local render (canonical)

Documents render **locally with Chromium** — Playwright's `page.pdf()` driving the same Jinja2 HTML
templates. This is the only render path; there is no remote/server-side renderer.

Reference implementation in `vehicle-valuation/assets/templates/`:
- `_base.html.j2` — base template with header, footer, content block
- `report.html.j2` — market valuation report template
- `styles.css` — print-safe A4 CSS with `@page` rule and all table styles

Render engine: set `VALUATION_RENDER_ENGINE=chromium` (Playwright `page.pdf()`). The logo is embedded
as a base64 data URI and CSS is injected inline so the PDF is self-contained.

### Embedding brand fonts (Tw Cen MT / Futura)

So the brand faces actually render on titles, the letterhead and display text (not just fall back to
Arial), the local Chromium render must embed this skill's brand fonts. The font binaries already ship
in this skill's `fonts/` dir, declared in `styles/colors_and_type.css`. Vendor that `@font-face` block
into the render — either link/inline `styles/colors_and_type.css`, or add the faces directly, pointing
at the in-repo files (same base64/local-file approach as the logo):

```css
@font-face {
  font-family: "Tw Cen MT Std";
  src: url("fonts/TwCenMTStd.otf") format("opentype");      /* regular */
  font-weight: 400; font-style: normal;
}
@font-face {
  font-family: "Tw Cen MT Std";
  src: url("fonts/TwCenMTStdBold.otf") format("opentype");  /* bold */
  font-weight: 700; font-style: normal;
}
@font-face {
  font-family: "Futura PT";
  src: url("fonts/FuturaCyrillicBook.ttf") format("truetype");
  font-weight: 400; font-style: normal;
}
@font-face {
  font-family: "Futura PT";
  src: url("fonts/FuturaCyrillicBold.ttf") format("truetype");
  font-weight: 700; font-style: normal;
}
```

The `url(...)` paths above are relative to wherever the `@font-face` rule is injected (the render context / the HTML or CSS file's location), so adjust the prefix to match — the canonical `styles/colors_and_type.css` uses `url("../fonts/…")` because it sits one level down in `styles/`; copy-pasting the wrong prefix yields a 404 and a silent Arial fallback.

Then set titles/display to the brand stack (`"Tw Cen MT Std", "Futura PT", sans-serif`) while keeping
body copy on `Arial, Helvetica, sans-serif`. The full weight set (Light/SemiBold/ExtraBold for Tw Cen,
Medium/Demi for Futura) is in `styles/colors_and_type.css` — reuse it rather than re-declaring.
