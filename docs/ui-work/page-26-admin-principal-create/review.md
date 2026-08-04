# Page 26 — Administration / Create principal: review

Source: `src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml`.
Screenshot: `principal-create.png`. Governing standards: `../ui-standards-and-review.md`
(§2 vocabulary, §4 presentation rules).

## 1. Aesthetics

The page is close to acceptable: one form panel, three fields, one primary action. The
problems are in the words, not the layout.

- The lede — *"A principal code becomes an immutable identity. Later correction uses a
  linked successor rather than editing this code or moving existing cases and references."*
  — is architecture narrated as a subtitle. It front-loads a worst-case governance lecture
  before the operator has typed anything, and it duplicates the field-level hint
  (*"…cannot be edited"*) below the code input. Standards §4.1: no ledes; consequence
  guidance belongs beside the control it concerns, one sentence maximum.
- The overflow note under the panel — *"The organization selector is bounded. Use the
  Organizations workspace to confirm an organization that is not shown."* — is dev-speak.
  "Bounded" is projection jargon (banned, standards §2) and "confirm an organization" does
  not say what the operator should actually do (search for it).
- The eyebrow "ADMINISTRATION" plus the back link plus the nav's active Administration item
  is three renderings of the same location (standards §4.7: one heading stack).
- The immutability hint is split across two voices: *"The code is normalized to uppercase
  and cannot be edited"* — "normalized" is implementation vocabulary for "saved in
  capitals".

## 2. Practicality

- The happy path works: pick organisation, type code, pick mode, submit. Field order is
  right and the blocking card for the no-Work-Provider state is a genuinely designed empty
  state with a way out ("Go to Organizations") — the best-behaved screen state in the
  Administration area.
- The most consequential fact on the page — the code is permanent — is stated twice in
  passing (lede + hint) but never at the moment of commitment. There is no consequence
  sentence at the "Create principal" button, which is where standards §4.1 puts it.
- The organisation select is capped at a fixed page of results, and the recovery path when
  the wanted organisation is missing is a full context switch to another workspace with
  instructions written in another language ("bounded", "confirm"). At minimum the copy must
  say what is shown and what to do: "Showing the first N organisations — search in
  Organisations to find one that is not listed."
- The Inspection mode hint is the one piece of guidance that earns its place (a real
  consequence: address autofill on every new case), but at two lines it is the longest text
  on the form and reads ahead of need — it applies only when "Image Based Assessment" is
  chosen.

## 3. Performance / Design / Good practice

- The Work Provider filtering happens in the view (`.Where(...Roles.Contains(
  OrganizationRole.WorkProvider))` over `Model.Organizations.Organizations`) — display
  logic leaking policy shape into markup, and the "HasMoreOrganizations" note can be
  misleading: more organisations may exist that are *not* Work Providers, so the overflow
  sentence can render when every relevant organisation is in fact shown.
- The select renders raw organisation GUIDs as option values — fine as values, but the
  page has no other identifier hygiene issues; that is worth preserving.
- `OperationKey` hidden input gives idempotent resubmission — good, keep.
- Validation is standard ASP.NET summary + per-field spans; no designed success state is
  defined (where does the operator land after creation, and with what confirmation?).
- Accessibility: labels are properly associated, but the hint texts use the class
  `empty-state` — a semantic misuse of a state class for help text, which will fight any
  future restyle of genuine empty states.
