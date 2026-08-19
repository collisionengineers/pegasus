## Takeover — 2026-08-19

Agent: claude-code. Release ticket: DELIV-012. Reason: operator decision (handed
off from a prior agent mid-PR #423, review stage). Worktree
`.worktrees/intk-008`, branch `intk-008-image-initiated-lifecycle`.

On pickup: `git status` showed one uncommitted change, `CONTEXT.md` (the Case
and Image intake glossary-entry rewording for Image-initiated Case
terminology). Read it, judged it clearly part of this ticket's own
terminology work (matches the ticket's "reconcile ... CONTEXT.md" scope), and
committed it as-is before merging `origin/dev` (branch was 4 ahead / 7 behind).

## Operator ruling to implement (supersedes current PR wording)

Operator was shown this PR's `docs/operator-notes.md` rewording and answered,
verbatim, 2026-08-19:

> It could be either an image initiated case, OR it could be images being
> received for an existing case. ie if we get images, with a registration that
> doesnt match any existing case, then that creates an image initiated case.
> If they match an existing case (by VRM), then get get attached as evidence
> to that case.

Both branches must be stated explicitly in `docs/operator-notes.md` (protected
— add/clarify, never delete the existing "definitive match / linked manually
by staff" sentence), and the PRD/FRD wording must agree with the same two
branches. Implemented as a new "Two branches for a readable registration"
subsection under the existing 2026-08-19 Image-initiated Case clarification in
operator-notes.md, plus matching paragraphs in `docs/prd/pegasus-product.md`
and `docs/frd/frd-02-intake-and-source-identity.md` (the FRD paragraph also
reconciles the two-branch business framing with the actual mechanism: the
pipeline still allocates the Image Intake Reference and merges in the same
pass when there is a match, so what the operator sees is images already
attached as evidence — not a contradiction, just business outcome vs.
mechanism).
