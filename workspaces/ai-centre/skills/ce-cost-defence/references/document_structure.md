# Document Structure & `data.json` Schema

> **Source-workspace boundary:** Retain payload schema/renderer shape, remove court/legal sign-off as authority and state it is an experimental document fixture. This is evidence, an example, a package-local format, or an experiment only; `Pegasus.Core`, current operator authority, and an authorised human own every accepted fact, cost, category, outcome, legal position, send, report issue, and approval.


The generator (`scripts/build_report.js`) always lays the report out in this fixed order. You only supply content; the styling is automatic.

```
[Header band]  logo (left)   Our Ref / Your Ref / Date (right)
FAO The Court
<care-of instructing-party address lines>

            REPAIR COST DEFENCE REPORT            (centred, bold, underlined)
   RE: <matter / claimant / accident date>        (centred, bold)

Dear Sirs,
<intro paragraphs — refer to our original report & spec, state what we've been asked to do>

SUMMARY OF THE MATTER IN DISPUTE       (red-ruled heading + 2-col table)
<summary table: our cost, their cost, rate, hours, vehicle, etc.>
<optional summary note>

<ordered sections — methodology, breakdown of hours, response to defendant, etc.>

CONCLUSION
<conclusion paragraphs — invite the Court to accept our costs>

<CPR 35.6 availability line — automatic unless overridden>

STATEMENT OF TRUTH
<standard CPR statement — automatic unless overridden>

<signature block: name, qualifications, role, email, date>

[Footer band]  www.CollisionEngineers.co.uk  +  registered address line
```

## `data.json` schema

All fields are optional except where noted; sensible defaults fire for the standard chrome.

```jsonc
{
  "our_ref": "qdos231773",                 // top-right ref block
  "your_ref": "JFO/RS/20911/1",
  "date": "29th May 2026",                  // also used in signature block

  "addressee_title": "FAO The Court",       // default "FAO The Court"
  "addressee_lines": [                       // the instructing party, care-of
    "C/o QDOS Assistance", "1st Floor, Barfield House",
    "24-28 Alderly Road", "SK9 1PL"
  ],

  "title": "Repair Cost Defence Report",    // centred underlined title (auto-uppercased)
  "re_line": "Road Traffic Accident \u2013 <Claimant> \u2013 <DD/MM/YYYY>",
  "salutation": "Dear Sirs,",

  "intro_paragraphs": [ "para 1", "para 2" ],

  "summary_heading": "Summary of the Matter in Dispute",
  "summary_rows": [                          // 2-column label / value table
    ["Make / Model", "..."],
    ["Registration", "..."],
    ["Our Assessed Repair Cost", "£X inc VAT"],
    ["Defendant's Assessed Cost", "£Y"],
    ["Labour Rate Applied", "£Z per hour"],
    ["Total Repair Hours", "N hours"]
  ],
  "summary_note": "one short paragraph under the table",

  "sections": [                              // ordered free-form sections
    {
      "heading": "Repair Methodology",
      "paragraphs": [ "..." ]
    },
    {
      "heading": "Breakdown of Labour Hours",
      "paragraphs": [ "intro to the table" ],
      "table": {                             // optional headed table (red header row)
        "columns": [
          {"header": "Category", "width": 4800},
          {"header": "Hours", "width": 2400, "align": "center"},
          {"header": "Basis", "width": 2438}
        ],
        "rows": [ ["New / R&R", "8.00", "Manufacturer times"], ... ],
        "note": "optional caption under the table"
      }
    },
    {
      "heading": "Response to the Defendant's Position",
      "paragraphs": [ "lead-in" ],
      "rebuttals": [                         // numbered list; quote the challenge, then answer
        { "challenge": "\u201Ctheir exact point in quotes\u201D",
          "response": "our specific, evidence-based answer" },
        { "challenge": "...", "response": "..." }
      ],
      "after_paragraphs": [ "closing point — e.g. they offered no contrary evidence" ]
    }
  ],

  "conclusion_paragraphs": [ "..." ],

  "cpr_line": "...",                          // optional override; default is standard CPR 35.6 line
  "statement_of_truth": "...",                // optional override; default is standard wording

  "signatory": {                              // default A. Patterson / M.Inst.IAEA
    "name": "A. Patterson",
    "lines": ["M.Inst.IAEA", "Independent Motor Engineer",
              "Collision Engineers Ltd", "engineers@collisionengineers.co.uk"]
  }
}
```

### Column widths

Table columns are in DXA (1440 = 1 inch). The usable content width is **9638 DXA**. Make each table's column widths sum to ~9638 (a few units off is fine). The summary table is handled automatically — you only set widths for `section.table` tables.

### `align` values

`"left"` (default), `"center"`, `"right"`, `"justify"` — passed straight through to the alignment.

## Recommended section set

A strong cost-defence report usually contains, in this order:

1. **Repair Methodology** — state the professional standard (return to pre-accident condition; recognised Audatex methodology / manufacturer times) so the rest is measured against it.
2. **Breakdown of Labour Hours** (and/or paint, parts) — a table that itemises exactly what the disputed hours/costs comprise. This pre-empts "no breakdown given".
3. **Response to the Defendant's Position** — numbered rebuttals, each quoting the defendant's point then answering it; close by noting what the defendant did NOT provide.
4. **Conclusion** — restate the figure, summarise why it stands, invite the Court to accept it.

Add or rename sections as the job requires (e.g. "The Necessity of Blending", "Comparison of the Two Specifications", "Additional Damage Found on Strip-Down"). Keep the four pillars above as the backbone.

## Writing the rebuttal

- Quote the defendant's point verbatim in the `challenge` field (use smart quotes `\u201C \u201D`).
- Answer with a concrete technical or evidential reason, then tie it back to our original report / itemised spec.
- Where the defendant offered no inspection, no alternative method, no contrary times and no evidence, say so plainly — that absence is your strongest source-supported evidence.
- Never disparage the other engineer personally.
