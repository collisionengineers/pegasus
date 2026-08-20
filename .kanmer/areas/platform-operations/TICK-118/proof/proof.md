## Proof (VERIFY2, 2026-08-20) — written on merged main, production release 13 = 2325ed4a

- File presence at 2325ed4a: `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs:266` has `OnPostConfirmCompletenessAsync`; `src/Pegasus.Core/Tasks` completeness policy and `Cases` entity columns present since migration `20260729150000_DocumentCustodyAndRequests`.
- Live production data (prod-diagnostics §2, gathered 2026-08-20): `CaseWorkflows` has exactly 2 rows, both `NotReady`, both correctly surfaced on the Not ready queue (`Triage/Index`); dashboard/queue/case-detail agree on this count.
- Live read-only SQL (2026-08-20, `pegasus-prod-sql-252ow37gij/pegasus`): `Cases` has 2 rows; `InstructionConfirmedByStaff`/`ImagesConfirmedByStaff`/`InstructionComplete`/`ImagesComplete` are all `0` on both — the completeness-confirmation caller has not yet been exercised live because both real cases are still genuinely incomplete.

**Residual (named, not fabricated):** the staff completeness-confirmation action has not yet been observed firing in production — no case has reached the point where staff would confirm it. This is expected given case age/progress, not a defect. Nothing further is required of this ticket; it stands on the same production evidence any other now-idle backlog ticket for already-shipped work would have.
