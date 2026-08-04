# Page 27 — Administration / Replace principal: review

Source: `src/Pegasus.Web/Pages/Administration/Principals/Replace.cshtml`.
Screenshot: `principal-replace.png`. Governing standards: `../ui-standards-and-review.md`
(§2 vocabulary, §4 presentation rules).

## 1. Aesthetics

- The lede — *"The existing code, principal row, cases, references, and reference ownership
  will not be edited. Replacement disables this predecessor, links a new active successor,
  and continues the same sequence lineage."* — is the heaviest immutability narration in
  the Administration area: five nouns of database vocabulary ("principal row", "reference
  ownership", "sequence lineage") delivered before the operator has seen the form. The
  operator needs exactly one consequence, at the moment of commitment, not a paragraph of
  reassurance at the top.
- The Predecessor panel prints **"Sequence lineage: 911df17b-234e-47f3-bcbf-e72958947310"**
  — a raw GUID on an operator screen (banned, standards §4.4) — and **"Version: 0"**, an
  internal concurrency integer with no operator meaning.
- The "Active" chip floats top-right of the heading with an info icon, disconnected from
  the Predecessor panel whose status it describes; "Status: Active" is then repeated inside
  the panel — the same fact twice.
- The overflow note — *"The organization selector is bounded. Confirm an organization that
  is not shown in the Organizations workspace."* — is the same dev-speak as the Create
  page, and here it is even reworded slightly differently, so the two sibling pages
  disagree on their own jargon.

## 2. Practicality

- The two-column predecessor/successor layout is genuinely good: what you are replacing on
  the left, what replaces it on the right. Keep it.
- The button label — **"Disable predecessor and create successor"** — is honest about the
  double effect, which is right; but the effect on *existing work* (nothing moves, the old
  code keeps its cases) is only stated in the lede the operator has learned to skip. One
  sentence at the button would carry the whole story.
- "Allocated cases: 0" is the one predecessor fact with real decision weight (replacing a
  principal with 250 live cases feels different from one with 0) — yet it sits last in the
  list, below a GUID.
- The already-replaced state — *"This principal has no replacement action because it is
  disabled or already linked to a successor."* — explains why the page refuses instead of
  helping: it does not say which of the two reasons applies, and it does not link to the
  successor that supposedly exists.
- Reason for replacement has no hint about who reads it or where it surfaces (it becomes
  the permanent audit reason).

## 3. Performance / Design / Good practice

- Same view-side Work Provider filtering as Create (`.Where(...)` in the page), with the
  same misleading-overflow risk: `HasMoreOrganizations` is computed before the role filter.
- `ExpectedVersion` + `OperationKey` hidden inputs give optimistic concurrency and
  idempotent resubmission — sound engineering, keep both; only stop *displaying* the
  version integer.
- The defensive `@if (Model.Organization is not null && Model.Predecessor is not null)`
  wrapper renders an entirely blank page body if the handler contract ever changes — a
  silently empty screen is the worst failure state; the styled not-found page (standards
  §4.6) is the right owner.
- The status chip partial is fed the string `"Active"`/`"Disabled"` — fine — but the
  page-heading chip and panel status can drift apart if only one is updated; a single
  rendering (in the panel) removes the risk.
- Success state is undefined on this page: after replacement the operator should land
  somewhere that shows both codes and their new states, with a status card.
