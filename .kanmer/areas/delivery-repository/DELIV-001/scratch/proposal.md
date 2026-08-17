# Proposal — simplicity rails for AGENTS.md (and the two downstream edits that carry the mechanics)

Evidence base: PR #385 (SIMPLI-008/009) simplification pass on 2026-08-17 — five lenses over one 29-file diff found: a result record invented to smuggle an exception past a design constraint; three parallel exception-type lists in one class; a second copy of the persisted-state string table in another layer; a fresh `TempData` convention beside the existing `?duplicate=` route-value one; three copies of one test fake and two copies of one drain loop; nine reflection-built processors where one helper would do. `docs/engineering.md` already forbids most of this ("Add an interface only for…", "stop at the third copy") — the rules exist, they are just not in the file every agent reads first, and no workflow step forces the check. Separately, SIMPLI-010's plan was 13 steps and ~2,500 words for a ~50-line change, and both its research and the SIMPLI-009 plan argued a fact about production data instead of running a five-minute read-only query — process over-engineering, which the same rails should catch.

Principle: **AGENTS.md carries the rule in one line and points at the mechanics; `docs/engineering.md` carries the mechanics.** Keep AGENTS.md short.

---

## A. AGENTS.md — new subsection under "Planning process" (insert after the "Prove the actual caller" bullet)

```markdown
## Simplicity rails

Over-engineering is a defect, not a style. Detail and mechanics are owned by
[engineering](docs/engineering.md#simplicity); these are the rules every task
carries:

- **Search before you build.** Name the existing port, helper, convention, or
  test fake you reuse, or say in the plan why none fits. A third copy of
  anything is a stop condition (see [One Core owner](docs/engineering.md#one-core-owner)).
- **One list per concept.** An exception taxonomy, a state vocabulary, a label
  table, a precedence order lives in exactly one place. A second copy in
  another layer is duplication even when it is "just strings".
- **No abstraction without a second concrete caller or an external boundary.**
  A wrapper, result record, flag, or optional parameter added so that one call
  site can carry something past a design constraint is a smell: fix the
  constraint or use the host's own mechanism.
- **The existing convention wins.** A new way to do something the codebase
  already does (a notice, a header, a refresh, a fake) needs a reason recorded
  in the ticket plan, not just a preference.
- **Facts are checked, not argued.** When a plan's premise is a fact about the
  world — production data, a caller's existence, a deployed shape — run the
  read-only check (permitted without approval) and record it, instead of
  reasoning it away in a research document.
- **Plans are proportional to their diff.** A plan longer than the change it
  describes, or with ritual steps (separate overlap checks, separate diff-review
  steps, full-suite reruns CI already performs), is itself over-engineered.
  Prefer six real steps to thirteen procedural ones.
```

## B. AGENTS.md — "Repository task workflow", step 4 (replace the current text)

```markdown
4. **Work and PR.** Implement and verify in the task worktree. Before opening
   the PR, run the simplification pass over the branch's own diff — reuse,
   simplification, efficiency, altitude (`/simplify` plus the
   `code-simplifier` agent, or equivalent independent lenses) — apply the
   behaviour-preserving fixes, and record findings and dispositions in the
   ticket's plan under a dated "Simplification pass" heading. It is part of the
   work, not a review stage. The PR targets `dev`. Keep the ticket's stage and
   checklist current as you go.
```

## C. AGENTS.md — "Repository task workflow", step 5, add one sentence

```markdown
   … whether implementation missed anything in the plan, and whether the
   simplification pass ran and its dispositions are honest (unapplied findings
   named, with a reason or a ticket).
```

## D. AGENTS.md — "Repository task workflow", step 3, add one sentence

```markdown
   … `proof.md` is required before the ticket reaches the final stage. A plan
   states, per step, what existing code it reuses; research states which of its
   premises were verified by a read-only check and which are assumed.
```

## E. docs/engineering.md — new section `## Simplicity` (the mechanics AGENTS.md points to)

```markdown
## Simplicity

The [simplicity rails](../AGENTS.md#simplicity-rails) in AGENTS.md are the
rules; these are the mechanics.

### The four lenses

Run each over the branch's own diff before the PR opens; each answers one
question and returns `file:line`, the cost, and the concrete alternative:

| Lens | Question | Typical find |
| --- | --- | --- |
| Reuse | Does the codebase already have this? | a second `IsRetryable…` unwrapper; a hand-rolled page header beside `_PageHeader`; a third test fake |
| Simplification | What does the diff add that nothing reads? | enum values with no reader; a forwarder whose only reason left; a `?? default` hiding which path names a value |
| Efficiency | What work is repeated or blocking? | two round-trips one correlated subquery would do; a fixed 2 s reload against a 60 s dispatcher |
| Altitude | Is this a special case on a shared mechanism? | a result record carrying an `Exception` to a composition root; Core matching BCL exception types instead of adapters naming faults |

Findings are recorded in the ticket plan with a disposition each: applied,
deferred-to-ticket (name it), or not-applicable (say why). Nothing evaporates.

### Fault handling shape

- Adapters name faults (`IntakeDependencyUnavailableException`); Core matches
  intake types, plus the BCL types only where no adapter sits in between.
- One classifier per decision, looking through `InnerException` — EF wraps a
  SQL deadlock in `DbUpdateException`, and a store's retry helper rethrows the
  last attempt.
- The catch-all is the shared safety policy (`IntakeExceptionPolicy.IsRecoverable`),
  never a local `is not OperationCanceledException`.
- Unexpected faults are persisted as terminal (so the operator surface is
  honest) and then rethrown, so the host logs them in full and the redelivery
  finds the work settled. Do not swallow into a bounded outcome; do not carry
  an `Exception` in a Core result.

### Test support

One fake per concept, in the shared driver, `internal`; one helper for each
composition fact tests must repeat ("Web does not register the processor" →
`IntakeWebDriver.CreateProcessor`); one drain loop. A fake or helper copied
into a second test file is the third-copy rule applied to tests.

### Plan sizing

A plan states its diff estimate first. Six real steps beat thirteen procedural
ones; a step that only re-runs what CI runs, or re-checks what `git diff` shows,
is deleted. Research separates verified facts (read-only checks, with the
command) from assumptions; an assumption a five-minute query would settle is
run, not defended.
```

## F. Skill-side follow-through (not repository text; noted so the rails are enforced where agents actually work)

- `kanmer-execute`: before "open the PR", add the simplification pass and the plan append.
- `kanmer-plan`: emit the diff estimate and the "reuses / verified premises" lines the rails require; refuse ritual steps.
- `kanmer-review`: check the plan's "Simplification pass" heading exists and its dispositions are honest.

These are Kanmer-owned files (`.claude/skills/kanmer-*`, managed by `kanmer-setup`); the repository cannot edit them durably, so AGENTS.md carries the requirement and the skills are expected to honour it.
