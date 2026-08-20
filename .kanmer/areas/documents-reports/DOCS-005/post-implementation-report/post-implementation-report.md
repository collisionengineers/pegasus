# Post-implementation report — DOCS-005

Branch task/box-custody (b842cd47). Delivered every limb:

- **Bindings dropped**: case root, audit folder and accepted source write no `pegasus-*-binding.json`; the staged `.pegasus-create-{token}` create/promote survives unchanged (crash-safe replay); an existing reference-named folder is adopted as the case's; `ValidateRootAsync`'s DB-remote-id equality remains the identity authority; the image fold still deletes a legacy binding so pre-15 folders fold; `BoxDocumentContentStore` no longer requires the case binding. Five dead helpers + two constants deleted (net −60 lines in the adapter).
- **Attachments as files**: new fail-closed `ICaseCustody.RetainAcceptedIntakeAttachmentAsync` (Box + Local); `EfQueuedCustodyProcessor` reads the receipt's `attachment`-kind assets (already retained at intake) and lands each as `002 name.pdf` onward beside the source in Evidence/Original instruction — idempotent upload-or-verify replay, per-attachment operation keys.
- **.eml claim answered in code** (ticket body): the retained file is hash-verified original MIME; the "PDF extraction" the operator saw is the base64 attachment body — now also retained as its own PDF.
- **Deployment step deferred to T10**: deleting the existing binding JSONs from the live case folders (approved Box write, exact targets listed at execution).

Tests: ProductionBoxCustodyTests + BoxDocumentContentStore 19/19 (binding assertions inverted; new attachment path; adoption of a pre-existing folder; lease-budget boundary retuned to the unbound create); new end-to-end `AcceptedCaseRetainsInstructionAttachmentsBesideTheSource` through ProcessIntake → acceptance → real processor → Local custody; custody batch 45/45; Release build 0/0.

Deviation: subagents barred — self-reviewed.

## Verification hand-off
Post-deploy: a new case's Box folder holds `001 *.eml` + `002 *.pdf` and no JSON files; legacy folders keep working; T10 deletes the old binding files.
