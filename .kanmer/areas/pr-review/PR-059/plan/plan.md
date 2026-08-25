# Plan — PR-059: Reconcile ENG-016 ticket evidence with the final implementation

## Approach

Run this ticket last, after PR-055, PR-056, PR-057, PR-058 and PR-060 have landed on PR #539. Then take one fresh snapshot of the final diff, governing documents, head SHA, tests and CI, and reconcile ENG-016’s Kanmer record and PR description against that snapshot. This avoids repeatedly rewriting evidence while the implementation is still changing. Preserve historical research with an explicit supersession note; replace only documents that are intended to describe current scope or final outcome. Add no product or repository code.

## Governing docs

- **Meets — `docs/frd/frd-07-eva-and-external-engineering-handoff.md`.** The reconciled ENG-016 documents will state its final one-Review/one-Export rule: populated suggestions are usable; VAT and mileage are optional; mileage needs a unit when present; blank Inspection Date defaults on Export; Case/Audit custody and accepted-only status are not duplicate Export gates; the three delivery routes remain distinct.
- **Also trace — `docs/frd/frd-04-parties-accounts-and-access.md`.** Link ENG-016 to the permanent action-history requirement and identify the final replay/history implementation and tests.
- **Also trace — `docs/adr/0030-non-additive-schema-changes-before-cutover.md`.** Link ENG-016 to the authority for direct pre-cutover table removal, roll-forward recovery, and later release-record obligation.
- **Also trace — PR-057’s superseding ADR.** After PR-057 creates/accepts it, link that exact ADR and record that deletion of the obsolete EVA MCP tools follows the new decision rather than contradicting accepted ADR-0021.
- **No governing document is modified by PR-059.** PR-057 owns the ADR/capability correction; this ticket only links and reports the final authorities.

## Steps

1. Wait until PR-055, PR-056, PR-057, PR-058 and PR-060 are present on PR #539. Confirm their dispositions from their ticket outcomes and the final PR diff; do not infer completion from ticket titles.
2. Refresh ENG-016, all of its documents and scratch review, PR #539’s complete changed-file list/diff, final head SHA, check results, and the current governing documents.
3. Link ENG-016 through Kanmer to FRD-07, FRD-04, ADR-0030 and PR-057’s actual superseding ADR. Re-run `get_doc_gates ENG-016` and confirm the governing-document requirement is satisfied.
4. Append a dated final section to ENG-016 research that names the operative conclusion and explicitly marks the earlier permissive-only and accepted-only/custody conclusions as superseded. Preserve the earlier research as history.
5. Replace ENG-016 `files.md` with the final amended PR surface. List every changed file individually or in a group only where the files share one exact rationale; distinguish implementation files from context files and record deliberate exclusions.
6. Reconcile ENG-016’s plan and checklist with the final implementation and all review-ticket dispositions. Untick or remove any statement not proved on the final head; do not claim concurrent replay, unconditional readiness, batching, ADR consistency or migration accuracy merely because another ticket was opened.
7. Rewrite ENG-016’s post-implementation report as the final file/rationale inventory and governing-doc compliance report. Include each PR-055–PR-060 disposition, final commits/head SHA, exact local test results and final CI state. Keep deployment explicitly unclaimed until release proof exists.
8. Update ENG-016’s body/traceability fields and PR #539’s description so they agree with the reconciled ticket documents and carry the correct Kanmer footer.
9. Perform a final read-only audit: compare the PR changed-file list to `files.md` and the report, compare each governing requirement to its implementation/test evidence, confirm no contradictory current rule remains, and record the audit in PR-059’s post-implementation report.

## Verification

- `get_doc_gates ENG-016` shows the governing-document requirement satisfied.
- The set from `gh pr diff 539 --name-only` is fully accounted for by ENG-016 `files.md` and its final report.
- Searches across ENG-016’s current-state documents find no operative accepted-only, thirteen-required-field, Case/Audit-custody, or no-default Inspection Date rule; historical occurrences are explicitly labelled superseded.
- ENG-016 checklist claims match the actual final tests/checks and blocker dispositions.
- `gh pr view 539` and the ticket record name the same final head, commits, PR and CI result.
- PR-059’s post-implementation report records this evidence. After merge, `kanmer-verify` confirms the reconciled documents against merged `main` and writes proof.

## Risks / open questions

- **Evidence races with implementation changes:** sequencing is mandatory; if any blocker changes PR #539 after reconciliation starts, refresh and repeat the inventory before reporting completion.
- **Erasing useful history:** append the research supersession note rather than rewriting old research.
- **Duplicating another blocker:** PR-059 records outcomes only. Product code, tests, ADR content, capability edits, batch reads, replay enforcement and migration corrections remain owned by PR-055/056/057/058/060.
- **Premature deployment claims:** record `not-deployed` until the release workflow supplies actual production evidence.
- No open questions.
