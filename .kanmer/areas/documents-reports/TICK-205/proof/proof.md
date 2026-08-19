# Proof — TICK-205

Verified on merged `dev` at `4d1bff3db4ed16692e7646ea07e7f4491365defd`.

## Decision and ownership evidence

- `rg -n -C 2 "ENG-01|RPT-03|conservative|maximised|uplift" docs/capabilities.md` confirmed:
  - ENG-01 remains one canonical repair specification with accepted route provenance and Engineer review;
  - RPT-03 intentionally preserves conservative and maximised specifications, records uplift, and requires both accepted specification versions;
  - the dependency schedule keeps accepted ENG-01 data/workflow ahead of RPT-03 rendering.
- TICK-205's operator-resolved question records two immutable Audit repair specifications—`conservative` and `maximised`—with each canonical for its role/version and neither overwriting the other.
- TICK-093 research consumes this decision as the owner of the shared versioned repair-specification aggregate, source provenance, acceptance, and correction lineage.
- TICK-098 research consumes the same decision as the owner of later Audit pair selection, monetary uplift, and immutable report binding, and remains blocked by aggregate/template prerequisites.
- TICK-207 remains linked as the deferred owner for representative Audit layout and wording. Audit rendering is unavailable until supplied/approved evidence exists; assessment templates cannot be repurposed.
- Percentage uplift remains explicitly parked until denominator and rounding rules are separately accepted.
- Inspection of SIMPLI-014's active `src/Pegasus.Core/Reports`, `src/Pegasus.Infrastructure/Reports`, and report-template files found no Audit/conservative/maximised/uplift implementation.
- The TICK-205 worktree is clean, and `origin/dev...HEAD` has no repository diff.
- `pwsh -NoProfile -File scripts/Test-DocumentationLinks.ps1` passed: all relative Markdown links resolve across 224 files.
- TICK-205 retains governing refs to FRD-06, FRD-11, and ADR-0025, links to TICK-093/TICK-098/TICK-207, and deployment `n/a`.

## Result and evidence boundary

The apparent conflict is resolved as one canonical accepted repair-specification version per role/purpose, with the Audit comparison intentionally requiring exactly one accepted conservative version and one accepted maximised version. Pegasus.Core derives the monetary uplift once.

This proof establishes the decision and ownership allocation only. It does not claim the versioned aggregate, persistence migration, Audit renderer/template, percentage uplift, deployment, or operator acceptance. No repository, Azure, Worker, or `main` change was performed.
