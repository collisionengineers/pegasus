# Checklist — PLAT-065

- [x] Add the existing-template FormRecognizer/S0 account, disabled local auth, custom subdomain, one account-scoped Worker role and Worker endpoint/output; extend the existing template contract test.
- [ ] Root verifies focused Bicep/template/composition checks, records all attempts and reviews honest source/current-state documentation; author stops at PR/Review.
- [ ] Root binds the integrated release SHA/manifest, exact target/cost/identity and fresh inventory to a reviewed preview before provisioning through the existing release route.
- [ ] Prove actual Worker retained-source canary and replay, readable PDF without OCR, Web identity denial, and refresh operations/current-architecture with exact live evidence and limitations.

## Progress notes

The seven-file implementation is frozen for root verification on branch
`PLAT-065-document-intelligence`, worktree `.worktrees/plat-065`, base
`d367219669ad26d5f2b727bd10b582330febc906` (merged TICK-041).
`git diff --check` passed, exit 0. No author build/test, provider analysis,
provisioning or deployment. Root alone runs the focused commands in the plan.

Canonical docs distinguish implemented declaration from not-yet-activated
production. ADR-0040 is present in the exact base and linked by source docs;
Kanmer `link_doc` refused the additional ref because its configured shared
repoRoot lacks this newly merged file. Existing FRD refs remain valid. Do not
copy files into the user-owned checkout or alter MCP configuration to bypass it.

EPIC-011/EPIC-014 membership and source qualification/import ownership in
TICK-041/TICK-085 remain unchanged. No repeated stress/full-suite verification.

Root attempt 1: installed Bicep compilation PASS; Local deployment-plan FAIL
exit 1 at the obsolete blanket deferred-service ban. Build/tests did not start.
The authorized correction now permits only the declared FormRecognizer/S0
keyless account and rejects other Cognitive Services plus the existing deferred
services. The existing isolated Local validator test now has seven negative
cases, including its original rogue Worker-setting assertion. Correction is
frozen for root; no author build/test and no cloud write.
