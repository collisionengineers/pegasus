# Post-implementation report — INTK-065

## Outcome

Corrected the current principal-evidence source inventory and its generated package. The five current records now point to the current policy sources, including the QDOS extraction v8 identifier and current shared-taxonomy bytes. Historical evaluation, cohort, and review-baseline content remain unchanged.

The two principal profile documents now live at `docs/principal-profiles/`. The relocated QDOS document is byte-identical; the README contains only the approved clarification distinguishing the historical v1 review baseline from current source links. The documentation index and the existing placement gate/fixture recognise the canonical destination.

## Scope delivered

- Updated the existing generator and matching Core corpus expectations.
- Regenerated only `reference/workproviders-and-repairers/principal-identification-corpus.v1.json` with the existing helper.
- Moved the two approved documentation files and updated only the matching index link.
- Extended the existing Markdown-placement allow-list and regression for the new canonical path.
- Added the required documentation-routing convention to `AGENTS.md`.

No runtime policy, original corpus input, historical evaluation, deployment configuration, cloud resource, or `docs/docs-review-temp/**` record changed.

## Verification

Focused host-serialized verification passed against the frozen working-tree changes based on HEAD `7b6aa189c2112ab3cf8df2c2e337fc9f2b0dabae`, later committed as `5e0aeb47b9cb87258e12f66967e1efee4743b3f3`:

- Generated package SHA-256: `494e0a0f42ced164aab97cd50ebb497c1479c09eaf9f0a4db177949bbdc7c251`; canonical bytes, five current snapshots, 26 approved evidence-reference replacements, seven unchanged historical sections, and the allowed JSON delta all passed.
- `python -m unittest discover -s scripts/reference_data/tests -p test_build_principal_identification_corpus.py` — exit 0; 2 passed.
- `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1` — exit 0; 140 files checked.
- `pwsh -NoProfile -File ./scripts/Test-TestMarkdownPlacement.ps1` — exit 0.
- `dotnet restore tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --locked-mode` — exit 0.
- `dotnet build tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj -c Release --no-restore` — exit 0; 0 warnings, 0 errors.
- Focused `PrincipalIdentificationCorpusTests` — exit 0; 7 passed.
- `git diff --check` — exit 0.

The verifier's first ignored structural harness exited 1 because it hashed compact JSON rather than the generator's canonical bytes. A verifier-only harness correction was explicitly authorized; it made no product-source change. The corrected harness exited 0 and the initial failed attempt remains recorded in `scratch/execution.md`.

Full original-input regeneration is unavailable on this host and was not claimed as PASS; the approved existing-helper evidence boundary was used.

## Original pre-merge handoff

Commit `5e0aeb47b9cb87258e12f66967e1efee4743b3f3` is pushed and draft [PR #706](https://github.com/collisionengineers/pegasus/pull/706) targets `dev`. It is ready for independent review. No self-review, merge, proof, closeout, or deployment action has been performed.

## Evidence-record correction after integration — 8 September 2026

The earlier report copied an incorrect frozen SHA and three incorrect command names from the verifier summary. Root and verifier checked the actual execution receipts and retained TRX, and the current report above now uses those observed values. The old summary and explicit correction are both retained in scratch/execution; no command was rerun to repair this transcription error. The placement command was the existing regression harness, not a parameterless invocation of the placement gate. Actual focused TRX: artifacts/verification/intk-065-source-inventory.trx; SHA-256 E32A60A18AED5DA1687AFD6B6D373405A04FFCF68ECB28ED9EA620AD62D2F886; seven executed/passed, zero failed/skipped. Original harness failure remains recorded.

PR #706 has since merged into dev at d76de2534ec6651c1a434a55f76593b7b140bf1c. That does not turn this pre-merge evidence into post-merge proof. Exact-merge verification is separately prepared and remains pending in scratch/verify.
