# Remove docs/ui-work and true up NOW.md

The operator's 2026-08-04 decision was that `docs/ui-work/` is adopted as the
specification for a whole-application UI rebuild **and then deleted**. The
rebuild shipped as releases 6 and 7 and was verified against the deployed
instance, so the folder's purpose is served. 202 files go.

## What survives, and why

`ui-standards-and-review.md` was not only a review record: the same decision
made it "the enforced presentation contract". Deleting an enforced contract
without rehoming it would lose a rule set the codebase is actively held to —
and this programme proved the rules are load-bearing, not decorative. Four
raw identifiers and forty wrong-timezone dates survived the page PRs and were
only caught because the document existed to check against.

So the durable rules move into `design/product/ui-spec.md` § Presentation
responsibilities, which already owns how requirements are presented and
operated. Seven rules, stated as contract rather than as review findings:
words never codes, no raw identifiers, one clock, sizes in MB, designed
empty/loading/failure states, absent versus disabled, and — the lesson this
programme paid for — that counts and times cannot be proved locally.

Everything else in the folder is spent: page-by-page alteration plans whose
changes shipped, defect and hidden-feature registers whose entries are
implemented, wireframes and mockups whose pages now exist, and screenshots of
an interface that no longer looks like that. All of it stays in git history.

## NOW.md

Four entries went stale as the programme closed:

- the `Doing` claim for the programme, deployment and live verification —
  complete through release 7;
- the `Doing` claim for the operations.md release records — merged as PR 351;
- the `Next` item defining the programme — done, and its three
  operator-facing decisions rehomed (the two provenance glyphs and the
  not-shipped features stay in the queue as their own concerns; the
  `claudeuiverification` credential moves to *Merged, not deployed*, where a
  live account belongs);
- the `Next` item asking for releases 4 and 5 to be recorded — superseded by
  PR 351, which recorded 4 through 7.

The **Merged, not deployed** section said "nothing here runs anywhere". That
stopped being true at release 6. It now says so plainly, and lists the two
things that are deployed as code without being active: the AI-09 surface,
which is composition-gated off, and `claudeuiverification`, which is an
enabled Administrator on the production estate with a committed password and
must be removed before go-live.

## Verification

Documentation links resolve across the remaining 111 files. No file outside
`docs/ui-work/` referenced it except `NOW.md`, and those references are gone.
