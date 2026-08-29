## Plan (profile: fix, small/self-contained)

1. Confirm the defect and the intended fix by convention, not invention.
   - Read: current template is
     `@page "{organizationId:guid}/{principalId:guid}/EvaSubmission"`
     (relative route + a literal trailing page-name segment) → effective
     route `/Administration/Principals/EvaSubmission/{organizationId}/{principalId}/EvaSubmission`.
   - Convention check (read-only): the sibling `Replace.cshtml` in the same
     folder uses `@page "{organizationId:guid}/{principalId:guid}"` for the
     identical two-guid shape, catalogued as
     `/Administration/Principals/Replace/{organizationId:guid}/{principalId:guid}`.
     Confirms the fix the ticket names is exactly this repo's own pattern,
     not a new one.
2. Fix: change `EvaSubmission.cshtml` line 1 to
   `@page "{organizationId:guid}/{principalId:guid}"` — reuses the
   `Replace.cshtml` convention verbatim.
3. Callers: `git grep -in evasubmission` across `src/`, `tests/`, `docs/`,
   filtered to page/route hits (the raw term also matches unrelated
   business types — `EvaSubmissionModelConfiguration`, `EvaSubmissionOutcome`,
   `IUpdatePrincipalEvaSubmission`, etc. — not this page). Only in-repo
   caller is `Index.cshtml`'s `asp-page="EvaSubmission"` link, which is
   tag-helper-generated and needs no edit. No catalogue entry, no
   `docs/` route mention, no other test file references this route
   (see `files` doc for the full negative-result list).
4. Redirect-stub decision: **no stub**. Per the greenfield rule ("unless
   the brief names users or data, add no fallback/compatibility/deprecation
   path"), the doubled URL was a routing defect, not a published address:
   it has no external distribution (no email template, no bookmarked
   support flow, no doc reference), and its one in-app entry point is the
   dynamic `asp-page` link that will render the corrected URL the moment
   this ships. `docs/operations.md` release 36 (2026-08-28, today) shows
   this page already reached production, so an admin could in principle
   have the doubled URL in local browser history — but that is not "named
   users or data" in the ticket or operator-notes sense, and the app has
   no way to reach a private browser history entry anyway. No redirect
   stub added.
5. Tests: extend `OrganizationAdministrationWebTests` (the only Web-route
   proof surface for this admin folder) rather than adding a new file:
   - `AdministratorRoutesAreDiscoverableAndPostThroughCoreEfCallers`: after
     the existing principal-index assertions and before the `Replace` walk
     (so the principal is still active/unreplaced), GET the corrected
     single-segment `EvaSubmission` route, assert the page renders, POST
     `?handler=Update` with both EVA toggles + reason, assert redirect, and
     assert the `Principals` row now has `EvaManualSubmission = 1`,
     `EvaAutomaticSubmission = 0` — proves the fixed route resolves,
     round-trips through the real handler and the real EF caller, mirroring
     the existing `Replace` proof shape exactly.
   - `DirectOrganizationAndPrincipalRoutesDenyNonAdministratorSession`: add
     the corrected `EvaSubmission` route to the denied-routes array,
     mirroring the existing `Replace` entry.
6. Build (`dotnet build --configuration Release`) for compiler feedback,
   then run the focused filter
   `--filter "FullyQualifiedName~OrganizationAdministrationWebTests"` and
   record the pass count.
7. Simplification pass over this branch's own diff (two files, ~30 lines):
   reviewed manually against the four lenses (reuse, simplification,
   efficiency, altitude) given the diff's size — no separate agent
   invocation warranted for a diff this small. Findings: none; the fix is
   a one-line convention match and the test additions are a direct copy of
   an existing, already-reviewed pattern in the same file. Recorded here
   under a dated heading per the repository workflow.
8. Kanmer: `get_doc_gates` → `take_ticket` (done) → this `files`/`plan` pair
   → `move_item` to `implementing` → implement (done ahead of the doc walk,
   per lane instructions) → post-implementation report → `move_item` to
   `review`. Do not write `proof` or move to `done` — orchestrator-owned.
9. Commit to `task/plat-052-eva-submission-route`, push, open the PR
   against `dev`. Do not merge.

## Acceptance conditions

- Exactly one route for this page:
  `/Administration/Principals/EvaSubmission/{organizationId}/{principalId}`.
- `Index.cshtml`'s "EVA API" link still resolves (unchanged markup, tag
  helper regenerates the URL).
- `OrganizationAdministrationWebTests` passes and now exercises the
  corrected route both as an authorized round trip and as a denied route
  for a non-administrator session.
- Build green; no other file in the repo references the doubled route.

## Simplification pass — 2026-08-28

n/a for a separate agent pass at this size (2 files, ~30 lines changed).
Manually reviewed against reuse / simplification / efficiency / altitude:
no findings. The route fix reuses `Replace.cshtml`'s exact template; the
test additions reuse the existing test's own form-post and assertion idioms
verbatim rather than introducing a new helper or pattern.

## Remediation round 2 — 2026-08-29 (adversarial verifier findings)

An independent verifier re-ran the build/tests/diff and found the scope and
build/test claims true, but raised two majors and one minor on the ticket's
own catalogue acceptance condition and the pipeline's honesty about it. See
PR #614 for the verifier's findings; dispositions below.

### Fix: add the catalogue entry (retracts the "no entry exists" premise)

The plan/files/post-implementation-report all previously said no catalogue
entry existed for this page anywhere. That was false: [[UIIMP-005]]'s own
unmerged branch (`task/uiimp-005-test-ui-gate`, PR #609, open) already
carries one — with the *old, doubled* route text, because it catalogued the
page as shipped before this ticket's fix. I did not check that linked
branch originally; the verifier did and was right.

Fix applied:

1. Added a `docs/design/test-ui/catalogue.json` entry for
   `Administration/Principals/EvaSubmission.cshtml`, `classification:
   "visual"`, with `route` set to the corrected single-segment route this
   ticket ships (`/Administration/Principals/EvaSubmission/{organizationId:guid}/{principalId:guid}`)
   — i.e. UIIMP-005's own entry with only the `route` field corrected.
2. Added `docs/design/test-ui/pages/administration-principal-eva-submission--default.html`,
   copied byte-for-byte from UIIMP-005's branch (`git show
   origin/task/uiimp-005-test-ui-gate:docs/design/test-ui/pages/administration-principal-eva-submission--default.html`).
   Confirmed the prototype's own markup never embeds the route text (its
   form `action` and back-link both point at sibling prototype filenames,
   not the real route), so reusing it verbatim under the corrected route
   entry is not a mismatch. This is reuse of already-captured, real output
   — not a fabrication and not a run of the barred
   `Update-TestUiSnapshots.ps1` capture script.
3. Left `docs/design/test-ui/index.html` untouched. It is a generated
   artifact (the whole file is a single minified line, rewritten wholesale
   by the `TestUiSnapshotTests` class that `Update-TestUiSnapshots.ps1`
   drives) and `Test-UiCatalogue.ps1` never cross-checks it against
   `catalogue.json` — only broken-local-reference / missing-`img`-`src`
   checks within whatever `index.html` already contains. Regenerating it
   requires the barred script; the next real snapshot-capture pass (the
   orchestrator's normal wave loop, or UIIMP-005 itself) will pick up this
   entry automatically. Flagging this so it isn't mistaken for an omission.

Verified narrowly, with a temporary local-only diagnostic (not committed —
reverted before commit): with a throwaway placeholder entry added for the
unrelated `Cases/Eva/Send.cshtml` gap (see below) and a scratch copy of the
validator that reports every error instead of stopping at the first, the
*only* remaining error was a pre-existing broken reference in
`vehicle-images-details--default.html` (also below, also unrelated). No
other error is introduced by this entry. The diagnostic entry and the
scratch script were both deleted before commit; `git diff --stat` still
shows exactly the four files listed in `files`.

### Two more pre-existing, out-of-scope catalogue defects found while fixing this one

`Test-UiCatalogue.ps1` uses `Write-Error` under `$ErrorActionPreference =
'Stop'`, so it halts at the *first* error alphabetically and never lists
the rest. Both the original implementation and the verifier only ever saw
"`Administration/Principals/EvaSubmission.cshtml` is not classified"
because it sorted first. Fixing it exposed two more, both pre-existing,
both outside this ticket's lane:

1. **`src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml` is not classified.**
   Introduced by the same commit as this ticket's bug (`09beefef`,
   TICK-077, PR #574) — confirmed via `git log --oneline -- src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml`.
   This file belongs to [[CASE-012]]'s Wave-2 "E1" lane per
   `waves.md` (`Pages/Cases/Details.*, ..., Eva/Send.*`), which has an
   **open** PR (#615, "complete lane E1 (Eva/Send, Create, Workflow,
   Closure)"). Not touched — not this ticket's file. Orchestrator: confirm
   CASE-012 (PR #615) adds its own `catalogue.json` entry before relying on
   a fully green `Test-UiCatalogue.ps1` run; it won't go green on `dev`
   until that lands, independent of anything in this PR.
2. **Broken local reference in `docs/design/test-ui/pages/vehicle-images-details--default.html`:
   `vehicle-images--default.html`.** That target prototype no longer
   exists — `src/Pegasus.Web/Pages/VehicleImages/Index.cshtml` (the list
   page) is already deleted from `src/` (EPIC-011 decision D1: "delete
   `/VehicleImages` list only; keep the detail page"), but the surviving
   detail prototype's "All Image-initiated Cases" link was never updated to
   match. `git log --oneline -- docs/design/test-ui/pages/vehicle-images-details--default.html`
   shows no recent touch — unrelated to TICK-077/CASE-012 and to this
   ticket. No obvious current owner; likely Wave 5 territory
   (`waves.md`: "removals, catalogue, current-state docs, final walk").
   Not touched — reporting only, per lane rule ("report defects outside
   your files; never fix them").

Both are real, reproducible, and will keep `Test-UiCatalogue.ps1` red on
`dev` regardless of anything in this PR. Neither is this ticket's to fix.

### Merge-order hazard with UIIMP-005 (PR #609) — handed over explicitly

[[UIIMP-005]] is one of this ticket's own `links`. Its branch
(`task/uiimp-005-test-ui-gate`) already contains an `EvaSubmission.cshtml`
catalogue entry at the *same insertion point* in `catalogue.json` (between
the `Create` and `Index` entries) with the **old, doubled** route text.
`git diff origin/dev...origin/task/uiimp-005-test-ui-gate -- docs/design/test-ui/catalogue.json`
shows that entry as a clean 13-line addition, plus a second, non-overlapping
13-line addition for `Cases/Eva/Send.cshtml` elsewhere in the file.

**Recommendation to the orchestrator: merge PLAT-052 (PR #614) before
UIIMP-005 (PR #609).** When UIIMP-005 is later rebased onto `dev` (post-
PLAT-052 merge) or merged, Git will conflict on the `EvaSubmission` block
(both sides insert content at the same location) rather than silently
picking either side — but the person/agent resolving that conflict must be
told explicitly: **keep this ticket's single-segment `route`** (and this
ticket's copy of the prototype file, byte-identical to UIIMP-005's own
anyway) **and drop UIIMP-005's doubled-route version of that one entry**,
while keeping UIIMP-005's unrelated `Cases/Eva/Send.cshtml` addition and
its script-hardening/CI-gate work intact. If the order is reversed instead
(UIIMP-005 merges first), PLAT-052 would need to rebase and change its own
diff from *adding* a catalogue entry to *updating* the `route` field of the
one UIIMP-005 landed — which is livable, but leaves this ticket blocked on
a larger, still-open ticket (UIIMP-005 hardens tooling + adds a CI gate)
for no benefit, so merging PLAT-052 first is the lower-friction order.

## Review findings — dispositions (round 2), 2026-08-29

Verdict from the adversarial verifier: needs-work. Four findings; all
disposed below.

1. **[major] Ticket's own verification condition ("Test-UiCatalogue.ps1 ...
   pass") not met — catalogue gate fails.**
   **Disposition: fixed, partially.** Added the missing `EvaSubmission`
   catalogue entry + prototype file (see "Fix" above); confirmed by direct
   `pwsh -NoProfile -Command "& ./scripts/Test-UiCatalogue.ps1"` re-run that
   `EvaSubmission.cshtml` is no longer reported. The full repo-wide script
   still exits 1 on this branch (and will on `dev`) because of two other,
   pre-existing, out-of-scope defects (`Cases/Eva/Send.cshtml` uncatalogued;
   the `vehicle-images-details` broken reference) — see above. This
   ticket's own contribution to the gate is closed; the gate as a whole is
   not this ticket's to close alone, and I did not touch either unrelated
   file. **Risk accepted / flagged to orchestrator**, not silently claimed
   green.

2. **[major] Pipeline documents record a false premise about the
   catalogue; the resulting merge-order hazard with UIIMP-005 was never
   handed over.**
   **Disposition: fixed.** `plan` and `files` corrected in place (see
   "Correction — round 2" in `files` and the retraction above);
   `post-implementation-report` corrected below. The merge-order hazard
   with UIIMP-005/PR #609 is now recorded explicitly, with a stated
   recommended merge order, above.

3. **[minor] Mitigating detail: the stale `route` text is a docs-
   correctness defect, not a second gate failure — nothing validates
   `route` against the page template.**
   **Disposition: accepted as correct, no code change needed.** Confirmed
   independently (`grep -n route scripts/Test-UiCatalogue.ps1` — `route`
   appears only in error-message text). Recorded here so the merge-order
   handover above is read with the right severity: the hazard is a
   docs-accuracy regression at merge time, not a second red gate.

4. **[honesty] Overclaim: "no catalogue entry exists ... EvaSubmission
   never was [catalogued]" — the entry exists on UIIMP-005's linked,
   unmerged branch; the lane never checked it.**
   **Disposition: fixed.** Retracted explicitly in `files` ("Correction —
   round 2") and in `post-implementation-report`, not silently edited
   out — the wrong claim, why it was wrong, and what replaced it are all
   left in place for the record.

## Simplification pass — 2026-08-29 (round 2)

n/a — this round only adds one data-file entry and one static prototype
file (both copied from an existing, already-produced source rather than
authored fresh), and corrects prose in three existing documents. No new
code, no new abstraction, nothing to simplify.

## Follow-up ticket filed — 2026-08-29

Filed [[PR-070]] (area `pr-review`, group `EPIC-011`) for the
`vehicle-images-details--default.html` broken-reference defect above — it
had no current owner among the in-flight tickets, unlike the
`Cases/Eva/Send.cshtml` gap ([[CASE-012]], PR #615, already open and
already the file's owner). Not creating a duplicate ticket for the
`Send.cshtml` gap; flagging it to the orchestrator to confirm CASE-012's
own PR covers it instead.

## Remediation round 3 — 2026-08-29 (merge `origin/dev`, snapshot regeneration BLOCKED)

Orchestrator instruction for this round: merge `origin/dev` (7 PRs landed
today: PLAT-053, INTK-046, CASE-026, UIIMP-008, ENG-025, CASE-012,
DELIV-031), then run the snapshot regeneration this ticket's own gate names
(`Update-TestUiSnapshots.ps1` → `-Verify -SkipCapture` →
`Test-UiCatalogue.ps1`), commit only what belongs to this ticket, then
build/test/push.

### Step 1 — merge `origin/dev`: clean, no manual conflict resolution needed

`git fetch origin -q && git merge origin/dev --no-edit` at merge-base
`9868cf58` (60 commits behind). Git's own three-way merge (`ort` strategy)
resolved `docs/design/test-ui/catalogue.json` automatically — no
`CONFLICT` marker, no manual edit by me. Verified the result is correct
before trusting it:

- The `EvaSubmission` entry this ticket added is intact and untouched by
  the merge (single-segment route, `visual` classification).
- The `Closure.cshtml`/`Workflow.cshtml` entries now read `classification:
  "protocol"` (not the `"redirect"` value at this branch's merge-base) —
  confirmed this is CASE-012's own round-2 remediation commit `2dcf69a4`
  (already on `origin/dev`, correcting an untrue `"redirect"` claim to the
  accurate `"protocol"` one), not a merge artifact and not something this
  branch ever asserted differently, so git's move-forward resolution
  (dev's changed side wins over this branch's untouched side) is correct.
- `Cases/Eva/Send.cshtml` **remains uncatalogued on `origin/dev` even after
  CASE-012 (PR #615) merged today** — confirmed by grep after the merge.
  The plan's round-2 note ("flagging to the orchestrator to confirm
  CASE-012's own PR covers it") is now answered: **it did not.** Still not
  this ticket's file to fix (owned by CASE-012/E1 per `waves.md`); flagging
  again since it's now a *merged-and-still-broken* state, not just an
  open-PR gap.
- `vehicle-images-details--default.html`'s broken reference to the deleted
  `vehicle-images--default.html` also still stands on `origin/dev`,
  unrelated to any of today's 7 merges — [[PR-070]] (filed round 2) still
  has no owner and no fix in this round.
- Build after merge: `dotnet restore --locked-mode` then
  `dotnet build --configuration Release` — **0 warnings, 0 errors.**

### Step 2 — snapshot regeneration: FAILS before writing anything, for a cause outside this ticket's files

`pwsh -NoProfile -Command "& ./scripts/Update-TestUiSnapshots.ps1"` ran the
full capture filter (`WebTests|Category=Browser|StaffSignInSecurityTests|
TestUiFocusedRenderTests|QdosCustodialWebTests|
AutomationConnectorAuthorizationTests|ImageViewingWebTests`) — **that phase
passed clean: 355 passed, 0 failed, 11 skipped, 366 total, 19 min** — a
strong independent signal the merge itself is behaviourally sound. The
script's second phase (`TestUiSnapshotTests` in `update` mode, which
regenerates `index.html` and every `pages/*.html` from the capture) then
**threw and wrote nothing**:

```
Failed Pegasus.IntegrationTests.TestUiSnapshotTests.CapturedRazorResponsesMatchCommittedTestUiSnapshots [7 s]
Error Message:
 No captured Razor response matched:
- queues--empty (/Cases)
- inbox--empty (/Inbox)
- operations--empty (/Operations)
- cases--empty (/Search)
- cases--unavailable (/Search)
```

`TestUiSnapshotTests.Generate()` asserts `missing.Count == 0` *before*
calling `WriteGenerated` — so this is an all-or-nothing gate: none of the
56 pages, including this ticket's own `administration-principal-eva-
submission--default.html`, got regenerated. Confirmed the working tree is
untouched (`git status --short` empty) — no partial or fabricated output
was produced or considered for commit.

**Root cause, verified, not guessed:** `TestUiSnapshotTests.cs`'s
`StateMatches` dictionary picks the "empty"/"unavailable" candidate for
each route by a hardcoded literal substring in the rendered HTML (e.g.
`queues--empty` → `"No cases are waiting."`, `inbox`/`operations` `--empty`
→ class marker `"empty-state"`, `cases--unavailable` → `"<h2>Cases are
unavailable</h2>"`). Four different pages — `Pages/Cases/Index.cshtml`
(queues, owned by CASE-025/C1), `Pages/Mail/Index.cshtml` (Inbox, owned by
MAIL-025/B), `Pages/Operations/Index.cshtml` (owned by PLAT-023/H) and
`Pages/Search/Index.cshtml` (owned by CASE-026/D) — no longer emit that
text at all:

- `Pages/Cases/Index.cshtml`'s empty branch is a bare `@foreach` over
  `Model.Rows` with **no** fallback markup when the collection is empty
  (confirmed: zero hits for `No `, `empty`, or `waiting` anywhere in the
  file) — the design system's "no empty-state prose" rule (`context.md`,
  `docs/design/README.md`) means there is nothing left to render, so the
  captured HTML for zero rows is byte-identical in structure to a partial
  page and carries no distinguishing marker at all.
- `Pages/Search/Index.cshtml` still has an "unavailable" notice but as
  `<strong>Cases are unavailable</strong>`, not the `<h2>` wrapper the
  marker requires — a tag-level drift, not a wording drift.
- Confirmed candidates for all four routes DO exist in the capture
  directory (23/50/12/18 HTML responses respectively for
  `/Cases`,`/Inbox`,`/Operations`,`/Search`) — this is a stale-marker
  problem, not a missing-test-coverage problem.

**Why this is not this ticket's fix.** The one file that would need
editing to restore matching (`tests/Pegasus.IntegrationTests/
TestUiSnapshotTests.cs`, the `StateMatches` dictionary) is shared test
tooling used by every UI lane's snapshot pass — not in this ticket's
`files` list, not owned by `platform-operations`/PLAT-052, and the correct
fix requires a design call this ticket has no authority to make for four
other lanes' pages: what should now visually/textually distinguish an
"empty" render from a "default" one, now that the explanatory sentence
those markers depended on has been deliberately removed under the "no
explanatory copy" rule? That is not a one-line change in an unowned file
(D19's fix-it-anyway allowance) — it is a design decision spanning four
different pages across four different lanes (CASE-025, MAIL-025, PLAT-023,
CASE-026) plus the shared tooling file, several of which (PLAT-023,
CASE-026) merged their PRs *today*, so "the lane is not currently in
flight" does not clearly hold either. Per the hard rule ("Touch ONLY your
lane's files. Report defects outside them.") this is reported, not fixed,
here.

**Disposition: escalate to the orchestrator (no ticket filed by me — this
lane's tool set has no `create_item`).** Recommending either (a) the
orchestrator assigns a fix to whichever ticket/lane owns
`TestUiSnapshotTests.cs` (UIIMP-005/PLAT-029, per `waves.md`'s tooling
ownership), choosing new discriminating markers for all five states (or
retiring the "empty"/"unavailable" catalogue states entirely for pages that
no longer render distinguishing content, which is itself a call for
whoever owns the manifest), or (b) explicit authorization for a follow-up
ticket to do the same. Until resolved, **`Update-TestUiSnapshots.ps1`
cannot succeed for *any* ticket, not just this one** — this blocks the
epic's snapshot corpus more broadly, not only PLAT-052's two named stale
artefacts.

Consequently `-Verify -SkipCapture` was not run (it re-executes the same
`Generate()` step in verify mode and would fail identically before ever
reaching the file-comparison stage — confirmed by reading
`TestUiSnapshotTests.cs`'s `CapturedRazorResponsesMatchCommittedTestUiSnapshots`,
which calls the same `Generate()` regardless of mode). The retained capture
directory (`artifacts/test-ui-capture`, gitignored, 1300+ files) is left in
place in the worktree in case the orchestrator wants a `-SkipCapture` rerun
once the marker fix lands, saving the ~19-minute capture phase.

### Step 3 — `Test-UiCatalogue.ps1`: baseline unchanged by the merge

Run before attempting regeneration (static analysis, independent of the
capture/update pipeline): **exit code 1**, single reported error (the
script stops at the first alphabetically):

```
Write-Error: Routed Razor source is not classified: src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml
```

Identical to the pre-merge baseline and to round 2's documented finding —
the merge introduced **no new** catalogue errors. This is the
`Cases/Eva/Send.cshtml` pre-existing gap from round 2 (§"Two more
pre-existing, out-of-scope catalogue defects"), confirmed still open on
`origin/dev` post-CASE-012-merge (see Step 1 above). Not run again after
the failed regeneration attempt since nothing changed on disk to alter the
result.

### Step 4 — build and focused test, real counts

- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` (post-
  merge): **0 Warning(s), 0 Error(s)**.
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "FullyQualifiedName~OrganizationAdministrationWebTests"`: **Passed! —
  Failed: 0, Passed: 2, Skipped: 0, Total: 2, Duration: 49 s.**

### Step 5 — commit and push

Nothing new to commit: the merge produced no conflicts requiring a manual
resolution commit beyond git's own merge commit, and the snapshot
regeneration wrote no files (see Step 2). Pushed the merge commit only:
`git push origin task/plat-052-eva-submission-route` →
`0a0d9eee..48df8f58`. No PR merge performed.

### Disposition summary (round 3)

1. **Merge conflict handling** — n/a, resolved automatically by git;
   verified correct by hand (see Step 1). No disposition needed.
2. **Stale snapshot artefacts named in this round's brief
   (`administration-principal-eva-submission--default.html`,
   `index.html`)** — **blocked, not fixed.** Cannot be regenerated until
   the unrelated `StateMatches` marker drift (Step 2) is resolved, because
   the generator is all-or-nothing across all 56 pages. **Disposition:
   accept risk / escalate** — recorded here, not silently claimed done.
3. **`StateMatches` marker drift across 4 other lanes' pages +
   shared tooling file** — **out of lane, reported.** **Disposition: defer
   to the orchestrator** (no ticket filed — `create_item` not in this
   lane's tool set). This is the blocking finding for the round.
4. **`Cases/Eva/Send.cshtml` uncatalogued, confirmed still true after
   CASE-012's merge** — **out of lane, reported** (already known from round
   2; now confirmed persisting post-merge). **Disposition: unchanged from
   round 2** (CASE-012/E1's file; orchestrator should confirm whether a
   follow-up is needed since PR #615 did not close this gap).
5. **`vehicle-images-details--default.html` broken reference** — **out of
   lane, already ticketed.** **Disposition: unchanged from round 2**
   ([[PR-070]] filed, still unowned, still not this ticket's to fix).

## Simplification pass — 2026-08-29 (round 3)

n/a — this round is a merge plus a blocked regeneration attempt; no new
application code was written. The merge itself introduced no code changes
of this ticket's own authorship to simplify.

## Review findings — dispositions (round 2), 2026-08-29

(Fourth verifier pass on this ticket by the ticket's own chronology — the
remediation harness labels every lane's current pass "round 2"; kept as
instructed, cross-referenced against this ticket's own round 3 above so
the history stays honest.) Verdict: needs-work. Independently re-ran every
number below before recording it; none are repeated from the verifier
without a fresh run.

1. **[major] PR #614's CI is genuinely RED, not "pending".**
   Independently confirmed: `gh pr view 614 --json statusCheckRollup` shows
   `sql-integration (1)` = `FAILURE` on run `33246469257` (the merge
   commit `48df8f58`), every other check `SUCCESS`/`SKIPPED`; the two prior
   commits' runs (`33243385908`, `33214148886`) were both fully green.
   PR state is `OPEN`/`MERGEABLE` — not merged, no false "ready" claim
   reached the board. **Disposition: fixed (documentation) + risk
   accepted (the failing job itself).** Corrected in
   `post-implementation-report` below rather than left standing. The
   failing job is not this ticket's file — see #2.

2. **[major] Root cause of the CI failure is a flaky assertion in
   `tests/Pegasus.IntegrationTests/PrincipalCredentialPersistenceTests.cs:62`
   (TICK-061's file), not a PLAT-052 regression.**
   Independently verified: `git diff 0a0d9eee 48df8f58 --
   tests/Pegasus.IntegrationTests/PrincipalCredentialPersistenceTests.cs`
   is empty (the file is untouched by this branch or its merge), and
   `git log --oneline` for that file shows exactly one commit, `4aec2703`
   (TICK-061), authored entirely outside this lane. Read the assertion and
   the generator it exercises
   (`src/Pegasus.Core/Cases/PrincipalCredentials.cs:296`,
   `Base64Url`/`SecretBytes = 32`): `firstSecret[..^1] + "A"` is meant to
   produce a *wrong* secret, but the Base64Url tail's last character
   encodes only the final bits of the random payload, so roughly 1 run in
   4 it regenerates the *original* secret and `Assert.Null` legitimately
   fails. This will re-occur at that rate on `sql-integration (1)` for
   every lane in the epic until TICK-061 (or its owner) fixes it — not
   something this ticket can fix without touching another in-flight
   lane's file. **Disposition: reported, not fixed — out of lane
   (`task/tick-061-provider-credentials` is in flight; not this ticket's
   file per the hard rule).** Escalating to the orchestrator explicitly:
   this red job will keep failing ~25% of runs across the epic until
   TICK-061 lands a fix (e.g. asserting `Assert.NotEqual` against a
   guaranteed-different secret, or excluding the one bit pattern that
   round-trips).

3. **[minor] Overclaim in this plan's own round 3, Step 3: "single
   reported error" restates `Test-UiCatalogue.ps1`'s baseline without
   repeating that the real count is 2, contradicting round 2's own
   finding of two defects (`Send.cshtml` uncatalogued +
   `vehicle-images-details--default.html` broken reference) in the same
   plan document.**
   Re-implemented the script's checks read-only and independently got
   `TOTAL ERRORS: 2`, matching the verifier exactly. Round 3's "single
   reported error (the script stops at the first alphabetically)" is
   mechanically true — but by naming only `Send.cshtml` in that step and
   not re-stating the second, already-known error, it reads as if the
   script found one thing when the ticket's own round 2 already
   established two. **Disposition: fixed (documentation).** Restated
   accurately here rather than edited in place: `Test-UiCatalogue.ps1`
   reports exactly one error per run because of its own
   `$ErrorActionPreference = 'Stop'` fail-fast (script line 4, `Write-Error`
   at line 156), and the real, standing count on this branch and on `dev`
   is **two** — `Send.cshtml` (CASE-012's file, PR #615 merged today but
   did not add the catalogue entry) and the `vehicle-images-details`
   broken reference (PR-070, unowned). Neither is newly introduced by this
   PR; neither is touched here.

4. **[minor] `catalogue.json` carries the `EvaSubmission` entry but
   `index.html` does not list it — a real staleness, independent of the
   `StateMatches` blocker.**
   Confirmed: `grep -c "eva-submission" docs/design/test-ui/index.html` →
   0. Already disclosed in round 2 ("Left `docs/design/test-ui/index.html`
   untouched... regenerating it requires the barred script") and again in
   round 3 (blocked behind the unrelated `StateMatches` marker-drift
   finding, which still stands unresolved). **Disposition: accept risk,
   unchanged from round 2/3 — no new action.** Nothing about today's fixes
   (status-token correction, the new `StateMatches` entry) changes this;
   `index.html` regeneration still needs the barred capture script and
   still depends on the round-3-escalated marker-drift fix landing first.

5. **[minor] Stale prototype: the committed
   `administration-principal-eva-submission--default.html` showed
   `status status--neutral` for "Active"; the live render is
   `status status--navy`.**
   Root-caused, not guessed: `src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml`
   — the repo's single source of truth for state-to-tone mapping — maps
   `"active" => "navy"` (line in the `tone switch`), so the prototype
   drifted after this branch's `git merge origin/dev` pulled in changes
   made against `_StatusChip.cshtml` by other lanes since this page was
   originally captured. **Disposition: fixed.** Changed the one class
   token in `docs/design/test-ui/pages/administration-principal-eva-submission--default.html`
   line 170 from `status--neutral` to `status--navy`. This is the only
   difference between the committed file and the verifier's own live
   capture (`artifacts/test-ui-capture/0b042ecd.../response.html`), so no
   other correction is needed.

6. **[minor] Latent hazard this ticket's own test introduced: the denied-
   route test gives the snapshot generator a second capture candidate
   (an error page) for this route, and the catalogue entry carries no
   `StateMatches` key, so today's correct selection is an ordinal-string
   tie-break, not a guarantee.**
   Confirmed the mechanism by reading `TestUiSnapshotTests.cs`'s
   `Generate()`: with no `StateMatches` entry, `stateMatch` is null and
   `otherMatches` is empty (this route has only one catalogued state), so
   `otherMatches.All(...)` is vacuously true and *both* candidates —
   the authorized page and `DirectOrganizationAndPrincipalRoutesDenyNonAdministratorSession`'s
   error-page capture — pass the filter; `.Order(StringComparer.Ordinal)`
   then picks whichever normalizes lexicographically first, which today
   is the real page only because `<title>E...` sorts before `<title>W...`.
   **Disposition: fixed.** Added
   `["administration-principal-eva-submission--default"] = new("EVA API
   submission for WEBP", "We could not complete that request")` to the
   shared `StateMatches` dictionary in
   `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` (appended
   between the existing `administration-mail-categories--default` and
   `case-details--unavailable` entries; nothing reordered). The `Required`
   text is this ticket's own fixture principal code (`WEBP`, seeded by
   `OrganizationAdministrationWebTests.cs`, this ticket's own file), and
   the `Excluded` text is `Pages/Error.cshtml`'s literal heading — the
   same denied-route candidate the hazard was about — so selection is now
   deterministic regardless of future title changes on either side.
   `TestUiSnapshotTests.cs` is shared tooling not in this ticket's
   original `files` list, but `git log --oneline` shows it untouched by
   any of the last 7 merges to land on `dev` today, and the change is a
   single, minimal, non-reordering dictionary entry scoped to this
   ticket's own scenario key and its own fixture data — the same
   append-only discipline as `OperatorLabels.cs`. Flagging this loudly per
   the disposition rule's tier 2 ("fix it anyway... say so loudly") since
   the file is shared, even though the edit itself cannot collide with
   another lane's own scenario key.

### Verification, re-run for this round

- `pwsh -NoProfile -Command "dotnet build ./Pegasus.slnx --configuration Release"`
  — **Build succeeded, 0 Warning(s), 0 Error(s)** (includes the two edits
  above; `TestUiSnapshotTests.cs` compiles with the new dictionary entry).
- `pwsh -NoProfile -Command "dotnet test ./Pegasus.slnx --configuration Release --no-build --filter 'FullyQualifiedName~OrganizationAdministrationWebTests'"`
  — **Passed! Failed: 0, Passed: 2, Skipped: 0, Total: 2**, unchanged.
- `gh pr view 614 --json statusCheckRollup,mergeable,state` and
  `gh run list --branch task/plat-052-eva-submission-route --limit 10` —
  both re-run directly, results recorded in finding 1/2 above.
- The full `Update-TestUiSnapshots.ps1` capture/regenerate pass was **not**
  re-run: it is the orchestrator-owned gate named in the lane brief, it
  takes ~19 minutes, and it is still blocked by round 3's escalated,
  unrelated `StateMatches` marker-drift finding across four other lanes'
  pages — today's two fixes do not touch or depend on that blocker. Both
  fixes were verified by direct code-and-evidence tracing (the
  `_StatusChip.cshtml` source of truth for #5; the `Generate()` selection
  logic plus the verifier's own captured-response bytes for #6) rather
  than by running the barred script.

### Commits pushed

- `1ac0fac6` — `fix(admin): correct stale status token and add snapshot
  discriminator (PLAT-052)`, pushed to
  `task/plat-052-eva-submission-route` (`48df8f58..1ac0fac6`).

## Simplification pass — 2026-08-29 (round 4)

Two one-line/one-entry changes, both reusing existing patterns exactly
(`_StatusChip.cshtml`'s own tone vocabulary for #5; the existing
`StateMatch(Required, Excluded)` record and dictionary shape, already used
by five other entries, for #6). No new abstraction, no new file, nothing
to simplify.
