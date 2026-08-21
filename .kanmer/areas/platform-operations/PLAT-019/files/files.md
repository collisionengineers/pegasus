# Files — PLAT-019

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml` | `DialogConsequence` mechanism removed (default sentence, variable, notice render); `Required.` hint and placeholder removed; label, control, required marker, buttons, focus behaviour kept. Inline dialog script moved out (see below) |
| `src/Pegasus.Web/wwwroot/js/site.js` | The dialog binding (open/close/focus trap/Escape/backdrop) now lives here — the deployed CSP (`default-src 'self'`) discards inline scripts, so the partial's script was dead in Production (latent defect found during MAIL-006) |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | The four `DialogConsequence` values gone with the MAIL-006 rebuild; dialog titles name the target |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | No assertions pinned the removed strings; association helpers unaffected |

`grep -r DialogConsequence src/` → 0 matches. Only Mail/Message ever supplied
it; no other screen renders the partial on dev.
