# Plan — bind the additive rule to cutover

Docs-only. Two files, three edits.

## Why this is a scoping change, not a deletion

The rule is correct — *after* cutover. It is being applied before the thing it
protects exists. Evidence, checked before planning:

| Check | Result |
| --- | --- |
| `grep -rn -iE 'roll.forward\|additive migration\|rollback' reference/` | zero hits — no operator source |
| present in the root commit? | no |
| when did it enter? | `25e170ff`, 2026-08-20, "Record release 14 and the previous-artifact rollback procedure" |
| has cutover happened? | no — step **7 of 8**, `docs/open-decisions.md:22-33`; we are around step 5 |
| is there business data to preserve? | no — DB rebuilt from empty twice; `EvaHandoffRevisions` empty (`docs/operations.md:410-411`) |

Note the root-commit check: unlike the EVA manifest, git *can* date this one, so
the citation is load-bearing rather than an artifact of `git log -S` returning a
file-creation commit. That trap is recorded in [[DOCS-013]].

## Steps

1. `docs/runbook.md` step 3 of the rollback procedure — qualify the additive
   requirement with **"From cutover"**, and point at the new section.
2. `docs/runbook.md` — new `### Before cutover` section after rollback step 5:
   the exemption, what replaces it (name the affected capability in the release
   record; rebuild-from-empty is the recovery route), and that it ends at
   cutover.
3. `docs/open-decisions.md` step 7 — the cutover checklist gains the
   switch-over, so the constraint turns itself on rather than being remembered.

Deliberately **not** done: no other runbook rule swept. The ticket flags that
other pre-cutover rules may be paying the same insurance; anything found gets its
own ticket rather than riding along here.

## Simplification pass

n/a — docs-only.

## Correction during implementation

My first edit to the `### Before cutover` insertion **deleted the line
"A production recovery exercise must:"**, orphaning the numbered 1-8 recovery
list and making it read as if it belonged to the new section. Caught by reading
the edited region back rather than trusting the edit. Restored.

The lesson is the read-back: an `Edit` whose `old_string` ends at a boundary line
will consume that line unless the replacement repeats it, and the failure is
silent — the tool reports success.

## Verification

- runbook states plainly when the additive rule starts binding
- the pre-cutover recovery route (rebuild, not rollback) is stated
- `open-decisions.md` step 7 carries the switch-over
- the `#before-cutover` anchor resolves from step 3's link
- the production-recovery list still has its lead-in
- [[ENG-014]]'s column drop no longer reads as a rule violation
