# Page 24 review — Organization edit ("Manage {organization}")

Screenshot: `organization-edit.png` · Source: `src/Pegasus.Web/Pages/Administration/Organizations/Edit.cshtml`
Page model: `src/Pegasus.Web/Pages/Administration/Organizations/Edit.cshtml.cs`
Route: `/Administration/Organizations/Edit/{id}` · Reviewed against `docs/ui-work/ui-standards-and-review.md`.

## 1. Aesthetics

- **Four orientation devices stack before any content**: the back link "Back to Organizations"
  (line 7-10), the eyebrow "ADMINISTRATION" (line 18), the H1 "Manage QDOS development fixture"
  (line 19), and the lede (line 20). §4.7 allows one heading stack; a breadcrumb replaces the
  first two outright.
- **"ORGANIZATION ROLES" is printed twice, adjacent, both uppercase** — once as the section label
  (line 71) and once as the fieldset legend immediately below it (line 78). In the screenshot the
  two labels sit 45px apart with nothing between them. One is redundant by construction.
- The visible table caption "Principals owned by QDOS development fixture" (line 43) restates
  the H1 in the row directly under the section label "PRINCIPALS". Three labels — H1, section
  label, caption — for one three-column table.
- **The layout is upside down.** `.split-main` gives the principals table the wide left column and
  the roles form a 380px right rail, but the principals table is *read-only* and the roles form is
  the entire purpose of the page. In the screenshot the left column is a single row of data
  followed by ~500px of empty paper while the page's only control sits squeezed in the rail.
- "Version 0" floats alone in the top-right heading actions slot where sibling pages put a status
  chip or a primary button — a number with no label context, aligned as if it were an action.
- The Reason textarea is a bare 3-row box with no size, no counter, no required marker, and a
  hard `resize` grabber visible in the corner; it is the tallest element on the page and the least
  explained.

## 2. Practicality

- **The pagination note is dev-speak**: "The principal projection is bounded; additional
  principals exist." (line 65). Flagged verbatim in the root review §1.3. It tells the operator
  a fact about the query object, gives no count, and offers no way to see the missing rows —
  the page has no pager at all. The real cap is 100
  (`GetOrganization.MaximumPrincipalCount`, `OrganizationAdministration.cs:174`), so the honest
  copy is "Showing the first 100 principals" and the honest fix is a pager.
- **"Version 0" is a raw optimistic-concurrency integer** (`@Model.Organization.Version`,
  line 23) — banned by §4.4, exactly the same defect as the Version column on page 23. It is
  already carried correctly as `<input asp-for="ExpectedVersion" type="hidden" />` (line 75);
  the visible copy adds nothing an operator can act on.
- **The lede carries one genuinely useful sentence in the wrong place**: "Roles are independently
  selectable. Work Provider cannot be removed while an active principal belongs to this
  organization." (line 20). Sentence one is domain teaching; sentence two is a *consequence of
  unticking a specific checkbox* and belongs against that checkbox, per §4.1. Today the operator
  reads it before they know what they are about to do, then unticks Work Provider and discovers
  the rule again as a post-submit error string
  (`MutationErrorMessage`, `Edit.cshtml.cs:136-137`).
- **The rule is knowable at render time and is not applied.** The page already holds
  `Organization.Principals` with `IsActive`; whether Work Provider can be removed is decidable
  before the operator commits. Instead the checkbox is freely untickable and the failure is a
  server round-trip.
- Likewise "Select at least one organization role." (`Edit.cshtml.cs:61`) — an unticks-everything
  state that is detectable in the form and is only reported after a POST.
- **Reason is required and nothing says so.** `[Required, StringLength(500, MinimumLength = 1)]`
  (`Edit.cshtml.cs:27`), rendered as the plain label "Reason for change" with no required marker,
  no 500-character limit stated, and no statement of where the reason goes. An operator learns it
  is mandatory by being rejected.
- **"Update roles" is always enabled**, including on first load when the checkboxes still hold the
  saved values. Submitting an unchanged form with a reason writes an administration record for a
  no-op.
- **The principals table is a dead end.** Code / Status / Allocated cases, no link anywhere —
  yet page 25 offers Replace on the same rows and page 23 offers "Create principal" for the same
  organization. Arriving here from "Manage roles" and wanting to act on a principal means going
  back two screens.
- The empty state "This organization has no principals." (line 37) states the fact and offers no
  next step, even for a Work Provider organization where creating one is the obvious action.
- `<caption>` text is the only place the organization name is repeated, so removing it visually
  costs nothing; keeping it visible costs a row of duplicate text on every load.

## 3. Performance, design and good practice

- Single `IGetOrganization` call, no scripts, no N+1 — the allocated-case counts ride the same
  projection (`EfOrganizationAdministration.cs:503-528`). No performance concern on this page.
- The 100-principal cap is a sensible query guard; only its copy and its missing pager are wrong.
  Note the cap is a *hard* limit in Infrastructure (`MaximumProjectedPrincipals = 100`,
  line 24) — real paging needs an offset parameter added to `IOrganizationAdministrationQueries.GetAsync`,
  not just a larger number.
- `OperationKey` hidden input and the expired-form message are correct and worth keeping.
- The role display logic is again inline in markup (`WorkProvider` / `InstructionIntermediary`
  ternaries appear on pages 23, 24 and 25). One shared operator-label map, per §4.3.
- The status card `TempData["AdministrationStatus"]` = "The organization roles were updated."
  is fine, but it renders below the heading and above the split, so on a tall page the operator
  who just pressed the button in the right rail may not see it without scrolling up.
- Accessibility is largely right: `aria-labelledby` on both sections, `<caption>` present,
  `asp-validation-for` on Reason. The gaps are the doubled legend/section label (screen readers
  announce "Organization roles, Organization roles") and the absence of `aria-describedby`
  linking the Work Provider checkbox to the rule that governs it.
- The H1 grammar "Manage {name}" differs from every sibling administration detail page and puts
  a verb where the breadcrumb already says what the section is. The record's name is the title.
