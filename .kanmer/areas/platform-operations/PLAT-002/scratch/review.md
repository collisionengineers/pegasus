# Review — PLAT-002 / PR 467

Independent review. Author is Codex (`codex-mcp-client` on `task/plat-002-staff-page-root`); reviewer is Grok. Not an author-review.

Checked: ticket body, research, files, plan, checklist, open-questions, post-implementation-report, execute scratch, PR 467 (24 files, +104/−255, head `62502995`), worktree source, `StaffActorFactory`, authorization on migrated pages, inventories, architecture-test guard, governing-docs claim, four-lens dispositions. Strict maintainability pass (bundled code-review) applied to the same diff.

## Changes

- `src/Pegasus.Web/Pages/StaffPageModel.cs` — new metadata-free `PageModel` adapter: the only Web `StaffActorFactory.TryCreate` call, plus public static GUID-N `NewOperationKey`.
- `AdministrationPageModel`, `CaseMutationPageModel`, `UploadConfirmationPageModel` — now derive from that root; duplicate actor/key helpers deleted. Admin keeps `IsOperationKeyValid`. Case mutation keeps lease/TempData/command wrappers. Upload confirmation keeps the shared decision handlers.
- Seventeen former direct actor callers now inherit `StaffPageModel` (or already did via a migrated base). Inline factory clauses become `TryGetActor`; local SubjectId→Guid overloads remain on PasswordChange and Triage/Details; Upload still mints `ExternalReceiptToken` locally.
- `Uploads/Request` stays `[AllowAnonymous]` on `PageModel` and calls `StaffPageModel.NewOperationKey` statically.
- `DependencyDirectionTests.WebPagesHaveOneStaffActorAndOperationKeyOwnerPerConcept` — source-scan: one factory site, exactly two GUID-N sites in Pages `*.cs`, Request remains anonymous and outside the staff type.
- `docs/current-architecture.md` — as-built note for the root and the two deliberate exceptions.

No Core, authorization-metadata, receipt-token, routing, or handler-result change in the diff. Razor still resolves `CaseMutationPageModel.NewOperationKey` via the inherited public static.

## Report against diff

The post-implementation report's file table matches the PR file set (24 files). Rationales are honest. Inventories claimed (one factory, two GUID-N `*.cs` sites, anonymous Request) match the worktree. Verification commands and 98 architecture / 114+6 focused-integration results are recorded; the timed-out combined wrapper is disclosed.

## Governing docs

Chore profile; no linked PRD/FRD/ADR; none required. No durable architecture decision changed. `docs/current-architecture.md` is the allowed as-built refresh. Plan's Governing-docs section holds.

Open questions: the one question is ticked (complete consolidation). Parked: none.

## Code-quality (strict)

The structural move is the right one: a common parent over the two (now three) existing abstract bases, not a new helper convention. `TryGetActor` needs `User`; putting it on a `PageModel` subclass is the existing local pattern, not a new one. The root is 18 lines and earns its keep as the single adapter. No file crosses 1k lines (largest touched, `DependencyDirectionTests`, ~430 lines). No new ad-hoc branches in unrelated flows. FindFirst vs FindFirstValue unification is behaviour-preserving. Nullable `[NotNullWhen(true)] out ActionActor?` is the tidier signature the ticket asked for.

No code-judo that would delete a whole layer is sitting unused. An extension method on `PageModel` would be a third convention next to the existing bases. A neutral key utility was considered and rejected in research for lack of a second policy; the anonymous page is the awkward second caller and is called out in architecture tests rather than hidden.

## Comments

1. **Non-blocking — leftover `using Pegasus.Core.Actors`.** After the factory call moved, `Intake/Details`, `Operations/Index`, `Mail/Message`, `Account/PasswordChange`, and `Triage/Details` still import `Pegasus.Core.Actors`. `ActionActor` lives in `Pegasus.Core.Identity`; only `StaffPageModel` still needs the Actors namespace. SDK does not fail the build on unused usings (`IDE0005` is not a compiler warning here), so the plan's "remove only compiler-proven unused usings" rule left them.
2. **Non-blocking — `IsOperationKeyValid` still has two owners.** `AdministrationPageModel` and `Cases/Assessment/Index` each keep an identical GUID-N validator. The plan explicitly retained both. Lifting the one-liner onto `StaffPageModel` would be a later cleanup, not a regression this PR introduced.
3. **Non-blocking — Razor still inlines GUID-N operation keys.** `Intake/Details.cshtml`, `Administration/Roles/Index.cshtml`, and `Administration/Mailboxes.cshtml` still write `Guid.NewGuid().ToString("N")` into hidden fields. Ticket inventories and the architecture guard are `*.cs` only; Case Razor already calls the inherited static. Out of this chore's stated surface.
4. **Non-blocking — two local staff-Guid overloads with swapped parameters.** PasswordChange is `TryGetActor(out ActionActor, out Guid)`; Triage/Details is `TryGetActor(out Guid, out ActionActor)` and also rejects `Guid.Empty`. Planned preservation of local parsing, including the extra empty-Guid check on Triage. A third caller would justify lifting; two is not enough to demand it in this PR.
5. **Non-blocking — anonymous Request names the staff type.** `StaffPageModel.NewOperationKey()` from an `[AllowAnonymous]` page is a type-level coupling the architecture test documents rather than a staff inheritance leak. Accepted trade-off vs a one-line utility.

No blocking comments.

## Disposition

1. leftover Actors usings — won't-do-because: not a compiler failure; plan scoped using cleanup to compiler-proven unused; not worth a PR-review ticket.
2. duplicate `IsOperationKeyValid` — won't-do-because: planned retention; behaviour-preserving; optional follow-up only.
3. Razor GUID-N copies — won't-do-because: outside the ticket's `*.cs` inventory and architecture guard; pre-existing view pattern.
4. swapped Guid overloads — won't-do-because: planned local parsing; Triage's empty-Guid extra check is real and must not be silently unified.
5. Request → StaffPageModel static — won't-do-because: explicit complete-consolidation choice, guarded by test.

## Verdict

**Pass.** Report matches diff; governing-docs claim holds; open questions resolved; code is the planned one-root consolidation without behaviour change; simplification pass is honest; no structural regression under the strict maintainability bar. Merge when `repository-check` is green, then move to verifying.

Merged PR 467 into `dev` at 2026-08-20T09:33:33Z (merge commit `a3c88a7bbdb43cf4cbd9303022397f6e028d7bf9`). repository-check green (infrastructure path-skipped). Ticket moved to verifying.
