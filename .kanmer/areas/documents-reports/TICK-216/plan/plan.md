# Plan — TICK-216: Decide whether unaccepted wording and signature assets may ship behind a closed gate

## Approach

Treat TICK-216 as a resolved product-decision and acceptance slice subsumed by [[SIMPLI-014]], not as an independent repository implementation. The operator's 2026-08-19 “all yes” answer authorizes the exact wording, named qualifications, and three governed engineer signatures supplied by `reference/rendererref1/` for active assessment-report draft generation. It does not authorize invented content: wording or qualifications explicitly absent or marked as placeholders remain unavailable. Core must require one matching authorized engineer name/qualification/signature tuple and fail closed on missing, unknown, mismatched, or substituted values; Infrastructure may only map that accepted tuple to byte-verified governed assets. Generation remains a draft, with human approval required before issue. Audit, diminution, and addendum wording remain outside this acceptance.

SIMPLI-014 is actively changing the same FRD-11, Core report contract, Infrastructure resource mapping, templates, tests, and artifacts, so it is the sole implementation owner. Its owning PR must also reconcile the now-stale “Report wording” row in `docs/open-decisions.md`; leaving that row unresolved would contradict the recorded operator decision and merged behaviour. TICK-216 itself creates no competing worktree or diff.

## Governing docs

- **Modifies and meets — `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`, through SIMPLI-014 only.** SIMPLI-014 is already authorized to record the operator-resolved initial activation contract. It must distinguish the exact accepted rendererref1 wording/qualifications/signatures from absent placeholder content, require matching authorization and fail-closed behavior, limit activation to assessment/fee-note drafts, and preserve human approval before issue. TICK-216 makes no separate FRD edit.
- **Meets — `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md`.** Core owns authorization/readiness; Infrastructure embeds and maps governed signature assets behind the application port. No signature/wording policy is delegated to the imported renderer or a closed dormant surface. No new ADR is required.
- **Reconciles existing canonical register — `docs/open-decisions.md`, through SIMPLI-014's documentation pass.** Remove or narrow the resolved report-wording entry so it no longer asks whether the accepted assessment baseline is approved. Preserve only genuinely unresolved wording outside rendererref1 or explicitly absent from it.
- **Shared EPIC-004 constraint.** `reference/rendererref1/` remains immutable supplied evidence and is not edited or used as a second runtime policy owner.

## Steps

1. Confirm that SIMPLI-014's final implementation scope retains the resolved TICK-216 contract: exact supplied rendererref1 assessment wording, named qualifications, and Andy Patterson/Ed Mawdsley/Neil O'Reilly signature assets are accepted for draft generation; Core validates the selected authorized tuple; missing/unknown/mismatched/substituted content fails closed; no placeholder or absent wording is invented; and human approval remains required before issue.
2. Ensure SIMPLI-014's owning documentation pass updates FRD-11 with that precise accepted/unavailable boundary and reconciles the stale report-wording entry in `docs/open-decisions.md`. Keep Audit, diminution, addendum, and any wording absent from approved evidence explicitly unavailable. Do not modify `reference/rendererref1/**`.
3. After SIMPLI-014's independently reviewed PR is merged, inspect its exact merged Core/Infrastructure/resource/template/test/docs diff. Verify a closed authorized engineer mapping, byte-identified governed assets, no silent omission/fallback/custom signature path, no placeholder content, draft-versus-issue separation, FRD/open-decision consistency, and negative tests for every missing/unknown/mismatch case.
4. Inspect representative real-render evidence for each of the three accepted engineer identities where fixtures exist, plus fail-closed negative evidence. Confirm the artifact contains the selected person's matching name, qualification, and signature and cannot substitute another person's asset.
5. Record a no-code post-implementation report and outcome linking the SIMPLI-014 PR, merge commit, wording/signature tests, resource hashes, representative render evidence, and proof. State that TICK-216 was subsumed and created no repository branch, worktree, commit, PR, deployment, or cloud action; then complete its remaining Kanmer gates.

## Verification

The post-implementation report and eventual proof will cite SIMPLI-014's exact merged PR/commit and record read-only checks on merged `dev`:

- inspect FRD-11 and `docs/open-decisions.md` for one consistent accepted/unavailable wording boundary;
- confirm `reference/rendererref1/**` is unchanged and the three embedded signature assets match the governed source bytes/hashes;
- focused Core tests for accepted engineer tuples and rejection of missing name, qualification, signature, unknown key, mismatched person/key/asset, and arbitrary custom substitution;
- focused Infrastructure/resource tests proving exact asset mapping and no silent omission or fallback;
- representative Chromium/PDF evidence proving selected name, qualification, signature, fixed accepted wording, no placeholders, and draft status, without claiming approval/issue/send;
- negative evidence that absent category/recovery/storage/statement/qualification content and Audit/diminution/addendum wording remain unavailable;
- confirmation that TICK-216 itself has no repository commit, PR, worktree, deployment, or cloud action.

Final acceptance depends on SIMPLI-014's merged implementation and evidence. TICK-216 owns the resolved decision and acceptance slice only; SIMPLI-014 owns all repository changes.

## Risks / open questions

- **Active overlap:** FRD-11, Core, Infrastructure, resources, templates, and tests are all claimed by SIMPLI-014. Mitigation: no independent worktree or diff; add the acceptance obligation to the owning implementation.
- **Over-reading “all yes”:** the answer approves exact supplied content, not text the evidence says is still absent. Mitigation: keep absent/placeheld wording unavailable and fail closed rather than infer it.
- **Professional attribution mismatch:** a valid signature image could be paired with another engineer's name or qualifications. Mitigation: Core validates one closed tuple and tests every mismatch direction; Infrastructure cannot choose or substitute identity.
- **Closed-gate false delivery:** embedding assets without a caller does not prove delivery, while shipping unaccepted dormant assets adds risk. Mitigation: activate only the accepted caller-backed family and report evidence at the exact tier proved.
- **Stale canonical decision register:** FRD implementation could land while `docs/open-decisions.md` still says approval is unresolved. Mitigation: require reconciliation in SIMPLI-014's same documentation pass.
- **Operator questions:** none remain; the 2026-08-19 answer is recorded in TICK-216 open questions.
