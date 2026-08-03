# Plan: release-3 record (task/release-3-record)

Post-deploy record for release 3 (2026-08-03, revision `ef987ac4…`), plus the
two defects the deployment surfaced.

1. operations.md deployed evidence: release 3 as the current release (digest,
   healthy revision, migration-before-activation, nine Worker functions,
   smoke result including the live-verified https sign-in redirect); demote
   releases 2 and 1 to history; move the "from the composition-fix
   deployment" phrasings to present tense; record the applied Key Vault
   grants with the healthy-start resolution proof.
2. NOW.md: reduce the shipped release item to the remaining vault
   consolidation; release this task's claim line in-PR.
3. scripts/Invoke-ProductionSmoke.ps1: disable redirect following so the
   anonymous-denial check sees the raw 302 (with auto-redirect it mistakes
   the login page's 200 for anonymous access; it only passed previously
   because the pre-release-3 http:// redirect could not be followed), and
   assert the sign-in redirect is https.
4. .azure/deployment-plan.md: replace the stale RPO/RTO second-deployment
   gate with the 2026-08-03 decision (OPS-09 recovery proof deferred, gates
   no release) — it contradicted operations.md and was already bypassed by
   release 2.

Verification: fixed smoke run against production returned "Production smoke
passed."; doc link check passes; independent two-question review on the PR.
