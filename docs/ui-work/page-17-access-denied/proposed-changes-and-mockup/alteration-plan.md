# Page 17 — Access denied — alteration plan

## Review summary

The page is structurally fine but tonally wrong: a red "Denied" alarm chip plus an "Access denied"
heading says the same thing twice and treats a routine permission boundary as an incident, and the
two-sentence lede narrates role policy at every reader to serve a rare edge case. The redesign is
a neutral card: one heading, one sentence, one exit.

## Changes

1. **Chip removed**: red lock chip "Denied" → gone. Red returns to meaning primary action or
   genuine failure only; the heading alone states the fact.
2. **Heading**: `Access denied` → unchanged (it is plain and accurate).
3. **Body copy**: *"Your current staff role does not authorize this page or action. If your access
   was changed, sign out and sign in again."* → one sentence: *"Your account does not have access
   to this page."* The sign-out-and-back-in guidance leaves the default page (see Open questions).
4. **Action**: "Return to Pegasus" → **"Return to Dashboard"** (matches the renamed home nav
   item), kept as a neutral secondary button — nothing on this page earns red.
5. **Shell**: full app layout → navless centered card, consistent with the auth/error family. The
   deliberate trade-off: the nav's other destinations are (mostly) still available to this user,
   but family consistency and a single calm exit read better than chrome around a boundary page.

## Dependencies

- Home nav item renamed to Dashboard (IA change owned by the root standards document); until that
  lands the action label follows whatever the home item is called.
- Navless error-card shell shared with page 18.

## Open questions

- Stale-role guidance: "sign out and sign in again" only helps when roles changed mid-session.
  Options: drop it entirely (recommended — administrators communicate access changes), or show it
  only when the app can detect a recent role change for this account (no such signal exists
  today).
- UK vs US spelling ("authorise"/"authorize") needs a single app-wide ruling; the proposed copy
  sidesteps the word.
- Should the shell keep the nav since the session is live? Plan recommends navless for family
  consistency; reverse if operator feedback prefers the escape routes.
