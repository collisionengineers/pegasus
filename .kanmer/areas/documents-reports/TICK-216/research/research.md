# Research — wording and signature activation

## Question

May the imported renderer's wording and named signature assets become active production report content merely because they are present in the repository or hidden behind a closed gate?

## Findings

1. FRD-11 says signatures embedded in governed renderer documents are provenance-sensitive document assets, not decorative imagery. Issued reports require authorised human review and approval before issue.
2. `docs/open-decisions.md` still records report wording as unresolved: salvage-category wording, recovery/storage, final statement of truth, and named qualifications require acceptance; the prescribed default is to keep affected wording review-gated and not invent missing text or qualifications.
3. `reference/rendererref1/DESIGN_SPEC.md` is marked locked July 2026 and supplies four assessment outcomes, fixed/composed wording rules, statement-of-truth direction, fee-note behavior, and three engineer identities/signature keys. The schema restricts signatures to `andy_patterson`, `ed_mawdsley`, and `neil_oreilly`.
4. `reference/README.md` records the rendererref1 logo/signatures as supplied evidence and byte-identical to governed design assets. This proves source/provenance, not authority for a particular person’s signature to be applied to a particular report.
5. The workspace NOTICE states bundled signatures may be used only for authorised document production. Current workspace template behavior can silently omit an unknown signature key, which is incompatible with fail-closed production report generation.
6. The repository safety rail says a closed feature gate is disabled, not partially shipped, and cannot be claimed as delivered. Therefore moving unaccepted wording/signatures into production binaries behind a closed gate adds risk without satisfying the ticket.
7. The operator's current direction explicitly points to `rendererref1` as the key template information reports should use. That is strong authority to use it as the assessment-template baseline, but one operator-only confirmation remains: whether this direction accepts its exact report wording, named qualifications, and three signature assets for active production use, or only as implementation evidence pending per-engineer approval.

## Implications

- Do not ship unaccepted wording or signature assets merely behind a closed gate.
- Treat `rendererref1` as the canonical supplied baseline for mapping and tests.
- Rendering must fail closed when the selected engineer/signature/qualification is missing or unauthorised; never silently omit or substitute a signature.
- Report generation creates a draft artifact; existing human approval/issue rules remain separate.
- Operator confirmation is required before the named wording/signature set becomes active production content.
