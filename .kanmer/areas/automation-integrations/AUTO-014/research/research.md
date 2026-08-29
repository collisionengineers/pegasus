# Research — AUTO-014

The audit that produced this ticket is in the ticket body itself and is not
repeated here. This document records what was **verified by a read-only check**
during implementation versus what was assumed, as the repository requires.

## Verified

- **`ListForSubjectAsync` had no production consumer.** `git grep` across `src/`
  returned its declaration (`Core/AiWork/AiJobs.cs:196`), its EF implementation
  (`Infrastructure/Persistence/EfAiJobStore.cs:239`) and two test fakes. Nothing
  else.
- **[[PLAT-049]] does not supply it**, contrary to AUTO-011's `proof/proof.md:249`.
  PLAT-049's page loads `ListOpenAsync()` unioned with `ListRecentAsync(200)`.
  That proof claim was false and is the reason AUTO-011 was reversed.
- **`AiJobKind.QueryResponse` had no Web caller** — only Core mapping, validation
  and construction, the migration check constraint, and one MCP parameter
  description string.
- **`Pages/Mail/**` has no in-flight claimant.** Checked every remote `task/*`
  branch on 2026-08-29; only this lane changes `Message.cshtml(.cs)`. MAIL-025
  owns the folder but is held in `verifying` with no branch.
- **The build and the two new tests**, re-run by the orchestrator rather than
  taken on report.

## Assumed, and why the assumption is safe

- **That the Inbox message's Case tab is the right home for the by-subject
  query.** The ticket said to "prefer an existing record surface over a new page"
  and to name the chosen surface; it did not mandate which. The message's linked
  Case is the record the jobs were raised against, and the operator is already
  looking at it. FRD-11's AI Job List does not forbid a second, record-scoped
  reader.

  **If the design contract intends this list somewhere else** — the Case page
  itself, say — the query is reusable as-is and only the render moves. Nothing
  about the port choice is load-bearing.

- **That a staff-initiated `QueryResponse` job belongs on a linked post-report
  message.** The ticket left open whether the kind is in alpha scope at all,
  naming removal as the alternative if [[TICK-101]]'s activation gate meant it was
  not. The kind is constructed in Core, has a migration check constraint, and is
  named in AUTO-011's own contract — so wiring it was preferred over deleting a
  built capability. **This is the assumption most worth challenging at review**:
  if the operator does not want staff raising query-response jobs in the alpha,
  the correct outcome is removal of the kind and its construction path, not this
  caller.

## Not established

Whether `docs/operations.md` shows the administrator AI switch enabled in the
deployed estate. The control is *conditionally* disabled on that switch, which
is legitimate state under D21 either way — but if the switch is closed in
production, the capability is drawn and not delivered, and AUTO-011's re-audit
must say so rather than counting it.
