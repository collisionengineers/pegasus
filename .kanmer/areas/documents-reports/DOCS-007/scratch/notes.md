## 2026-08-22 — what is left, exactly

Deployed in release 17 (`71911734`), carried to release 20 (`05fe7a7f`).

This ticket's registration code is what exposed [[DOCS-008]]: it was the first
caller to write `CaseDocuments` / `DocumentVersions` / `DocumentOccurrences` from
the **Worker**, which had never been granted those tables. The code was correct;
the database refused it. Release 20 fixed the grant and the permission is
verified live as the Worker principal itself.

Production today:

```
CaseDocuments for QDOS26009: 0
CaseDocuments for QDOS26010: 0
```

Zero because every write was denied, not because registration was skipped.

**Single gate:** one case whose custody completes. Then the row count is non-zero
and the Evidence tab serves from Box through `IDownloadCaseDocument` rather than
the staging blob. Either a new instruction, or the **Retry custody** control on
an existing failed case (`_CaseWorkflow.cshtml:486`), produces it.
