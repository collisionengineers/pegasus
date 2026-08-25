# Files — PR-059

*Surveyed before planning. This is an evidence-only reconciliation ticket.*

## Where the change lands

| Path | Why |
|---|---|
| Kanmer metadata for `ENG-016` | Link the actual governing documents so the feature profile's governing-doc requirement reflects reality. |
| `.kanmer/areas/engineering-assessment/ENG-016/research/research.md` via `set_ticket_doc` | Append a short final reconciliation that identifies the operative conclusion and explicitly supersedes the earlier permissive and accepted-only/custody conclusions without erasing research history. |
| `.kanmer/areas/engineering-assessment/ENG-016/files/files.md` via `set_ticket_doc` | Replace the stale strict/custody file map with an exact map of the amended PR diff and the final one-Review/one-Export behaviour. |
| `.kanmer/areas/engineering-assessment/ENG-016/plan/plan.md` via `set_ticket_doc` | Keep the intended design, add the final blocker dispositions and governing-doc section, and remove any completion claim not supported by the amended head. |
| `.kanmer/areas/engineering-assessment/ENG-016/checklist/checklist.md` via `set_ticket_doc` | Make completion boxes match what PR-055/056/057/058/060 and final verification actually prove. |
| `.kanmer/areas/engineering-assessment/ENG-016/post-implementation-report/post-implementation-report.md` via `set_ticket_doc` | Produce the final exact file/rationale inventory, governing-doc compliance, blocker dispositions, SHA, tests and CI result. Remove the false “all findings applied” claim unless it is true on the final head. |
| ENG-016 ticket body/traceability fields via Kanmer MCP | Record final commits/PR and an outcome consistent with the merged blocker work; do not claim deployment before release proof exists. |
| GitHub PR #539 description | After all blocker changes land, make its summary, test evidence and Kanmer footer agree with the final ticket record. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/frd/frd-07-eva-and-external-engineering-handoff.md` | Binding feature behaviour: Review is the sole business readiness decision; populated suggestions and the named optional/default rules are allowed; custody is not a second readiness gate; three delivery routes remain distinct. |
| `docs/frd/frd-04-parties-accounts-and-access.md` | Every export requires permanent attributed action history as part of the business transaction. |
| `docs/adr/0030-non-additive-schema-changes-before-cutover.md` | Direct pre-cutover removal is allowed, but recovery is roll-forward and the affected capability must be recorded when released. |
| `docs/adr/0021-automation-actor-direct-write-assessment-contract.md` | The accepted record still requires the deleted EVA MCP tools; PR-057 must supersede/reconcile it before ENG-016 can truthfully close. |
| `docs/capabilities.md` | Capability status must agree with the final ADR and removed tool inventory; PR-057 owns that governing-doc correction. |
| `docs/current-architecture.md` | Current as-built summary to cross-check against the final code; it is not a substitute for the exact ticket file inventory. |
| ENG-016 `scratch/review.md` | Independent review's six blocking findings and their ticket dispositions. |
| PR #539 final diff and checks | Sole authority for the final changed-file inventory, SHA and CI claims; refresh after every blocker merge. |
| PR-055, PR-056, PR-057, PR-058 and PR-060 | Owners of the actual code/ADR/migration changes. PR-059 records their final dispositions but must not duplicate their implementation. |

## Ripple effects

- ENG-016's governing-doc gate becomes satisfied only after refs are linked.
- A fresh reviewer should be able to trace each final changed file to a rationale and each governing requirement to implementation/test evidence without reading superseded conclusions as current.
- PR #539's description, ENG-016's body/checklist/report, and the final GitHub checks must name the same head and outcome.
- Release/deployment evidence remains separate and is added only after an actual release; PR-059 must not pre-write `docs/operations.md` or claim production state.

## Out of scope

- No changes to Pegasus source, tests, migrations, infrastructure, or repository governing documents.
- No implementation of PR-055, PR-056, PR-057, PR-058, or PR-060.
- No deletion or rewriting of historical research; add an explicit supersession note.
- No release, Azure write, merge, verification, proof, or closeout.
- No rollback/compatibility machinery for the unreleased product.
