## Independent review — PR #473 at `480f19fef2e4a400fa13623dbb20a759b2fc3f26` (2026-08-20)

### Changes

1. `docs/capabilities.md` records the locally implemented MAIL-13 catalogue prerequisite while leaving mutation, Graph, permission, deployment, and live evidence undelivered.
2. `docs/design/README.md` adds Outlook category display-name administration to the Administrator boundary and alpha Administration flow.
3. `docs/frd/frd-08-email-mailbox-and-background-processing.md` defines one global Active/Disabled exact-display-name allowlist and its Active-only MAIL-13 lookup.
4. `scripts/Invoke-AzureDatabaseBootstrap.ps1` adds Web SELECT/INSERT/UPDATE and DELETE-denial expectations with no Worker grant.
5. `src/Pegasus.Core/Identity/StaffAuthorization.cs` adds the named Administrator-only management right.
6. `src/Pegasus.Core/Intake/ApprovedOutlookCategories.cs` adds the catalogue record/state, management and resolver ports/use cases, validation, and typed failure vocabulary.
7. `src/Pegasus.Infrastructure/DependencyInjection.cs` registers the one EF store behind management and Active-only resolver interfaces plus the Core use cases.
8. `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyEntities.cs` adds the category entity with id, display/normalized names, state, and version only.
9. `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyModelConfiguration.cs` maps one global table and unique normalized-name index.
10. `src/Pegasus.Infrastructure/Persistence/EfApprovedOutlookCategoryStore.cs` implements serializable update/replay, Active-only lookup, disable-in-place, and append-only ActionHistory.
11. `src/Pegasus.Infrastructure/Persistence/Migrations/20260820114412_ApprovedOutlookCategoryCatalogue.Designer.cs` is the generated migration model.
12. `src/Pegasus.Infrastructure/Persistence/Migrations/20260820114412_ApprovedOutlookCategoryCatalogue.cs` creates the one table/index and exact Web grants/DELETE denial.
13. `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` records the catalogue model.
14. `src/Pegasus.Web/Pages/Administration/Index.cshtml` adds one Outlook categories card.
15. `src/Pegasus.Web/Pages/Administration/MailCategories.cshtml` renders add/update/disable forms carrying internal id, expected version, operation key, and reason.
16. `src/Pegasus.Web/Pages/Administration/MailCategories.cshtml.cs` supplies the Administrator-authorized GET/POST caller and conflict/error translation.
17. `tests/Pegasus.Core.Tests/Identity/AutomationActorTests.cs` proves Automation lacks catalogue-management authority.
18. `tests/Pegasus.Core.Tests/Intake/ApprovedOutlookCategoryTests.cs` proves trimmed management input, Administrator update authority, and Active-only casework lookup.
19. `tests/Pegasus.IntegrationTests/AdministrationSearchAccountWebTests.cs` adds route, navigation, antiforgery, denial, and policy-inventory coverage.
20. `tests/Pegasus.IntegrationTests/ApprovedOutlookCategoryAdministrationWebTests.cs` proves non-Administrator GET denial and one Administrator add without Graph/color fields.
21. `tests/Pegasus.IntegrationTests/ApprovedOutlookCategoryPersistenceTests.cs` proves one sequential replay, case-insensitive duplicate refusal, disable-in-place, Active-only resolution, and two history rows.
22. `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs` adds the route to the authenticated axe/markup inventory.
23. `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` updates the committed migration inventory.

`git diff --check` is clean. The 23-file GitHub inventory matches `git diff --name-only origin/dev...HEAD`.

### Comments and disposition

- **Blocking — deferred UI re-entry is not reconciled. Filed as [[PR-026]].** MAIL-13 remains Next / 0.3.0, while the diff adds an active Administration route/card and inserts it into the alpha flow. The cited design authority requires specification, alternatives, independent review, explicit approval, visual generation, and manual visual review before deferred UI implementation. The ticket records the operator's functional request and reuses existing patterns, but does not record the narrow activation/re-entry or the disposition of visual/manual review.
- **Blocking — planned and claimed acceptance evidence is incomplete. Filed as [[PR-027]].** The current tests do not prove Web update/disable, validation, stale conflict, operation conflict, replay/recovery, or denied POST; persistence version/operation conflict, simultaneous retry/concurrency, exact before/after history, or runtime DELETE/no-Worker grants. The Core test also does not cover list authority, invalid ids/input, or actor denial on the resolver. These are explicitly promised by the files/plan/PIR rather than speculative new scope.
- **Blocking — PIR is not exact. Filed as [[PR-028]].** Its grouped table does not account for every changed file and its verification language overstates the tests above.
- **Pass — narrow product shape.** The code implements one global Active/Disabled display-name allowlist. It stores no Graph id/color, mailbox identity, provider metadata, search/link fields, classification taxonomy, or generic settings abstraction.
- **Pass — concrete consumer boundary.** TICK-054 still requires one configured category by internal id; Core `ResolveApprovedOutlookCategory` reloads an Active server-owned name under ordinary casework authority. MAIL-004 performs no message mutation or Graph call.
- **Pass — authorization/composition.** The management use cases require a named Administrator-only right; the page also declares the Administrator policy; Automation is denied management while retaining only the ordinary downstream casework boundary.
- **Pass — persistence shape.** One normalized table, one unique normalized-name index, expected-version update, no delete method, serializable transaction, and append-only ActionHistory reuse the existing administration convention.
- **Pass — grants and exclusions in implementation.** The migration and bootstrap matrix request Web SELECT/INSERT/UPDATE, explicitly deny DELETE, and add no Worker grant. No Graph sync, permission change, external write, deployment, search, linking, or arbitrary editor is present.
- **Non-blocking — ticket body state.** MAIL-004's body-level Verification boxes remain unticked even though the gated checklist is complete; reconcile them with the final evidence during blocker follow-up/closeout.

### Governing-document check

FRD-08's new global allowlist behavior matches the implementation. FRD-12 and the design authority support Administrator-only configuration and prohibit generic rules, Graph metadata, and permanent deletion. The implementation does not change operator truth or require an ADR. The outstanding conflict is the design re-entry procedure recorded in PR-026.

### Report and simplification check

The implementation stays within the plan's Core/Infrastructure/Web boundaries and its handwritten size is proportional; generated migration metadata dominates the 7,631 additions. The four simplification lenses name real reuse, one narrow external persistence boundary, indexed lookup, and correct layer ownership. No generic framework or speculative caller was added. Those dispositions are honest; the missing acceptance proofs and overstated PIR are evidence defects, not concealed simplification work.

### CI

At the reviewed head, changes, documentation, local-development-scripts, reference-data, and infrastructure were green. Unit, browser, and three SQL shards were pending when this needs-changes verdict was recorded. CI cannot make the PR mergeable while the substantive blockers remain.

### Repository review questions

1. **Did the plan miss anything implied by the ticket?** Yes: the active deferred UI needed an explicit design re-entry/visual-review disposition.
2. **Did implementation miss anything in the plan?** The production shape is present, but a material portion of the promised Core/persistence/Web/grant acceptance evidence is absent.
3. **Did the simplification pass run with honest dispositions?** Yes. The design and evidence defects are separate from the simplification lenses.

### Verdict

**Needs changes.** Do not merge PR #473 and keep MAIL-004 in Review. Implement [[PR-026]], [[PR-027]], and [[PR-028]], correct the report, obtain fully green replacement CI, then run independent re-review.
