## 2026-08-22 04:40Z — why there were no logs, and what the record does say

The missing exception was never lost by the application. It was dropped at the
Application Insights ingestion boundary: the workspace runs a **0.1 GB daily
quota resetting at 03:00Z**, and both custody failures fell in a capped window
(QDOS26009 at 23:00:58Z, QDOS26010 at 02:02:19Z). Full evidence in
[[PLAT-034]]'s research. Worker telemetry itself has been healthy all along —
71,078 traces over the retained week — so the moment the cap is raised, or a
retry runs inside the ingestion window, the failure is readable.

What the production record does say, read at 04:38Z:

```
Reference  Kind                       State      Attempts  FailureCode                  Reason
QDOS26010  create_case_custody        failed     1         custody_unexpected_failure   Case evidence could not be stored.
QDOS26010  vehicle_lookup             completed  1
(none)     create_image_case_custody  completed  1                                      (2026-08-21T23:31:52Z)
QDOS26009  create_case_custody        failed     1         custody_unexpected_failure   Case evidence could not be stored.
QDOS26009  vehicle_lookup             completed  1
```

Two findings worth keeping:

1. **`create_image_case_custody` completes against the same Box tenant** twenty
   nine minutes before QDOS26010's `create_case_custody` fails. Box credentials,
   connectivity and the root folder are therefore all working. Whatever fails is
   specific to the *case* custody path, not to Box access.
2. **`AttemptCount = 1`.** Both failed on their first attempt and were never
   retried — the classifier routed an unclassified exception to a terminal
   failure rather than a transient one. Given the operator confirms the files
   *did* reach Box, the throw is after the uploads, in the record-writing half.

`CaseDocuments` for both cases is **0 rows**, consistent with the throw landing
between the Box uploads and the document registration [[DOCS-007]] added.

Release 19 (`42125b34`, live 04:21Z) carries the diagnostic that appends the
exception type to the failure code. The next occurrence — a real instruction, or
an operator retry from the case Custody page — names its own type, and that
retry now falls inside the ingestion window as well.
