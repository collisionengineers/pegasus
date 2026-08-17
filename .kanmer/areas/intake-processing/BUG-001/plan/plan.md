# Plan — BUG-001

## Chosen approach

Make the accepted principal context an explicit, required input to instruction extraction and automatic allocation.

For mailbox intake, `ProcessIntake` already owns the authoritative `MailRouteEvaluationResult`. It will derive a small provider-neutral established-principal context only from an accepted selected route and use that context to select and invoke QDOS field extraction. `QdosInstructionExtractionPolicy` will stop evaluating routes and stop scanning content or metadata for QDOS identity. Once supplied an established QDOS context, it will extract instruction fields across readable content without requiring the word “QDOS” anywhere.

Automatic mailbox allocation will use the persisted accepted route principal as authority and fail closed if the extraction draft is missing its principal or disagrees with that route. Manual-upload and automation content will not be allowed to establish QDOS merely by containing QDOS wording; those channels need their own separately authenticated or staff-confirmed principal context.

This is preferred to:

- **Passing only a Boolean such as `isQdos`:** it loses provenance and cannot enforce route/draft/allocation consistency.
- **Letting extraction re-run `QdosMailRoutePolicy`:** that preserves duplicate identity ownership and cannot support authenticated non-mail contexts safely.
- **Keeping content markers as fallback evidence:** this directly violates the settled operator rule.
- **Changing Box, PDF/OCR, or queue code:** those components are downstream and did not cause the failure.
- **Building a generic multi-provider framework:** only QDOS is active; the smallest provider-neutral context preserves the seam without introducing dormant policies.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: the plan keeps receipt separate from case creation, requires an established Principal for automatic allocation, and fails closed on missing or conflicting identity.
- `docs/frd/frd-05-documents-extraction-and-custody.md`: documents provide instruction fields only after identity is established; custody remains downstream of immutable allocation.
- `docs/frd/frd-09-provider-and-intermediary-routes.md`: exact effective-sender route identity remains separate from classification and case association. The three accepted QDOS domains and prior-sender rule remain unchanged.
- `docs/operator-notes.md` and accepted ADR-0008 are binding context: a Collision Engineers sender is transport provenance and the one proved prior/original sender drives the forwarded route.
- No PRD, FRD, ADR, capability allocation, or operator-note change is required. This is a defect correction to conform code to existing behaviour.

## Ordered implementation steps

1. Start from fresh `origin/dev` in the ticket worktree. Remove or recreate the abandoned uncommitted BUG-001 worktree so no earlier body-identification experiment survives. Take the ticket into Implementing and record the exact base SHA.
2. Add failing Core regressions before production changes:
   - direct QDOS sender on each accepted exact domain;
   - Collision Engineers staff forward with one proved prior sender on an accepted QDOS domain;
   - instruction fields split across body/attachments with no QDOS token in any readable content;
   - expected QDOS draft and `CaseCreated` result.
3. Add identity-negative regressions:
   - QDOS text in body, subject, filename, transport metadata, or attachment with a non-QDOS effective sender;
   - manual-upload and automation content containing QDOS plus instruction labels without an established principal context;
   - staff forward with missing, malformed, internal, conflicting, or multiple prior senders;
   - subdomain and suffix-widening attempts.
   Every negative must produce no QDOS draft, automatic allocation, case link, custody work, or Box work.
4. Introduce the smallest provider-neutral established-principal/extraction context in Core. It must carry the authoritative principal code and its accepted provenance/route decision; it must be required rather than optional on the automatic extraction path.
5. Refactor `ProcessIntake` orchestration:
   - keep existing mailbox route evaluation and early fail-closed exits;
   - derive QDOS extraction context only from `Accepted` plus selected route/work provider `QDOS`;
   - invoke the matching extraction policy using that context;
   - do not derive QDOS context for manual-upload or automation content without a separately established principal;
   - keep mail classification and case matching as distinct decisions; neither re-proves QDOS.
6. Refactor `QdosInstructionExtractionPolicy`:
   - remove its internal `QdosMailRoutePolicy`;
   - remove `QdosMarkerRegex`, `qdos-content-marker`, `qdos-transport-marker`, and the same-fragment QDOS-plus-two-label gate;
   - require established principal `QDOS`;
   - extract fields from all appropriate readable fragments using existing field conflict/missing-value rules;
   - create the draft principal from the established context, not document text;
   - preserve OCR information and accepted Triage matcher evidence;
   - bump the extraction policy version.
7. Add a route/draft/allocation invariant:
   - automatic mailbox allocation obtains principal from the persisted accepted route;
   - missing accepted route, missing selected principal, or a route/draft mismatch fails closed before allocation;
   - manual staff-create authority remains separate and unchanged;
   - replay uses the same immutable principal and remains idempotent.
8. Update composition and wrapper/test implementations for the required extraction context without adding provider-specific policy to Infrastructure or duplicating business rules.
9. Replace—not merely supplement—tests that encode content-derived QDOS identity. Audit QDOS extraction, ProcessIntake, multi-format/manual upload, automation ingress, QDOS Web/Triage, sent-evidence, architecture, and wrapper-policy fixtures so every definitive QDOS path names its authorised principal source.
10. Add focused integration proof for:
    - accepted direct and staff-forward sender → fields without QDOS text → one Case/PO and case link;
    - exactly one custody work item under replay;
    - content-only and route/draft mismatch paths → zero downstream work;
    - retained effective sender, route policy key/version, and extraction policy key/version.
11. Run canonical verification: locked restore, Release build, focused QDOS route/extraction/ProcessIntake/allocation/custody tests, Worker/composition and architecture tests, full test suite where practical, and `git diff --check`. Record exact commands and results in the post-implementation report.
12. Independently review the PR against the governing docs and this plan. The reviewer must explicitly check that no content source can identify QDOS, the prior-sender route remains exact, automatic allocation uses route authority, negative paths create no downstream work, and no unrelated classification/Box/parser scope entered.
13. Push and open a PR targeting `dev`; do not merge until independent review passes and CI is green.
14. Deployment and production receipt reevaluation remain separate exact-target approval gates. If authorised after merge, deploy the immutable reviewed revision, refresh `docs/current-architecture.md` and `docs/operations.md`, then re-evaluate only the approved retained receipt and verify one allocation, Case/PO/link, custody item, and Box outcome with no duplicates.
15. On merged `main`, write `proof.md` from test/CI evidence and any separately authorised production evidence, then close out only when the resolved gates pass.

## Acceptance conditions

- The effective sender is the only QDOS mailbox identity source: direct sender, or the one proved prior/original sender of a Collision Engineers staff forward.
- Accepted domains remain exactly `qdosassist.co.uk`, `qdosassists.co.uk`, and `qdoslaw.co.uk`, with no subdomain or suffix widening.
- Once the QDOS route is accepted, extraction never asks content to re-prove QDOS.
- QDOS instruction fields may be spread across readable fragments and none needs to contain “QDOS”.
- Body, subject, filename, attachment, metadata, OCR, or AI text alone cannot establish QDOS on any channel.
- Automatic mailbox allocation uses the persisted accepted route principal and rejects missing or inconsistent route/draft identity.
- Existing field ambiguity, case-match ambiguity, replay, allocation, custody, and Box sequencing remain fail closed and idempotent.
- The valid corrected path produces exactly one Case/PO, link, custody work item, and eventual Box folder after authorised deployment; negative paths produce none.
- No change is made to Box integration, queue topology, database schema, PDF/OCR parsing, classification rules, accepted suffixes, or forward reconstruction.

## Risks and mitigations

- **Large fixture ripple:** many tests use manual content-only QDOS. Classify each fixture by intended principal source and convert deliberately; do not mechanically label everything mailbox.
- **Generic-contract leakage:** keep the context limited to established principal/provenance and route selection; do not embed QDOS-specific fields.
- **Silent principal mismatch:** validate at processing and allocation boundaries and add adversarial tests.
- **Classification scope creep:** preserve classification as a separate recorded fact; this ticket removes principal re-identification only.
- **Replay regression:** run same-evaluation and repeated-consumer integration cases and assert one allocation/custody result.
- **Stale experimental worktree:** recreate from `origin/dev` before execution and verify a clean status/base SHA.

## Proof strategy

The post-implementation report will map every changed file to the governing docs and include the exact focused/full command results. Independent review and green CI provide pre-merge evidence. `kanmer-verify` will repeat the locked build and focused identity/allocation/custody tests on merged code. Production deployment, retained-message reevaluation, and Box proof occur only after explicit exact-target approval and are never inferred from local tests.

## Operator clarifications applied during review — 2026-08-17

These later operator statements supersede narrower planning language above:

- Pegasus is pre-release; there are no legacy live receipts and no migration/recovery compatibility requirement for pre-route data.
- QDOS identity is established from the exact recorded sender suffixes, or the proved prior/original sender of a Collision Engineers staff forward. An uploaded EML's parsed sender may be evaluated by that same rule; no additional authentication framework is introduced in BUG-001.
- Once QDOS is identified, BUG-001 does not add a second “instruction versus non-instruction” or scan-completeness identity gate. Defining non-instruction email behaviour is future work.
- Senderless scanned documents still retain their provider-neutral `OcrRequired` processing outcome; OCR never establishes QDOS.
