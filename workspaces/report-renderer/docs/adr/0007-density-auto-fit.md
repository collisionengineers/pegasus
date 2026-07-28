# 0007 — Density auto-fit (Normal → Compact → Ultra)

## Status

Accepted

## Context

Some documents are expected to sit on a single page when the content is short — a market
valuation evidence sheet is most readable and most professional as one tidy page. But the
same template must also cope gracefully when the content is long. A fixed density cannot
satisfy both: too loose and a slightly-too-long document spills onto a second, near-empty
page; too tight and short documents look cramped. The renderer needs to choose a density that
keeps short documents tidy while letting genuinely long ones flow cleanly across pages
(ADR 0006).

## Decision

Provide **density auto-fit** for templates whose `DensityProfile` is `FitToPages`. The three
density levels are `Density.Normal`, `Density.Compact` and `Density.UltraCompact`. When
`RenderOptions.Fit` is `DensityFit.Auto`, a fit-to-page template renders at Normal, then
Compact, then Ultra-compact, stopping as soon as the output lands within the template's
`FitTargetPages` target — measured by counting the pages of the produced PDF. `DensityFit.Fixed`
disables this and honours the requested `Density` as-is. The chosen level is reported back in
`RenderResult.Density`.

Of the built-in templates, `market-valuation-evidence` uses `DensityFitProfile.FitToPages`
(target one page); `advert-evidence-pack`, `fee-note` and `expert-report` use
`DensityFitProfile.None` and simply flow. This was validated: the sample valuation auto-fits
to Compact to stay on one page.

## Consequences

- Short fit-to-page documents stay on one tidy page automatically, with no manual tuning.
- Long documents are not forced to fit; once Ultra-compact still overflows, content flows
  across pages using the multi-page furniture from ADR 0006.
- Auto-fit costs extra render passes (up to three) for fit-to-page templates, since each
  candidate density is rendered and its page count measured. Accepted for the quality gain.
- Callers retain control: `DensityFit.Fixed` plus an explicit `Density` (and the CLI
  `--density normal|compact|ultra`) bypass auto-fit when a specific look is wanted.

## Alternatives considered

- **A single fixed density for every document:** simplest, but either wastes a page on
  near-fits or cramps short documents. Rejected.
- **Continuous scaling (e.g. shrink-to-fit zoom):** can blur the brand's exact type sizes and
  the 8.8 pt/10 pt registers, and produces inconsistent typography between documents. Rejected
  in favour of three discrete, designed density levels.
- **Manual density selection only:** workable for power users via the CLI, but pushes a
  layout decision onto non-technical GUI users for every short document. Rejected as the
  default; still available via `DensityFit.Fixed`.
