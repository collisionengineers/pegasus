# Page 23 review — Organizations

Screenshot: `organizations.png` · Source: `src/Pegasus.Web/Pages/Administration/Organizations/Index.cshtml`
Route: `/Administration/Organizations` · Reviewed against `docs/ui-work/ui-standards-and-review.md`.

## 1. Aesthetics

- The split layout (table left, create form right) matches page 19 — good sibling consistency —
  and suffers the same imbalance with one row against a tall form.
- This page has a lede: **"Organization roles are independent. A Work Provider can own
  principals; an Instruction Intermediary can route work without becoming the principal."**
  Per §4.1 ledes are banned; this one is also *teaching material*, not page description — it
  explains the domain model to whoever is about to use the create form, which is exactly where
  it should live instead (as the roles fieldset's help text).
- The visible caption "Organizations and their independently selectable roles" repeats the lede
  *and* the section label — the role-independence fact is stated three times before the first
  row.
- "ORGANIZATION ROLES" appears as an uppercase legend inside the create form directly under the
  uppercase section label "CREATE ORGANIZATION" — stacked uppercase, two labels deep, for a
  two-checkbox group.
- The pager renders "Page 1" with no links when only one page exists — chrome that states
  nothing actionable.

## 2. Practicality

- **The "Version" column shows a raw concurrency integer** ("0" in the screenshot). It is
  `organization.Version` — the optimistic-concurrency token. No operator decision ever depends
  on it; it is a database internal in a business table and §4.4 bans exactly this. (The row
  count in "Active principals" is meaningful; the version is not.)
- **"Bounded view; more exist"** renders under the principal count when an organization has
  more principals than the page loaded — projection jargon as pagination copy, flagged verbatim
  in the root review (§1.3). The operator needs "and more" in words: e.g. "Showing first 20".
- The Actions column mixes two different verbs of different weight — "Manage roles" (edit this
  row) and "Create principal" (create a *different* entity pre-linked to this row). Both are
  legitimate; the row reads better with the row-edit action first and consistent ordering.
- The create form's role checkboxes carry no guidance at all once the lede is removed — this
  is the one place the role-independence sentence genuinely helps a decision, so it must land
  here.
- No Reason field on organization creation, while account creation (page 19) and every other
  administration mutation requires one. If the administration record covers creations, the
  asymmetry is surprising; if intentional, fine — flagged as an open question.
- Empty state "No organizations are available on this page." — the "on this page" hedge is
  paging machinery leaking into an empty state; plain "No organizations yet." reads correctly
  on page 1, and deeper pages cannot be empty unless the pager overshot.

## 3. Performance, design and good practice

- `HasMorePrincipals` exists because the query caps principals per organization — a sensible
  data guard; only its copy is wrong.
- Role display hardcodes the ternary `role == OrganizationRole.WorkProvider ? "Work Provider" :
  "Instruction Intermediary"` — a two-value operator-label map inline in markup. Works today;
  a third role would silently render wrong. The map belongs in one place (shared with the
  Principals and Edit pages, which repeat the same logic).
- The pager is inconsistent with the standards' compact-corner rule but consistent with the
  app's other pagers; hiding it entirely on a single page is the cheaper fix.
- `OperationKey` hidden input on create — correct.
- Single query, no scripts; no performance concerns. The per-organization principal counts ride
  on the same query rather than N+1 — good.
- "Organizations" (US spelling) is the settled product spelling here while business data will
  hold UK spellings ("Organisation One"); cosmetic, but the heading and the data will disagree
  on screen. Noted, not proposed for change (rename would touch routes and canonical docs).
