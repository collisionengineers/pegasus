# Research — PLAT-044: Assessment opening latency

## Question

Why does opening QDOS26016's Assessment screen take 5–10 seconds, which checks are duplicated after the Case has entered Review, and which existing batch mechanisms can be reused?

## Findings

- Production Web is a healthy one-replica Container App (1 CPU / 2 GiB, min=max=1); the delay is not scale-to-zero startup. Live EF command timings during the investigation were normally 1–5 ms.
- QDOS26016 is in Review and has three custody-confirmed report JPEGs. `IndexModel.OnGetAsync` calls `GenerateCaseAssessmentReportDraft.PrepareAsync`; its EF projection source reloads the broad Case and Assessment projections, selects confirmed documents, and downloads each photograph serially.
- On deployed PLAT-041 code, a single Box managed read costs four calls. Three photographs therefore imply about twelve Box requests during page opening. PLAT-041's `ReadVersionsAsync` already provides the correct ordered, bounded-concurrency, hash/length-verified batch route and explicitly named this report loop as follow-up.
- The GET serially composes broad `GetCase` and `GetCaseAssessment` projections twice, plus separate specification, AI, and report-document queries: approximately 55 application SQL commands for this case. The broad Case projection loads documents, history, custody preparation, tasks, upload links, and other data the Assessment screen does not render.
- Review entry already owns instruction-completeness and image-completeness. The operator resolved the behavioural question in this task: report readiness must not recalculate requirements needed to reach Review; being in Review is the evidence that they passed. Missing downstream evidence is an invariant/operational failure at generation, not an ordinary readiness blocker.
- `AssessmentReportProjection` currently contradicts that decision by rechecking claimant/reference/addressee/incident/inspection facts and photograph/source counts, and FRD-11 describes those checks. FRD-11 and operator notes must be corrected with the explicitly supplied operator decision.
- `Cases.CustodyRootRemoteId` already persists the Box case-folder identity. `BoxDocumentContentStore` nevertheless lists the entire approved root by Case reference for every operation. Carrying the durable id in `ManagedDocumentContentAddress` removes that growing lookup while existing child/list/upload methods retain ancestry fencing.
- Static .NET inspection found no material sync-over-async, per-call HttpClient, serialization, or string hot-path defect. The material problem is duplicated database and remote I/O.

## Implications

- The Assessment GET must use one narrow projection and perform zero document/content-store work.
- Report readiness contains only assessment/report-preparation work; Review prerequisites are removed from its list.
- Actual report generation reuses the narrow relational projection, loads document metadata once, then performs one `ReadVersionsAsync` batch.
- All managed content callers receive the durable case-root id; streaming ZIP and single-file callers keep their existing byte-access shapes.
- No migration, cache, compatibility shim, background work, or new deployment component is justified.

## Open questions

None. The operator explicitly resolved the lifecycle/readiness boundary and approved implementation.
