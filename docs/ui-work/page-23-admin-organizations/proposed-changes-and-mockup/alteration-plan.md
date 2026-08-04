# Alteration plan — Organizations (page 23)

## Review summary

A sound split page carrying two rule violations: a raw optimistic-concurrency integer displayed
as a "Version" column, and a teaching lede that states role independence three times (lede,
caption, section label) while the create form — the one place the fact drives a decision —
carries no guidance at all. Pagination copy leaks machinery ("Bounded view; more exist", "No
organizations are available on this page", a link-less "Page 1").

## Changes

1. **Navigation and orientation.** New global nav; breadcrumb `Administration / Organizations`
   replaces eyebrow + back-link. H1 "Organizations". No lede (§4.1).
2. **Remove the "Version" column.** The concurrency integer is internal (§4.4); the edit page
   keeps it as a hidden form input only. Table becomes Name / Roles / Active principals /
   Actions.
3. **Lede relocation.** Old lede: "Organization roles are independent. A Work Provider can own
   principals; an Instruction Intermediary can route work without becoming the principal." →
   deleted; its content compresses to the create form's roles help text: **"Roles are
   independent — a Work Provider owns principals; an Instruction Intermediary passes work
   through without becoming the principal."** (One sentence, next to the decision it informs.)
4. **Pagination copy.** Old: "Bounded view; more exist" (under principal counts) → New:
   **"Showing first 20"** (count from the actual cap). Old empty state: "No organizations are
   available on this page." → New: **"No organizations yet. Create the first one with the form
   on the right."** Pager hidden entirely when neither Previous nor Next exists.
5. **Caption.** "Organizations and their independently selectable roles" becomes
   screen-reader-only and shortens to "Organizations and their roles".
6. **Actions column.** Order fixed as row-edit first: **"Manage roles · Create principal"**;
   "Create principal" only for Work Provider organizations (unchanged behaviour).
7. **Create form.** "Organization name" field, roles checkbox group with the help text from
   change 3; legend "Organization roles" becomes screen-reader-only (the section label already
   says "Create organization"). Primary action "Create organization".
8. **Role labels.** The Work Provider / Instruction Intermediary display map moves to one
   shared helper used by this page, the organization edit page, and Principals (today the
   ternary is repeated inline).

## Dependencies

- Breadcrumb, `.hint` class, chip and table patterns shared with pages 19–22.
- Change 4 needs the principal cap constant exposed to the view (it exists in the query today;
  the view only gets a boolean `HasMorePrincipals`). Small page-model addition.
- Shared role-label helper touches three pages; coordinate with pages 24 and 25.
- No route changes; `Create` handler contract untouched.

## Open questions

- Should organization creation require a Reason like every other administration mutation
  (account create/disable, role change, review, organization role change all record one)? The
  asymmetry looks accidental. Needs an operator/policy statement; the mockups show the form
  without a Reason field, matching current behaviour.
- Is the organizations list ever expected to exceed one page in practice? If not, consider
  loading all and dropping the pager entirely rather than styling it.
- US "Organizations" vs UK business data ("Organisation One"): settled spelling or worth a
  canonical-docs decision? Not changed here.
