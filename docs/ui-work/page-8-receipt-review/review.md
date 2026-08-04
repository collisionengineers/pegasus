# Page 8 — Receipt review (`/Intake/{id}`) review

Reviewed from `receipt-review.png` (Needs sorting), `receipt-review-instruction-draft.png`
(ready-to-review with the full correction and accept surface), and
`receipt-review-image-upload.png` (image upload with Register form), against
`src/Pegasus.Web/Pages/Intake/Details.cshtml` (labels in `Details.cshtml.cs`). Under the
new IA this becomes **Received item review**, reached from Inbox rows. It is the most
important operator screen in the product and currently the worst one.

> **Scope correction.** The findings below describe the screen as built, including its
> accept surface. That surface should not exist: definitive authorised intake creates the
> case directly (`requirements.md:251`) and only ambiguous or unidentified material reaches
> this screen, as `Needs sorting` (`operator-notes.md:204`). Read every accept-related
> finding as evidence about a control that is being removed, not as a control to redesign.
> See `defects-and-non-functional.md` §B4.

## 1. Aesthetics

- One mile-long single column. The instruction-draft screenshot is roughly 6,000px tall:
  Result → Missing fields → resolution forms → case association → typed draft → address →
  accept → assets → suggested fields → decision evidence, every one a boxed panel with an
  uppercase letter-spaced label. There is no visual hierarchy between "correct a claimant
  name" and "allocate an immutable case reference".
- Three full-width red primary buttons compete on one page: "Record corrected draft"
  (`Details.cshtml:179`), "Register Image intake" (`Details.cshtml:105`), and "Accept and
  allocate case reference" (`Details.cshtml:440`). Red is supposed to be the sparse
  primary; here it is a fire drill.
- The header is eyebrow + decision + reason lede: "INTAKE REVIEW" / "Needs sorting" /
  "The readable content does not provide enough evidence to suggest a principal."
  (`Details.cshtml:11-13`). The h1 is a *state*, not a page — so the page's own name
  changes as the record changes, and a receipt already turned into a case is still headlined
  "Instruction draft" (`Details.cshtml.cs:624`) with no case indication anywhere.
- "Result" panel prints the raw source-receipt hex token:
  "Source receipt c13eb471c31949b7a139da4e95ed1ebd" (`Details.cshtml:43`) — 32 hex chars
  an operator can do nothing with.

## 2. Practicality

- **Triple narration everywhere.** Almost every section opens with a policy essay:
  - "Every correction, block and re-evaluation is versioned and retained in permanent
    history." (`Details.cshtml:146`)
  - "Each attachment, inline image and discrete embedded image remains a separate review
    occurrence. Matching hashes are grouped, not removed." (`Details.cshtml:449`)
  - "Each retained image was read automatically. A suggestion never registers, links or
    identifies anything on its own; an unreadable image, an unavailable engine and a
    technical failure remain distinct recorded outcomes." (`Details.cshtml:114`)
  - "Image-only material with a usable normalised registration becomes a pre-Case Image
    intake with a permanent reference. It never becomes a Case by itself."
    (`Details.cshtml:98`)
  - "Missing or conflicting physical-address evidence remains unresolved. Pegasus will
    not infer an address from a spreadsheet, geocoder or model." (`Details.cshtml:351`)
  These are architecture affidavits read aloud at a person trying to open a case.
- **The operator hand-operates concurrency.** "Current case association" asks for a typed
  "Case identifier" and a typed "Expected case version" *number* before "Enter case edit
  mode" (`Details.cshtml:233-239`), with the helper "Enter the current version of case X
  to enter case edit mode." (`Details.cshtml:229`). No human knows a case's version
  integer; the server does. Failure lands as "The claimed case does not match the current
  association. Reload and enter edit mode for the current case." (`Details.cshtml:273`).
- **Duplicated confirmation checkboxes**: "Instruction evidence is complete" AND "I have
  confirmed the instruction evidence"; "Image evidence is complete" AND "I have confirmed
  the image evidence" (`Details.cshtml:423-438`) — four boxes encoding two facts, an audit
  schema leaking into a form.
- **The accept surface is buried — and should not be here at all.** "Accept as case" sits
  mid-page below the address panel and below a "Typed review draft" section
  (`Details.cshtml:282`) that read-only-duplicates the correction form directly above it. Its
  lede narrates: "Confirm the principal, case type and evidence completeness before allocating
  an immutable case reference." (`Details.cshtml:373`) — which is the acceptance gate
  `requirements.md:251` forbids, stated as policy in the UI. The burial is a layout symptom;
  the control itself is the defect (§B4). What belongs here is **Create case** as the sorting
  resolution for an ambiguous item, and nothing at all for a definitive one.
- Queue-speak and banned-term copy throughout: page `<title>` and eyebrow, "Intake
  resolution" (`Details.cshtml:145`), "Reason for blocking intake" / "Block intake"
  (`Details.cshtml:194-196`), "Reason for accepting this intake" (`Details.cshtml:378`),
  "This intake receipt is not currently linked to a case." (`Details.cshtml:209`),
  "Receive another" as the escape link (`Details.cshtml:15`) — which actually just goes
  back to the list.
- The suggestion disposition renders as a raw lowercased enum: "pending"
  (`Details.cshtml:120`), and decision evidence keys render as PascalCase identifiers —
  "EmailBody", "PdfContent", "SystemDefault" (screenshot) — with no label map.
- Block and re-evaluate reason boxes are always expanded, giving destructive/rare actions
  the same permanent screen weight as the primary flow.

## 3. Performance / Design / Good practice

- The page renders every section for every state — resolution forms, association forms,
  address forms, accept form, asset grid, OCR pages, suggested-field cards, evidence
  list — producing an enormous DOM per request; the same draft data is painted three
  times (correction form values, "Typed review draft" `<dl>`, "Suggested fields" cards).
- Per-render GUID operation keys are minted inline in at least five forms
  (`Details.cshtml:100,130,238,252,265`) — the idempotency pattern is right, its
  scattering is not; one page-model source would do.
- The accept form's server-side guards (`ReviewedReceiptVersion`, acceptance operation
  key, validation summaries) are sound and must survive the redesign — the review is of
  the surface, not the invariants.
- Good bones worth keeping: `aria-labelledby` sectioning, `asp-validation-*` wiring,
  `<time>`-adjacent formatting in `dd MMM yyyy HH:mm`, required attributes on reasons,
  and the strict fail-closed accept handler.
- No sticky/fixed affordance exists anywhere, so on a 6,000px page the operator's
  actions live at arbitrary scroll offsets; every decision requires re-finding its form.
