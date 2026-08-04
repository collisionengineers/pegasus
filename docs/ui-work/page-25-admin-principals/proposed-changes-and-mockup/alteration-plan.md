# Alteration plan — Principals (page 25)

## Review summary

Eight columns carry four facts. One of them prints a raw internal GUID, two of them read "None"
on every row in the captured state and print more GUIDs when they do not, and the page opens with
a three-sentence lede that teaches the replacement data model to an operator who has not chosen
anything yet. The Actions column narrates its own emptiness ("No replacement action"), the create
action wears an upload icon, and the pagination note is the same "projection is bounded" copy
found on pages 23 and 24. The columns that are genuinely useful — Code, Status, Inspection mode,
Allocated cases — survive untouched.

## Changes

1. **Navigation and orientation.** New global nav. Breadcrumb `Administration / Principals`
   replaces the eyebrow "ADMINISTRATION" and the "Back to Administration" link. H1 "Principals".
2. **Delete the lede.** Old: **"Principal codes and existing case/reference ownership are
   immutable. Replacement disables the predecessor and creates a linked successor on the same
   sequence lineage."** → removed from the page. One consequence sentence takes its place, shown
   against the Replace action (change 6).
3. **Delete the SEQUENCE LINEAGE column.** `@principal.SequenceLineageId` is an internal
   identifier (§4.4) and no operator decision depends on it. Removed outright — not relabelled,
   not truncated, not moved to a tooltip.
4. **Collapse PREDECESSOR and SUCCESSOR into one "Replacement" column**, rendered as words and
   only when a relationship exists:
   - successor present → **"Replaced by QDOSB"** (the successor's **code**, linking to its row;
     never an identifier)
   - predecessor present → **"Replaces QDOSA"**
   - neither → empty cell.
   The column header is **Replacement**. Old cell values `None` / `911df17b-…` are gone.
5. **Empty the dead Actions text.** Old: **"No replacement action"** → an empty cell. Replace
   remains the only action and appears only on active principals with no successor — unchanged
   logic.
6. **Consequence guidance moves next to Replace.** A single sentence renders under the table
   (and again as the lead line of the Replace confirmation page): **"Replacing a principal
   disables it and creates a linked successor. The code, its cases and its references never
   change."** One sentence, at the point of decision, per §4.1.
7. **Final column set**: Code / Status / Inspection mode / Allocated cases / Replacement /
   Actions. Allocated cases right-aligned with tabular numerals. **Inspection mode keeps
   "Physical address" and "Image Based Assessment" verbatim** — genuine business terms.
8. **Section heading grammar aligned with pages 23 and 24.** The organization name becomes an
   uppercase section label, and the per-organization create action moves out of the body text
   into the section header row as a right-aligned **"Create principal"** link. Old link text:
   "Create principal for this organization" → new: **"Create principal"** (the section it sits in
   names the organization).
9. **Header action keeps its place, loses its icon.** "Create principal" stays as the page's
   single primary red action; `#icon-upload` is replaced with a plus glyph (or no icon).
10. **Caption becomes screen-reader-only** and shortens to "Principals owned by this
    organization".
11. **Pagination copy.** Old: **"The principal projection is bounded; additional principals
    exist."** → new: **"Showing the first 100 principals"** in a table footer beside a
    per-organization `Previous · Page 1 · Next` pager. The organization-level pager at the page
    foot renders only when more than one page exists (today it prints a link-less "Page 1").
12. **Empty states in business language.** Old: "No organizations or principals are available on
    this page." → new: **"No organizations yet."** with a link to Organizations. Old: "No
    principals belong to this organization." → new: **"No principals yet."** plus a "Create
    principal" link for Work Provider organizations.
13. **Operator-label maps shared.** The inspection-mode ternary and the Work Provider role check
    move to the shared administration label helper introduced in the page 23 plan.

## Dependencies

Plan only — no application code is changed by this document.

- **Change 4 needs the successor's and predecessor's codes**, which
  `PrincipalAdministrationSummary` does not carry — it holds `PredecessorId` / `SuccessorId`
  only (`OrganizationAdministration.cs:27-37`). Add `PredecessorCode` / `SuccessorCode`
  (nullable strings) to the summary and resolve them in the projection. Within one organization
  the join is local to the already-loaded set; across organizations it needs a second lookup.
  Until that lands, the column can only be a presence indication ("Replaced") without the code.
- **Change 11 needs a per-organization principal pager.** `MaximumProjectedPrincipals = 100` is a
  hard ceiling in `EfOrganizationAdministration.ListAsync` with no offset. Either thread an offset
  through the query, or (better for this screen) load a small head per organization — 5 rows plus
  a "View all N principals" link into page 24, which already owns the per-organization list.
- **Performance follow-up**: `principal.Cases.Count` is a correlated count per principal
  (`EfOrganizationAdministration.cs:449`). If the head-load option above is taken the cost falls
  out naturally; if not, consider a grouped count.
- Change 8 reuses the existing `Create` route and its `organizationId` parameter — no new route.
- Change 6's sentence should be the same string used on the Replace page (page 27); land it once.
- Shared label helper is common to pages 23, 24 and 25.

## Open questions

- **Should this page exist as a table at all?** With the lineage and relationship columns gone it
  is Code / Status / Inspection mode / count / action, grouped by organization — which is almost
  exactly page 24's principals table repeated per organization. The cheaper information
  architecture is: Organizations (page 23) lists organizations with principal counts, and page 24
  owns the principals. Worth an operator ruling before both are built.
- Is "Replacement" the right column name, or does the business say "Superseded by" / "Replaces"?
  The proposed cell text uses both directions; the header needs one settled word.
- Should a disabled principal's row be visually de-emphasised beyond its Disabled chip (muted
  code, lighter row)? Proposed as chip-only to avoid colour-carried meaning.
- Should the header "Create principal" survive at all once every organization section has its own
  create link? Two entry points to one form is one more than necessary.
