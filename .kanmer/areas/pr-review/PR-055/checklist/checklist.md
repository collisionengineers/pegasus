# Checklist — PR-055

- [ ] Serialize `EvaHandoffStore.RecordExportAsync` with a short transaction and case-row lock before replay lookup, committing verified replay and successful writes.
- [ ] Add the SQL regression for simultaneous identical exports, one history row, and conflicting same-key reuse.
- [ ] Run the simplicity/scope review, Release build and focused `ExportingACaseProducesTheEvaFormatArchive` integration test; write the post-implementation report with the results.

## Progress notes

(append with `set_ticket_doc(doc: "checklist", append: true)`)
