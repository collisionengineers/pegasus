# Minimal desktop email evaluator

- Date: 2026-07-29
- Decision: [ADR-0010: Standalone local desktop email evaluator](../decisions/ADR-0010-standalone-desktop-email-evaluator.md)
- Status: implemented; exact-head review pending

## Accepted scope

Replace the web-only email-evaluation page with an independently runnable
Windows WinForms tool in `scripts/email-eval-desktop/`. The tool selects a local
folder, reads the first unreviewed top-level `.eml` in deterministic filename
order, displays safe decoded message content, shows an advisory result from the
existing intake reader and extraction policy, and lets a reviewer file a copy
under the retained Received/Sent taxonomy or a validated `Other` category.

Successful filings append one UTF-8 JSONL record to
`emailevallocal/evaluation-log.jsonl`. The source remains unchanged. Malformed
logs, parse failures, invalid custom names, collisions, and copy/log failures
are visible and fail closed for the affected operation.

## Canonical owners and affected artifacts

- `docs/reference/CollisionSPikeCurrenttree.txt` remains the taxonomy owner.
- `Pegasus.Core` remains the business-policy owner.
- `Pegasus.Infrastructure` remains the MIME/source-reader adapter owner.
- `scripts/email-eval-desktop/` owns only local queue, safe display, reviewer
  choice, output, and JSONL orchestration.
- The old Razor page, page model, and focused web test are removed after the
  desktop smoke and focused tests pass.
- `.gitignore` protects every `emailevallocal` output tree.

## Deferred-capability impact

Deferred: Outlook or other mailbox connectors, Box writes, cloud/deployment
activation, database persistence, automatic classification/final filing,
Pegasus case/reference allocation, production audit history, telemetry, and
AI Centre/model integration. The preserved seams are the Core intake reader and
extraction-policy contracts, source path/name identity, the parsed taxonomy,
and the explicit JSONL schema. None of those seams activates a deferred
capability or authorizes a production caller.

## Conflicts and irreversible choices

The old web route is removed rather than redirected or retained. The desktop
project is outside `Pegasus.slnx` by design. Filing copies rather than moves
source files, and destination collisions never overwrite. These choices avoid
mutating repository-provided or immutable local source material and make
restart filtering evidence-based through the append-only log.

## Evidence and outcome

Focused proof completed:

- `dotnet restore scripts/email-eval-desktop/Pegasus.EmailEvaluation.Desktop.csproj`
- `dotnet build scripts/email-eval-desktop/Pegasus.EmailEvaluation.Desktop.csproj --configuration Release --no-restore`
- `dotnet test scripts/email-eval-desktop/tests/Pegasus.EmailEvaluation.Desktop.Tests.csproj --configuration Release` — 8 passed
- `dotnet restore` and `dotnet build --configuration Release` — passed
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release` — 87 passed, 11 skipped
- The WinForms executable launched successfully as a local smoke process and was stopped without cloud, mailbox, or Box access.
- Focused tests cover deterministic queueing, genuine fixture parsing, twelve taxonomy folders, advisory `No category` behavior, standard/Other filing, source preservation, JSONL escaping, collisions, malformed logs, and copy/log rollback.

Active source search leaves old route/page references only in intentional historical
or requirements evidence; the solution contains no desktop project and no
application project references it. This record does not claim deployment, cloud
operation, mailbox access, Box access, or operator acceptance.
