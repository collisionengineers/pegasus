# Open questions — TICK-058

- [x] Retire API-02; API-01 returns a durable receipt and API-03 owns only terminal Case/PO resolution. — Operator decision, 2026-08-21.
- [x] Use the stable Pegasus Principal as the isolation boundary and retain ADR-0004. — Existing authority.
- [x] Reuse grouped durable intake, SQL outbox, Storage Queue, Worker, custody store, Container App, managed identity, and telemetry. — Code/live Azure verification.
- [x] Return no files, reports, source material, or outbound delivery. — Operator clarification and contract separation.
- [x] Do not add APIM/Front Door/Service Bus/another Function/store without measured need. — Simplicity and verified topology.
- [x] **Reopened and re-settled 2026-08-28.** Exact route, credential presentation, request media/parts, idempotency header, response schema/statuses and error codes. — The 2026-08-28 multipart settlement is **superseded by the operator's structured-schema ruling the same day** (see below). The route, credential, idempotency header and status codes are unchanged; the request media is now `application/json` with the instruction fields stated and files carried inline as base64.
- [x] How a provider submission binds to its Principal inside processing. — The `ProviderSubmissions` row is the binding; `ProcessIntake` reads it through `IProviderSubmissionBindings` for the `provider_api` channel and skips mail-route selection.
- [x] **Document-derived or provider-declared instruction?** — Provider-declared. The multipart contract required the Principal's extraction policy to read the business values back out of the submitted documents; that policy recognises QDOS only, so a non-QDOS Principal was retained for sorting and never allocated. It also had no caller: QDOS arrives by e-mail, and a provider integrating over HTTP already holds the fields. **One endpoint, not two** — operator decision, 2026-08-28.
- [x] **Case type vocabulary.** — `inspection` → `Inspection`, `audit` → `Audit`, `auditreport` → `InspectionAndAudit`, `triage` → opens a Triage record with no Case/PO. Triage is not required to begin from mail classification; that is merely how it usually arrives. Operator decision, 2026-08-28.
- [x] **Who decides the Audit verdict.** — The provider's declared `originalReportVerdict` wins and derives the `a.` / `ap.` prefix. This overrides FRD-01's "Pegasus reads the literal outcome in that original report" for this route, and FRD-01 is amended accordingly. Operator decision, 2026-08-28.
- [x] **Provider reference vs claim number.** — One field. `YourRef` is renamed `claimNumber` and is the identity-critical value; Pegasus's own Case/PO remains what fills EVA's `ExternalRef`. Operator decision, 2026-08-28.
- [x] **Acceptance actor guard.** — `AcceptIntake`, `EfCaseAcceptanceStore` and `AddCaseNote` are widened to admit `ActorKind.Provider` rather than laundering the submission through the system-worker identity, which would lose the attribution FRD-09 requires. Operator decision, 2026-08-28.
- [x] **File semantic role.** — Accepted per file but optional; absent, the ordinary classification/extraction path decides the role, so a caller that cannot classify its own attachments is not blocked. Operator decision, 2026-08-28.

## Parked (explicitly deferred)

- [ ] Name the first provider, public hostname/custom domain, request/throttle values beyond the code default (60/min/key), capacity target, and rollout date. Deferred to activation and exact-target approval (capabilities.md boundary).
- [ ] Support multiple simultaneous credentials per Principal or APIM gateway policy. Deferred until concrete callers/traffic justify them.
- [ ] Result lookup beyond the submission's own Case/PO reference (API-03 scope). Deferred; TICK-060 owns it.
- [ ] Whether the declared-verdict ruling is also recorded in `docs/operator-notes.md`. That file is protected; the ruling is recorded in FRD-01 and FRD-09 and in this document, and the operator has not yet been asked whether operator-notes should carry it too.
- [ ] Cover type, excess, sum insured, repairer block, policy number, insurer/third-party name, estimate money and private-hire licensing. EVA's instruction model carries them; Pegasus holds no case field for any, and inventing domain data is a stop condition. A later widening needs an operator-accepted field for each.
