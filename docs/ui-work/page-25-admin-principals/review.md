# Page 25 review — Principals

Screenshot: `principals.png` · Source: `src/Pegasus.Web/Pages/Administration/Principals/Index.cshtml`
Page model: `src/Pegasus.Web/Pages/Administration/Principals/Index.cshtml.cs`
Route: `/Administration/Principals` · Reviewed against `docs/ui-work/ui-standards-and-review.md`.

## 1. Aesthetics

- **Eight columns to carry four facts.** CODE / STATUS / INSPECTION MODE / SEQUENCE LINEAGE /
  PREDECESSOR / SUCCESSOR / ALLOCATED CASES / ACTIONS (lines 60-67). In the screenshot the single
  data row is stretched across the full 1400px viewport with a 36-character hex string doing most
  of the stretching; Code sits at x=118 and Actions at x=1400. The eye has to travel the whole
  screen to link a code to its action.
- Four orientation devices stack before content, as on page 24: back link "Back to
  Administration", eyebrow "ADMINISTRATION", H1 "Principals", and a three-line lede.
- **The primary action carries the wrong icon.** "Create principal" renders
  `<use href="#icon-upload" />` (line 22) — an upload arrow on a create action, the same icon the
  document-upload surface uses.
- Heading grammar breaks with its siblings: the organization name is a full-weight H2
  ("QDOS development fixture"), whereas pages 23 and 24 use the uppercase `section-label`
  treatment for section headings. Three administration pages, three heading systems.
- "Create principal for this organization" (line 46) is a bare underlined paragraph link wedged
  between the organization heading and its table — the same action as the red header button,
  styled as body text, 8px from a heading.
- The visible caption "Principals owned by QDOS development fixture" repeats the H2 immediately
  above it.
- The pager renders "Page 1" with no links when there is one page — chrome that states nothing
  actionable (identical to page 23).

## 2. Practicality

- **SEQUENCE LINEAGE prints a raw GUID.** `@principal.SequenceLineageId` (line 77) renders
  `911df17b-234e-47f3-bcbf-e72958947310` on screen. §4.4 bans exactly this, and the root review
  names it as evidence of prototype status (§1.2). No operator decision depends on it; it is the
  internal key that links a replaced principal to its successor, and the *relationship* is what
  matters, not the key.
- **PREDECESSOR and SUCCESSOR are two columns that almost always read "None."**
  `@(principal.PredecessorId?.ToString("D") ?? "None")` (lines 78-79) — and when they are *not*
  "None" they render another raw GUID, which tells the operator nothing about which principal is
  meant. Two columns, 100% dead in the screenshot, and useless in the other case.
- **The lede narrates the data model**: "Principal codes and existing case/reference ownership
  are immutable. Replacement disables the predecessor and creates a linked successor on the same
  sequence lineage." (line 6). Three internal terms in two sentences, presented before the
  operator has selected anything. It is consequence guidance for the Replace action and nothing
  else — §4.1 puts it next to Replace, one sentence, or nowhere.
- **"No replacement action"** (line 90) is printed in the Actions cell of every principal that
  cannot be replaced — a cell explaining its own emptiness. An empty cell says the same thing
  without adding a row of grey text to every disabled or already-replaced principal.
- The pagination note "The principal projection is bounded; additional principals exist."
  (line 100) is the same dev-speak defect as pages 23 and 24. The real cap is 100 principals per
  organization (`MaximumProjectedPrincipals`, `EfOrganizationAdministration.cs:24`) and no
  per-organization pager exists to reach the rest — the pager only pages *organizations*
  (lines 107-117).
- **The two create paths are unexplained.** The header "Create principal" and the per-organization
  "Create principal for this organization" go to the same page; the second pre-fills the
  organization. Nothing on screen says so, and the header button is the visually dominant one
  despite being the version that makes the operator choose an organization again.
- Empty state "No organizations or principals are available on this page." (line 35) — the "on
  this page" hedge is paging machinery leaking into an empty state, and the "organizations or
  principals" disjunction makes the operator guess which is missing.
- The organizations without the Work Provider role render a heading, a caption and an empty-state
  panel, but no create link (line 43) — correct behaviour, though nothing explains why the link
  is absent there and present elsewhere.
- **KEEP: "Physical address" / "Image Based Assessment"** (line 76) are genuine business terms
  and the only column here that is doing honest work besides Code, Status and Allocated cases.

## 3. Performance, design and good practice

- **The allocated-case count is a correlated subquery per principal.**
  `principal.Cases.Count` inside the per-organization `Take(MaximumProjectedPrincipals + 1)`
  projection (`EfOrganizationAdministration.cs:428-451`) means up to 25 organizations × 101
  principals of `COUNT(*)` subqueries in one page load. `AsSplitQuery` limits the cartesian blow-up
  but not the count work. Today's data makes it free; it is the one thing on this page that will
  degrade badly with real volume.
- The page loads up to 100 principals per organization on a list screen where the operator
  usually wants one row. A per-organization principal pager (or lazy per-organization expansion)
  is the structural fix that also gives change to the capped-load note.
- The inspection-mode ternary (line 76) and the role check (line 43) are operator-label maps
  inline in markup for the third time in this folder set; §4.3 wants one shared map.
- `PrincipalAdministrationSummary` carries `Version` (`OrganizationAdministration.cs:35`) and the
  page correctly does *not* print it — the one place in this administration set that gets the
  version-integer rule right by accident. Worth locking in deliberately.
- Accessibility: `aria-labelledby="organization-@organization.Id"` puts a raw GUID into a DOM id.
  Harmless functionally, but it means the page's ids are unreadable and unstable across
  environments; an index or slug is enough.
- No scripts, one query, correct `scope="col"` on every header, `<caption>` present on every
  table — the structural fundamentals are sound. It is the column set and the copy that fail.
