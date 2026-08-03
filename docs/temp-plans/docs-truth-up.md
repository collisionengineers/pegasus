# docs-truth-up

Operator-vetted 2026-08-03 (three-reviewer documentation sweep after the five
2026-08-03 merges; every change below was approved before work began).

## Scope

1. Blocking corrections: architecture.md no longer lists VRM recognition as
   absent (ADR-0019 engine is merged and composition-registered);
   design/README.md deferred list stops forbidding the delivered INT-28/32
   pairing; requirements.md INT-28/32 sequencing and mailbox-scope sentences
   conform to the delivered MAIL-21/22 + ADR-0020 state;
   Invoke-AzureDatabaseBootstrap.ps1's grant matrix extends to the four
   2026-08-03 migrations; operations.md's grant description matches.
2. Should-fix sweep: stale capability rows (MAIL-21, OPS-10), release counts
   (129/5), design label table (ImageIntakeRegistered + Associated with
   Case), UI-07 field list, architecture caller map, design/product route
   and actor rows, UI-15 routeless-artifact cross-references, operations.md
   ADR-0020 carve-out and smoke summary, deployment-plan recovery-gate
   sentence, native ONNX/SkiaSharp first-load note, open-decisions trims to
   ADR-0020 pointers, engineering.md CI lane nit.
3. Delete the three orphaned qdos temp-plan evidence notes; record the
   deletion in the ADR-0020 index entry.
4. NOW.md: replace the DraftReady tile "business decision" line with the
   operator's resolution — keep the shipped label for now; relabelling to
   the design authority's `Instruction draft` mapping is a queued task
   (internal intake-decision wording leaking into the UI).

No product behaviour change; the only non-doc file is the bootstrap script's
verification matrix.
