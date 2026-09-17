# `evaestimate.pdf` — extraction record

Extracted 16 September 2026 with pdfplumber/PyMuPDF (word positions) and
`pdftotext -layout`. One A4 portrait page (595.32 × 841.92 pt), producer
"Microsoft: Print To PDF", three embedded CID fonts (Arial-like), two raster
images (the Collision Engineers logo, 1990×580, and the EVA badge/footer strip,
1798×130). 539 vector lines, 11 rectangles (table rules and box borders).
Rendered page: [evaestimate-page1.png](evaestimate-page1.png).

## Header block (right-aligned label : value, one line each)

| Label | Value |
| --- | --- |
| Date | 16/09/2026 |
| Our Ref | 00038381 |
| Claimant | Mr Dan Fuller |
| Vehicle | CITROEN C2 CODE |
| Registration | YL57KUF |

Logo top-left spanning ~14–567 pt wide, 0–125 pt tall (edge to edge).

## Line table (y 240–490 pt; header row bold, zebra rows, hairline borders)

Columns (left x in pt): Qty 23 · Description 49 · Type 200 · Labour 260 ·
Paint 312 · Litres 356 · Mat Price 388 · Anti-C 444 · A/C Price 479 · Price 548.
Numeric columns right-aligned; money with `£`; hours to two decimals.

| # | Qty | Description | Type | Labour | Paint | Mat Price | Price |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 1 | Right Front Door Membrane | New | 0.10 | | | £103.18 |
| 2 | 1 | Right Front Door Protective Moulding | New | | | | £43.19 |
| 3 | 1 | Rear Bumper Lining | R & R | 0.80 | | | |
| 4 | 1 | Right Rear Side Panel | Repair | 10.00 | | | |
| 5 | 1 | Right Side Panel Protective Moulding | New | 0.10 | | | £14.26 |
| 6 | 1 | Right Front Door Strip & Set-Up for Pain[t] | R & R | 1.30 | | | |
| 7 | 1 | .Assessment Damage Appraisal Charge | Specialist | | | | £176.96 |
| 8 | 1 | .Environmental Charge | Specialist | | | | £31.23 |
| 9 | 1 | .QC & Road Test | Specialist | 1.00 | | | |
| 10 | 1 | .Standard shutdown | Specialist | 1.00 | | | |
| 11 | 1 | .Sundries | Specialist | | | | £20.00 |
| 12 | 1 | .System Diagnostic Check (Post Repair) | Specialist | 1.00 | | | |
| 13 | 1 | .System Diagnostic Check (Pre Repair) | Specialist | 1.00 | | | |
| 14 | 1 | .Vehicle Care Kit | Specialist | | | | £10.41 |
| 15 | 1 | .Wash/Clean | Specialist | 1.00 | | | |
| 16 | 1 | OSR tyre | Specialist | | | | £180.00 |
| 17 | 1 | Wheel Alignment (check) | Specialist | | | | £112.42 |
| 18 | 1 | Right Front Door, Complete | Paint | | 0.80 | £191.20 | |
| 19 | 1 | Right Rear Side Panel, Complete | Paint | | 1.60 | £187.60 | |
| 20 | 1 | Prep. metal (on vehicle without pre-pain[ting] | Paint | | 1.70 | £120.16 | |
| 21 | 1 | Colour mixing (1) | Paint | | 0.30 | | |
| 22 | 1 | Sample colour creation (1) | Paint | | 0.30 | £11.62 | |

Litres, Anti-C and A/C Price are empty on every row. Long descriptions are
clipped at the column edge, not wrapped (rows 6 and 20). The leading `.` on
rows 7–15 is EVA's own convention for standard-charge items and is printed
verbatim.

## Summary boxes (bordered cells, bold labels above values)

**Hours by operation** (y 528–554):

| New | Repair | Paint | R & R | Blend | Specialist | Check | Total Hours |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0.20 | 10.00 | 4.70 | 2.10 | | 5.00 | | 22.00 |

**Rate and discounts / money by category** (y 580–602, two boxes side by side):

| Labour Rate | Labour Disc | Material Dis | Parts Dis | | Labour | Materials | Parts | Other |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| £83.28 | 0.00% | 0.00% | 0.00% | | £1,832.16 | £510.58 | £160.63 | £531.02 |

**Discount amounts / totals** (y 630–690):

| Labour Discount | Materials Discount | Parts Discount | Other Discounts | | Total Disc | £0.00 |
| --- | --- | --- | --- | --- | --- | --- |
| £0.00 | £0.00 | £0.00 | £0.00 | | Nett | £3,034.39 |
| | | | | | Vat | £606.88 |
| | | | | | Gross | £3,641.27 |

## Footer

EVA badge bottom-left ("Document produced using the EVA system");
`www.CollisionEngineers.co.uk` centred in red, underlined. No page numbers,
no VAT number, no company address.

## Arithmetic check (every figure reconciles)

- Hours: New 0.10 + 0.10 = 0.20; Repair 10.00; R & R 0.80 + 1.30 = 2.10;
  Specialist 5 × 1.00 = 5.00; Paint 0.80 + 1.60 + 1.70 + 0.30 + 0.30 = 4.70;
  total 22.00.
- Labour £ = 22.00 × £83.28 = £1,832.16 — **EVA prices Specialist hours at the
  labour rate** (17.00 non-specialist hours would give £1,415.76).
- Materials £510.58 = Σ Mat Price on Paint rows (191.20 + 187.60 + 120.16 + 11.62).
- Parts £160.63 = Σ Price on New rows (103.18 + 43.19 + 14.26).
- Other £531.02 = Σ Price on Specialist rows (176.96 + 31.23 + 20.00 + 10.41 + 180.00 + 112.42).
- Nett £3,034.39 = 1,832.16 + 510.58 + 160.63 + 531.02.
- VAT £606.88 = 20 % of Nett (606.878 rounded half-up). Gross £3,641.27.
