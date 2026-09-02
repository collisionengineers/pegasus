---
kind: proof-record
merged_sha: "2a48be0456e42d22994193b35d6b4cc33bc90a59"
environment: "Detached verification worktree C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-032-2a48be0456e42d22994193b35d6b4cc33bc90a59 at 2a48be0456e42d22994193b35d6b4cc33bc90a59 (detached, clean); Windows 11, .NET 10 SDK, PowerShell 7"
verified_at: "2026-09-02T03:19:43Z"
result: INCONCLUSIVE
failure_class: inconclusive
attempts:
  - attempted_at: "2026-09-02T03:11:00Z"
    command: "gh pr view 640 --json state,mergeCommit,url,mergedAt"
    cwd: "C:\\Users\\PGUSER\\documents\\github\\pegasus"
    exit_code: 0
    result: PASS
    summary: "state MERGED, mergeCommit.oid 2a48be0456e42d22994193b35d6b4cc33bc90a59, mergedAt 2026-09-02T03:11:14Z, url https://github.com/collisionengineers/pegasus/pull/640."
  - attempted_at: "2026-09-02T03:12:00Z"
    command: "git -C <verify-worktree> rev-parse HEAD; git -C <verify-worktree> symbolic-ref --short -q HEAD; git -C <verify-worktree> status --short --branch"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-032-2a48be0456e42d22994193b35d6b4cc33bc90a59"
    exit_code: 0
    result: PASS
    summary: "HEAD = 2a48be0456e42d22994193b35d6b4cc33bc90a59 (matches PR mergeCommit.oid exactly); symbolic-ref empty (exit 1, detached as expected); status short/branch shows '## HEAD (no branch)' with no changes — clean."
  - attempted_at: "2026-09-02T03:14:27Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-032-2a48be0456e42d22994193b35d6b4cc33bc90a59"
    exit_code: 0
    result: PASS
    summary: "Runner evidence (v1-restore.md): restored all 7 projects successfully in locked mode, no errors."
  - attempted_at: "2026-09-02T03:14:37Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-032-2a48be0456e42d22994193b35d6b4cc33bc90a59"
    exit_code: 0
    result: PASS
    summary: "Runner evidence (v2-build.md): build succeeded, 0 Warning(s), 0 Error(s), elapsed 22.74s."
  - attempted_at: "2026-09-02T03:15:05Z"
    command: "dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-032-2a48be0456e42d22994193b35d6b4cc33bc90a59"
    exit_code: 0
    result: PASS
    summary: "Runner evidence (v3-core-tests.md): Passed! Failed: 0, Passed: 1185, Skipped: 0, Total: 1185, Duration 635 ms."
  - attempted_at: "2026-09-02T03:15:14Z"
    command: "dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-032-2a48be0456e42d22994193b35d6b4cc33bc90a59"
    exit_code: 0
    result: PASS
    summary: "Runner evidence (v4-architecture-tests.md): Passed! Failed: 0, Passed: 100, Skipped: 0, Total: 100, Duration 8s."
  - attempted_at: "2026-09-02T03:15:30Z"
    command: "pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-032-2a48be0456e42d22994193b35d6b4cc33bc90a59"
    exit_code: 0
    result: PASS
    summary: "Runner evidence (v5-ui-catalogue.md): Test UI catalogue valid: 54 routed sources, 58 prototypes, 0 broken local references."
  - attempted_at: "2026-09-02T03:16:00Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"Category!=Corpus&Category!=Browser\""
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-032-2a48be0456e42d22994193b35d6b4cc33bc90a59"
    exit_code: null
    result: INCONCLUSIVE
    summary: "Runner evidence (v6-sql-integration.md): LocalDB absent on this host (expected per plan note). All 710 failures trace to Microsoft.Data.SqlClient.SqlException error 52 (Unable to locate a Local Database Runtime installation). Raw: Failed 710, Passed 395, Skipped 3, Total 1108, Duration 1m17s. Substituting evidence: hosted GitHub Actions CI run 33581617718 at PR #640 head 3bf28244 shows sql-integration (1), (2), (3) and sql-integration-coverage all conclusion success, alongside browser and test-ui also success; the merge commit's other parent is origin/dev fbf8ee40 and the merge (2a48be04) was conflict-free, so this CI evidence is directly attributable to the merged tree."
  - attempted_at: "2026-09-02T03:17:15Z"
    command: "gh run view 33581617718 --repo collisionengineers/pegasus --json jobs -q '.jobs[] | select(.name|test(\"sql|browser|test-ui\";\"i\")) | {name, conclusion}'"
    cwd: "C:\\Users\\PGUSER\\documents\\github\\pegasus"
    exit_code: 0
    result: PASS
    summary: "test-ui: success; sql-integration (1)/(2)/(3): success; sql-integration-coverage: success; browser: success. All at PR #640 head 3bf282441ddd3ba8c0355b8e59d06bea3d501cfb, the commit merged conflict-free as 2a48be0456e42d22994193b35d6b4cc33bc90a59."
  - attempted_at: "2026-09-02T03:18:00Z"
    command: "git -C <verify-worktree> merge-base --is-ancestor 2a48be0456e42d22994193b35d6b4cc33bc90a59 origin/dev"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-032-2a48be0456e42d22994193b35d6b4cc33bc90a59"
    exit_code: 0
    result: PASS
    summary: "The merge SHA is an ancestor of origin/dev — the merge has integrated into the dev integration branch (Part 1 completeness)."
  - attempted_at: "2026-09-02T03:18:05Z"
    command: "git -C <verify-worktree> merge-base --is-ancestor 2a48be0456e42d22994193b35d6b4cc33bc90a59 origin/main"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-032-2a48be0456e42d22994193b35d6b4cc33bc90a59"
    exit_code: 1
    result: FAIL
    summary: "Not yet an ancestor of origin/main — Part 2 release reachability does not hold today. Expected: MAIL-032 has not shipped to production. This is the pending fact driving the top-level INCONCLUSIVE, not a code defect."
  - attempted_at: "2026-09-02T03:18:10Z"
    command: "git -C <verify-worktree> merge-base --is-ancestor 2a48be0456e42d22994193b35d6b4cc33bc90a59 0b3ec847aae42ee1c1bee4fb99459f9192534dca"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-032-2a48be0456e42d22994193b35d6b4cc33bc90a59"
    exit_code: 1
    result: FAIL
    summary: "Not an ancestor of production release 37's deployed source SHA (0b3ec847aae42ee1c1bee4fb99459f9192534dca, docs/operations.md release table, deployed 2026-08-30, the estate's current release). Confirms MAIL-032's merge post-dates the currently deployed release and has not shipped. Pending release 38 (or later) will carry it."
  - attempted_at: "2026-09-02T03:18:40Z"
    command: "git -C <verify-worktree> cat-file -e 2a48be0456e42d22994193b35d6b4cc33bc90a59:src/Pegasus.Web/Pages/Mail/Index.cshtml.cs; git -C <verify-worktree> grep -n OnGetPreviewAsync -- src/Pegasus.Web/Pages/Mail/Index.cshtml.cs; git -C <verify-worktree> grep -n restoreSelection -- src/Pegasus.Web/wwwroot/js/site.js; git -C <verify-worktree> cat-file -e 2a48be0456e42d22994193b35d6b4cc33bc90a59:src/Pegasus.Web/Pages/Mail/Index.cshtml"
    cwd: "C:\\Users\\PGUSER\\Documents\\github\\pegasus-worktrees\\verify-mail-032-2a48be0456e42d22994193b35d6b4cc33bc90a59"
    exit_code: 0
    result: PASS
    summary: "Named production caller from the post-implementation report exists at the merge SHA: src/Pegasus.Web/Pages/Mail/Index.cshtml (the routed /Inbox page) and Index.cshtml.cs:204 OnGetPreviewAsync exist; wwwroot/js/site.js:835 defines restoreSelection, wired at :851 and :861 into the pointerleave/blur handlers on the [data-mail-preview-workspace] element (site.js:693). data-mail-preview-workspace present at src/Pegasus.Web/Pages/Mail/Index.cshtml:80."
---

# Proof — MAIL-032

Verification evidence for PR #640's exact GitHub `mergeCommit` SHA
`2a48be0456e42d22994193b35d6b4cc33bc90a59`, gathered in a disposable detached
worktree, never on the mutable `main`/`dev` checkout.

## Part 1 — code evidence at `merged_sha` (holds)

**Merge and worktree assertions.** `gh pr view 640 --json state,mergeCommit,url,mergedAt`
returned `state: MERGED`, `mergeCommit.oid: 2a48be0456e42d22994193b35d6b4cc33bc90a59`
(non-null, full SHA), `mergedAt: 2026-09-02T03:11:14Z`. The detached verification
worktree `C:\Users\PGUSER\Documents\github\pegasus-worktrees\verify-mail-032-2a48be0456e42d22994193b35d6b4cc33bc90a59`
has `rev-parse HEAD` exactly equal to that SHA, `symbolic-ref --short -q HEAD`
empty (detached), and `status --short --branch` clean.

**Runner evidence (controller's test runner, this worktree, this SHA):**

| Lane | Result | Detail |
|---|---|---|
| restore (locked-mode) | PASS | 7 projects |
| build (Release, --no-restore) | PASS | 0 warnings, 0 errors |
| Core tests | PASS | 1185/1185 |
| Architecture tests | PASS | 100/100 |
| UI catalogue | PASS | 54 routed sources, 58 prototypes, 0 broken refs |
| SQL/Browser integration (local) | INCONCLUSIVE | LocalDB absent on this host — all 710 failures are `SqlException` error 52 (no Local DB Runtime), an environment gap, not an assertion failure |

**Substitute evidence for the local SQL-integration gap.** Hosted CI run
`33581617718` at PR #640 head `3bf282441ddd3ba8c0355b8e59d06bea3d501cfb` is
green on every job, confirmed directly: `sql-integration (1)`, `(2)`, `(3)`,
`sql-integration-coverage`, `browser`, and `test-ui` all report
`conclusion: success`. The merge commit's other parent is `origin/dev` at
`fbf8ee40…`, and the refresh merge that produced the PR head was
conflict-free (per the implementer's and reviewer's independently verified
`git log -1 --format=%H %P 3bf28244` — parents `ed19e77f` + `9b8f78a3` — and
name-only diff confined to the 14 `origin/dev` paths), so this hosted result
is directly attributable to the tree that was merged as `2a48be04`, not a
stale or unrelated run.

**Production caller confirmed present at the merge SHA.** The routed `/Inbox`
page `src/Pegasus.Web/Pages/Mail/Index.cshtml` (with
`data-mail-preview-workspace` at line 80) and its `OnGetPreviewAsync` handler
in `Index.cshtml.cs:204` exist at `2a48be04…`. `wwwroot/js/site.js` defines
`restoreSelection` (line 835) and wires it into both the `pointerleave`
handler and the trigger `blur` timeout (lines 851, 861) inside the
`[data-mail-preview-workspace]` enhancement (line 693) — the exact
behavioural fix the ticket describes, reachable from the production route,
not merely present in a test fixture.

**Known limitation, not a failure (reviewer finding F-001, accepted-risk,
owned by MAIL-034).** PR #640 review recorded F-001: the two added
`.row-button[aria-current="true"]` CSS selectors (`site.css:270`, `:653`)
match no element on the Inbox (which carries `aria-current` on the inner
`a.row-title` trigger, not the `.row-button` container), and instead restyle
the unrelated `/Cases` list's selected row. The reviewer verified this
accurately, judged it benign against the plan's stated regression boundary
(no test, snapshot or verification box depends on those selectors; the
Inbox's own behaviour is unchanged), and accepted it as a follow-up rather
than a merge-blocking defect. Recorded here as a known limitation carried
forward with the shipped code, not as evidence against Part 1.

## Part 2 — release evidence (pending)

Reachability against the two ends currently required does **not** hold:

- `git merge-base --is-ancestor 2a48be0456e42d22994193b35d6b4cc33bc90a59 origin/main` → **false** (exit 1). MAIL-032 has not reached `main`.
- `git merge-base --is-ancestor 2a48be0456e42d22994193b35d6b4cc33bc90a59 0b3ec847aae42ee1c1bee4fb99459f9192534dca` (production release 37's deployed source SHA, `docs/operations.md` release table, deployed 2026-08-30, the estate's current release) → **false** (exit 1). MAIL-032 post-dates the currently deployed release.
- `git merge-base --is-ancestor 2a48be0456e42d22994193b35d6b4cc33bc90a59 origin/dev` → **true** (exit 0), confirming the merge is at least integrated into `dev` and eligible to ride the next release.

**What Part 2 will need once a release ships this SHA (or a descendant):**

- Re-run the two reachability checks above against the then-deployed release
  source SHA from the updated `docs/operations.md` release table, and against
  `origin/main`, and both must hold.
- `scripts/Invoke-ProductionSmoke.ps1` reference: per `docs/operations.md`,
  every release's smoke asserts health live/ready 200, an exact version and
  source-SHA match against the release manifest, and an anonymous `/Cases`
  302 to the https sign-in route — the release row for the release that
  carries `2a48be04…` (or its `dev`/`main` descendant) must record this
  smoke having passed against that source SHA.
- **Canary named in the post-implementation report:** the behavioural proof
  is `MailWorkspaceBrowserTests.HoverPreviewRestoresTheSelectedMessageAndKeepsThePaneActionsReachable`
  plus the inverted focus-away assertion in the accessibility/overflow
  browser test, both `Category=Browser` — these are the tests that must be
  green in the release-carrying CI run (already true at PR #640 head
  `3bf28244`, per the `browser` job success above) and are the named
  regression guard for any future change touching this surface.
- **UI ticket — operator visual acceptance still outstanding.** This is a
  UI-behaviour fix (Inbox preview pane restore on pointerleave/blur). No
  operator has yet visually confirmed the deployed behaviour. Recorded as a
  pending manual attempt:

  ```yaml
  - attempted_at: null
    command: "Operator visual acceptance: /Inbox with a message selected and pointer moved off the list — pane still shows selected message with 'Open full message' and 'Open linked Case' reachable; for F-001, /Cases with a row selected."
    cwd: "production (post-deploy)"
    exit_code: null
    result: INCONCLUSIVE
    summary: "Not yet performed — no release has shipped this SHA to production. Route and expected view per the post-implementation report's Verification hand-off section."
  ```

## Not covered

- Local SQL-integration and browser lanes could not run directly on this host
  (no LocalDB, no local Playwright capture in this environment); hosted CI at
  the exact merged tree is the substitute evidence and is treated as
  authoritative per the merge-attribution argument above, not as a lesser
  substitute.
- Part 2 release-to-production evidence: pending. This proof will be replaced
  in full (not appended) once a release carries `2a48be0456e42d22994193b35d6b4cc33bc90a59`
  or a `dev`/`main` descendant of it, the smoke and reachability checks above
  hold, and the operator's visual acceptance is recorded.
- F-001 (CSS selector reach onto the unrelated `/Cases` list) is a known,
  reviewer-accepted limitation owned by a separate ticket (MAIL-034), not
  reopened or re-litigated here.

**Part 2: pending release.**
