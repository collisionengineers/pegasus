# ADR-0023: Restructure repository documentation and reference evidence

- Date: 2026-08-10
- Status: accepted

## Context

Repository guidance had grown across `docs/operations.md`, `design/README.md`,
`design/product/`, and repeated rule summaries. Supplied evidence lived below
`docs/reference/`, so the prose documentation tree also held spreadsheets,
PDFs, images, and vendor material. Procedures and dated current-state evidence
shared one operations file, and design prose had three owners.

The repository invariant requires an accepted ADR before adding a top-level
directory. The existing `docs/` boundary cannot remain prose-only while it
also owns supplied binary evidence, and a path-only link convention cannot
separate evidence authority from documentation authority. A top-level
`reference/` boundary makes that distinction structural while preserving the
evidence bytes and history.

## Decision

1. `docs/` is the canonical prose-documentation tree. Supplied evidence moves
   from `docs/reference/` to the new top-level `reference/` directory, with
   attributes and all current path references updated in the same change.
   Moving evidence does not change its authority or make it product input.
2. `docs/operations.md` owns current production, release, evidence-profile,
   monitoring, and recovery state. `docs/runbook.md` owns executable setup,
   local development, database, testing, release, approval, monitoring,
   recovery, and maintenance procedures.
3. `docs/design.md` is the sole Pegasus UI and product-design prose owner. It
   absorbs the still-applicable content of `design/README.md` and
   `design/product/`. The top-level `design/` tree retains assets only:
   `brand/`, `references/mockups/`, and `assets/report-renderer/`.
4. Repository rules have one owner. `docs/engineering.md` owns task workflow,
   claims, reviews, Git safety, Markdown conventions, merge authority, and the
   evidence-tier ladder. `NOW.md` owns tracker and staleness rules.
   `docs/index.md` owns the new-Markdown-file rule. This index owns ADR
   immutability and the blanket evidence qualification. Product invariants
   remain in `docs/requirements.md`; `AGENTS.md` retains only its sanctioned
   constitutional summary. CI behavior stays executable and explained in its
   workflow comments, with other documents linking to it.
5. `CLAUDE.md` remains a symbolic link to `AGENTS.md`. `.codex/`, imported
   workspace internals, protected AI skill packages, `corpus/`, and
   report-renderer staging assets are unchanged by this decision.

## Consequences

Links and source-path literals must use the new owners. Evidence history is
preserved as Git renames and binary attributes continue to protect supplied
assets. A later content pass may refine wording inside the new owners but must
not recreate parallel authorities. This structural decision changes no Core
policy, application caller, deployment, external service, or production data.
