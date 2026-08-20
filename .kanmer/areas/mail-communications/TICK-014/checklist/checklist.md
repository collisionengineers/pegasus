# Checklist — MAIL-16 verification (backfill)

- [x] Auto-link branch compared against capability text and FRD automatic-matching rule
- [x] Single-case-identity-only auto-link confirmed; ambiguous stays retained-unlinked
- [x] File presence confirmed on origin/main (2325ed4a ancestor path)
- [x] Unit tests for link/no-link/ambiguous branches located and named
- [x] Live SQL read back: `CaseReportSentEvidence` = 0 rows; 1 `Unmatched` outcome ever
- [x] Residuals recorded (zero live auto-links; unit-only coverage of auto-link path)
