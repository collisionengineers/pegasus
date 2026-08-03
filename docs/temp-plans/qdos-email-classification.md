# QDOS email identification, classification, and case matching

Task line: shared Core classification foundation and the settled mailbox
taxonomy — MAIL-21 and MAIL-22. Branch `task/qdos-email-classification`.

**Scope widened by operator decisions 2026-08-03** (recorded in
[ADR-0020](../adr/0020-accepted-qdos-case-association-predicates.md) and the
re-scoped `NOW.md` claim): this branch also delivers the route/extraction
policy split from [the split proposal](split-qdos-extraction-policy.md), the
operator-accepted three-domain route set (`qdos_mail_route` v3, from the
[sender-domain inventory](qdos-email-classification-sender-domains.md)), and
the operator-accepted QDOS automatic case matching and association — the
QDOS-direct subset of MAIL-09 pulled forward. The original scope sections
below stand for the MAIL-21/22 half.

Supporting evidence:
[sender-domain inventory](qdos-email-classification-sender-domains.md), which
records the operator-accepted three-domain QDOS route set and the non-QDOS
provider domains held as inventory only.

## Scope

MAIL-21 and MAIL-22 are the only QDOS-deliverable classification allocations
at `Now` / `0.1.0-alpha.1`. Behavior is owned by
[settled mailbox taxonomy and correction](../requirements.md#settled-mailbox-taxonomy-and-correction);
the capability rows own allocation only.

In scope:

- The eight Received families and their confirmed subtypes, and the four Sent
  families, exactly as the requirements tables record them.
- Reply as mirrored context on the underlying Received or Sent category, never
  a standalone recorded type.
- `Other`, requiring both a new category name and reasoning.
- A versioned Core policy key and version, decision evidence, and an explicit
  ambiguity outcome that fails closed rather than guessing.
- Category held separate from application queue, Triage routing, and Outlook
  folder destination.
- The acceptance cohort MAIL-21 names, drawn from repository-provided sources.

Out of scope, and why:

- `EVAL-01`–`EVAL-05`, `MAIL-20`, `OPS-22` — the
  [evaluator allocation boundary](../capabilities.md) assigns the evaluator to
  a separate delivery; ADR-0013 clause 13 repeats it. No evaluator route,
  command, workspace workflow, or reviewer campaign is added here.
- `MAIL-01`–`MAIL-13`, `MAIL-23`, `UI-10`, `UI-14` — `Next` / `0.3.0`. No
  queue surface, folder recommendation, folder move, or email workspace.
- `AI-02`, `AI-03` — `Later` / `0.6.0`, activated only if rule-based behavior
  proves insufficient. No model, prompt, or classifier transport.
- `MAIL-14`, `MAIL-16` — allocated but off the `NOW.md` path.

## Constraints this task must not cross

- Multi-rule precedence, the ambiguity winner, and any numeric confidence
  score or threshold are unresolved in
  [open decisions](../open-decisions.md#mailbox-rule-activation-automatic-matching-and-confidence-display).
  This task records the ambiguity fact and fails closed; it does not invent a
  winner or a score.
- No generic rule engine and no transport-specific second classifier. The
  QDOS direct sender suffix alone classifies nothing.
- `Pegasus.Core` owns the policy. Nothing here duplicates `IMailRoutePolicy`
  or `QdosInstructionExtractionPolicy` — mail-route evaluation identifies the
  provider route, which is a different fact from message-type classification.
- No mailbox mutation: no read-state, category, flag, folder move, or delete.
- Classifier precedence must be explicit and ordered with contradiction tests,
  per [engineering](../engineering.md); terminal, transient, and unknown
  failures stay distinguishable.

## Approach

1. Read the requirements taxonomy tables and open decision 2 in full, and
   confirm every family and subtype against them before writing any type.
2. Add the Core taxonomy: family, subtype, direction, Reply context, and the
   validated `Other` name and reason. Nothing outside `Pegasus.Core` defines
   a category.
3. Add the versioned classification policy alongside the existing mail-route
   seam, reusing its policy-key/version/evidence shape rather than a second
   mechanism.
4. Add the decision record: source identity, policy key and version, outcome,
   material evidence references, ambiguity facts, actor or automated identity,
   and time. Corrections append; they never overwrite.
5. Assemble the acceptance cohort from repository-provided sources and record
   its per-family results.
6. Confirm the split stays encoded: `CoreAssembly.cs` already lists MAIL-21
   and MAIL-22 as QDOS-owned and omits the evaluator cluster.

## Verification

- `dotnet restore`, `dotnet build --configuration Release`, focused
  `dotnet test`, then a full `dotnet test` before the PR.
- Taxonomy tests asserting the exact eight Received and four Sent families and
  their confirmed subtypes, so a drift from requirements fails the build.
- Reply-mirroring tests for both directions.
- `Other` tests: a name without reasoning, and reasoning without a name, are
  both rejected.
- Ambiguity tests: two simultaneously matching families produce the recorded
  ambiguity outcome and no invented winner.
- Separation tests proving a category carries no queue, Triage, or folder
  destination.
- Correction tests proving the original decision survives and history appends.
- Cohort results reported per family with the ambiguous and unclassified
  counts stated, not rounded away.

## Coordination

`task/image-led-intake` is in flight over `INT-13/27/29/30` and `UI-07`. Both
touch `src/Pegasus.Core/Intake/`, so this task keeps to new classification
files and does not restructure shared intake contracts; if a shared edit
becomes unavoidable, merge `origin/dev` first and keep the change minimal.

## Known repository defect found while scoping

`scripts/email-eval-desktop/CategoryCatalog.cs` loads
`docs/reference/CollisionSPikeCurrenttree.txt`, which was deleted in commit
`4e084ca`; ADR-0016 still names that path. The evaluator therefore cannot load
its catalog. It belongs to the separately owned evaluator delivery, so this
task records the defect and does not fix it here.
