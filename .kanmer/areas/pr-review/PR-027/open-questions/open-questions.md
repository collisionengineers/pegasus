# Open questions — PR-027

- [x] Must production behavior expand? No; tests target the accepted catalogue boundary, with production changes allowed only for an exposed defect.
- [x] Can shared LocalDB contention justify omitting focused relational evidence? No; run focused lanes exclusively and avoid an unrelated full-suite run.
- [x] Does resolver authorization require Administrator? No; MAIL-13 is casework, so `PerformCasework` is required and non-casework/system actors fail closed.
