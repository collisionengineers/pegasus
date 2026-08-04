# Page 17 — Access denied (`/Account/AccessDenied`)

Screenshot: `access-denied.png`. Source: `src/Pegasus.Web/Pages/Account/AccessDenied.cshtml`.

Current facts: inside the full authenticated layout, a centered card containing a red status chip
(`status-chip--red`) with a lock icon and the word **"Denied"**, `<h1>Access denied</h1>`, lede
*"Your current staff role does not authorize this page or action. If your access was changed, sign
out and sign in again."*, and a hairline secondary button **"Return to Pegasus"** linking to the
home page.

## 1. Aesthetics

- **The red alarm treatment is wrong for what this page is.** A Reviewer clicking an
  administration link is the routine, expected case — a permission boundary working as designed,
  not an incident. Red is the scarcest colour in the system (primary action + genuine failure);
  spending it on a lock chip teaches operators that red sometimes means "nothing is wrong".
- Chip and heading say the same thing twice — "Denied" then "Access denied" — violating the
  one-heading-stack rule (§4.7). The chip adds an icon and colour but no information.
- The card composition itself (panel, spacing, secondary action) is otherwise consistent with the
  auth family and needs no structural rescue.

## 2. Practicality

- The lede is two sentences of policy narration. The first restates the heading in role-speak
  ("Your current staff role does not authorize this page or action"); the second — "sign out and
  sign in again" — is speculative guidance shown to everyone, though it only helps the rare
  operator whose roles changed mid-session. Most readers need one fact and one exit.
- "Return to Pegasus" is a sound, calm action — but the operator is *in* Pegasus; the link goes to
  the home page, and after the IA rename the honest label is "Return to Dashboard".
- "authorize" is US spelling in a UK business's copy; the proposed sentence avoids the word rather
  than adjudicating the spelling here (flagged as an open question for app-wide copy).

## 3. Performance / design / good practice

- Server-rendered, no logic, no identifiers leaked, nothing sensitive echoed about *what* was
  denied — all correct; keep.
- Rendering inside the authenticated layout is defensible (the user has a live session), but the
  error/auth card family standard for the mockups is the navless centered card; the single
  "Return to Dashboard" action is the designed exit either way.
- No accessibility faults beyond the chip: colour + icon carry the "denied" signal redundantly
  with the text, which passes the not-colour-only rule — the chip's problem is tone and
  duplication, not accessibility.
