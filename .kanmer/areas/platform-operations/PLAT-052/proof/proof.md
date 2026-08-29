# Proof — PLAT-052: the EvaSubmission page route is no longer doubled

## Scope of this proof (decision D15)

Written against **merged `dev` at `450b9234a6f5626f21adea3c4da244550a3bdace`**
(2026-08-29 18:03:20 +0100).

This is **dev-merged evidence, pending the single wave-5 `dev` → `main`
promotion**. `main` serves release 36 (`783b4b88`) and still carries the
doubled route. Per D15 the ticket walks to Done on this evidence; the
exact-SHA, non-force promotion to `main` happens once, at wave 5.

## The work is on `dev`

PR [#614](https://github.com/collisionengineers/pegasus/pull/614) merged as
`6ee14bae` ("fix(admin): un-double the EvaSubmission page route (PLAT-052)",
2026-08-29 15:25:33 +0100).

```
git merge-base --is-ancestor 6ee14bae 450b9234   -> exit 0 (ancestor)
```

`git show --stat 6ee14bae` — 5 files, 386 insertions, 2 deletions: the page
template, the catalogue entry, a new captured render, the web test and the
snapshot state constant.

## Capability → production caller

Capabilities enumerated from this ticket's own **What** and **Verification**
sections.

| Capability the ticket names | Production caller | Evidence |
| --- | --- | --- |
| One route, not a doubled one | The page's own route template | `git show 450b9234:src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml` line 1 is now `@page "{organizationId:guid}/{principalId:guid}"` — the trailing `/EvaSubmission` segment is gone. Effective route: `/Administration/Principals/EvaSubmission/{organizationId}/{principalId}` |
| The page is reachable from the Principals index | `Pages/Administration/Principals/Index.cshtml:94` `<a asp-page="EvaSubmission" …>` | the index itself is on the admin rail at `Pages/Administration/Shared/_AdminNav.cshtml:21` |
| The page still performs its work after the route change | `EvaSubmissionModel` at `EvaSubmission.cshtml.cs:22`, calling `updatePrincipalEvaSubmission.ExecuteAsync` at `:88` | the injected port is `IUpdatePrincipalEvaSubmission` (`:24`) |
| The catalogue entry uses the route as it ships | `docs/design/test-ui/catalogue.json:223` | `"route": "/Administration/Principals/EvaSubmission/{organizationId:guid}/{principalId:guid}"` — single-segment, matching the template |
| `OrganizationAdministrationWebTests` URL updated with it | `tests/…/OrganizationAdministrationWebTests.cs` (+30/-…) in the same merge | the change rides the same diff, as the ticket required |

The route is a real production surface: an authenticated administrator
reaches it by a rendered link on a rendered page. No registration-only code
was added.

## The ticket's own verification item

| Item | Status | Evidence |
| --- | --- | --- |
| One route | **PASS** | `EvaSubmission.cshtml:1`, quoted above; `git grep` finds no second `@page` template for this model |
| `Test-UiCatalogue.ps1` and snapshot verify pass | **NOT CLAIMED — see below** | The ticket body itself records that `Test-UiCatalogue.ps1` still exits non-zero on `dev` for two pre-existing reasons this ticket does not own |

The two disclosed non-owned failures were re-checked at `450b9234`:

- `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml` — now present on `dev`;
  catalogue coverage is **CASE-012**'s, not this ticket's.
- `docs/design/test-ui/pages/vehicle-images-details--default.html` — the stale
  reference to the deleted `/VehicleImages` list prototype, filed as
  **PR-070**, still unowned by any in-flight ticket.

Neither is in this ticket's owned file set, and neither was introduced by it.
Per AGENTS.md rule 22 both are recorded here with the disposition **deferred
to their named owners (CASE-012, PR-070)**, not silenced.

## Commands run, with exit codes

Run in the main checkout on `dev` at `450b9234`, Windows + PowerShell 7.

```
dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false
  -> Build succeeded. 0 Warning(s), 0 Error(s).   exit 0

dotnet test ./Pegasus.slnx --configuration Release --no-build
  --filter "FullyQualifiedName~OrganizationAdministrationWebTests"
  -> see the run record appended below
```

CI on the branch head `1ac0fac6` (run 33254423411): **success**, all four
`sql-integration` shards green.

## What this evidence does NOT prove

- **Nothing here is deployed.** `main` is at release 36; the doubled route is
  still what production serves. Tier-2 evidence only.
- **`Test-UiCatalogue.ps1` was not run to a green exit** and is not claimed
  green. It exits non-zero on `dev` today for the two pre-existing,
  non-owned reasons named above.
- **No browser walk.** No claim about rendering, clipping or overflow at any
  breakpoint. **UIIMP-010** owns that.
- **The merge-order hazard with UIIMP-005 (PR #609) is still live.** UIIMP-005
  still carries the old doubled-route catalogue entry on its own branch. When
  it lands, the `catalogue.json` conflict on the `EvaSubmission` entry must be
  resolved by keeping **this** ticket's single-segment route. That is
  UIIMP-005's merge to get right; this proof records the hazard, it does not
  discharge it.
- **PLAT-050 may supersede the page entirely** by folding it into the
  Principal settings dialog. This proof asserts the route is correct today,
  not that the page survives.
