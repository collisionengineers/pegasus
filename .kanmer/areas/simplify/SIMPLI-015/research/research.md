# Research — SIMPLI-015 integration direction preserved from KANMER-002

## Operator decision

On 2026-08-14 the operator chose integration of the report renderer and document extractor into Pegasus, not extraction into standalone repositories/packages. This supersedes the contrary direction in SIMPLI-013 and SIMPLI-014.

## Preserved content from the retiring renderer plan set

The deleted `docs/temp-plans/report-renderer-integration*.md` set explored and resolved these implementation seams:

- keep Pegasus.Core as business-policy owner; workspace code cannot become an application caller merely by being present;
- integration requires an accepted thin ADR, explicit project/solution references, composition-root registration, and caller-backed tests;
- embed or explicitly copy governed templates, report CSS, logo and signatures from the canonical design tree; pin logical resource names and verify the complete resource set to prevent silent drift;
- retain the current renderer workspace ADRs as workspace history until the integration ADR deliberately supersedes the relevant mechanism; do not rewrite historical decisions mechanically;
- consolidate renderer MCP/tool surfaces rather than shipping duplicate hosts; production execution location, distribution boundary and authorization remain decisions to resolve in this ticket;
- migrate current architecture, operations, engineering/runbook and workspace documentation only when the real caller lands;
- preserve the 2026-08-03 resolution that the GUI host was removed, .NET 10/runtime uplift was completed, and unaccepted wording/assets remain fail-closed;
- renderer capability and decision coverage remains discoverable through TICK-203–TICK-216, all related to this ticket via the consolidation owner established by KANMER-001.

## Still-live work for SIMPLI-015

1. Write the accepted integration ADR and update the owning FRD for behavioural consequences.
2. Re-scope/archive SIMPLI-013 and SIMPLI-014 with explicit migration notes.
3. Select the application seam, project dependency direction, DI registration and production execution boundary.
4. Define MCP/tool consolidation and authorization.
5. Implement caller-backed build/test/runtime proof before adding either workspace to Pegasus.slnx or deployment.
6. Update current-state docs only after implementation/deployment evidence exists.

## Provenance

This summary was created by KANMER-002 immediately before retiring the temporary renderer plan files. Git history remains the complete verbatim record; this document is the actionable durable handoff.
