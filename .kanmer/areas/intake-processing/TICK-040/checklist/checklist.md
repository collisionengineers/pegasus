# TICK-040 checklist

- [x] Activation boundary resolved (operator direction 2026-08-20; ADR-0025 scope option recorded in FRD-05)
- [x] `.msg` extraction implemented behind `IIntakeSourceReader` (SIMPLI-013 branch)
- [x] Attachments re-dispatch through the existing per-format pipeline (PDF via PdfPig proven end-to-end)
- [x] Fail-closed fallback preserves the manual-sorting outcome
- [x] Unit + end-to-end evidence green on the implementing branch
- [ ] SIMPLI-013 PR reviewed and merged
