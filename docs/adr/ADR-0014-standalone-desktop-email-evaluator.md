# ADR-0010: Standalone local desktop email evaluator

- Date: 2026-07-29
- Status: accepted

## Context

The repository's email-evaluation page is a small local-development aid, not a
production intake capability. Keeping it in Pegasus.Web couples a reviewer-only
workflow to the web composition root and makes a local folder adjudication tool
look like an application route. The retained review taxonomy is
`docs/reference/CollisionSPikeCurrenttree.txt`; Pegasus.Core remains the owner
of intake and classification policy.

## Decision

Replace the web email-evaluation surface with one independently runnable,
Windows-only WinForms executable under `scripts/email-eval-desktop/`.

The executable:

- reads only top-level `.eml` files from a reviewer-selected local folder;
- parses and displays messages without executing HTML or remote resources;
- calls the existing shared Core/infrastructure intake reader and extraction
  policy as advisory evidence;
- lets the reviewer choose a retained Received or Sent category, or create a
  validated `Other` category;
- copies reviewed files into a local `emailevallocal` tree and appends a JSONL
  adjudication log; and
- never writes to Outlook, Box, Azure, Foundry, a database, or a Pegasus
  production store.

The desktop project is deliberately omitted from `Pegasus.slnx`, is not
referenced by Pegasus.Web or Pegasus.Worker, and has no production deployment
configuration. It is an independently restored and run local development tool.
The web route, page, navigation entry, and page-only test are removed in the
same clean cutover; no redirect, alias, or dormant compatibility route remains.

No new category predicates or case policy are introduced. Reply remains context
on its underlying Received or Sent category and does not create a standalone
folder. Source files are copied, never moved or modified.

## Consequences

The local evaluator has a clear non-production boundary and cannot be mistaken
for a mailbox integration or application capability. Its standalone project and
focused tests must be verified independently. The source folder remains the
reviewer's responsibility, and local output is ignored by Git.

The deferred capabilities are explicit: mailbox selection, Outlook mutation,
Box storage, automatic filing, persistent Pegasus case linkage, production
review history, deployment, and cloud/model experimentation remain excluded.
The preserved seam is the existing `IIntakeSourceReader` and
`IInstructionExtractionPolicy` contract plus the exact source-file identity and
JSONL record fields. Activation of any deferred capability requires a new
accepted decision and a caller-backed contract; this decision does not reserve
or implement it.
