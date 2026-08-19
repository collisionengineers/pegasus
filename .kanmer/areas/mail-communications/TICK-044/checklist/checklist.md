# Checklist — MAIL-02

- [x] Revalidate prerequisites and the exact existing Core/Infrastructure/Web helpers to reuse.
- [x] Author and review FRD-08's exhaustive classification criteria/method/evidence/destination/folder catalogue before policy code.
- [x] Implement the minimal Core contract/policy with fail-closed validation.
- [x] Keep the destination deterministic rather than persisting duplicate state; record downstream UI-14/MAIL-23 callers.
- [x] Preserve one Core business-rule owner for later retained-mail and Automation callers.
- [x] Add focused acceptance tests for every taxonomy member, reasoned Other, Ambiguous/Unclassified, and Triage routing.
- [x] Run `dotnet restore` and `dotnet build --configuration Release`.
- [x] Run focused tests and the relevant full Core suite.
- [x] Run and record the four-lens simplification pass.
- [x] Update governing/current-state documentation only to the evidence tier actually reached.
- [x] Write the post-implementation report with commands, results, residual risks and deployment qualification.

- [x] Address MAIL-001: preserve every known classification as a typed operational destination and reserve Other for genuinely novel classifications.

- [x] Wire `MailOperationalDestinationPolicy` into the retained-mail Core projection as the real caller. — *Delivered by [[TICK-045]] / PR #422 (merged to `dev` 2026-08-19, merge `00a6787f`) under [[DELIV-012]]: `Pages/Mail/Message.cshtml.cs` gained a `Destination(MailClassificationResult)` helper calling `MailOperationalDestinationPolicy.Map` as a pure derivation of the already-loaded dossier. Recorded here because the work satisfies this item but landed on TICK-045's diff, not this ticket's branch.*
- [x] Carry the exact classification and derived operational destination to the mailbox list/detail view without duplicate persistence. — *Same PR: no new persisted state; the destination and policy key/version are computed at render time on `/Inbox/{id}`.*
- [x] Display both values in the retained mailbox viewer with distinct fail-closed states. — *Same PR: the "Operational destination" and "Destination policy" rows render inside the existing Classification-evidence panel; the abstention (fail-closed) value renders via `OperatorLabels.MailOperationalDestinationLabel`, which INTK-007 subsequently migrated to the "Unidentified" vocabulary (enum member renamed `MailOperationalDestination.Unidentified`).*
- [x] Add integration/Web tests proving the deployed-shaped viewer path consumes the Core policy. — *Same PR: `MessageDetailShowsTheOperationalDestinationDerivedFromAClassifiedDecision` plus an extension of the existing detail test; both were proven able to fail (helper temporarily broken, both failed on the exact rendered rows, reverted, reran green).*
- [x] Rerun Release build, focused/full tests, simplification pass, and update the post-implementation report after the caller lands. — *PR #422's own verification: Release build 0/0, Core 640/640, targeted integration 31/31 on LocalDB; its plan carries the dated review-response/simplification section. This ticket's own post-implementation report retains its original honest qualification; the caller evidence lives on TICK-045 and in [[DELIV-012]]'s review notes.*

- [ ] After deployment, run and record an authenticated read-only production mailbox-viewer check showing real retained-mail classification and operational destination without any Outlook/cloud mutation. — *Owned by [[DELIV-012]]'s release-12 verification; this is the item that keeps TICK-044 in `verifying` until the deployment is proven.*
