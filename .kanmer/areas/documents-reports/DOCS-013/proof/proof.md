# Proof — DOCS-013

Merged to `dev` as PR #526, promoted to `main` in release 27 at
`7d4c8f005261d3963cdecf806b3e06c17552be9b`. Docs-only, so the proof is what the
governing documents say on merged `main`.

## The manifest is gone from the normative docs

| File | Before | After |
| --- | --- | --- |
| `docs/frd/frd-07-*.md` | "writes a SHA-256 manifest over the JSON and image identities and bytes"; and the clause forbidding its removal — "without changing the exact package contents, **manifest**, or manual-handoff boundary" | the package is the ordered thirteen-key JSON and the eligible images, "and no companion file: neither a manifest nor a provenance sidecar" |
| `docs/operator-notes.md` | "a reviewed JSON/image/manifest download" | "a reviewed JSON/image download" |
| `docs/capabilities.md`, `docs/design/README.md`, `docs/open-decisions.md` | manifest named in the EXT-03 and hand-off descriptions | struck |

`grep -rn -i manifest docs/ --include=*.md` returns only release-artifact hits
(`operations.md`, `adr/0007`, `adr/0015`) — a different concept sharing the word.

## The protected-file edit, and its authority

`docs/operator-notes.md` is protected: its meaning may not change without user
resolution. The resolution is the operator direction of 2026-08-24 — that the
manifest is an AI invention that entered the governing docs and was never asked
for — given in response to the traced evidence, and named explicitly in the PR
description rather than passed off as routine wording.

The tracing is what makes the direction checkable, and it held:

- the word "manifest" appears **nowhere** in `reference/` — not in the EVA
  schema, the screenshots, the EVA information notes, or either retained JSON
  example. That is the whole operator-supplied corpus;
- it entered FRD-07 via `2e3db7aa` (documentation-taxonomy restructuring) and
  `operator-notes.md` via `3f4a35ba` (a release record) — neither an operator
  statement;
- the operator's own `Final-Format-Example-02.json`, committed to
  `docs/json-extraction-parity/` on 2026-08-24, is thirteen keys and no
  companion file.

## `Reference` now says which reference it carries

FRD-07 previously listed the key and never said whose reference it held — a gap
that existed only in code, which is how the export came to carry the Pegasus
case reference. It now states the work provider's own reference, cites both
retained examples (`"Work Provider": "SBL"` with `"Reference": "SBL-B0492438"`;
`"AX"` with `"1070277"`), and says plainly that which EVA field the imported
value lands in is EVA's business and is not established here.

**Independent review challenged this** as unimplemented at the time, and was
right to: it was implemented in [[ENG-015]], which maps `Reference` from
`caseData.Claim.Number` and shipped in the same release. The doc and the code
agree on merged `main`.

## Verified

- Both docs gates pass: `Test-DocumentationLinks.ps1` (196 files) and
  `Test-TestMarkdownPlacement.ps1`.
- The downstream ticket it unblocked, [[ENG-014]], stopped producing the
  manifest in the same release, so no window exists where the docs and the code
  disagree.

## Not proved

No EVA import has been attempted against a package produced by the new format.
That is an operator action against a live case and belongs to [[ENG-015]]'s
verification, not this one.
