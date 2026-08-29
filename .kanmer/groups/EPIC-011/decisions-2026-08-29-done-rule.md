# EPIC-011 — what Done requires (2026-08-29)

## D20 — Strict rule 14: every named capability needs a production caller

AGENTS.md rule 14 reads: *"Done means wired. New code needs a named production
caller; registered-but-unreachable or test-only code is not done."*

The first proof round applied this unevenly. PLAT-048 was held because one named
half of its ticket (the Engineer activity report) has no caller, while AUTO-011,
ENG-026 and PLAT-029 were moved to Done with individual ports unreachable, on the
implicit rule "headline capability reachable, deferred seams disclosed and ticketed".

**Operator decision: the strict reading binds.** A ticket reaches Done only when
**every capability it names** has a real production caller — a route, a rendered
control posting to a handler, or a registration plus a named consumer that is itself
reachable. A registered-but-unreachable port does not qualify, however honestly it is
disclosed and ticketed.

### Consequences, accepted deliberately

- Tickets already moved to Done that carry an unreachable named capability come back
  to `verifying` and wait for the consumer that wires them. The consumer is, in every
  known case, a later ticket inside this same epic.
- Much of EPIC-011 will therefore complete near the end, bottom-up, rather than
  progressively. That is the accepted cost of the stricter evidence bar.
- Wave 5's `dev` → `main` promotion evidence inherits this reading, which is the
  reason it had to be settled before then.

### Scope of the rule

- It binds **code** tickets. A documentation ticket (an FRD, an ADR, a design
  authority rewrite) ships no runtime capability, so rule 14 does not bite: its
  "caller" is the document being cited by what depends on it. AUTO-009, MAIL-024,
  PLAT-047, UIIMP-006 and UIIMP-007 stay Done on that basis.
- A **disabled seam** that the design contract explicitly draws as disabled (D7 —
  Experian, Glass's, Audatex, Cazana) is not an unreachable capability. It is a
  drawn, ticketed, deliberately-inert integration point, and it is permitted.
- A capability behind a **composition or feature gate** counts as reachable only if
  the gate is actually open in the deployed estate. `docs/operations.md` records
  `Features:AutomationMcp` as enabled in production since release 9 (2026-08-18), so
  MCP-tool callers count. A closed gate does not — per CLAUDE.md, a closed gate is a
  disabled flag, not a partially shipped feature.

### Known reversals to apply

From the first round's own evidence:

| Ticket | Unreachable named capability |
| --- | --- |
| AUTO-011 | `ICancelAiJob` (no caller), `ListForSubjectAsync` (no caller), `IConfirmAiJob` (not operator-reachable) |
| ENG-026 | `ISetCurrentEstimate`, `IDuplicateEstimate`, `IDiscardEstimate`, `JsonEstimateParser` |
| PLAT-029 | the workspace-tab record mechanism — `WorkspaceRecord` has a consumer at `_Layout.cshtml:43` and **no producer anywhere** |

Every other Done code ticket is re-audited against this rule before it keeps its
status.
