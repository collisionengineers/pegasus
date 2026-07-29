# Rebuttal Document Structure

Select the output format before drafting. Do not force a defensive insurer response or solicitor advice note into a full rebuttal report unless the user asks for a report.

Use:

- **Formal rebuttal report** for rebutting a served third-party diminution report.
- **Solicitor-facing advice** for tactical advice before correspondence is sent.
- **Insurer-facing response** for an external email or letter answering objections.
- **Formal addendum/report** where court-facing expert evidence, Part 35 answers, or a signed addendum is requested.

---

## Solicitor-facing advice structure

Use this where the instructing solicitor asks what to do next.

1. Brief background.
2. Issue raised by insurer or opponent.
3. CE's recommended position.
4. Evidence available.
5. Evidence to obtain, if any.
6. Technical/procedural explanation.
7. Litigation risk and proportionality.
8. Recommended next step.

Keep the tone practical. State the recommended route before presenting options. Explain that a court may ask what evidence supports the vehicle's history, but that does not mean CE must prove that no minor cosmetic work ever occurred during the vehicle's life.

## Insurer-facing response structure

Use this for emails or letters to the defendant insurer.

1. Brief background.
2. Issue raised by insurer.
3. CE position.
4. Technical explanation.
5. Evidence relied on.
6. Conclusion and invitation to provide evidence.

Keep this external response formal and concise. Avoid conversational reassurance. Maintain the assessed figure unless evidence genuinely undermines it.

Useful paragraph sequence:

- The claim arises from the documented accident and repair.
- The repair scope and cost are evidenced.
- Any prior-history assertion is unsupported unless proper evidence is produced.
- Paint-depth readings are not determinative without method, substrate, raw readings, photographs, and a link to PAV or diminution.
- CE stands by the assessed diminution, or CE will provide a limited addendum on the identified issue.

## Formal addendum/report structure

Use this where court-facing expert evidence is requested.

1. Instruction and documents reviewed.
2. Issue requiring addendum.
3. Facts and assumptions.
4. Testing, inspection, or measurements reviewed or declined, with reasons.
5. Opinion and any range or limits.
6. Summary conclusion.
7. PD35 statement of truth where it is an expert report/addendum.

Read `expert-procedure-and-evidence.md` before drafting this format.

---

## Rendering — collisionrenderer, `templateId: diminution-rebuttal`

Formal rebuttal reports and addenda are rendered by the `collisionrenderer` connector. The
template owns all page furniture — A4 letterhead, Our/Your Ref/Date block, repeating header and
footer, page numbers, uppercase section headings with the red rule. You supply **content only**,
as a camelCase payload:

- `meta` — `ourRef` (vehicle registration), `yourRef`, `date` (DD/MM/YYYY)
- `title` — e.g. `Rebuttal Report – Claim for Diminution in Value`
- `salutation` — e.g. `FAO: The Instructing Solicitor`
- `intro` — list of opening paragraphs (RE: line context, instruction summary)
- `sections` — list of `{ heading, blocks }`; block types: `paragraph`, `bullets`, `datatable`,
  `keyvalue`, `evidencetable`, `valuebox`, `mediarow`. Use a `keyvalue` block for the Summary of
  Matter in Dispute table.
- `signature` — for a standard rebuttal send the firm-only form `{ "name": "", "role": "" }`
  (see Sign-off below); for a named-engineer Part 35 report or addendum send `name`,
  `qualifications`, `signatureImage`, `aqpNumber` — `role` and org default correctly

Always fetch the live payload shape with `get_template_sample` and run `validate` before
`render`. Do not restate figures the payload already carries.

## Title block

```
REBUTTAL REPORT – CLAIM FOR DIMINUTION IN VALUE

RE: Road Traffic Accident – [Claimant Name] – [Accident Date DD/MM/YYYY]

FAO: The Instructing Solicitor
```

## Summary of Matter in Dispute table (`keyvalue` block, first section)

Immediately below the title block. Include:

| Field | Value |
|---|---|
| Claimant | [Name] |
| Vehicle | [Make Model Registration] |
| Date of Accident | [DD/MM/YYYY] |
| Underlying Report | [CE report ref, if applicable] |
| Third-Party Assessor | Exclusive Vehicle Assessors (EVA) / [other firm] |
| EVA Reference | [EVA's report reference] |

## Section headings

- Plain-language headings; the renderer sets them uppercase with the red rule
- No numbering — neither numbered sections nor numbered sub-points

## Body text

- Bullet points throughout for all lists (never numbered)
- Each bullet is a full sentence or two — not a fragment
- Plain-spoken professional register (engineer's voice, not lawyer's)
- One "in our view" / "in our professional opinion" per section maximum

## Recommended section order

1. SUMMARY OF MATTER IN DISPUTE *(the table above)*
2. BACKGROUND AND SCOPE OF INSTRUCTION
3. NATURE OF THE DAMAGE AND REPAIR *(point 1)*
4. THE VEHICLE AND ITS MARKET *(points 2–5 as applicable — combine or separate per case)*
5. THE THIRD-PARTY METHODOLOGY *(points 6–10 as applicable)*
6. CONSISTENCY WITH ABI GUIDANCE *(point 9 — always include)*
7. PROCEDURAL AND EVIDENCE MATTERS *(points 11-12, and point 14 if applicable)*
8. CONCLUSIONS

## Sign-off

```
Yours faithfully,

Collision Engineers Ltd
```

Nothing else. No named individual. No qualifications. No Statement of Truth. No CPR reference. No date block. In the payload use the `signature` block with `name` and `role` explicitly empty:

```json
"signature": { "name": "", "role": "" }
```

An explicit empty string suppresses that line; omitted fields keep their defaults, so the block prints `Yours faithfully,` (default `closing`) followed by `Collision Engineers Ltd` (default org) and nothing else. Do not add a sign-off paragraph to the final section — the signature block owns the sign-off. (This matches the template's bundled sample, which uses the same firm-only form.)

*(Exception: a Part 35 addendum or expert report signed by a named engineer uses the payload `signature` block — name, qualifications, `signatureImage`, `aqpNumber` — and carries the Statement of Truth; see `expert-procedure-and-evidence.md`.)*
