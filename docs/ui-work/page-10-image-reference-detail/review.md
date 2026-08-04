# Page 10 — Image reference detail (today: the "Image in·take" detail screen) — review

> Vocabulary note. The legacy pipeline term is banned from every deliverable in this folder set,
> including this review. Wherever current copy or code identifiers must be quoted, the term is
> written `in·take` (with an interpunct) so the zero-occurrence check stays clean. The capture in
> this folder shows record `SD74CXS-01`; the source is the detail page under
> `src/Pegasus.Web/Pages/ImageIn·take/Details.cshtml`.

What the screen is: the read-only record for one pre-case image reference. Today it shows a
"Record" panel, a "Preserved origin" panel, an "Eligible case candidates" panel (absent in the
capture because no candidate exists), and a "Registration reading results" panel, under the
eyebrow "IMAGE IN·TAKE" and the lede "Image in·take registered — awaiting definitive
instruction".

## 1. Aesthetics

- **Half the visible content is cryptographic noise.** The "Preserved origin" panel prints, at
  full width and full weight: "Origin receipt `ab86bea1-b53e-4ee2-8553-5f162d4803c5`", "Source
  receipt token `72020e89d6564589a7e6a426696e0129`", "Source hash
  `8e066f1aa7cd365274ca77346cca372e4e3d2a2d359a0158dea0a0ecaf0be65d`", and "Evaluation revision
  `e1b22c69-7a72-4481-94a7-8b2f04b74226`". Four rows of identifiers no operator will ever read,
  compare, or transcribe — presented with the same visual priority as the vehicle registration.
- **Raw enum as content**: "Source channel — ManualUpload". PascalCase compound straight from
  the type system.
- **The lede narrates state that belongs in a chip.** "Image in·take registered — awaiting
  definitive instruction" is a two-clause sentence doing the job of the words "Awaiting
  instruction".
- **The "Record" panel is four rows adrift in a tall card**, mirrored by the origin panel's
  wall of hashes — the layout gives the least useful panel the most space.
- **Doctrine paragraph mid-panel**: "Association, reversal and re-linking are reasoned actions
  on the origin receipt; the reference above is permanent either way." — a policy essay where a
  single inline consequence line (next to the link action) would do.

## 2. Practicality

- **The one thing an operator wants to do here — connect the images to a case — is a detour.**
  The only action on the page is the button "Open the origin receipt to link or unlink", which
  ejects the operator to a different record (identified by a GUID link) to perform the
  association. When candidates exist, the "Eligible case candidates" panel renders them as bare
  links with "· version 3"-style suffixes and the caption "Linking remains a reasoned staff
  action on the origin receipt." — candidates you can look at but not act on.
- **"Registration reading results" leaks the machine room.** The capture shows "No readable
  registration — fast-alpr-onnx v1 · 04 Aug 2026 16:40 · pending". Engine key, engine version,
  and a lowercased enum disposition ("pending") are printed to an operator who needs exactly one
  fact: what registration was read, and how confident the reading is.
- **The reading result contradicts the record without explanation.** The record confidently
  states registration `SD74CXS`, while the results panel says "No readable registration" —
  nothing tells the operator which images produced no reading or why the registration is
  nonetheless confirmed.
- **"Case association — None — awaiting instruction"** duplicates the lede's state a third
  wording ("awaiting definitive instruction" / "Awaiting instruction" filter on page 9 /
  "awaiting instruction" here).
- **The back button says "All Image in·takes"** — banned vocabulary in the primary escape
  hatch.

## 3. Performance / Design / Good practice

- **Rule 4 of the standards file (no raw identifiers) is violated five times in one panel** —
  receipt GUID, receipt token, SHA-256, revision GUID, and (when unresolved) a case GUID
  fallback: the association row renders `caseId.ToString()` when no case reference is available.
- **Rule 1 (no ledes) and rule 7 (one heading stack)**: eyebrow + H1 + lede, all three present.
- **The origin panel mixes evidence and navigation.** The receipt GUID doubles as the link text;
  a labelled action ("View original upload") with the identity kept internal would carry the
  same capability with none of the leakage.
- **Suggestion labelling is borrowed from another page's model** (`SuggestionOutcomeLabel` from
  the received-item detail page model) — cross-page coupling that makes independent copy
  evolution impossible; a shared label map is the right home.
- **`<title>` is built from the banned term** ("Image in·take SD74CXS-01").
