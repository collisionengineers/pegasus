# Proof — PLAT-047: FRD-01 and FRD-04 wording: workflow display labels, one Principals area, Action Logs

## What was verified, and where

Verified in the primary checkout `C:/Users/PC/Documents/GitHub/pegasus` on
`dev` at `b92cb9a7` (`git rev-parse HEAD` →
`b92cb9a7b8bf7727b452aa397d9df04084da1270`). PR #583 — "PLAT-047: FRD-01/FRD-04
wording for display labels, one Principals area and Action Logs" — merged
2026-08-28T08:24:14Z as `58a578a6`, base `dev`, head
`task/plat-047-frd01-frd04`. `git merge-base --is-ancestor 58a578a6 b92cb9a7`
exits 0, so the merge is reachable from the verified tip, as are all four
recorded commits (`8f45f36d`, `ce56c28a`, `16b7e884`, `64534588` — each
`--is-ancestor … HEAD` exits 0). This is a docs-only ticket; the merge changed
three files and nothing else.

```
git diff --stat 58a578a6^1 58a578a6
 docs/frd/frd-01-case-identity-and-lifecycle.md     | 45 ++++++++++++++++++++
 docs/frd/frd-04-parties-accounts-and-access.md     | 49 +++++++++++++++++++++-
 ...eports-correspondence-and-reviewed-proposals.md | 12 +++---
 3 files changed, 100 insertions(+), 6 deletions(-)
```

## Evidence

### FRD-01 carries the D3 workflow display-label map

Tier: **merged documentation on `dev`** — the deliverable itself.

`docs/frd/frd-01-case-identity-and-lifecycle.md:83-103`:

```
83:### Workflow display labels and stage-bound actions
85:Core lifecycle states are unchanged. The operator sees display labels only,
86:owned by the single code-to-words map
87:`Pegasus.Web.Presentation.OperatorLabels` named in the design README's
88:[Enforced presentation rules](../design/README.md#enforced-presentation-rules):
90:| Core state | Display label |
94:| `Report preparation`, `Post report` | With Engineer |
95:| `Post-report complete` | Complete |
96:| `Held` | Held (exception, never a workflow step) |
97:| `Provider cancelled`, … source e-mail unlinked | Closed · `<outcome>` |
99:The Cases workflow rail lists Not ready, Review, With Engineer and Complete,
100:with Held as an exception group; the other terminal outcomes never appear in
101:that rail and render as Closed · `<outcome>` in Search. A label is never a
102:state: every transition remains a named Core action, and history records the
103:Core state, not the label.
```

The stage-bound actions follow at `:105-127` — Send to EVA (Review), Assessment
(deferred to FRD-11), Report sent, Return to Engineer, Close Case.

### The label map the FRD names is real, and has production callers

Tier: **build/test** for the code's existence; **registration plus named
caller** for the wiring. Not a deployment claim.

The FRD's named owner exists at `src/Pegasus.Web/Presentation/OperatorLabels.cs`
and its switch matches the FRD table:

```
134: public static string CaseStage(CaseLifecycleState state) => state switch
139:     CaseLifecycleState.ReportPreparation or CaseLifecycleState.PostReport => "With Engineer",
140:     CaseLifecycleState.PostReportComplete => "Complete",
141:     CaseLifecycleState.ProviderCancelled => "Closed · Provider cancelled",
144:     CaseLifecycleState.SourceEmailUnlinked => "Closed · E-mail unlinked",
```

`git grep -n "OperatorLabels.CaseStage" -- src/` returns render sites, not
registrations only — among them
`src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:90-95` (the Cases workflow rail:
Not ready / Review / With Engineer / Complete, Held in the Exceptions group —
exactly the rail composition FRD-01:99-101 specifies),
`src/Pegasus.Web/Pages/Search/Index.cshtml.cs:318`,
`src/Pegasus.Web/Pages/Cases/Details.cshtml:36`,
`src/Pegasus.Web/Pages/Mail/Message.cshtml:409`. That code was shipped by other
tickets; it is cited as corroboration that this FRD names a real map, never as
PLAT-047's own delivery.

### FRD-01 records D10 as evidence-driven, entering post-report work, not closure

Tier: **merged documentation**, cross-checked against protected operator truth
and against Core.

`docs/frd/frd-01-case-identity-and-lifecycle.md:113-122`:

```
- **Report sent** is evidence-driven; no manual "sent" assertion exists. …
  Either path enters post-report work, still displayed as With Engineer. The
  Case action offers only confirmation of detected evidence;
  `Post-report complete` remains the separate, reasoned closure that ends
  post-report work.
```

That matches `docs/operator-notes.md:202` (protected, unedited by this ticket):

```
A retained acknowledgement, source receipt, outbound message record, or
`Report sent` event is not post-report completion. Report sent enters
post-report work; the separately named, reasoned closure outcome ends it.
```

And it matches Core, which this ticket did not touch —
`src/Pegasus.Core/Lifecycle/CaseLifecycle.cs:199-201`:

```
throw new InvalidOperationException(
    "Exact report-Sent evidence can enter post-report work only from Report preparation.");
```

### D10 itself was corrected on the board by this ticket

Tier: **board record with a commit SHA.**

The brief had extended D10 to a detected send "completing the Case". PLAT-047
stopped on that against operator-notes:202 and the decision was rewritten.
Board worktree `.worktrees/kanmer`, commit `ccfd59fb` ("chore(kanmer): sync
board 2026-08-28T08:15:43.586Z"), `.kanmer/groups/EPIC-011/context.md`:

```
-| D10 | "Report sent" is evidence-driven: sent from Pegasus auto-links; sent
       through EVA is detected and attached; no manual assertion. |
+| D10 | … Per protected operator-notes (~line 202) a linked Sent item enters
       **post-report work** (still displayed "With Engineer");
       `Post-report complete` stays the separate reasoned closure via
       "Close Case". … (Corrected 2026-08-28 by PLAT-047.) |
```

The same commit propagated the correction into the §1.8 Case workspace
contract: "**Report sent** (primary, With Engineer — confirm detected Sent
evidence, D10; enters post-report work, does not close)". Its timestamp sits
inside PLAT-047's working window (taken 08:13:02Z, review 08:14:35Z, verifying
08:24:21Z). The sibling routine-call correction — "the dialog exposes the two
ADR-0034 toggles rather than a three-value select (PLAT-047 review,
2026-08-28)" — is board commit `7c7d7243`.

### FRD-11 carries the D11 Assessment-access wording, and shipped Core cites it

Tier: **merged documentation**, plus **build/test** for the consuming code.

`docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md:101-107`:

```
The Assessment workspace is available once the Case has entered `Report
preparation` or later (displayed "With Engineer") and a successful EVA export
or submission exists for the current Review cycle. It is never available in
`Not ready`, `Review` or `Held`; it is editable in `Report preparation` and
`Post report`, read-only in `Post-report complete`, and unavailable in the
other terminal outcomes.
```

`src/Pegasus.Core/Assessment/AssessmentWorkspace.cs:30-61` implements that and
names this FRD as its authority:

```
30: /// D11 (FRD-11): the workspace opens once the case is With Engineer
31: /// (Report preparation or later) and a current-cycle export exists.
45: public static bool CanOpen(AssessmentAccessState access)
48:     return access.State
49:             is CaseLifecycleState.ReportPreparation
50:                 or CaseLifecycleState.PostReport
51:                 or CaseLifecycleState.PostReportComplete
52:         && access.LatestExportVersion is { } exportedVersion
53:         && exportedVersion >= access.LatestReviewVersion;
59:     return access.State == CaseLifecycleState.PostReportComplete;   // IsReadOnly
```

That code arrived later, in `7b919b69` ("feat(assessment): D11 access policy —
With Engineer or onwards, read-only once Complete (ENG-025)", merged as
`21b35398`, PR #616). `git merge-base --is-ancestor 58a578a6 7b919b69` exits 0:
PLAT-047's wording preceded and is cited by the implementation. Widening the
owned files to FRD-11 was review finding F1 on PR #583; this is the evidence
that widening was right — FRD-11 is the one owner and FRD-01:110-112 only
cross-references it.

### FRD-04 carries one Principals area, the settings dialog, Staff accounts and Action Logs

Tier: **merged documentation.**

`docs/frd/frd-04-parties-accounts-and-access.md`:

- `:27-37` `### Principals administration` — "One **Principals**
  administration area lists every principal code with its organisation name,
  roles, state, and a Settings action. The organisation remains the reusable
  directory identity and the owner of case-party roles… **Create Principal**
  creates the backing organisation inline (name and roles) and allocates the
  code in one action" (D2).
- `:39-54` the settings dialog: route e-mail addresses read-only (owned by
  FRD-09); "the two EVA API submission settings (manual, automatic) owned by
  FRD-07 and ADR-0034; ZIP export needs no setting" — review finding F2, which
  replaced an earlier three-value policy enumeration; and the Provider API
  credential (D8, API-04), secret shown once and hash-only retention.
- `:59-66` `### Staff accounts` — Save enabled only once the role changed and
  requiring a reason; Create / Disable / Review; "An account never disables or
  reviews itself."
- `:74-80` — "**Action Logs** is the one administration view over permanent
  action history and the security log… there is no separate Access review or
  Automation Activity page." No leftover contradiction survives: `git grep -n
  -i "access review page\|Organisations administration" -- docs/` returns
  nothing, and "access review" remains in FRD-04 only as an Administrator
  *action* in the role matrix (`:19-21`), which is what D2 intended.
- `:19` role-matrix row rewritten per review finding F4: the Administrator
  must-not now reads "Pegasus's own credential-secret, cloud, or release
  administration through the staff UI", and the may-change column adds
  "including a Principal's Provider API credential lifecycle".

### The shipped text is still the shipped text

Tier: **command output.**

`git diff 58a578a6 b92cb9a7 -- <the three FRDs>` shows only three later,
unrelated edits: two FRD-01 edit-lease paragraphs (CASE-024 `bd08df8a`,
KANMER-005 `45a43b63`) and a three-line extension of PLAT-047's own Provider
API credential bullet by TICK-061 (`c0a55807`, PR #592: reset of a paused
credential, reissue after revocation). The label table, the D10 bullet and the
D11 paragraph are untouched since the merge.

### Every cross-reference the new text adds resolves

Tier: **command output.**

```
pwsh ./scripts/Test-DocumentationLinks.ps1
  -> All relative Markdown links resolve (129 files checked).
  -> exit 0
```

That script checks link *paths* only — its own header says "External URLs and
same-file anchors are not checked" — so each anchor the added text cites was
checked independently against the target files' headings, all resolving:
`design/README.md#enforced-presentation-rules`,
`design/README.md#operator-experience-requirements`,
`frd-07…#eva-and-external-engineering-handoff`,
`frd-07…#direct-eva-api-submission`, `frd-11…#report-draft-entry-point`,
`frd-08…#outbound-correspondence-evidence`,
`frd-01…#principal-reference-organisation-and-case-party-identity`,
`frd-09…#provider-and-intermediary-routes`,
`frd-09…#provider-api-principal-and-contract-boundary`, and the file
`docs/adr/0034-per-principal-eva-api-submission-settings.md`.

Both edited FRDs sit in the governance chain: `docs/index.md:11` routes
behaviour questions to `docs/frd/README.md`, and `docs/capabilities.md` names
them canonical owner for ACC-01…ACC-11 (`:84-94`) and CASE-01…CASE-10
(`:130-137`).

### Build and test tier

Tier: **build/test**, cited from the canonical gate evidence for merged `dev`
at `b92cb9a7`, not re-run here: `dotnet restore --locked-mode` exit 0;
`dotnet build --configuration Release` "Build succeeded. 0 Warning(s), 0
Error(s)"; `dotnet test … --filter 'Category!=Corpus&Category!=Browser'` →
ArchitectureTests 100/100, Core.Tests 1133/1133, IntegrationTests 1022 passed
with 2 pre-existing unrelated skips, 0 failed. PLAT-047 changed no compiled
file, so no test bears on it directly; the suite is cited to show the tree it
merged into is green.

PR #583's own CI (`gh pr checks 583`) is consistent with a docs-only change:
`documentation` **pass** (27s), `changes` **pass**,
`local-development-scripts` **pass**, `reference-data` **pass**; `unit`,
`browser`, `infrastructure`, `sql-integration` and
`sql-integration-coverage` **skipping** via the change-flags gate.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| No Core state is renamed; only display labels are specified | Proven | `git diff --name-only 58a578a6^1 58a578a6` lists three `docs/frd/*.md` files and nothing under `src/` or `tests/`; `CaseLifecycleState` at `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs:11-23` still holds the same ten members (`NotReady, Held, Review, ReportPreparation, PostReport, PostReportComplete, ProviderCancelled, CollisionEngineersRejected, CreatedInError, SourceEmailUnlinked`); FRD-01:85 states "Core lifecycle states are unchanged" |
| `scripts/Test-DocumentationLinks.ps1` passes | Proven | `pwsh ./scripts/Test-DocumentationLinks.ps1` on `dev` at `b92cb9a7` → "All relative Markdown links resolve (129 files checked)", exit 0; PR CI `documentation` job also passed. Anchors are outside that script's scope and were checked separately above |

## Outstanding

- **The Administration surfaces FRD-04 now specifies are not yet shipped.** On
  `dev` at `b92cb9a7` there is no Action Logs page — `git grep -ln
  "ActionLogs" -- src/` returns nothing — and
  `src/Pegasus.Web/Pages/Administration/` still contains `Access/`, `Roles/`
  and `Organizations/` alongside `Principals/`. This is not a defect in
  PLAT-047, which owns FRD text only, and an FRD states required behaviour
  ahead of the code; it is recorded so nobody reads this proof as a delivery
  claim. Owners: **CASE-028** (`ActionLogs.cs`, its composite-index migration
  and the `ReviewActionLogs` right, per the 2026-08-29 EPIC-011 decisions),
  **PLAT-027** (`Administration/Accounts/**`, `Access/**`, `Roles/**`) and
  **UIIMP-009** (removal of superseded surfaces).
- **Review finding F4's wording is unconfirmed by the operator.** The plan
  records the Administrator "credential" reinterpretation as "for the operator
  to confirm" and it was stated in the PR body. `docs/operator-notes.md:400` is
  untouched, but no operator confirmation of the reading is on the record.
  Unproven here; it needs an operator answer, not another agent's judgement.
- **D11 versus `docs/operator-notes.md:559-560`.** The protected note says the
  Assessment workspace "is unavailable while a case is `Not ready`. It opens
  only after a successful EVA export in the current Review cycle." FRD-11 as
  shipped adds "never available in `Not ready`, `Review` or `Held`". The note
  never grants Review access, so this is a narrowing rather than a
  contradiction and the note was not edited — but the narrowing rests on
  decision D11, not on operator-notes, and is flagged as it was in the plan.
- **Layout/overflow at 1580/1100/760 does not apply.** PLAT-047 renders no UI;
  the browser walk owned by **UIIMP-010** has nothing to check for this ticket.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.
