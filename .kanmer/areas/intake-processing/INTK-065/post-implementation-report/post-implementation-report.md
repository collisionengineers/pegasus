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

Focused host-serialized verification passed against frozen head `7b6aa189b9d00d772819e3d3f21022be549ee229`:

- Generated package SHA-256: `494e0a0f42ced164aab97cd50ebb497c1479c09eaf9f0a4db177949bbdc7c251`; canonical bytes, five current snapshots, 26 approved evidence-reference replacements, seven unchanged historical sections, and the allowed JSON delta all passed.
- `python -m unittest tests/test_generate_principal_identification_evidence.py` — exit 0; 2 passed.
- `python scripts/verify_docs_links.py` — exit 0; 140 files checked.
- `pwsh -NoProfile -File scripts/Test-MarkdownPlacement.ps1` — exit 0.
- `dotnet restore tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --locked-mode` — exit 0.
- `dotnet build tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj -c Release --no-restore` — exit 0; 0 warnings, 0 errors.
- Focused `PrincipalIdentificationCorpusTests` — exit 0; 7 passed.
- `git diff --check` — exit 0.

The verifier's first ignored structural harness exited 1 because it hashed compact JSON rather than the generator's canonical bytes. A verifier-only harness correction was explicitly authorized; it made no product-source change. The corrected harness exited 0 and the initial failed attempt remains recorded in `scratch/execution.md`.

Full original-input regeneration is unavailable on this host and was not claimed as PASS; the approved existing-helper evidence boundary was used.

## Handoff

The implementation is ready for one bounded commit and a draft PR targeting `dev`. No self-review, merge, proof, closeout, or deployment action has been performed.
