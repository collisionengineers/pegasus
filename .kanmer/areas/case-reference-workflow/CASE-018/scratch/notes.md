## Pre-merge review — 2026-08-22

Single-agent run, so this is a self-review against the three questions the repository workflow asks. Stated plainly rather than dressed up as independent.

### 1. Did the plan miss anything the ticket implied?

**Yes, and it was caught during implementation, not planning.** The plan said "delete the read-only row list from Case detail". Reading the list properly showed seven of its seventeen rows — contact name, e-mail and phone, VAT status, inspection date, deadline, address and mode — have no other home on the page. Executing the plan literally would have satisfied "show each fact once" by deleting seven facts.

Fixed by moving them into the block-grid first. The `files` document records the correction and the reason.

The plan also proposed manufacture-year and fuel-type rows; `CK_CaseDataFields_FieldName` has no entry for either, so that was dropped during file mapping rather than being discovered late.

### 2. Did the implementation miss anything in the plan?

One deliberate drop: the Export control naming which fields are blank. That is new operator-facing explanatory copy, which `docs/design/README.md` forbids and the approved necessary-copy list does not carry. Recorded in the simplification pass and in [[CASE-019]]'s report, not silently omitted.

Checked against each ticket's own "How to verify":

| Claim | Check |
| --- | --- |
| Registration appears once | In read-only view, once — the Vehicle block. The two remaining references in `_CaseWorkflow` are **form input values** under an edit lease, not restatements. |
| "Where this case stands" / "Engineer queries" gone | `grep` finds neither `block-standing` nor `block-queries` |
| Mileage appears once | The lookup panel's copy is gone; [[ENG-013]] puts the value on the Vehicle field |
| Photographs read as images | Integration test passes; migration dry run on production shows 8 rows on QDOS26011 and 6 on QDOS26010 corrected, 9 embedded photographs and 3 PDFs untouched |
| Export downloads the archive | Core tests pass; the live check is still owed and belongs in `proof` |

### 3. Did the simplification pass run with honest dispositions?

Yes — recorded on this ticket's plan under a dated heading, with two findings **not** applied and both named with reasons: the duplicated candidate projection between the export and generate image loaders, and the dropped gap-naming copy.

Two self-inflicted problems were found and fixed rather than shipped: a duplicated thirteen-field list I introduced (`b9743538`), and byte-order marks my edit tooling added to seven files that did not have them (`9e3fe232`).

### Not yet evidenced

Everything above is code and test evidence. The operator-visible claims — the page renders once, the archive actually downloads — are **not** proven until the live check after deploy. No ticket moves past `verifying` on this note alone.
