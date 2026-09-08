# Plan — DOCS-019

## Objective and governing docs

Correct only the Supplied engineer signatures row in
`docs/design/README.md`. Root approved this existing ticket's factual
correction; no signatory behaviour, design decision or ADR changes.
Meet current FRD-11/D31: the report uses the Case Sign-off Engineer account's
supplied tuple, not an embedded Andy Patterson application resource.

Audience: future implementers consulting the design asset table. This removes
one false implementation claim; it does not replace the account/signatory
policy or create another source of truth.

## Starting state

Evidence: remote dev `498144b0bb55b68fd53b9a31ffc89ef90622c73a`,
confirmed by `git ls-remote` on 2026-09-08; DOCS-019 initial revision
`rev1:5b3a53f7e1cd91e0`; current EPIC-012 D31 and inherited EPIC-011
context/waves read. Current root instructions own verification sequencing.

The stale row is now line 628. `PlaywrightAssessmentReportRenderer.cs:99–103`
builds the signature URI from snapshot SignatureContent/SignatureContentType;
`EfAssessmentReportProjectionSource.cs:147–151` supplies the account tuple.
Infrastructure's project file embeds templates, stylesheet and logo but no
signature resource. The existing NoSignatoryResourceIsEmbedded test records
the same invariant. Source-only `brand.signatures` census found no match;
that expected search exit 1 is absence evidence, not a test failure or new
runtime PASS. No declared external documentation sources apply.

A read-only census of all 59 recorded claims found historical README writers
[[DOCS-012]], [[PLAT-029]], [[CASE-038]] and [[PLAT-075]]. [[PLAT-008]]
mentions unresolved future placement. These records remain untouched and
require root's explicit file handoff before execution. [[PLAT-005]] and
[[PLAT-027]] read it only; [[TICK-085]] excludes it; [[ENG-029]] is untaken.

## Required change and expected files

| Action | Path | Responsibility |
| --- | --- | --- |
| Modify | docs/design/README.md | Replace only the Supplied engineer signatures table row; no generated artifact changes. |

Replacement text:

> | Supplied engineer signatures | The report snapshot carries the Case Sign-off Engineer account's printed name, optional qualifications and supplied signature image bytes/media type (D31, DOCS-017). No signature is embedded as an application resource. Supplied signature assets remain governed and are never Web decorative imagery. |

## Do not modify

`src/**`, `tests/**`, `scripts/**`, `docs/design/test-ui/**`,
`docs/operator-notes.md`, other README rows, assets and other tickets'
documents/claims. No new dependency, signature upload, renderer behaviour,
schema, UI, snapshot capture or deployment.

## Ordered steps and acceptance

1. After root reads/approves this plan and explicitly clears the historical
   file overlap, obtain fresh gates and an isolated execution packet/worktree
   from current origin/dev. Confirm the exact stale row and source invariant
   still hold, then replace that row only using apply_patch.
2. Review the one-row diff and run the existing commands below in that
   ticket's worktree. Retain actual exit evidence; publish the ordinary
   documentation PR only after required checks pass. Do not run dotnet or
   regenerate the unchanged Test UI.

## Commands

- `git diff --check` — exit 0.
- `rg -n -F "brand.signatures" src` — no matches, expected exit 1.
- `rg -n "SignatureContent|SignatureContentType" src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`
  — existing supplied-byte caller found, exit 0.
- `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1` — exit 0.
- `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` — exit 0.
- `git diff -- docs/design/README.md` — exactly one table row corrected;
  neighbouring content and supplied assets unchanged.

## Failure and deviation rules

Stop on source drift, unresolved ownership, failed checks, an unexpected
signature resource or any necessary change outside the single row. Preserve
failure evidence; do not repair unrelated links/catalogue entries or claim
runtime tests were executed.

## Stop condition

Preparation stops at whole-plan readback to root, Preparing and untaken.
No source/take/branch yet. Subsequent explicitly authorized execution stops
at independent PR review; no self-review, self-merge or deployment.
