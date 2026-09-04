---
kind: review-attestation
pr: "667"
head_sha: "287fc2e46aeee4999c8bab18349ea44f32b40b4d"
verdict: needs-changes
reviewer: "review_deliv_047"
independent: true
plan_hash: "cf7c8963f7477253"
ticket_updated: "2026-09-04T20:33:03.557Z"
board_sha: "7afd0b3a51f53e44504a18abd487f0ddfd53a954"
expected_reviewers:
  - "review_deliv_047"
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Administrator bootstrap rejects every schema-3 release manifest."
    disposition: open
  - id: F-002
    severity: major
    summary: "Artifact validation does not reject a non-executable Linux migration bundle."
    disposition: open
  - id: F-003
    severity: minor
    summary: "The ticket does not link the new governing ADR-0037."
    disposition: open
---

# Independent review — DELIV-047

## Scope and implementation assessment

Reviewed the complete PR #667 diff at exact head `287fc2e46aeee4999c8bab18349ea44f32b40b4d` against the ticket packet, EPIC-013 context, ADR-0007, ADR-0014, proposed ADR-0037, the runbook, current-state documents, release skills, release scripts, direct consumers and tests.

The plan missed one schema-3 direct consumer implied by the ticket: `Invoke-ProductionAdministratorBootstrap.ps1`. The implementation otherwise follows the planned file set and keeps product code, database schema, infrastructure templates, CI, operator truth and production state out of scope.

The implementation missed the plan's explicit negative requirement that a non-executable `efbundle` fail artifact validation. The simplification pass did run and its reuse/removal decisions are reasonable; these findings are correctness and contract-completeness defects, not an argument for restoring the duplicated Zcode route or adding another abstraction.

## Findings and dispositions

### F-001 — major — open

`Invoke-ProductionAdministratorBootstrap.ps1` first calls `Test-AzureDeploymentPlan.ps1 -Mode Artifact`, which now accepts only schema 3, then immediately rejects unless `$manifest.schemaVersion -eq 2`. The new release route therefore makes this approved manifest-bound bootstrap impossible whenever a release legitimately needs administrator reconciliation. A local execution against the exact generated schema-3 manifest passed Artifact validation and then exited 1 with “Administrator bootstrap requires the schema-2 bootstrap-only web.zip entry.”

Update the direct consumer and its release-contract coverage to accept the one current schema-3 manifest contract without weakening manifest/hash/target checks.

### F-002 — major — open

The plan requires a non-executable Linux migration bundle to fail explicitly and the acceptance claim is an executable `efbundle`. `Test-ArtifactManifest` validates only name, size and SHA-256; Unix execute mode is neither recorded nor checked, and content hashes do not cover file permissions. The release sequence can therefore approve and upload an artifact set whose migration command cannot execute.

Add a Linux fail-closed executable-mode check at the owned artifact-validation boundary and focused evidence that removing execute permission is rejected. Preserve the existing exact name, size and hash checks.

### F-003 — minor — open

The plan explicitly says to link ADR-0037 to DELIV-047 after creating it, but the live ticket's `refs` contains only ADR-0007. Add `docs/adr/0037-linux-authorised-release-workstation.md` to the existing ticket refs during remediation so the new governing decision is traceable.

## Checks and review evidence

PASS at gather: changes, documentation, local-development-scripts, reference-data and infrastructure. Unit, SQL shards, Browser and Test UI were still running; a final pass would require all applicable checks green. The exact local artifact and canonical-test attempts are retained in the report, including the initial architecture-fixture failure. GitHub has no reviews, comments or review threads on this head.

## Residual risk

No production promotion, Azure write or database write was performed or authorized by this review. The ticket must remain on the same PR and worktree for the one allowed remediation batch. Production promotion remains separately gated by fresh authentication, exact-target write approval and immediate `MERGE AUTH GRANTED`.
