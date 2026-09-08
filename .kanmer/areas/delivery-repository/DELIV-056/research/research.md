# Research — DELIV-056: definitive instruction fixture alignment

*This records why the scoped test inputs need correction.*

## Question

Why did the D2 failures in PR #700 run 34196369756 stop before their existing assertions, and what is the smallest correction that makes each listed fixture establish a QDOS definitive instruction under the current policy?

## Findings

- The failed CI checkout was PR #700 merge SHA `98f4b701b0814900006a424719c17645b7288197`. Its D2 failures are fixture setup failures, distinct from the migration and ENG fixture failures in the same run.
- QDOS extraction selects an instruction document only when its document content contains all of `QDOS`, `Registration:`, and `Our Client’s Vehicle:`; sender identity, filename, and email body text do not select the profile.
- Classification is a separate normal-policy decision. An auto-allocating QDOS instruction also needs a formal request title, such as `ENGINEER NOTIFICATION`, in `DocumentContent` or `PdfContent`. A title placed only in an email body is not definitive.
- The 13 ticket-named integration tests retain old `QDOS instruction` / `Vehicle Registration:` body shorthand. It cannot establish either the selected document profile or the document-based definitive classification.
- The documented QDOS instruction structure is sufficient for self-contained test evidence; its generated MIME or document bytes are not represented as received production emails. The existing pipeline paths, MIME/PDF/DOCX builders and parser remain under test.
- A small extension to the existing `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` helper is proportionate: `IntakeTestEvidence.DefinitiveQdosInstructionDocument(...)` provides the documented formal-document text while callers retain their scenario identity/data and put it in actual document content.
- `AllocationTestData.StoreDefinitiveReceiptAsync` is appropriate only for display/setup paths whose claim is an already accepted case, not an intake-pipeline regression.
- `SendToAiIntegrationTests.cs` separately expects a disabled gated Send to Claude control. Current pre-handoff markup omits both dialog and control; its absence and denied-POST checks remain the relevant access boundary.
- No current live owner holds these fixture lines. UIIMP-012 and CASE-045 have expired leases and separate current scope.

## Implications

- Every intake-pipeline scenario must use the existing transport and format path, but attach or upload a document whose content carries both the QDOS signature and `ENGINEER NOTIFICATION`.
- No sender/body shortcut, corpus gate, source-policy change, schema change, or production change is permitted.
- Preserve existing scenario identities, routes, lifecycle, access and destination assertions; report an exposed defect rather than weaken it.

## Open questions

- None.
