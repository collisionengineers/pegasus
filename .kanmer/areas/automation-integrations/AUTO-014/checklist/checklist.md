# Checklist — AUTO-014

- [x] `ListForSubjectAsync` has a non-test production consumer that is itself
      reachable — `Pages/Mail/Message.cshtml.cs:706`, route
      `GET /Inbox/{message-id}?section=case`, rendered at `Message.cshtml:525`
- [x] A staff action reachable in the deployed estate creates an
      `AiJobKind.QueryResponse` job — control `Message.cshtml:74`, handler
      `Message.cshtml.cs:228`, Core command `Message.cshtml.cs:257`
- [x] No control shipped permanently inert; the disabled state is conditional on
      Case eligibility and the administrator AI switch, with `data-condition`
      always set (D21 "legitimate state" row, not a D7 seam)
- [x] No closed composition gate used to satisfy either item; no feature flag
      touched (D26)
- [x] No new port, command or query added — `Core/AiWork` is untouched
- [x] `OperatorLabels.cs` appended in its own nested class; nothing reordered
- [x] Build: `dotnet build ./Pegasus.slnx --configuration Release` — 0 errors,
      0 warnings, 0 `CS####` (re-run independently by the orchestrator)
- [x] Focused tests — Failed 0, Passed 2 (re-run independently)
- [x] Assertion integrity — 0 removed `Assert.`, 0 new `Skip`, 0 deleted tests
      across `origin/dev...HEAD` (verified by the orchestrator)
- [x] File ownership — `Pages/Mail/**` has no in-flight claimant; recorded as a
      D19 case-2 change in `files/files.md`
- [ ] Independent cross-model review — **outstanding**. Built by Codex, so it
      must be reviewed by a Claude-family agent.
- [ ] CI green on the PR — **outstanding**
- [ ] Merged to `dev` — **outstanding**

## Then, and only then

- [ ] [[AUTO-011]] re-audited against merged `dev` under D15/D20 and returned to
      Done. **This ticket is not finished until that happens** — supplying the
      callers is the means; unblocking AUTO-011 is the end.

## One open question for review

Whether a staff-initiated `QueryResponse` job is in alpha scope at all. The
ticket named removal of the kind as the alternative if [[TICK-101]]'s activation
gate meant it was not. Wiring was preferred over deleting a capability Core
already constructs and a migration constraint already pins — but that is a
judgement a reviewer should test, not inherit. See `research/research.md`.
