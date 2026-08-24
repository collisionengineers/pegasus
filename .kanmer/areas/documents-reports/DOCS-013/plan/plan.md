# Plan — strike the invented manifest from the governing docs

Docs-only. Six files, seven edits. No code, no tests, no migration.

## Why this is a deletion, not a redesign

The manifest was checked against the operator corpus before planning:

| Check | Result |
| --- | --- |
| `grep -rn -i manifest reference/` | **zero hits** — the whole operator-supplied corpus |
| `git log --reverse -S'manifest' -- docs/frd/frd-07-*.md` | `2e3db7aa` "adopt PRD/FRD/ADR taxonomy" |
| `git log --reverse -S'manifest' -- docs/operator-notes.md` | `3f4a35ba` "release-3 record" |
| predecessor output EVA accepts (`QDOS_NX14AXY.json`) | bare JSON, no companion files |

Both entries are internal doc restructuring. Nothing traces to an operator
statement. Operator direction 2026-08-24 confirms it: *"this is an AI invention
that isnt a thing"*.

## Steps

1. **`docs/frd/frd-07-eva-and-external-engineering-handoff.md`** — three edits:
   - line 12: drop `and writes a SHA-256 manifest over the JSON and image
     identities and bytes` and the following `Stable manifest ordering…`
     sentence. Reuse the existing sentence structure; do not rewrite the
     surrounding accepted-boundary paragraph.
   - line 42: `the complete JSON, all-eligible-image, and manifest bundle` →
     drop `and manifest`.
   - line 45: drop `, manifest` from the `without changing the exact package
     contents, manifest, or manual-handoff boundary` clause.
2. **FRD-07, same pass** — add two things the doc has never said:
   - which reference `Reference` carries (the work provider's own — EVA's
     `Claim no`, not `Case/Po`), citing
     `reference/eva_information/eva_information.md:31-45`, which is operator
     source. This is what [[ENG-015]] implements.
   - the CASE-019 operator export as a distinct artefact: same package, but it
     records no revision and no `First sent to Engineer` proxy.
3. **`docs/capabilities.md:175`** — EXT-03 row: drop `, and a SHA-256 manifest`.
4. **`docs/design/README.md:697`** — `EVA JSON/image/manifest generation` →
   `EVA JSON/image generation`.
5. **`docs/open-decisions.md:29`** — drop `+ SHA-256 manifest`.
6. **`docs/open-decisions.md:240`** — `manual JSON/image/manifest handoff` →
   `manual JSON/image handoff`.
7. **`docs/operator-notes.md:505`** — `a reviewed JSON/image/manifest download`
   → `a reviewed JSON/image download`. **Protected file, meaning change.**

## What is deliberately not touched

- `docs/current-architecture.md:526` — as-built, not normative. [[ENG-014]] owns
  it because [[ENG-014]] is what changes the as-built shape.
- `docs/operations.md` and `docs/adr/0007`, `docs/adr/0015` — **release-artifact**
  manifests. Same word, unrelated concept. Sweeping these would be a real defect.

## Protected-file authority

`docs/operator-notes.md` is authoritative operator truth and the rails require
user resolution before changing its meaning. The resolution is the operator
direction of 2026-08-24, given in response to the traced evidence above. The PR
description must name this edit and its authority explicitly rather than let it
pass as routine wording.

## Simplification pass

n/a — docs-only.

## Verification

- `grep -rn -i manifest docs/ --include=*.md` returns only the release-artifact
  hits and `current-architecture.md:526`
- `docs/index.md` authority chain still resolves
- No code, so no build/test; CI runs anyway and must be green

## Correction during implementation (2026-08-24)

The planned file list was **incomplete**. My original sweep piped `grep` through
`head -20` and silently truncated, so a seventh assertion was missed:

- **`docs/runbook.md:857`** — the EVA acceptance criterion asserted "repeated EVA
  export proves byte-identical ordered UTF-8 JSON and image order for the same
  accepted inputs, **the SHA-256 manifest**, the image eligibility…". Struck.

Caught by re-running the sweep untruncated after the planned edits. The lesson
is the sweep, not the file: an untruncated `grep` is the verification step, and
a truncated one reads as completeness it has not earned.

Two false positives confirmed as leave-alone:

- `docs/runbook.md:811` — "manifests … beneath `artifacts/evaluation/`". Matched
  an EVA filter only because "eva" is a substring of "evaluation".
- `docs/current-architecture.md:526` — real, EVA-context, and deliberately left
  to [[ENG-014]], which is what changes the as-built shape.

Final file count: **6 files, 8 edits** (not 6/7 as planned).
