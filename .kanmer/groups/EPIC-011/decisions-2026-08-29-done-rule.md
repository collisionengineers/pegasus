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

## D21 — A disabled control or a closed gate is never a delivered capability

**Corrected 2026-08-29 by the operator: "we aren't meant to be shipping features
disabled."** An earlier draft of D20 carved out D7 seams as satisfying rule 14. That
was wrong, and it contradicted the repository's own standing rule, which appears
twice in CLAUDE.md (lines 154 and 338):

> A closed composition or feature gate is a disabled flag, not a partially shipped
> feature. Do not ship, release, merge as delivered, claim, or document a feature
> behind one as delivered; defer it through the documented backlog/decision process
> until it has its real caller and activation evidence.

The error was conflating two different permissions:

- **D7 permits DRAWING a control disabled** so the shipped UI matches the
  operator-approved prototype. That is a rendering decision.
- It does **not** make the capability delivered. Nothing about a disabled control or
  a closed gate counts as a production caller.

### The standard

| Shape | Counts as delivered? |
| --- | --- |
| Control enabled, posts to a real handler | **Yes** |
| Control conditionally disabled with a named condition, enabled when the condition is met | **Yes** — this is legitimate state, not a disabled feature |
| Control permanently inert (a D7 integration seam) | **No** |
| Capability behind a composition/feature gate that is CLOSED in the deployed estate | **No** |
| Capability behind a gate that is OPEN in the deployed estate, evidenced in `docs/operations.md` | **Yes** |
| Registered in DI with no reachable consumer | **No** |

A ticket that names a capability falling in a **No** row cannot reach Done on that
capability, no matter how honestly the gap is disclosed or ticketed.

### What this touches on merged `dev` at `b92cb9a7`

Permanently inert controls — both on the Assessment page, both D7 seams for
uncomposed integrations (EXT-09):

- `Pages/Cases/Assessment/Index.cshtml:211` — `Glass's`
- `Pages/Cases/Assessment/Index.cshtml:214` — `Audatex`

Conditionally disabled with a named condition, therefore **legitimate**: `Import
estimate` (:204), `Send to Claude` (:226), `Generate report draft` (:251), and the
Triage `Complete` control at `Pages/Triage/Details.cshtml:198`. Each renders enabled
when its condition is met.

Feature gates in the composition roots: `Features:SendToAi`, `Features:AutomationMcp`,
`Features:LocalIntake`, `Features:LocalDocumentCustody`, plus the per-Principal EVA
submission toggles and `Features:ProviderApi` (TICK-058, not yet merged). Each must be
checked against `docs/operations.md` for its deployed state before any capability
behind it is claimed.

`docs/operations.md` records `Features:AutomationMcp` as enabled in production since
release 9 (2026-08-18), so MCP-tool callers behind that gate are real. It also records
that no Principal has either EVA toggle enabled, so the EVA automatic-submission path
is closed and nothing behind it is delivered.

## Scope of rule 14

- It binds **code** tickets. A documentation ticket (an FRD, an ADR, a design
  authority rewrite) ships no runtime capability. AUTO-009, MAIL-024, PLAT-047,
  UIIMP-006 and UIIMP-007 stay Done on that basis.
- "Every capability it names" means the capabilities in **the ticket's own What /
  Owns / Verification sections** — not every capability in the design-contract
  §-section it references. The wave plan deliberately splits contract sections across
  tickets, so measuring a ticket against a whole § would make the epic's own
  file-ownership design incoherent. (Operator decision, 2026-08-29.)

## Consequences, accepted deliberately

- Tickets already Done that carry an unreachable named capability return to
  `verifying` and wait for the consumer that wires them.
- Much of EPIC-011 completes near the end, bottom-up, rather than progressively.
- Wave 5's `dev` → `main` promotion evidence inherits this reading, which is why it
  had to be settled first.
