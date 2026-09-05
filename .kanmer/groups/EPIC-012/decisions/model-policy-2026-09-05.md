# Model policy amendment — Codex exhausted (operator, 2026-09-05)

At 10:38Z on 2026-09-05 both Codex models (`gpt-5.6-sol`, `gpt-5.6-terra`)
returned "You've hit your usage limit … try again at Sep 8th, 2026 10:16 AM"
for the account this programme uses. Two lanes had already hit it (ENG-034's
round-2 fix and CASE-045's implementation were finished by their Sonnet
wrappers directly).

Operator decision (2026-09-05): **switch the remainder of EPIC-012 to Claude**
— Claude Sonnet implements and applies fixes (effort high), the wrapper does
the simplification pass itself, Claude Opus performs the independent review
read, dispositions, gates on CI and merges (effort high). Critics, proof
audits and adversarial refuters likewise run on Opus/Sonnet with distinct
lenses. No Fable agent runs inside any workflow. The §Model allocation text
in `context.md` is superseded by this note until Codex credit returns; the
run record ledger names the model per lane.

Consequence recorded honestly: verification is single-family until Codex is
back. The review remains independent of the implementer (different model,
separate agent, whole-diff read at high effort), but shares blind spots with
it. Lanes merged under this policy: from ENG-034 (round-3 fix) onward.
