# Page 28 — Workflow configuration: alteration plan

Source: `src/Pegasus.Web/Pages/Administration/Configuration.cshtml`.
Review: `../review.md`. Standards: `../../ui-standards-and-review.md`.

## Review summary

Four booleans rendered twice in two vocabularies, introduced by a self-negating lede
(*"This page contains no credentials, secrets, or cloud controls."*) and padded with an
internal policy key and version integer. The redesign collapses the page to a single form
whose checkboxes both display and edit the state, under one legend that states the business
rule plainly, with a designed conflict state and a last-changed line.

## Changes

1. **Remove the lede entirely.** Old: *"These versioned gates control whether a case can be
   assigned to an Engineer. This page contains no credentials, secrets, or cloud
   controls."* New: nothing — the form's legend (change 3) carries the meaning.
2. **Remove "Policy: case-workflow" and "Version: 1" from display.** Both are internal
   (standards §4.4). `ExpectedVersion` stays as a hidden field.
3. **Collapse the two panels into one form.** The "Current configuration" detail list is
   deleted; the checkboxes are the display of current state. Fieldset legend becomes the
   business rule stated plainly: **"A case cannot be sent to an Engineer until"** with four
   checkboxes:
   - Old *"Instructions are complete"* → **"Its instructions are complete"**
   - Old *"Images are complete"* → **"Its images are complete"**
   - Old *"A staff member reviewed the instructions"* → **"A staff member has reviewed the
     instructions"**
   - Old *"A staff member reviewed the images"* → **"A staff member has reviewed the
     images"**
4. **Add a last-changed line** under the H1 area of the panel: **"Last changed 28 Jul 2026
   14:05 by alex."** (see Dependencies). This replaces the version integer as the human
   answer to "is this current?".
5. **Reason field kept**, label unchanged in intent: **"Reason for this change"**, with hint
   **"Recorded permanently with the change."**
6. **Button label states the effect**: old *"Save workflow configuration"* → **"Save
   requirements"**, with one consequence sentence above it: **"Applies to every case not
   yet sent to an Engineer, from the moment you save."**
7. **Design the conflict state.** When another administrator saved first, render an
   attention status card above the form: **"These requirements changed while you had this
   page open. Reload to see the current settings, then reapply your change."** — moving the
   recovery text from a Razor comment into the operator's view.
8. **One heading stack.** Eyebrow and back link replaced by breadcrumb "Administration /
   Workflow configuration". H1 stays **"Workflow configuration"** (renaming the page is
   raised as an open question, not assumed).
9. **Success state kept**: `TempData["AdministrationStatus"]` status card ("Requirements
   saved.").

## Dependencies

- Last-changed timestamp and actor (change 4) need exposing on the configuration read
  model; the audit trail already records administrator actions, so this is surfacing, not
  new capture.
- Conflict-state rendering (change 7) needs the handler to distinguish the stale-version
  rejection from field validation errors so the page can choose the status card over the
  generic summary.
- London-time formatting for the last-changed line follows the application-wide time rule.

## Open questions

- Page name: "Workflow configuration" is developer vocabulary; **"Engineer assignment"**
  or **"Assignment requirements"** would say the job. Rename touches the Administration
  index card and nav copy — operator decision requested.
- Should unticking a gate (weakening the rule) carry stronger friction than ticking one?
  Standards §4.8 says one confirmation; the plan keeps the single reason field for both
  directions.
