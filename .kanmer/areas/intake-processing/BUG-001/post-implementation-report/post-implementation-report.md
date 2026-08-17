# Post-implementation report — BUG-001

## Outcome

QDOS principal identity is now established once, at intake routing, and only from an exact accepted sender domain or the proved prior/original sender of a Collision Engineers staff forward. The QDOS extractor consumes that established context and no longer searches body, attachment, filename, OCR, metadata, or AI text for QDOS identity. Automatic allocation independently requires the persisted accepted route and rejects missing or mismatched draft identity.

## Governing requirements

- `docs/frd/frd-02-intake-and-source-identity.md`: sender/prior-sender provenance remains the authority for intake identity and ambiguous identity fails closed.
- `docs/frd/frd-05-documents-extraction-and-custody.md`: extraction begins only after principal establishment and continues across readable fragments while preserving missing/conflicting/OCR evidence.
- `docs/frd/frd-09-provider-and-intermediary-routes.md`: the three configured QDOS domains and Collision Engineers forwarding path are enforced by the route policy; allocation uses the accepted route principal.

## Changed files and rationale

- Core intake contracts and orchestration: add required established-principal context, derive it from an accepted selected route, and enforce policy/draft consistency.
- QDOS extraction policy: remove duplicate routing and all content-based identity heuristics; extract fields under established QDOS context; bump policy version.
- Intake allocation: use the persisted accepted route principal and fail before allocation if route identity is absent or disagrees with the draft.
- Core and integration tests: cover all three domains, staff-forward prior sender, token-free/split content, content-only rejection, ambiguous/mismatched identity, replay/allocation behaviour, and updated wrapper contracts.

## Verification

Passed:

- locked restore;
- Release build;
- focused Core route/extraction/ProcessIntake/allocation tests: 79/79;
- complete Core suite: 579/579;
- architecture suite: 93/93;
- focused QDOS triage and mailbox-route integration: 8/8;
- focused QDOS allocation-recovery integration: 15/15;
- `git diff --check`.

The complete integration project was attempted twice but did not finish within the command windows. The final quiet run exceeded 10 minutes without producing a finalized TRX result or reporting a failure. This is recorded as incomplete, not as a pass.

## Risks and boundaries

- A non-mail source is route-evaluated only when the reader supplies transport sender identity (for example an uploaded EML); content without sender provenance cannot establish QDOS.
- Manual staff creation authority and replay/idempotency paths were not changed.
- No mailbox, Box, cloud, deployment, or production data was mutated. Deployment and production evidence remain approval-gated.
- Independent review and CI remain required before merge.

## Review-fix verification — 2026-08-17

Following CI investigation:

- migrated remaining content-only/manual/multi-format fixtures so definitive QDOS paths name an accepted sender and content-only documents remain Needs sorting;
- preserved `OcrRequired` for senderless scanned documents without establishing QDOS;
- isolated the custody fixture from parallel case-match collisions with unique synthetic match keys;
- removed the desktop evaluator's obsolete discarded extraction call, restoring its Release build;
- retained neutral senders for ordinary-correspondence tests rather than inventing a non-instruction QDOS policy.

Passed locally:

- Release solution build: 0 warnings, 0 errors;
- standalone desktop evaluator Release build: 0 warnings, 0 errors;
- focused `ProcessIntakeTests`: 40/40;
- complete Core suite: 580/580;
- architecture suite: 93/93;
- affected non-browser integration classes: 59 passed before three focused corrections; all three corrected tests then passed individually;
- affected browser journey: 1/1;
- `git diff --check`.

The standalone desktop test project builds, but seven fixture-based tests cannot locate the repository root from a Git worktree because their helper requires a physical `.git` directory; one non-fixture test passed. This is a pre-existing test-harness limitation and not the extraction caller compile regression. GitHub CI remains the authoritative broad regression gate after push.
