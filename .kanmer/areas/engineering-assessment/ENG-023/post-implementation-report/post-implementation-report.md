# Post-implementation report — ENG-023

PR: https://github.com/collisionengineers/pegasus/pull/580
Branch: `task/eng-023-release-36-docs`

## What shipped

Docs only, in the same task as the release 36 deploy.

- `docs/operations.md` — the release 36 row in the release table, and a
  "what it proved beyond smoke" entry recording the migrations, the four
  `EvaSubmissions` grants read back from production SQL, the eighteen resolved
  Worker Key Vault references, the single serving revision on the manifest
  digest, and the smoke result.
- `docs/capabilities.md` — the EXT-04 row said deployment was pending. It is
  deployed, against the EVA **test** credentials, with both Principal toggles
  off for every Principal.

`docs/current-architecture.md` needed no change: TICK-077 already wrote the
as-built shape and the review corrected it in the same PR, and deploying it did
not alter that shape.

## Deviations from the plan

None in content. The ticket asked for three specific things to be recorded that
a reader would otherwise be misled by, and all three are in.

## What the entry records that a release row normally does not

1. The two Key Vault secrets and the two secret-scoped Web `Key Vault Secrets
   User` grants were created by hand and are permanent estate state. TICK-077
   wired the secrets into the container app and shipped no grants; the first
   provision failed with the Web identity unable to fetch either. No CI gate can
   catch this, because `Test-AzureDeploymentPlan` prohibits a vault-wide grant
   in bicep and secret-scoped grants are therefore made outside IaC.
2. The provision before that failed on the BOM fixed by [[ENG-022]]. Neither
   failure deployed anything.
3. A deviation found rather than introduced: the Worker identity holds a
   *vault-scope* `Key Vault Secrets User` grant alongside its six secret-scoped
   ones, contradicting the secret-level-only posture the same document describes
   under the 2026-08-03 vault consolidation. Recorded as current state, not
   silently corrected — narrowing a live identity's access is not a docs task.

## Verification

`changes`, `documentation`, `local-development-scripts` and `reference-data`
green; the code and infrastructure suites correctly skipped, no build-relevant
path having changed.

## Left for the reviewer

The Worker's vault-scope grant is now written down but not dispositioned. It
wants its own ticket, or an explicit decision that vault scope is acceptable for
that identity.
