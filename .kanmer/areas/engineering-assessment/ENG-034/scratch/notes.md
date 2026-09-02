2026-09-02 (wrapper, second pass): an earlier run had already executed Codex
(gpt-5.6-terra xhigh, `.worktrees/research` at cad00be9, checkout clean) and
written `research`, but stopped before `files`. Reused that run's Document 2
from the scratch output rather than re-running Codex; added the two
`docs/design/test-ui/**` rows the research wrapper-check called for, and
recorded the out-of-lane Assessment-route callers found by
`grep -rn 'Cases/Assessment|/Assessment' src/Pegasus.Web` (Details.cshtml:276
→ CASE-038; Operations/Index.cshtml.cs:427 → still works through the 301).
No operator questions.

2026-09-02 (wrapper, third pass — currency check only): `research` and `files` already existed and match the ticket body (D30, 301 to `/Cases/{id}?section=estimate`, five sections, ENG-028 reuse, read-only once Complete, catalogue update). Confirmed `origin/dev` is still `cad00be9d` (= `.worktrees/research` HEAD, checkout clean, no commits since) and re-spot-checked five VERIFIED claims in the main checkout: `AssessmentAccessPolicy.CanOpen`/`IsReadOnly` (`AssessmentWorkspace.cs:45-59`), catalogue `visual` entry for `Pages/Cases/Assessment/Index.cshtml` (`catalogue.json:286-292`), `Suggestions.cshtml:37` Back link, `Details.cshtml:274-281` Open Assessment, `RedirectPermanent` stubs (Triage/Unidentified/Cases Index), `_CaseValuation.cshtml` absent, `Test-UiCatalogue.ps1` lines 20/37. Codex not re-run. Feature profile leave-preparing still needs `plan` and `checklist`. No operator questions. Note: kanmer MCP read tools returned only the project header this session; board state was read from `.worktrees/kanmer/.kanmer` directly.
