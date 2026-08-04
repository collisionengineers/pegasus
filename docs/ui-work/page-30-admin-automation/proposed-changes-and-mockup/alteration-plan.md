# Page 30 — Automation: alteration plan

Source: `src/Pegasus.Web/Pages/Administration/Automation/Index.cshtml`.
Review: `../review.md`. Standards: `../../ui-standards-and-review.md`.

## Review summary

A whole page whose only job, in every deployment Pegasus currently ships, is to explain
that it does not exist. The feature gate `Features:AutomationMcp` is set in no shipped
configuration file, so `Index.cshtml:25-28` always takes the false branch and the page's
entire content is one sentence of architecture narration. Standards §4.9 is unambiguous:
a capability that is not composed in a deployment is **absent**, not narrated. The primary
change is therefore removal — hide the Administration card and this route while the gate is
off — and the rest of the plan specifies what the page should look like on the day the gate
is on, so that turning it on does not ship the current copy.

## Changes

1. **Hide the page and its Administration card while the gate is off.** Today the
   Administration index always renders an Automation card, and this route always renders,
   producing the screenshot: a page-length explanation of its own absence. New behaviour:
   the card is not rendered and the route returns the styled not-found page when
   `Features:AutomationMcp` is off. The inert-state paragraph — *"The Automation ingress is
   not composed in this deployment: the configuration gate is off, no endpoint or token
   route exists, and no automation action is possible."* (`Index.cshtml:27`) — is **deleted
   outright**, not reworded. There is no operator decision behind it and no action it
   enables. Everything below describes the composed state only, which is the only state
   that should ever render.
2. **Delete the lede.** Old: *"One named Automation actor may call approved ordinary case,
   … and document actions through its own authenticated machine ingress. It is not a
   staff account, holds no administration authority, and every action it takes is
   permanently recorded. Client secrets are never shown here."* (elided: the quotation
   carries a banned term.) New: no lede (standards
   §4.1). The H1 stays **"Automation"**; the panel's facts are the explanation. The one
   sentence worth keeping is a consequence, and it moves to the control it concerns
   (change 6). "Client secrets are never shown here" is deleted — a page does not disclaim
   what it does not contain.
3. **Reorder the registration facts, human first.** Old order: Client identifier · Display
   name · State · Granted scopes. New order:
   - **Name** — the registration's display name (schematic: *Pegasus Automation*)
   - **Status** — chip, **On** / **Off** (old: "State" / "Enabled" / "Disabled";
     "On"/"Off" matches the control that changes it)
   - **Can use** — the permitted areas (change 4)
   - **Client identifier** — kept last, and kept, because administrators match it against
     the external configuration that calls Pegasus. This is a name the operator typed, not
     a generated identifier, so standards §4.4 does not exclude it.
4. **Label the permitted areas.** Old: `@string.Join(", ", registration.GrantedScopes)`
   (`Index.cshtml:47`) — a comma join of machine scope strings such as `automation.cases`
   and `automation.documents`. New: a hand-labelled list, one line per area, each with the
   job it permits:
   - **Cases** — find and open cases, and record case edits
   - **Evidence** — add, download and export case files, images and e-mail
   - **Inbox** — read received items and submit uploads

   The map lives in the page model (the pattern of `ActivityModel.RecordTypeLabel`), never
   in the view, and an unmapped scope is a build-time gap, not a raw string on screen
   (standards §4.3).
5. **Rename the toggle.** Old buttons: *"Disable the Automation client"* /
   *"Enable the Automation client"* → **"Turn off automation"** / **"Turn on
   automation"**. "Client" is the machine's word for itself.
6. **Move the consequence sentence above the button and rewrite it.** Old, positioned below
   the button (`Index.cshtml:67`): *"Disabling refuses new tokens immediately and rejects
   requests that present an already-issued token within seconds. Both changes are recorded
   as attributable administrator actions."* New, sitting between the Reason field and the
   button, separated by a hairline: **"Turning automation off stops it within seconds.
   Your name and reason are recorded permanently."**
7. **Reason field keeps its hint.** Label **"Reason"**, hint **"Recorded permanently with
   the change."** — the same wording as page 29, so the two administration forms read as
   one family.
8. **Collapse the Activity panel into a link row.** The current second panel
   (`Index.cshtml:71-75`) is a whole section containing one sentence — *"Every Automation
   action and every denied automation request is recorded in permanent history with a
   correlation identifier."* — plus one link. The sentence is deleted (it uses the banned
   term, standards §2, and repeats an audit promise the lede already made twice). The link
   becomes a single row at the foot of the registration panel: **"View automation
   activity"**.
9. **One heading stack.** The eyebrow `ViewData["Eyebrow"] = "Administration"` and the
   "Back to Administration" link are replaced by the breadcrumb **"Administration /
   Automation"**. H1 **"Automation"**. The uppercase "Client registration" section label is
   dropped — with one panel on the page, the H1 already names it (standards §4.7).
10. **Design the confirmation state.** After a successful change the page renders the
    status card **"Automation is on."** / **"Automation is off."** — a fact, not
    "Your change was saved".

## Dependencies

Backend work — plan only; nothing here is implemented by this document.

- **Conditional card and route** (change 1). The Administration index card list must become
  gate-aware, and the two Automation routes must fail closed to the styled not-found page
  when the gate is off. `AutomationMcpOptions.TryCreate` already returns `null` when
  `Features:AutomationMcp` is unset (`src/Pegasus.Web/Mcp/AutomationMcp.cs:49`), so the
  composition already knows the answer; the pages do not ask it. This is the same absent-
  not-inert change standards §4.9 requires of the dashboard tiles.
- **Scope label map** (change 4): a page-model switch over the three constants in
  `AutomationMcp` returning label plus one-line job description; unmapped values throw, as
  `RecordTypeLabel` already does.
- **Expected-version field on the toggle** (review lens 3): the form posts `TargetEnabled`
  computed from the state the page rendered with, so two administrators acting at once
  produce a last-writer-wins result with no stale-save state. The sibling administration
  forms carry `ExpectedVersion`; this one should too, with page 29's stale-save copy.
- **`OperationKey`** is already generated in the page model here — correct, and the pattern
  page 29 should adopt.
- Status chip values move from the literal strings `"Enabled"`/`"Disabled"`
  (`Index.cshtml:39,43`) to `"On"`/`"Off"`.

## Open questions

- When the gate is off, should the route 404 or redirect to the Administration index? The
  mockups assume the styled not-found page, because a bookmarked URL should not silently
  land somewhere else. Operator decision.
- Is "Inbox" the right label for the receiving area (change 4), given the new nav splits
  Inbox and Upload into two items and this one scope covers both? Alternative: **"Inbox and
  uploads"**. Mockups use "Inbox"; flagged.
- The registration cannot be created or renamed from this page — it comes entirely from
  configuration. Should the page say so at all, or is a read-only fact list with one toggle
  self-evident? The plan takes the second position (no explanatory copy).
- Should turning automation off be reversible from the same panel without re-reading the
  reason, or should each direction have its own confirmation? Mockups use one form whose
  button and consequence line change with the current status.
