# Open questions — TICK-050 / MAIL-08

No unresolved question remains for the minimum confirmed slice. Suggestions are advisory and re-derived; the only currently accepted suggestion is an eligible **Move** that delegates to MAIL-07's separate confirmation workflow.

## Parked (explicitly deferred)

- [x] **Which dependencies must land first?** — [[TICK-047]] and [[TICK-049]] now structurally block MAIL-08. MAIL-05 supplies the current recommendation; MAIL-07 owns eligibility, confirmation and execution. MAIL-23 is a transitive prerequisite through MAIL-05.
- [x] **Are suggestions stored or executable themselves?** — No. They are a pure current-state projection. Viewing advice writes no history or mailbox state, and the Move control invokes MAIL-07 rather than performing an inline/client-selected mutation.
- [ ] **Should MAIL-08 later suggest Case association, read/category/flag/delete, reply/forward/send, or other actions?** — Explicitly deferred because FRD-08 contains no accepted broader suggestion matrix. Do not infer these from [[TICK-051]], [[TICK-052]], [[TICK-054]], or [[TICK-088]] merely existing. If the operator later names one, its owning Core action must land first and the ticket must be replanned; MAIL-12 remains Later/0.5.0 rather than silently blocking this Next/0.3.0 slice.
- [x] **What live Outlook/Graph/cloud verification is required?** — Resolved by the operator on 2026-08-19. After deployment, perform an authenticated, read-only production message-detail check showing current suggested advice for a real retained message. Do not invoke Move or any other action, alter mailbox configuration, broaden Graph scope, or mutate Outlook/cloud state.
