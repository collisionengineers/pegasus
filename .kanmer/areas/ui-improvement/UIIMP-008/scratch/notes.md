## Review fixes — PR #598 review (2026-08-28, REQUEST CHANGES → fixed)

Findings and dispositions (blocking 1–2 and minor 3–4 fixed on the branch;
5–7 accepted/deferred by the reviewer, untouched):

1. FIXED (blocking) — `NeedsAttentionSkipsTriageWithAFinding…` was red as
   shipped: it fed an Open Triage record while asserting no Triage row,
   contradicting the repaired state queries. Now feeds only the
   FindingRecorded record (the row that must never surface).
2. FIXED (blocking) — `…StillListsOpenTriageBehindFiftySettledRecords`
   guarded nothing (stub ignored Page/PageSize). `StubListTriage` now pages
   the way `ListTriage` does (Skip/Take window, TotalCount = full match
   count, insertion order), so a revert to one unfiltered page truncates
   the Open record and the test genuinely fails.
   Evidence: `dotnet test tests/Pegasus.Core.Tests --filter
   "FullyQualifiedName~DashboardBoundaryTests"` → Failed 0 / Passed 8 /
   Skipped 0 / Total 8 (the one permitted focused run).
3. FIXED (minor) — Refresh/F5 dropped the selection; `Index.cshtml` now
   sets `ViewData["RefreshFields"]` with `["selected"]` per the
   Cases/Index convention.
4. FIXED (minor) — false coverage claim corrected in `research/research`
   (coverage deleted with the tile; channel split unpinned; wave 3
   CASE-028 owns `DashboardCounts.cs`) and in the PR body.

Commits: c07b4488 (test guards), f524b343 (refresh keeps selection);
pushed; Release build green.

## CI fixes — PR #598 run 33183320100 (2026-08-28)

Five cross-page failures (old landing pins), retargeted to the Work Centre
surface in commit 5a9ff906; build green; the Connection-Timeout shard-1
failures were runner flakes per the reviewer and were not touched.

| Test | Old pin → New pin | Strength |
| --- | --- | --- |
| TriageQueues NotReadyBadgeCount… | `data-state="not-ready"…metric__value` tile → `data-value="not_ready"…metric-value` metric | kept: badge == figure across both origins |
| HealthEndpoint LandingPage… | "Active cases"/"E-mail activity" labels → Work Centre heading + `/Cases/Create` href + not_ready/unidentified metric hrefs + "Unidentified" | strengthened: pins the workspace's entry points, not section labels |
| OperatorJourney Unimplemented… (:222) | `.metric .metric__value` (matched nothing) → `.metric .metric-value` | kept: every shipped metric renders a digit |
| OperatorJourney OperationsFirstJourney | h1 "Dashboard" → "Work Centre"; read order "active cases/e-mail activity/today and this week" → "work requiring attention/needs attention/selected work"; Review metric click-through unchanged | kept |
| Accessibility constrained viewport (:123) | h1 "Dashboard" → "Work Centre"; metric locator tightened from dual `.metric-value, .metric__value` to `.metric-value` only (its own comment deferred the tightening to this port) | strengthened |

No gaps: every retired surface has a Work Centre equivalent still pinned.
UploadDropzoneBrowserTests' `.page-heading` use targets /Upload (lane G),
not this page.

## Conflict resolution + first real CI — PR #610 (2026-08-28)

PR #610 was CONFLICTING, 10 ahead / 64 behind, and the branch head had
never scheduled a CI run (`gh pr checks 610` → "no checks reported"; two
empty `ci: retrigger` commits, `812e3516` and `57a51500`, produced zero
runs — an empty commit does not schedule one here).

Merged `origin/dev` (not rebased — `c07b4488`, `f524b343`, `5a9ff906`
must stay reachable). Merge commit `11f1f7de`; now 12 ahead / 0 behind,
`mergeable: MERGEABLE`.

One conflict: `TriageQueuesWebTests.cs`. Dev's CASE-025 rewrite renamed
`NotReadyBadgeCount…` → `NotReadyRailCountMatchesRowsAcrossBothOrigins`
and swapped the badge assertion for a `.scope-button` rail regex, on the
same lines this lane edited. Neither side was takeable: dev's surviving
test still scrapes `/` with `data-state="not-ready"…metric__value` —
markup this lane replaces — so "theirs" merges clean and fails at
runtime; "ours" reverts CASE-025's rail rewrite. Hand-merged from dev's
file with only this lane's three edits re-applied (tile regex →
`data-value="not_ready"…metric-value`, assert message, summary/comment
wording). Dev's test name, `.scope-button` assertion, `row-button` count
and `railCount` kept; `badgeCount` discarded. Diff vs `origin/dev` is
exactly those three hunks.

Auto-merges checked by inspection, all additions-only over dev:
`DashboardCounts.cs` (dev's `Complete = 0` + this lane's NeedsAttention
types), `OperatorLabels.cs` (dev's TriageState/CaseRequirement(s)/
RequestOperationState/four ServiceHealth* + this lane's six),
`Browser/OperatorJourneyTests.cs`.

Post-merge defect fixed: both sides had appended
`using Pegasus.Core.Operations;`, so the merged `OperatorLabels.cs`
declared it twice → `CS0105` build failure. Dev's sorted entry kept, the
trailing duplicate deleted (`b8c0cf77`); nothing reordered.

Evidence — Release build exit 0 (0 warnings, 0 errors); focused runs all
exit 0: TriageQueuesWebTests 8/8, DashboardBoundaryTests 8/8,
DashboardCountersWebTests 2/2, HealthEndpointTests 3/3. The
TriageQueuesWebTests pass is the load-bearing one: that test scrapes
CASE-025's rail markup and this lane's metric markup in the same run.

CI run 33212916874 scheduled by the content-bearing merge push — the
first real run this head has had.

For the orchestrator: `waves.md` allocates `DashboardCounts.cs` to
wave-2 lane A (this ticket), but CASE-025 (lane C1) edited it in
`95f69958` and also edited `TriageQueuesWebTests.cs`. That breach caused
this conflict.

## CI run 33212916874 — one failure, out of lane (2026-08-28)

First real CI on this head. Everything green except `sql-integration (2)`:
Failed 1 / Passed 329 / Skipped 2 / Total 332. `unit`, `browser`,
`sql-integration` 1 and 3, `sql-integration-coverage`, `changes`,
`documentation`, `local-development-scripts` and `reference-data` all
pass.

The single failure is
`PrincipalCredentialPersistenceTests.IssueResetPauseResumeRevokeAreHash
OnlyReplaySafeAndFailClosed` at `PrincipalCredentialPersistenceTests.cs`
line 62 — TICK-061's test file, arrived on this branch from `origin/dev`
in commit `4aec2703`. This lane never touched that test, nor
`Core/Cases/PrincipalCredentials.cs`, nor
`Infrastructure/Persistence/EfPrincipalCredentialStore.cs`, and nothing
this lane owns is reachable from it.

**It is a flake, and the rate is computable.** Line 62 is:

    Assert.Null(await authenticate.ExecuteAsync(
        firstKeyId, firstSecret[..^1] + "A", default));

It builds a "corrupted" secret by replacing the last character with
`"A"` — but when the real secret already ends in `A`, that expression
reconstructs the *correct* secret, authentication rightly succeeds, and
the assertion fails. The failure output shows exactly that: expected
null, actual `PrincipalCredentialAuthentication { State = Active,
MaySubmit = True }`.

Rate: `GenerateSecret` ends in `Base64Url(RandomNumberGenerator
.GetBytes(32))` (`PrincipalCredentialPolicy`, `SecretBytes = 32`). 32
bytes = 256 bits; the first 42 base64url characters carry 252, so the
43rd and last character encodes the remaining 4 bits in its high bits
and takes one of 16 values, of which `A` is one. **The test fails about
1 run in 16 (~6 %).**

Fix belongs to TICK-061's owner, not here — mutate to a character
guaranteed to differ, e.g. `firstSecret[..^1] + (firstSecret[^1] == 'A'
? 'B' : 'A')`. Reported, not fixed: outside this lane's files.

Re-ran the failed job only (`gh run rerun 33212916874 --failed`) to
confirm the flake.
