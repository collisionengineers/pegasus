# Plan — TICK-186: assemble the extraction cohort and untouched holdout

## Owned change

Produce a deterministic, reproducible split of the locally present QDOS
instruction-extraction corpus into an extraction-development cohort and an
untouched holdout, with a manifest (content hash + filename + split) that is
the single fence protecting the holdout from use. Local-only artifact —
`artifacts/` is gitignored — no repository code or tracked docs change.

Reuses: `docs/runbook.md#corpus-safety-and-evaluation`'s existing
`artifacts/evaluation/` output convention; the accepted INT-17/VRM
cohort:holdout ratio and one-time-confirmation pattern
(`docs/operations.md#dated-evidence-qualifications`); the corpus's own
content-hash identity convention already used by
`tests/Pegasus.IntegrationTests/MultiFormatGenuineCorpusWebTests.cs` and
`QdosEmailCohortTests.cs`. No new abstraction, no new top-level directory
under the source tree, no code change — this is evidence assembly only.

## Steps

1. Enumerate `corpus/*.eml` (top level only) — 256 files. Confirmed no PDFs/
   `.doc`/`.msg` at that level and no labelled `extraction-corpus/QDOS/{...}`
   tree on this machine (see `research.md`).
2. Compute each file's SHA-256 content digest (read-only).
3. Sort ascending by digest hex — a content-derived, seed-free deterministic
   order — and cut at `floor(0.8 × 256) = 204` → 204 cohort / 52 holdout.
4. Verify no duplicate-content group straddles the cut boundary (checked:
   4 duplicate-content groups, all resolve to a single side).
5. Write `manifest.csv`, the reproducing script, and a `README.md` recording
   the rule, the ratio's precedent, corpus findings, and exactly how the
   holdout is fenced (the manifest's `split` column is authoritative; no
   code currently reads it — wiring a real extraction-dev harness to respect
   it is follow-on work).
6. Record findings and scope boundary in the ticket's Kanmer docs; leave
   TICK-217's migrated checklist items unchecked and explicitly parked
   (labelled cohort + operator threshold decision not available here).

## Failure behavior

If `corpus/*.eml` count changed from 256 between runs, the build script exits
non-zero rather than silently rebuilding against a different population —
the manifest must never silently drift under a stated file count.

## Tests

None — this produces evidence artifacts, not application behavior. No
`dotnet build`/`dotnet test` change; nothing under `Pegasus.Core`,
`Pegasus.Infrastructure`, `Pegasus.Web`, or `Pegasus.Worker` changed.

## Acceptance evidence (proof tier)

Command-log tier: the exact commands run, their output (file counts, split
counts, the reproducibility diff check), and the manifest's own summary
counts. Recorded in `proof.md`.

## Explicitly out of scope / parked

- Labelled, ground-truth cohort and TICK-217's threshold-acceptance
  checklist items — need the `extraction-corpus/QDOS/{audits,inspections,
  inspection-and-audit,triage}` tree (absent on this machine) and an
  operator decision. Not guessed.
- Wiring any extraction-development code/tests to actually consult
  `manifest.csv` and skip holdout rows — no such extraction-dev harness
  exists yet to wire.
- Decomposing PDF/DOC/MSG attachments embedded inside the 256 `.eml` files
  into their own cohort entries — that is extraction work itself, not cohort
  assembly.

## Simplification pass — 2026-08-20

n/a — local evidence-artifact assembly only, no application code touched.
