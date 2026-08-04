# Alteration plan — Organization edit (page 24)

## Review summary

The page's only control — the roles form — is exiled to a 380px right rail while a read-only
three-column table owns the wide column and most of the empty paper. Four orientation devices
stack above the content, "ORGANIZATION ROLES" is printed twice in a row, a raw concurrency
integer is displayed as "Version 0", and the pagination note is the review's worst copy defect:
"The principal projection is bounded; additional principals exist." Two rules the page already
knows at render time (Work Provider cannot be removed while an active principal exists; at least
one role is required) are enforced only as post-submit error strings, and the required Reason
field is not marked required.

## Changes

1. **Navigation and orientation.** New global nav. Breadcrumb
   `Administration / Organizations / Organisation One` replaces the eyebrow "ADMINISTRATION" and
   the "Back to Organizations" link. H1 becomes the organization name — old:
   **"Manage QDOS development fixture"** → new: **"Organisation One"**. No lede (§4.1).
2. **Remove "Version 0".** The concurrency integer leaves the heading actions slot entirely
   (§4.4); it survives as the existing hidden `ExpectedVersion` input. The heading actions slot
   instead carries the organization's current roles as chips, which is the fact an operator
   arriving from the list actually wants confirmed.
3. **Roles form leads; principals table follows.** The `.split-main` inversion is undone. Layout
   becomes: **Organization roles** card at the top (max 640px, left-aligned), **Principals**
   table full width beneath it. The form is the page's purpose and the table then has room for
   an Actions column and a pager.
4. **Lede relocation and rule enforcement.** Old lede: "Roles are independently selectable. Work
   Provider cannot be removed while an active principal belongs to this organization." →
   deleted. Sentence one is dropped (page 23's create form already carries the role-independence
   help text). Sentence two becomes inline consequence guidance bound to the checkbox with
   `aria-describedby`, shown **only when the organization has at least one active principal**:
   **"Work Provider cannot be removed while this organization has an active principal."**
   In that state the Work Provider checkbox renders checked and disabled rather than freely
   untickable, and the value is preserved by a matching hidden input.
5. **Deselect-everything guard.** "Select at least one organization role."
   (`Edit.cshtml.cs:61`) stops being a post-submit-only message; the same sentence renders as
   inline field guidance under the fieldset and the submit is blocked while both boxes are clear.
   The server check stays as the authority.
6. **Kill the duplicate legend.** The fieldset legend "Organization roles" becomes
   screen-reader-only; the section label "ORGANIZATION ROLES" is the visible one.
7. **Reason ergonomics.** Old label: "Reason for change" → new: **"Reason for change"** with a
   `(required)` marker, a one-line hint **"Recorded against this change in the administration
   record."**, and a live `n/500 characters` counter matching
   `OrganizationAdministrationPolicy.MaximumReasonLength`. Field grows to 4 rows.
8. **Submit state.** "Update roles" stays as the primary red action but is disabled until the
   role selection differs from the saved selection, so an unchanged form cannot write a no-op
   administration record. Error and success text unchanged.
9. **Pagination copy and a real pager.** Old: **"The principal projection is bounded; additional
   principals exist."** → new: **"Showing the first 100 principals"** as a muted note in the
   table footer, beside `Previous · Page 1 · Next` links. The pager renders only when more than
   one page exists.
10. **Principals table gains an Actions column and loses its visible caption.** Columns become
    Code / Status / Inspection mode / Allocated cases / Actions, with **"Replace"** on active
    principals that have no replacement (identical rule to page 25) — the read-only dead end is
    removed. `<caption>` shortens to "Principals owned by this organization" and becomes
    screen-reader-only.
11. **Empty state gets a next step.** Old: "This organization has no principals." → new:
    **"No principals yet."** followed by a **"Create principal"** link, shown only for Work
    Provider organizations (the same condition page 23 already uses).
12. **Status card position.** The post-action confirmation moves to sit directly above the roles
    card so it is adjacent to the control that produced it.
13. **Role labels.** Work Provider / Instruction Intermediary display strings come from the one
    shared operator-label helper introduced in the page 23 plan, used here for both the chips and
    the checkbox labels.

## Dependencies

Plan only — no application code is changed by this document.

- **Change 9 needs new query work.** `IOrganizationAdministrationQueries.GetAsync` takes a
  `principalLimit` but no offset, and `MaximumProjectedPrincipals = 100`
  (`EfOrganizationAdministration.cs:24`) is a hard ceiling. Real paging needs an offset (or a
  keyset cursor on `Code`) threaded through `GetOrganizationRequest` → `GetOrganization` →
  `GetAsync`, plus a `HasPreviousPage`/`PageNumber` pair on `OrganizationDetails`. Until that
  lands, the honest interim is the "Showing the first 100 principals" note alone with no pager.
- **Change 4 needs `HasActivePrincipal` (or the equivalent) on `OrganizationDetails`.** It is
  derivable from the loaded `Principals` list *only while the list is uncapped*; with 100+
  principals the flag must come from the query, not from the projected rows.
- Change 8 needs the saved role set available to the view separately from the bound checkbox
  values, so the form can compare. `Organization.Roles` already provides this; the comparison is
  a view concern.
- Change 10 reuses the existing `Replace` page route and its `organizationId`/`principalId`
  parameters — no new route.
- Shared role-label helper is common to pages 23, 24 and 25; land it once.
- Breadcrumb, chip, table, pager and `.hint` patterns are shared with pages 19-23.

## Open questions

- Should the disabled-Work-Provider state (change 4) be a disabled checkbox or a visible-but-
  rejected one with an explanatory chip? A disabled control hides the rule from anyone who did
  not read the hint; the hint plus `aria-describedby` is proposed, but an operator ruling would
  settle it.
- Is a no-op role update (change 8) genuinely undesirable, or does the business want a reason-only
  administration record as a deliberate annotation mechanism? If the latter, drop change 8.
- Does the organization name belong in the H1 when the page's job is roles? The alternative is
  H1 "Organization roles" with the name in the breadcrumb only. Proposed as written because the
  page also lists principals and will gain their actions.
- Should this page's principals table and page 25's diverge at all, or should page 25 simply link
  here per organization and stop duplicating the table? Worth deciding before both are built.
