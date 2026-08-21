# Proof — DOCS-006 (deployed at release 16; live-tier pending fresh mail)

Type: test-output + command-log. Deployment evidence bundle: [[DELIV-015]] proof.

**Proven now:**
- Deployed to production at release 16 (`4111ad29`): the custody processor promotes the receipt's extracted embedded photographs beside the source (`Evidence/Original instruction`, continuing ordinals, per-asset operation keys), `InstructionEvidenceImages.Select` owns the selection rule (attached always, embedded ≥ 40 KB, inline never, hash-deduped preferring attached), and the case Evidence tab renders the instruction-photographs gallery through the receipt-asset image endpoint. Both runtime roles now hold the `ImageIntakes` UPDATE the image-custody path needs (PLAT-020, verified live).
- Behaviour proven at the deployed SHA: `InstructionEvidenceImagesTests` (selection rule), the real-corpus custody fact `AcceptedCaseRetainsEmbeddedPhotographsBesideTheSource` (EREF9 images-PDF email end-to-end through acceptance and custody), and the asset-endpoint web fact — green in merge CI.
- Live render check: the case Evidence tab on the current cases correctly shows no instruction-photographs gallery (their receipts were accepted before this deploy — the section renders only when non-empty, by design).

**Not claimed yet (honest tier):** a production acceptance running the new promotion — no instruction email has been accepted since the deploy.

**Completes when:** the operator's first post-wipe images-PDF instruction email shows its photographs on the case Evidence tab and as individual files beside the source in Box.
