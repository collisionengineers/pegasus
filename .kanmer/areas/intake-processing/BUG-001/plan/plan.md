# Plan — BUG-001

## Chosen approach

Repair the narrow QDOS marker-recognition defect exposed by the live 17 August receipt. Preserve the existing safety contract: a principal suggestion still requires one content fragment containing both a trusted QDOS marker and at least two recognised instruction-field definitions. Add only the observed PDF extraction form `OfQDOS` as a recognised QDOS brand token; do not assemble proof across attachments or make route/classification sufficient on their own.

This approach beats:

- **Cross-fragment aggregation:** unsafe because unrelated email/attachment fragments could jointly manufacture instruction proof; an existing test deliberately forbids it.
- **Trusting accepted sender + Audit classification:** conflates transport/classification with definitive instruction evidence and raises false-case risk.
- **Global PDF whitespace repair:** changes every PDF reader result and is disproportionate to one known stable extraction artifact.
- **Box/queue changes:** the live later receipt never reached allocation or external custody; those components did not fail.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: restore automatic allocation for a definitive authorised QDOS instruction while remaining fail closed for ambiguous/non-confirming material. Retain replay-safe allocation and reasoned re-evaluation.
- `docs/frd/frd-05-documents-extraction-and-custody.md`: make custody reachable only after the corrected instruction allocates its immutable Case/PO. Do not change Box naming, root fencing, source retention, or custody recovery.
- No governing-document behaviour change is planned; this repairs current code to meet existing FRD-02/FRD-05 behaviour.

## Ordered steps

1. **Create the ticket worktree at execution time.** Fetch `origin/dev`, create the dedicated BUG-001 branch/worktree, take the ticket into Implementing, and record the exact source head. Do not work from the primary checkout.
2. **Pin the live regression at Core policy level.**
   - Add a positive test using the non-personal observed structure: a single fragment containing `Proud Members OfQDOS Accident Assistance Ltd` plus at least two recognised instruction labels.
   - Assert `Applicable`, suggested principal `QDOS`, extracted fields, and `instruction-structure` evidence.
   - Retain the existing cross-fragment refusal test.
   - Add negative cases showing unrelated embedded strings containing `qdos` remain non-applicable.
3. **Implement bounded marker recognition.**
   - Centralise the accepted marker predicate used for content and transport evidence.
   - Recognise standalone `QDOS` and the exact word `OfQDOS` produced by the observed PdfPig extraction.
   - Do not accept arbitrary prefixes/suffixes, do not relax the two-label threshold, and do not aggregate confirmation across fragments.
   - Bump the extraction policy version if repository conventions treat recognition changes as a policy-version change; update assertions/persistence fixtures consistently.
4. **Add orchestration/replay coverage.**
   - Exercise the corrected Audit instruction shape through processing and automatic allocation.
   - Prove exactly one Case/PO and custody work item are produced.
   - Prove replay remains idempotent.
   - Prove a non-confirming lookalike remains `needs_sorting` with no allocation.
5. **Run local verification.**
   - QDOS extraction-policy unit suite.
   - focused intake processing and QDOS allocation/recovery integration suites.
   - custody outbox and Worker composition tests.
   - Release build plus `git diff --check`.
   - Record exact counts/output in the post-implementation report.
6. **Independent review and CI.** The reviewer must answer whether the implementation preserved the same-fragment/two-label safety boundary and whether the plan missed any live-evidence implication. Merge to `dev` only after review passes and CI is green.
7. **Prepare deployment evidence, but do not infer authority.**
   - Build/release using the repository manifest flow.
   - Before any Azure write, obtain explicit approval for the exact Web/Worker deployment targets and revision.
   - After deployment, read back the Web diagnostic SHA, Worker package/revision evidence, trigger settings, migrations, health, and telemetry; refresh `docs/current-architecture.md` and `docs/operations.md` in the same task.
8. **Recover the retained failed receipt only with separate exact-target approval.**
   - Target receipt `9a91fe16-d62f-4477-a11e-830fd96f672a`.
   - Use the existing authenticated, reasoned “Re-evaluate with current policy” command; never insert or edit allocation/Case/Box records directly.
   - Confirm history retains the prior `needs_sorting` decision and records the new policy evaluation.
   - Confirm exactly one allocation attempt, Case/PO, Case-intake link, `create_case_custody` work item, Box folder, retained source, and custody confirmation.
   - Confirm repeat/replay does not duplicate any effect.
9. **Disposition.**
   - If the corrected deployment and controlled re-evaluation pass, write `proof.md`, record commits/PR/deployment, and close BUG-001.
   - If re-evaluation still stops before allocation, preserve all evidence and stop; file a narrow follow-up at the newly identified boundary.
   - If deployment or live-write approval is absent, leave BUG-001 open as implemented but not production-verified.

## Proof production

`proof.md` must record separately:

- regression test demonstrating the `OfQDOS` extraction shape;
- negative and cross-fragment safety tests;
- local build/test evidence and exact source SHA;
- merged/deployed Web and Worker revision identity;
- pre/post state for receipt `9a91fe16-d62f-4477-a11e-830fd96f672a`;
- reevaluation history, allocation, Case/PO, link, external custody work, Box folder/source custody, and replay counts;
- exact approval record for deployment and production re-evaluation.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Broadened marker matching creates false cases | Accept only standalone `QDOS` or exact `OfQDOS`; retain same-fragment and two-label gates; add negatives |
| Cross-document evidence is accidentally combined | Keep `confirmingFragments` semantics and its existing refusal test unchanged |
| Policy change is persisted under a stale version | Follow the repository's policy-version convention and update all version-bound fixtures |
| Existing failed receipt is manually patched | Use only the reasoned reevaluation use case after deployment |
| Duplicate Case/PO or Box folder on recovery | Assert zero pre-state, one post-state, and replay idempotency across allocation/link/custody |
| Deployment is mistaken for live proof | Require actual retained-receipt reevaluation and SQL/telemetry/Box readback |
| PII leaks into tests or docs | Use only the non-personal structural marker/label shape; never commit the downloaded live EML/PDF |

## Stop point for this phase

Research and planning end here. Do not create the worktree, edit source, deploy, re-evaluate the production receipt, or mutate Box/Azure/SQL as part of this phase.
