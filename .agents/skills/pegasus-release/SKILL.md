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
| Web plan | `pegasus-prod-web-plan-252ow37gij` (Linux, B2, R711) |
| Web public origin | `https://pegasus-prod-web-252ow37gij.azurewebsites.net/` |
| Worker | `pegasus-prod-worker-252ow37gij` |
| Classifier | `pegasus-prod-classifier-252ow37gij` (exists only with `PEGASUS_CLASSIFIER_ACTIVATION=approved`) |
| Key Vault | `pegasusprodkv252ow37g` |
| SQL | `pegasus-prod-sql-252ow37gij` / `pegasus` |
| App Insights | `pegasus-prod-appi-252ow37gij` |
| Test instance | `https://cemattypc-1.tail432b53.ts.net/` (ADR-0088; serves `dev` by default, or a branch the operator named, R695; read only here, and it must be serving the candidate, R684) |

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
  differs from the one the previous release retained). It runs when its
  candidate has passed on the test instance, at any hour: the restart is the
  outage, and there is no window and no staff notice (R690).
- **Normal after an additive migration.** The migration runs first, with the
  runtime grants and the head check, and the packages deploy as above.
- **Destructive.** An explicitly approved short outage (ADR-0046): the
  classifier (R232), the Worker and the Web App are stopped and read back
  before SQL; the script records the database's `earliestRestoreDate` and a
  UTC restore point before SQL; after SQL, recovery is forward only. The
  approval names the old serving Web source SHA and the window, which is
  outside typical usage (R605); this procedure invents no standing hours.

The preflight prints the live head, the candidate's identity and the migration
sources the range adds. CI's migration class check
(`scripts/Test-MigrationClass.ps1`) is the first reader of each one's class,
and its pull request body's `## Migration` section records it (R760); the
agent still confirms it here. The agent reads those sources and classifies them by
the rule in [engineering](../../../docs/engineering.md#migrations) (R605):
additive runs on the normal route; non-additive runs only on the destructive
route, and the agent names each non-additive operation, the capability it
affects and the forward-only recovery boundary in the approval request.
Uncertainty is resolved before approval, never during the route.

## Version

Releases are Semantic Versions from 1.0.0 (R686): `MAJOR.MINOR.PATCH` with no
suffix, because the running build reports `<version>+<sha>` and the `+`
separates the two. The live version is the highest `v*` tag on `main`; the
released SHA gets its annotated tag `v<version>` in the promotion push after
the smoke (R688). Nothing in source carries a release version:
`Directory.Build.props` stamps `0.0.0-dev` into every build that is not a
release, the test instance included. The read-only run proposes the next
version (R687): PATCH when every pull request in the range closes only
`type:bug` issues, MAJOR when the route is destructive, MINOR otherwise. The
approval names the version it grants; the script refuses one that is not one
step above the live version, or not the one the manifest was built with. A
fix is a PATCH of `dev`'s tip; there is no hotfix branch from `main` (R689).
The version replaces the release counter: the record is "Release 1.0.0" and
the retained folder `release-<version>-<sha8>`.

## 1. The read-only run

```powershell
pwsh ./scripts/Invoke-Release.ps1
```

It fetches, checks `main` is an ancestor of `dev`, lists the range, the
candidate's push-to-`dev` CI run (R683) and the pull requests in it with their
checks, reads the deployed state (Web state and stack, `/diagnostics/version`,
Worker and classifier states, the schedule and Disabled settings), reads the
live migration head, prints the route and whether provision is needed, names
any Web App setting the candidate's template drops or changes that the live
package cannot start under (R1248), refuses
a deployable candidate whose azd environment lacks a key the provision needs
(every key `infra/main.parameters.json` references without a default and every
key PreProvision requires; the runbook's deployment inputs table names them)
or whose `PEGASUS_CLASSIFIER_ACTIVATION` would delete the existing classifier,
so the approval is given on a complete environment (#1931), reads
the test instance's `/diagnostics/version` over the tailnet and refuses a
deployable candidate the instance is not serving (R684), prints the live
version and the proposed next one (R687), then builds the artifacts at that
version in a detached worktree at `../pegasus-worktrees/release-<sha8>` and
validates them. It writes nothing to GitHub or Azure. `-PreflightOnly` stops
before the build; `-Version <v>` builds another version than the proposal,
the MAJOR when the agent classifies the range's migration as non-additive.

Read the report. A candidate whose push-to-`dev` run or any pull request in
the range is not green waits; nothing is waived, and `-Execute` refuses it
(R691). The `--- Protection ---` section reads the repository's rulesets back
([runbook](../../../docs/runbook.md#branch-and-tag-protection)); `dev` and
`main` each carry an active one, and a missing one is a question before the
approval. A head that differs from the candidate identity while the range adds
no migration is a question, not a route.

## 2. The one approval

One message from the operator names the release SHA, the manifest SHA-256,
the version (R687), the route, the targets, whether provision runs and, for a
destructive route, the old Web source SHA and the window (operator, 30
October 2026). That message grants the Azure writes for exactly those values
and the promotion and tag of `main` that follow the smoke (R682, R688);
nothing else is a grant. The script refuses if `origin/dev` moved
after the message, and a moved head needs a fresh message. The agent never
retries a refused promotion with a rebase, a reset, an unleased push or a
different SHA.

## 3. The approved run

```powershell
pwsh ./scripts/Invoke-Release.ps1 -Execute -Version <v> -Route Normal `
  -ApprovedReleaseSha <sha> -ApprovedManifestSha256 <hash> [-Provision]
pwsh ./scripts/Invoke-Release.ps1 -Execute -Version <v> -Route Destructive `
  -ApprovedReleaseSha <sha> -ApprovedManifestSha256 <hash> -ApprovedOldWebSourceSha <sha>
pwsh ./scripts/Invoke-Release.ps1 -PromoteOnly -ApprovedReleaseSha <sha>
```

The script checks the version is one step above the live tag and the one
the manifest was built with, refreshes the worktree's copy of the azd
environment from the primary checkout so a key repaired with `azd env set`
between the two runs is what it reads (#1930), re-validates the worktree and
the manifest against the approved values, reads `dev` and the test instance
again, requires the same complete environment the preflight did, runs
the route to a passing full smoke, then promotes `main` to the released SHA
and tags it `v<version>` in one leased push and reads both back (R682,
R688): `main` records what is live, so a failed release leaves it at the SHA
still serving, with nothing in git to reset. `-PromoteOnly` keeps the atomic
push of the SHA to `main` and `dev`, with no tag: nothing was deployed.

The normal route provisions when `-Provision` is given, deploys `web.zip`
with a restart, waits for the site to read back `Running`, ready and at the
exact SHA and version, deploys `worker.zip`, deploys `classifier.zip` when the
classifier exists, and runs the full smoke. An additive migration runs before
the packages.

One provisioned release takes another order (R1248, #2165). The script
compares the live SHA (`main`) with the candidate: a key the Web host requires
at start-up (the Production loop in `src/Pegasus.Web/Program.cs`) whose
setting the `webApp` resource in `infra/modules/platform.bicep` no longer
sets, or a `WEBSITE_WARMUP_PATH` or `WEBSITE_WARMUP_STATUSES` the candidate
adds or changes. Provisioning first would restart the live package onto
settings it stops on, as on Release 5.2.0, and deploying first would start the
new package without its own. So the read-only run and the approval request
name those settings, and the approved run stops the Web App and reads it back
`Stopped` and unserved, deploys `web.zip` to the stopped site with `--restart
false --track-status false`, provisions, starts the Web App and waits for the
exact read-back. The Web App is down from the stop to that read-back, which
the approval grants by naming the route with those settings listed. A run that
failed after the stop is resumed with the same line: the site is not stopped
again. Every other normal release keeps the first order.

The destructive route reads the Web App at the approved old SHA, sets every
Disabled setting `true`, proves the disabled census, stages the approved
`worker.zip` (staging is not activation), stops the classifier, the Worker and
the Web App with a bounded read-back of each (`Stopped`, and `/health/live` not
2xx), takes one fresh read-back immediately before the gate, records the
restore point, runs the migration, the runtime grants and the head check,
deploys `web.zip` to the
stopped site with `--restart false --track-status false`, provisions once with
Web and Worker approved and `--no-state` (the Disabled census was written
outside Bicep, so azd's stored state cannot see that it differs from the
template; #1857), starts the Web App and waits for the exact
read-back, starts the Worker and the classifier if they are still stopped,
deploys `classifier.zip`, and runs the full smoke with the classifier
`Running`.

A failure stops the script with the exact read-back; the estate is left as it
is and reported as it is. After SQL began, recovery is forward only: the same
`-Execute` line is run again and the script resumes from where the estate is
(Web `Stopped` before or after the migration, or already serving the release).
It never contains twice, never starts a package before the new `web.zip` is on
the stopped site and never starts old bytes. The rows of the record are kept
in `release-record.json` beside the manifest after each one is observed, so
the resumed run prints the whole release's record, the first run's
containment, restore point and migration included (#1945). An unfinished
destructive run is an outage, not a release. The recorded restore point is the input to the
runbook's [point-in-time restore](../../../docs/runbook.md#point-in-time-restore-commands)
when data must be recovered.

## 4. Judgement calls that stay with the agent

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

The script prints the record skeleton from what it observed, across every run
of the approved line. The record in
`docs/operations.md` is at most 15 lines (R459): what was released, the route,
the SHAs and hashes, the migration head, the restore point when a migration
ran, the deployment ids (the Web OneDeploy id, and the Worker and classifier
`config-zip` ids the CLI reports), the smoke time and what is owed. Owed items become GitHub issues, not sentences carried from
record to record. `docs/current-architecture.md` changes only when source
structure changed. The exact artifacts are retained at
`artifacts/releases/release-<version>-<sha8>` (ignored), beside the previous
releases', and the worktree is removed. The folder is a convenience: the tag
`v<version>` is the recoverable source, and a rollback without the folder
rebuilds it from the tag (R692).

Rollback is the runbook's [production recovery](../../../docs/runbook.md#production-recovery);
[troubleshooting](references/troubleshooting.md) names the symptoms seen so far.
