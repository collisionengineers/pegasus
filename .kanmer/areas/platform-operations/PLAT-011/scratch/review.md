## Independent review — PR #452 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green CI.

- One Core owner for the concept: `ActorDisplayNames.Resolve(kind, subjectId, staffNames)` with the honest `"Unknown user"` fallback — never a GUID — wired into the three existing query owners (GetCase, GetTriage, GetRetainedMail) rather than page-side lookups; automation client names resolved at the Web layer where that identity actually lives (OpenIddict). Correct altitude on both counts.
- The sweep found and fixed three leak sites beyond the two the ticket named (_CaseHistory, Triage/Details, Mail/Message) — all through the same owners, listed in the files doc.
- Read-model defaults use the same fallback so no path can render an unset name; the mail history's persisted `"{kind}:{subjectId}"` convention got a parse helper instead of a second string format.
- Tests pin the no-GUID rule per surface (including a case-history no-GUID assertion); Core 701/701, a11y 24/24 incl. the Activity page. Simplification pass applied its one finding.
