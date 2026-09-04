---
outcome: approved
pr: https://github.com/collisionengineers/pegasus/pull/659
head: fbb8f6622e7fe59c0897730fa2a588fbdd0c8687
merge_commit: e66e106993acbae39eaa6abd5c0e592a52302c61
reviewers: gpt-5.6-terra (xhigh, independent read); Claude Opus (dispositions, verification, gating, merge)
date: 2026-09-04
---

# Review attestation — CASE-032 — approved and merged

Re-reviewed PR https://github.com/collisionengineers/pegasus/pull/659 at head
`fbb8f6622e7fe59c0897730fa2a588fbdd0c8687` after the fix round. Merged to `dev`
as `e66e106993acbae39eaa6abd5c0e592a52302c61`.

The full record — every finding, disposition, command and exit code — is in this
ticket's `reference` document ("Review record — CASE-032 … re-review").

Both should-fix findings from the `ed0dc6ad2` round are genuinely closed:

1. `Index.cshtml.cs:549-552,574-582` — `Custody`, `Reference` and `Provider`
   quick-detail facts are now added only when their source value is non-null,
   matching `BlockedRow`'s convention. No placeholder substituted; the
   `"Unassigned"` assignee fallback at `:427-429` is untouched; pre-existing
   facts keep their order.
2. `TriageQueuesWebTests.cs:254-257` — the assignee is now proved by the
   contiguous decoded fragment `"{provider} · {assignee}"`, emitted only by
   `TriageRow`'s `Join(item.Provider, assignee)`. No assertion weakened.

The fix commit touches exactly the two files the fix packet named and
introduces no regression.

Two fresh nits from the independent read were dispositioned without a code
change: `ImageIntakeDetail.Custody`'s trailing default (**accept risk** — one
production construction site, which supplies the value; no Core policy reads
the member, so no fake can produce a false pass), and the absence of a snapshot
re-run on this head (**rejected** — no captured page in the repository renders a
queue row of any kind, so the changed row builders are unreachable from every
snapshot state, and no artifact, `.cshtml`, catalogue or tooling file changed).

Local verification at this head: restore 0, Release build 0 (0 warnings, 0
errors), Core.Tests 0 / 1225 passed, ArchitectureTests 0 / 100 passed,
`TriageQueuesWebTests` 0 / 9 passed. CI run `33879497231` on head
`fbb8f6622` completed with conclusion `success` before the merge.
