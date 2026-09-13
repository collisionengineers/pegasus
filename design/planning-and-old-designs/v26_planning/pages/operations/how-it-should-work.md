# Operations — how it should work

Decided with the operator on 13 September 2026 while planning the Work Centre ([`../work-centre/how-it-should-work.md`](../work-centre/how-it-should-work.md) D1). The rest of this page's planning is deferred.

- Operations is the only place failed external work appears. The Work Centre never lists it and the High chip is gone. The Operations rail badge counts retryable failures.
- Retry stays here, as now. Failed intake processing joins it (Received file D2): failed allocation, failed OCR and re-evaluation with current policy are rows here with their action; the Intake log under Administration › Logs holds the same actions with the history.
- Where a failure blocks a person's work, the record itself says so in operator words (Vehicle "Lookup failed", Files "Storage not ready"); Operations does not need to be visited to learn that.

Deferred, to be decided when this page is planned: whether Operations stays open to Engineers and Users or becomes Administrator-only.
