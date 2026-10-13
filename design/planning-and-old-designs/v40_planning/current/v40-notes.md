# v40 notes: Contacts

Stage 1 of the round the operator asked for on 13 October 2026: the Contacts list (`/Administration/Contacts`) and a contact's page (`/Administration/Contacts/Edit/{id}`), "organised better" now that every contact has exactly one type. The mockup is `pegasus_contacts_v40.html`. The operator settled every lettered item on 13 October 2026 (section 6) and approved the round; Stage 2 implemented it the same day (section 10).

## 0. The brief and the rulings it rests on

Operator rulings of 13 October 2026, settled and not on the sign-off list:

- Every contact has exactly one type: Principal, Claim Source, Repairer, Storage or Third Party Engineer. The type is chosen at creation and fixed afterwards, shown read-only on the contact. There is no type checkbox grid and no "add a type to an existing contact". A separate PR changes the domain model.
- The list is organised by type, with columns that fit each type, using only fields that already exist.
- The contact's page shows only the sections for its type.
- A Principal's panels keep FRD-17's order: Report generation, Report sending, Salvage matrix.
- v39's open items T and U (the Report sending panel) are carried into this round (items S and T below).
- Notes on every Case is seeded with SOP text that can run to about 1,300 characters, and the Principal's page must show it well.

## 1. What changed, and where to see it

Shot numbers refer to `v40-shots/NN-<name>-<width>.png`. Each shot is taken at 1580, 1440 and 760, and there is a `-full` whole-page shot of every page state.

| Today (live on `origin/dev`, read 13 Oct 2026) | Proposed | Shots |
| --- | --- | --- |
| One table of every contact with a Type column listing every type it holds | One panel per type in FRD-17's order, headed Principals, Claim Sources, Repairers, Storage, Third Party Engineers; a type with no matching contact is absent | 01 (today: 02) |
| The same seven columns for every type | Columns per type: Principal code first for Principals; Linked principals for the four other types; Postcode for Repairers and Storage | 01 |
| Type filter narrows the one table | Type filter shows one panel; search runs across every panel | 01, 04 |
| — | Alternative: one type at a time (`list: single`), with no "All types" | 03 |
| Sort: Name, Type, Last case | Sort: Name, Last case, applied within each panel | 01 |
| Lede "Principals, claim sources, repairers, storage and third party engineers." | Removed: the headings say it | 01 |
| "No contacts found." when nothing matches | Unchanged, now when no panel has a match | 05 |
| Add contact: the type tiles, then "New {type}" with "Enter the details for this {type}. You can add more types later." | Type tiles unchanged; the sentence is removed | 06, 07 |
| Check existing contacts: each match offers "Use this contact", which adds the new type to it | Each match shows its name and type and an **Open** link; there is no "use" | 08 |
| Contact: "One organisation may have several contact types." under the name; a **Contact types** grid of five checkboxes | Sentence and grid gone; **Type** is a greyed read-only box beside the name | 09, 19, 20, 22, 23 |
| Principal code: an editable input whose change the save ignores | Greyed read-only box; **Replace code** stays the route | 09 |
| Inspection mode: an editable select whose change the save ignores after creation | Greyed read-only box | 09 |
| **Principal settings** panel: the code again, a second Active chip, Accepted e-mail identities, and the summary where every Principal panel's refusal lands | Panel gone; Accepted e-mail identities is a greyed box in the contact form | 09 |
| "Report settings" panel heading (since EVA's removal on 7 October) | "Report generation", FRD-17's name and the brief's; its button stays **Save report settings** (FRD-17) | 09 |
| Notes on every Case: a four-row box; a 1,300-character note scrolls inside it | The box grows to its text: every line visible, in the 720px form column | 10 (today: 11) |
| Principal panels stacked down one long page | Unchanged by default; alternative: tabs Contact, Report generation, Report sending, Salvage matrix, Default inspection location, Principal API | 09; tabs 12, 13 |
| A panel's save reloads at the top of the page with the notice there | The save comes back to the panel it saved, with the notice inside it | 15 |
| A Principal panel's refusal shows in the Principal settings summary, far from the panel | The refusal shows at the top of the panel that refused, with the typed values kept | 16 |
| Linked principals: one subheading and one long column of every Principal for each non-Principal type held | One list for the contact's one type, no subheading, three columns | 20 (one column: 21) |
| Claim Source: Case guidance plus Linked principals; Repairer, Storage and Third Party Engineer: Linked principals | Unchanged, now fixed by the type rather than by ticked boxes | 19, 20, 22, 23 |
| Inactive (replaced) Principal: contact form plus "This principal is inactive. Change the settings on its successor." | Unchanged | 17 |

## 2. Live rules the mockup mirrors

| Rule | Source |
| --- | --- |
| Type labels Principal, Claim Source, Repairer, Storage, Third Party Engineer and their order | `IndexModel.RoleLabel`, `ContactRole` |
| Group headings (plurals) | FRD-17 *Contacts*: "the one directory for Principals, Claim Sources, Repairers, Storage and Third Party Engineers" |
| List fields: Name, Contact person, Email, Phone, Last case, Status chip (Active navy, Disabled neutral), Open | `Index.cshtml`, `ContactDirectoryRecord`, `OperatorLabels.StatusTone` |
| Principal code label "Principal code" | `OperatorLabels.PrincipalAdministration.Code` |
| Search label "Name or contact", Type and Sort selects, Apply, auto-submit 300 ms after typing | `Index.cshtml`, site.js `data-auto-submit`, FRD-17 |
| Add contact type tiles, "New {type}", "Save {type}", "Check existing contacts", "Possible existing contacts" | `Index.cshtml` |
| A Principal's code is written only when the Principal is created; a later Save ignores it | `EfContactDirectoryAdministration.SaveAsync` (`isPrincipal && entity.Principals.Count == 0`) |
| A Principal's inspection mode is written only at creation and copied to a successor | `EfContactDirectoryAdministration.SaveAsync`, `OrganizationAdministration` (successor) |
| Accepted e-mail identities come from the Principal route policy for the code | `EditModel.AcceptedIdentities` |
| Notes on every Case: at most 2,000 characters, Principal and Claim Source only | `ContactDirectoryPolicy.MaximumNotesOnEveryCaseLength`, FRD-04 |
| Guidance for new Cases: at most 4,000 characters, Principal and Claim Source only, applied to new Cases | `ContactDirectoryPolicy.MaximumGuidanceTemplateLength`, `EfCaseWorkspaceStore` |
| Report sending, Salvage matrix, Default inspection location and Principal API panels | the server's own render (v39 capture), `OperatorLabels.PrincipalAdministration` |
| Panel status lines: "The principal's report sending rules were updated." and the salvage refusal "Cat S: From must not be more than To." | `EditModel.OnPostUpdateReportSendingAsync`, `EditModel.SalvageMatrixErrorMessage` |
| Inactive Principal: the successor line and no settings panels | `Edit.cshtml` |

## 3. Frame rules

The mockup is the live shell. The numbers are the live ones: 48px utility bar, the 220px rail, the Administration nav beside the content, 13.5px body text, 36px controls and 40px table rows. The contact form keeps its 720px column (`.contact-editor`). The proposal changes no frame number. The tabs alternative uses the live `tabs`/`tab` classes.

## 4. Decisions taken and their authority

- One panel per type, with group headings in FRD-17's words: the brief ("organised by type") and FRD-17's list of the directory. The design authority's rule "Filters are dropdowns … not rows of pill tabs" rules out type tabs on the list, so the alternative offered is one type at a time through the existing Type select.
- Only existing fields as columns: the brief. No count, default fee or inspection default was added to a list.
- Type read-only on the contact and no type grid: the operator's ruling.
- Panel order Report generation → Report sending → Salvage matrix: the brief and FRD-17. The heading "Report generation" is FRD-17's and the brief's; the live heading "Report settings" came in with EVA's removal (commit `a85f745e3`, 7 October 2026) and is treated as drift.
- Notes on every Case shown whole: the brief.
- Report sending panel content: unchanged from the as-built render; its acceptance is items S and T.

## 5. Deliberate departures from live

- The list's lede and the contact's "One organisation may have several contact types." are removed (the second is false under one type). Items D and E.
- The create dialog's sentence "Enter the details for this {type}. You can add more types later." and the existing-contact path ("Adding {type} to {name}. Existing types, linked principals and settings are retained.", **Use this contact**) are removed under the ruling. Items E and F.
- The Principal settings panel is folded into the contact form. Item K.
- Principal code and Inspection mode render read-only. Items I and J.
- The mockup does not show the Principal API's issued key, a Replace code confirmation, or the Default inspection location's address fields filled; those panels are as captured.

## 6. Sign-off list

Each item was "Confirm, or …". **Outcome, operator, 13 October 2026:** every item is settled; the outcome follows each item's letter below, and the round is approved.

- **A.** Confirmed: one panel per type; search across every panel; Type narrows to one panel; a type with no match is absent.
- **B.** Confirmed as drawn: the columns listed in B, "Principal code" as the heading, no Type column.
- **C.** Choice: "by name". Each panel is sorted by Name; the Sort control and the Last-case and Type sort modes go (`ContactSort` deleted). Last case stays a column.
- **D.** Confirmed: the lede goes.
- **E, F.** Confirmed (already done by PR #1314).
- **G.** Choice: names are unique per type (PR #1314). The refusal reads "A contact with that name already exists." on both Add and Edit.
- **H, I, J, K.** Confirmed: Type, Principal code and Inspection mode are greyed read-only boxes; the Principal settings panel goes; Accepted e-mail identities is a greyed box in the contact form; refusal summaries move to each panel.
- **L.** Choice: "tabs", as shots 12 and 13 show, with Replace code below the tabs and the heading "Report generation". The tabs work without script (server-rendered, `?tab=`).
- **M.** Confirmed: Notes on every Case grows to fit its text.
- **N.** Confirmed: a Principal panel's save returns to its tab with its notice inside the panel; a refused save shows its message at the top of that panel and keeps what was typed.
- **O.** Confirmed: Save contact returns to the Contacts list.
- **P.** Confirmed: Linked principals is one list in three columns.
- **Q.** Confirmed: Stage 2 fixes the short Phone box and the fieldset border, and uses "Phone" in the New dialog, on the contact and in the list.
- **R.** Choice: remove Guidance for new Cases (done by PR #1314). No Stage 2 markup brings it back, and FRD-04 does not name it.
- **S, T.** Confirmed: Report sending keeps the full edit form; the condition kinds, in order: Claim Source, Repairer, Images from, Instruction mentions, Sender is not, Outcome, Bodyshop mentions.

The items as offered:

Contacts list (shots 01–05):

- **A.** Organisation: one panel per type in the order Principals, Claim Sources, Repairers, Storage, Third Party Engineers (FRD-17's words). Search runs across every panel, Type narrows to one panel, and a type with no matching contact is absent (shots 01, 04, 05). Confirm, or choose one type at a time: Type loses "All types" and opens on Principal (`list: single`, shot 03).
- **B.** Columns per type, all existing fields (shot 01). Principals: Principal code, Name, Contact person, Email, Phone, Last case, Status. Claim Sources and Third Party Engineers: Name, Contact person, Email, Phone, Linked principals (as codes), Last case, Status. Repairers and Storage: the same with Postcode before Linked principals. The Type column goes. The heading is "Principal code" (the existing label) where the brief said "Code". Confirm each type's columns, or strike Postcode or Linked principals, or ask for "Code".
- **C.** Sort keeps Name and Last case, applied within each panel; Type is dropped because the list is grouped. Confirm.
- **D.** The lede "Principals, claim sources, repairers, storage and third party engineers." under Contacts is removed. Confirm.

Add contact (shots 06–08):

- **E.** The New {type} dialog loses "Enter the details for this {type}. You can add more types later." with no replacement, and the existing-contact route ("Adding {type} to {name}. Existing types, linked principals and settings are retained." and **Use this contact**) goes with the ruling. Confirm.
- **F.** Check existing contacts stays. Each possible match reads "{name} · {type}" with an **Open** link to that contact, and nothing is added to it (shot 08). Confirm, or remove the step (`match: none`) and rely on the duplicate-name refusal at Save.
- **G.** The duplicate-name refusals point at the removed route: "A contact with that name already exists. Choose it from the matches instead." (Add) and "A contact with that organisation name already exists. Select it before adding a role." (Edit). Give the replacement sentence. Also, organisation names are unique across the whole directory today, so a firm that is both a Repairer and Storage cannot be two contacts with the same name. Confirm names stay unique, so the second contact needs a different name, or say that the domain-model PR should allow one name per type.

Contact page (shots 09–23):

- **H.** **Type** is a greyed read-only box beside Organisation name, labelled "Type" (shots 09, 19, 20). Confirm its place and label.
- **I.** **Principal code** becomes a greyed read-only box. Today it is an editable input, but the save ignores a changed code; **Replace code** stays the only route. Confirm.
- **J.** **Inspection mode** becomes a greyed read-only box. Today it is an editable select whose change the save ignores after creation; the Default inspection location panel holds the default staff can change. Confirm read-only, or remove it from the page.
- **K.** The **Principal settings** panel goes. Its code and Active chip repeat the contact's, its one fact (Accepted e-mail identities) becomes a greyed box in the contact form, and its refusal summary moves to each panel (item N). Confirm.
- **L.** A Principal's panels: stacked in FRD-17's order as today (shot 09), or as tabs Contact · Report generation · Report sending · Salvage matrix · Default inspection location · Principal API, with Replace code below the tabs (shots 12, 13). The design authority describes a single-record page with "sections as tabs". The first tab's label, "Contact", is new. Choose one.
- **M.** Notes on every Case grows to fit its text, so a 1,300-character note shows every line in the 720px form column without scrolling inside the box (shot 10; today's four rows: shot 11). Guidance for new Cases keeps four rows. Confirm, or ask for the same on Guidance for new Cases, or a wider box.
- **N.** A Principal panel's save returns to that panel with its notice inside it (shot 15). A refused save shows its message at the top of that panel and keeps what was typed (shot 16). Today every save reloads at the top of the page, and every panel's refusal appears in the Principal settings summary. Confirm.
- **O.** **Save contact** returns to the Contacts list, as today, while the Principal panels stay on the contact. Confirm, or ask for Save contact to stay on the contact too.
- **P.** Linked principals: one list for the contact's type, with no type subheading, in three columns (shot 20). Today it is one column per type (shot 21). Confirm.
- **Q.** These live defects show in both today's capture (shot 18) and the mockup: the Phone box is shorter than the boxes beside it; the contact form's fieldset draws a border round the fields; and the label is "Telephone" in New {type} but "Phone" on the contact and in the list. Confirm Stage 2 fixes all three, using "Phone" everywhere (FRD-04's word), or leave them.
- **R.** Guidance for new Cases is live on Principal and Claim Source contacts (it is applied to new Cases), but FRD-04 does not name it. Confirm it stays, and that Stage 2 writes it into FRD-04, or ask for it to be removed.

Report sending, carried from v39 (shot 14):

- **S.** (v39 T) The panel is a full edit form on the Principal's page, like the other Principal panels, with one spare blank rule row and the five hints. Confirm.
- **T.** (v39 U) The condition kinds, in this order: Claim Source, Repairer, Images from, Instruction mentions, Sender is not, Outcome, Bodyshop mentions. Confirm the order and the words.

## 7. Self-check

`check-v40.py` on 13 October 2026: `RESULT {"fail": [], "okCount": 262}`. That is 193 checks from `v40-selfcheck.html` (every state, every dialog, every variable and every query preset) and 69 console-error-free loads (23 shots at three widths), with 89 screenshots (`v40-shots/verification.json`). Neither the self-check nor the screenshots are application evidence.

## 8. Known limits

- The contact data is synthetic: the fifteen Principal names and codes are the seeded ones, and every other contact, address, e-mail and phone number is made up. **The Notes on every Case text is made up to the length of the seeded SOP text. It is not any Principal's SOP.**
- The Report sending, Salvage matrix, Default inspection location and Principal API panels, and the whole `principal-live` state, are the server's render captured on 6 October 2026 for v39 (PR 1015 head `4803fa7c4`). In that capture the Report panel still carries the EVA choice and the nav still says "Reports". The proposed states patch the nav to dev's and draw Report generation from dev's Razor. Today's list (`list-live`) is drawn from `Index.cshtml` with the same fixtures, not captured. The v28 captures (`../../v28_planning/current/v28-shots/s51-admin-contacts-*.png`, `s52-admin-contact-edit-*.png`) are the last real renders of the list.
- No .NET build was run for this round. Capturing dev's current pages needs one, and is left for Stage 2's conformance shots.
- Posts do nothing. Filtering and sorting happen in the page, in place of the server's. The saved and refused states are drawn rather than produced by a post.
- Shots scrolled to a panel show a strip of page background above the utility bar. This comes from capturing a scrolled page and is not part of the design.

## 9. Stage 2 plan (as offered)

Stage 2 started once every item above was settled. It stacks on the domain-model PR (#1314) that gives a contact one type.

- `Pages/Administration/Contacts/Index.cshtml(.cs)`: grouped panels (or single type), per-type columns, Sort without Type, the create dialog without the add-a-type route, matches with Open.
- `Pages/Administration/Contacts/Edit.cshtml(.cs)`: the read-only Type, code and inspection mode; the Principal settings panel removed; "Report generation"; per-panel status and refusal with a return to the panel; Linked principals for one type.
- `wwwroot/js/contacts-edit.js`: remove the role toggling; fit Notes on every Case; tabs if L chooses them.
- `wwwroot/css/site.css`: `contact-links--grid` and the fitted notes box, scoped next to the Contacts block; the Q fixes.
- FRD-04 *Contacts administration* and FRD-17 *Contacts*: one type, the list's organisation and columns, Guidance for new Cases (R), the panel behaviour (N).
- Web tests that assert the Contacts markup; conformance screenshots against `v40-shots/` at 1580, 1440 and 760.

## 10. Stage 2 (13 October 2026)

Branch `task/v40-contacts-stage2`, stacked on PR #1314 (one type per contact) and PR #1315 (this round).

| Item | Where it landed |
| --- | --- |
| A, B, D | `Pages/Administration/Contacts/Index.cshtml(.cs)`: one panel per type in `ContactRole` order, the per-type columns, no lede; `OperatorLabels.Contacts.Plural`; `ContactDirectoryRecord.PrincipalCode` for the code column, the active Principal choices for the Linked principals codes |
| C | `ContactSort` and its query modes deleted (`Core/Cases/ContactDirectory.cs`, `EfContactDirectoryAdministration.ListAsync` orders by name) |
| E, G, Q | The New dialog loses its last sentence and says Phone; both pages refuse a duplicate with "A contact with that name already exists." and a lettered phone with "Phone must be digits."; `site.css` adds `input[type="tel"]` to the text-input rule and drops the contact form's fieldset border |
| H, I, J, K, P | `Edit.cshtml`: Type, Principal code, Inspection mode and Accepted e-mail identities as greyed read-only boxes; no Principal settings panel; `.contact-links` three-column grid |
| L, N | `Edit.cshtml(.cs)`: server-rendered `nav.tabs` links (`?tab=`), one panel rendered per tab, each handler returning to its tab with the notice and the refusal at the top of the panel, typed values kept; a refused Replace code shows beside Replace code |
| M | `contacts-edit.js` fits Notes on every Case to its text; without script it keeps four rows |
| O, R, S, T | Unchanged from PR #1314 and dev, checked on the routed page |
| Docs | FRD-04 *Contacts administration*, FRD-17 *Contacts*, `docs/design/README.md` (the Contacts sort exception and link tabs) |

`capture-stage2-v40.py` shot the routed pages from a scratch Kestrel host over the integration-test database (the seeded Principals plus the Riverside, Accident Specialists, Eastfield and Pennine fixtures and the long DFD note) into `stage2-shots/`, numbered as `v40-shots/`, at 1580, 1440 and 760, and checked the promised behaviours on the routed page: `RESULT {"ok": 77, "fail": []}` (`stage2-shots/verification.json`). Shots 02, 08, 10, 11, 14, 17, 18 and 21 are not repeated: they show today's page, the unchosen alternatives or a state the fixtures did not hold.
