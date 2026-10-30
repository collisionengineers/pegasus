---
name: pegasus-release
description: Promote and release Pegasus through its authorised terminal route, or perform a promotion-only update without redeploying unchanged application code. Use for Pegasus production promotion, deployment, rollback, or release verification.
---

# Release Pegasus

One script owns the route: `scripts/Invoke-Release.ps1`, run twice. The
read-only run preflights, builds and prints the one approval request; the
approved run promotes, deploys, retains the evidence and prints the record.
This skill is the procedure around it: the target, the routes, the approval and
the judgement calls that stay with the agent. `azd up`, `azd deploy web` and
`azd deploy worker` are not release procedures.

Run from an authorised Windows x64 or Linux x64 PowerShell 7 terminal with that
platform's tools throughout; `az account show` and `azd auth login
--check-status` identify the operator, and the script refuses any account
outside the fixed subscription. Authentication is not approval. The Web is a
code-deployed Linux App Service Web App (ADR-0049); the Worker and the
classifier are Flex Consumption Function Apps. No image or registry is part of
the route. Release 50 replaced the Container App with the Web App; its record
is the Release 50 row in [operations history](../../../docs/operations-history.md).

The [index](../../../docs/index.md) names each policy owner: this skill owns
the release procedure, engineering owns verification policy and the rules for
writing a migration, operations owns the dated record.

## Fixed production target

| Target | Name |
| --- | --- |
| Subscription | `e6076573-23a5-46a8-acef-7e22d264e5db` |
| Tenant | `858cf5b3-aa0a-47a6-9b40-4851fd0afa94` |
| Resource group | `rg-pegasus-prod` |
| azd environment | `pegasus-prod` |
| Web App | `pegasus-prod-web-252ow37gij` |
| Web plan | `pegasus-prod-web-plan-252ow37gij` (Linux, B1) |
| Web public origin | `https://pegasus-prod-web-252ow37gij.azurewebsites.net/` |
| Worker | `pegasus-prod-worker-252ow37gij` |
| Classifier | `pegasus-prod-classifier-252ow37gij` (exists only with `PEGASUS_CLASSIFIER_ACTIVATION=approved`) |
| Key Vault | `pegasusprodkv252ow37g` |
| SQL | `pegasus-prod-sql-252ow37gij` / `pegasus` |
| App Insights | `pegasus-prod-appi-252ow37gij` |

The script carries these names; nothing is passed in. `PEGASUS_WEB_LOCATION`
is set only when an approval names a Web region other than the platform region.

## Routes

- **Promotion only.** The range `origin/main..origin/dev` touches no path under
  `src/` or `infra/`, nor `azure.yaml` or a dependency file. Documentation,
  tests, skills and release tooling alone are not a deployed release: the exact
  SHA is pushed to `main` and `dev` and nothing in Azure changes.
- **Normal.** A deployable change whose migration identity equals the live
  head. Packages deploy; provision runs only when the preflight says it is
  needed (an `infra/` or `azure.yaml` change, or an azd environment that
  differs from the one the previous release retained).
- **Normal after an additive migration.** The migration runs first, with the
  runtime grants and the head check, and the packages deploy as above.
- **Destructive.** An explicitly approved short outage (ADR-0046): the
  classifier (R232), the Worker and the Web App are stopped and read back
  before SQL; after SQL, recovery is forward only. The approval names the old
  serving Web source SHA and the window; after real release the window is
  outside typical usage, and this procedure invents no standing hours.

The preflight prints the live head, the candidate's identity and the migration
sources the range adds. The agent reads those sources and classifies: a change
that only adds (a table, a column with a default, an index) is additive; one
that drops, renames, narrows or rewrites is destructive, and the agent names
each non-additive operation, the capability it affects and the forward-only
recovery boundary in the approval request. Uncertainty is resolved before
approval, never during the route.

## 1. The read-only run

```powershell
pwsh ./scripts/Invoke-Release.ps1
```

It fetches, checks `main` is an ancestor of `dev`, lists the range and the
pull requests in it with their checks, reads the deployed state (Web state and
stack, `/diagnostics/version`, Worker and classifier states, the schedule and
Disabled settings), reads the live migration head, prints the route and
whether provision is needed, then builds the artifacts in a detached worktree
at `../pegasus-worktrees/release-<sha8>` and validates them. It writes
nothing to GitHub or Azure. `-PreflightOnly` stops before the build.

Read the report. A pull request that is not green is either waived by number
in the approval or the candidate waits. A head that differs from the candidate
identity while the range adds no migration is a question, not a route.

## 2. The one approval

One message from the operator names the release SHA, the manifest SHA-256,
the route, the targets, whether provision runs and, for a destructive route,
the old Web source SHA and the window (operator, 30 October 2026). That
message grants the promotion push and the Azure writes for exactly those
values; nothing else is a grant. The script refuses if `origin/dev` moved
after the message, and a moved head needs a fresh message. The agent never
retries a refused promotion with a rebase, a reset, an unleased push or a
different SHA.

## 3. The approved run

```powershell
pwsh ./scripts/Invoke-Release.ps1 -Execute -ReleaseNumber <n> -Route Normal `
  -ApprovedReleaseSha <sha> -ApprovedManifestSha256 <hash> [-Provision]
pwsh ./scripts/Invoke-Release.ps1 -Execute -ReleaseNumber <n> -Route Destructive `
  -ApprovedReleaseSha <sha> -ApprovedManifestSha256 <hash> -ApprovedOldWebSourceSha <sha>
pwsh ./scripts/Invoke-Release.ps1 -PromoteOnly -ApprovedReleaseSha <sha>
```

The script re-validates the worktree and the manifest against the approved
values, promotes with one leased atomic push and reads both refs back, then
runs the route to a passing full smoke.

The normal route provisions when `-Provision` is given, deploys `web.zip`
with a restart, waits for the site to read back `Running`, ready and at the
exact SHA and version, deploys `worker.zip`, deploys `classifier.zip` when the
classifier exists, and runs the full smoke. An additive migration runs before
the packages.

The destructive route reads the Web App at the approved old SHA, sets every
Disabled setting `true`, proves the disabled census, stages the approved
`worker.zip` (staging is not activation), stops the classifier, the Worker and
the Web App with a bounded read-back of each (`Stopped`, and `/health/live` not
2xx), takes one fresh read-back immediately before the gate, runs the
migration, the runtime grants and the head check, deploys `web.zip` to the
stopped site with `--restart false --track-status false`, provisions once with
Web and Worker approved, starts the Web App and waits for the exact
read-back, starts the Worker and the classifier if they are still stopped,
deploys `classifier.zip`, and runs the full smoke with the classifier
`Running`.

A failure stops the script with the exact read-back; the estate is left as it
is and reported as it is. After SQL began, recovery is forward only: the same
`-Execute` line is run again and the script resumes from where the estate is
(Web `Stopped` before or after the migration, or already serving the release).
It never contains twice, never starts a package before the new `web.zip` is on
the stopped site and never starts old bytes. An unfinished destructive run is
an outage, not a release.

## 4. Judgement calls that stay with the agent

- A release that adds or changes `WEBSITE_WARMUP_PATH` or
  `WEBSITE_WARMUP_STATUSES` passes `-WarmupSettingsChange` with `-Provision`:
  the previous package cannot answer the new warm-up, so the package deploys
  and reads back before provision, then again after it.
- A release that moves the Automation MCP server path (ADR-0087) has an
  Administrator re-add the one connector at `<origin>/mcp` and consent once;
  the classifier's `PEGASUS_MCP_URL` changes at provision, not with
  `classifier.zip`. Record the read-back URL and one tick that connects.
- A release that changes data access or telemetry snapshots Query Store
  before and after as the [runbook](../../../docs/runbook.md#statement-level-evidence-from-query-store)
  says, and the record names the files.
- The smoke proves the right bytes, configuration and intake liveness, not the
  changed journey. Run only the focused live check the released change needs
  and record its result without overclaiming.

## 5. The record

The script prints the record skeleton from what it observed. The record in
`docs/operations.md` is at most 15 lines (R459): what was released, the route,
the SHAs and hashes, the migration head, the deployment ids, the smoke time and
what is owed. Owed items become GitHub issues, not sentences carried from
record to record. `docs/current-architecture.md` changes only when source
structure changed. The exact artifacts are retained at
`artifacts/releases/release-<n>-<sha8>` (ignored), beside the previous
releases', and the worktree is removed.

Rollback is the runbook's [production recovery](../../../docs/runbook.md#production-recovery);
[troubleshooting](references/troubleshooting.md) names the symptoms seen so far.
