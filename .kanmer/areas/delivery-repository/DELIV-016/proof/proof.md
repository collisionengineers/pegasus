# Proof — releases 17 to 20 are live

## Release 20, verified end to end 2026-08-22

```
promotion   git push --atomic --force-with-lease=refs/heads/dev:05fe7a7f…
            main=05fe7a7f2d868d58dcd7509d0f00d4a81fb35113
            dev =05fe7a7f2d868d58dcd7509d0f00d4a81fb35113   (read back, equal)
artifacts   built from a clean tree at that exact HEAD
            digest sha256:90b58000aa337929917c92178f063b9aeefd83af459c2cbcb5b676a11b17a145
            migration head 20260822044425_GrantWorkerCaseDocuments
plan        Artifact      passed (Worker Disabled settings render 'true')
            PreProvision  passed (Worker Disabled settings render 'false')
image       oras cp → pegasusprodacr252ow37gij.azurecr.io/pegasus/web:05fe7a7f…
provision   azd provision — succeeded
worker      az functionapp deployment source config-zip — "Deployment was successful."
migration   efbundle.exe — Done.
smoke       Production smoke passed.
            Production Worker activation smoke passed (approved-live-worker).
```

Serving revision, read back from the estate:

```
pegasus-prod-web-252ow37gij--05fe7a7f2d86   2026-08-22T05:43:46Z   traffic 100
pegasus-prod-web-252ow37gij--42125b34e57a                          traffic 0
```

Migration head on the production database:

```
__EFMigrationsHistory  →  20260822044425_GrantWorkerCaseDocuments
```

And the thing the release existed to change — the Worker's permissions on the
three document tables, read from `sys.database_permissions` **after** the
bundle applied:

```
pegasus_worker_runtime_role  CaseDocuments        SELECT  GRANT   INSERT  GRANT   DELETE  DENY
pegasus_worker_runtime_role  DocumentOccurrences  SELECT  GRANT   INSERT  GRANT   DELETE  DENY
pegasus_worker_runtime_role  DocumentVersions     SELECT  GRANT   INSERT  GRANT   UPDATE  GRANT   DELETE  DENY
```

Before the release those three tables carried the DELETE deny and nothing else.
That is [[DOCS-008]] fixed at its root.

## Releases 17, 18 and 19

All three promoted the same way and smoked green at their own SHAs, recorded in
`docs/operations.md`: 17 `71911734` (`sha256:f625c947…`), 18 `1f3be493`
(`sha256:818fe360…`), 19 `42125b34` (`sha256:08aeeaed…`). None carried a
migration; release 20 is the first schema change since release 16.

## Git end state

```
worktrees  pegasus (main @ 05fe7a7f), .worktrees/kanmer (kanmer-board)
local      dev, kanmer-board, main
remote     origin/dev, origin/kanmer-board, origin/main
open PRs   none
```

`origin/dev` is one docs commit ahead of `origin/main` — PR #511, the
current-state refresh for these releases. It rides the next release, which is
how release 18's own docs were carried.

## What this release does **not** prove

The seven tickets waiting on custody — [[DOCS-006]], [[DOCS-007]],
[[CASE-013]], [[CASE-014]], [[CASE-017]], [[INTK-029]], [[INTK-030]] — are
deployed but unexercised. No case has been created since the grant landed, and
QDOS26009 and QDOS26010 both still carry their failed custody records because a
terminal failure is not retried automatically. One instruction through the
pipeline, or an operator pressing **Retry custody** on either existing case,
closes all seven at once.

Telemetry remains capped at 0.1 GB a day ([[PLAT-034]]), which is a billing
decision and not mine to take.
