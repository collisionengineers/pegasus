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

## Review verdict: FAIL — rewritten 2026-08-24

The independent review failed this and was right on every count. Both blocking
findings were in the region I had flagged for a second look, which is its own
lesson: flagging a risk is not the same as checking it.

### B1 — the new heading reparented three blocks

`### Before cutover` was a **level-3 sibling** of `### Production recovery`,
inserted mid-section. It therefore *closed* Production recovery, silently moving
under my new section: the "A production recovery exercise must:" 1-8 contract,
the down-migration prohibition, and the whole `#### Point-in-time restore
commands` block — whose opening line reads *"These commands implement contract
steps 2-7 above"*, an "above" that now crossed a section boundary.

Worse than the deleted-line error I had already caught and congratulated myself
for catching. My verification line was *"the production-recovery list still has
its lead-in"* — I checked **re-attachment** and never checked **placement**. The
gap was exactly where the check stopped.

**Fixed by removing the heading entirely.** The exemption is now folded into
rollback step 3, where it is read. No new heading, so nothing can be reparented;
the anchor from `open-decisions.md` points at
`#previous-artifact-rollback-web-and-worker`, an existing target.

### B2 — the same rule, unqualified, 30 lines above

`docs/runbook.md:1120`, in the `### Production recovery` intro:

> Database migrations are explicit and must remain compatible with the supported
> prior application artifact or have an accepted recovery strategy.

Untouched by my change. So the runbook contradicted itself inside one section,
and — the part that matters — **[[ENG-014]] still read as violating the runbook,
meaning this ticket did not deliver the unblock it exists for.** My PR body's
"no other runbook rule was swept" did not cover a duplicate of *the same rule*
in *the same section*. Now qualified.

### The counter-argument, which was the most valuable finding

**"There is no data to preserve" was false, and my recovery-route sentence was
dangerous.** I wrote: *"treat rebuild-from-empty rather than artifact rollback as
the recovery route."*

Both operator-approved wipes deliberately **preserved** 31 tables — identity,
automation-client, mailbox configuration, principal, provider reference,
workflow, audit, schema — *and the three sequence tables, "so the next case is
QDOS26013 and no reference is reused"* (`operations.md:375-376`). Reference
non-reuse is a **product invariant** in CLAUDE.md, not a convenience. Mailbox
poll cursors were preserved too; clearing them re-ingests every message still in
the mailbox.

Taken literally, my instruction would have burned the sequence tables and
re-issued a used reference — telling a future engineer to do the thing the
operator twice declined to do. The text now points at the operator-approved
selective wipe and names what it preserves, "never an unqualified rebuild".

### Evidence corrections

- **"Four days old" is true of the sentence, false of the requirement.**
  `docs/engineering.md:86` (test tier 11) has required *"previous-artifact
  compatibility"* since **2026-08-03** — 17 days earlier. The rule has an older
  second home I never looked for.
- **The root-commit check was worthless here.** This history has **13 root
  commits** (grafted/imported), so "not in the root commit" proves nothing. The
  pickaxe across `--all` is what actually carries the claim, and it does hold.
- **`reference/` is not the operator corpus.** It is supplied external evidence;
  CLAUDE.md names `docs/operator-notes.md` as the binding business truth. The
  reviewer grepped that separately and found no rollback or data-preservation
  requirement either — so the conclusion stands, but my cited evidence did not
  support it on its own.
- **"Around step 5" is inferred, not documented**, and sits in mild tension with
  `open-decisions.md:20`. The load-bearing half — cutover has not happened — is
  verified.

### Left for a follow-up, named rather than dropped

- `docs/engineering.md:86` still lists previous-artifact compatibility as
  required test coverage with no pre-cutover signal (review F3, non-blocking).
- The switch-over anchor is `open-decisions.md` step 7 — a prose sentence, not a
  checklist anyone ticks. The reviewer's better suggestion: a Kanmer ticket
  blocked on cutover, since the board is the only mechanism here with a queue
  behind it. Worth filing.
