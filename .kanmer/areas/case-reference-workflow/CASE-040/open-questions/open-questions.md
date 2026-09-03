# Open questions — CASE-040

- [x] What durable rule identifies the fallback A Patterson account for the
  default Sign-off Engineer? Operator answer 2026-09-03: an
  Administrator-maintained "Default sign-off Engineer" designation on one
  flagged account, exposed on PLAT-068's `SignOffEngineerProfile`; CASE-040's
  Core resolver reads it. No username or account ID is hard-coded. The
  plan's default stands.

- [x] Operator: does the first Send to EVA move the case from `Review` to
  `With Engineer`? **Operator answer 2026-09-03: yes — and FRD-07 is wrong
  and must be changed.** Send to EVA by either route (Download ZIP or Send
  via API) moves the case state. Recorded as **D47**: the first Send to EVA
  from `Review` performs the existing `StartCaseWork` transition atomically
  with the handoff, whichever route is chosen; a re-send from `With Engineer`
  changes no state. This supersedes FRD-07's two statements that neither
  route changes the Case state or version (lines 63 and 131). CASE-040's PR
  carries the FRD-07 correction, since CASE-040 owns the Send to EVA action
  and its Core transition; PLAT-070 carries only the D44/D45 doc lines.
  The plan's Core action changes accordingly: the send command performs the
  transition in one unit of work, and failure of either half leaves the case
  in `Review` with no partial handoff.

## Parked (explicitly deferred)

None.
