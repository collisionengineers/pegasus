# v32 notes: the Work Centre

Built on 5 October 2026 from `origin/dev` 81b571c36. These notes cover the offline proposals only and are not application evidence.

## 1. What changes, and where it shows

Shot numbers: `01`–`05` default (A–E), `06`–`10` Mine, `11`–`15` Held + Review, `16`–`20` open row, `21`–`25` after dismissing QDOS26203, `26`–`30` nothing needs attention, `31`–`35` Needs attention unavailable, `36`–`40` no work to show, `41`–`45` the Assign Engineer dialog, `full-<design>` whole page, `00-live` today's page. Each state is captured at 1580, 1440 and 760.

| # | Live today | Proposal (all five designs) | Shots |
| --- | --- | --- | --- |
| 1 | Dismiss is drawn three ways: inside the expanded Needs attention row only, an icon X on New cases, a text button on AI job cards (issue 1017) | One control at the end of every row of every list, an icon-only X named "Dismiss {reference}", always visible. The open row no longer repeats it. Dismissing takes the record's rows out of every list at once; the mockup also lands focus on the next row, or the list's heading when none is left. A strip switch shows the text button instead (item B). | 01–05, 21–25, 00-live |
| 2 | No activity figure since 28 August 2026 (b952ff4a8); queries deleted 6 September (993f7b0ee) | Seven figures in a Today / This week grid: New cases (today), Sent to Engineer (today, week), Reports sent (today, week), Completed (week), E-mails received (today). A half that is not defined is not drawn. Own refresh state; a failed read shows "Activity is unavailable." and no digit. Placement per design, or one panel under the counts headed "Activity" (figures across, Today and This week down; round 2) (item I). | 01–05, 36–40 |
| 3 | The next action is inside the expanded row | A and D draw it as a small dark button in the row, one width for every row (round 3); B, C and E keep it in the open row (item M). | 01, 04, 16–20 |
| 4 | Up to five clocks (rail, utility bar, header, two section heads) | One "Updated HH:MM" in the header; a section that failed says so in its own notice. Strip switch for the live per-section clocks (item J). | 01–05, 31–35 |
| 5 | "Lease expires HH:MM" on a Taken job | "Taken until HH:MM" (item K). Strip switch for the live wording. | 01–05 |
| 6 | Three tabs (v30 WD) | A keeps them; B and C put the lists side by side; D merges them into one ledger; E replaces them with four stage lanes (items A, L). | 01–05 |
| 7 | Eight kind chips (v30 WB) | A, C and E keep them (E above its lanes); B and D use a Kind dropdown with counts, D's adding New case and AI job (item L). | 02, 04 |
| 8 | AI jobs as cards | A draws them as a table with a State column and the actions on one line (round 2); C and D as rows of a table or compact rows; B lists the person's own on Mine; E puts the jobs in progress under "With AI" in the Review lane. | 01–05 |
| 9 | Every role opens on Office | B opens an Engineer on Mine and an Administrator on Office; the others keep Office (item N). | 02, 07 |

## 2. Live rules the mockup mirrors

- **Scope** (`NeedsAttentionPolicy.IsMine`, `CanTake`): Mine is the rows the person owns plus unowned Unassigned and Triage rows; seven of the fourteen fixture rows. The choice is remembered per browser (`work-centre.js`, `SCOPE_KEY`).
- **Chips** count over the whole scope before any filter; Find narrows by reference, title, detail and owner; Clear filters appears when either is on; a filter that matches nothing reads "No work matches these filters." (FRD-15).
- **Groups** Overdue (n), Due today (n), Later (n), an empty group not drawn, order by due instant with undated last (`NeedsAttentionPresentation.Groups`, `OperationsSnapshot.Order`).
- **Row text** (`NeedsAttentionPresentation.RowTitle`, `RowSubject`, `OwnerLabel`, `Facts`): "Chase due" for a chase, "Held decision", "Review Case", "Assign Engineer", "Vehicle images paired", the Unidentified reason, "Finding required", "{Kind} draft ready"; "No Engineer" or "No owner"; the six facts of the open row.
- **Dismiss** (`WorkCentreDismissalPolicy`): a dismissal belongs to the record, applies for everyone, hides every row of that record, returns when the row re-qualifies; no undo, no notice, no dismissed list; metrics unchanged. QDOS26203 (a chase and a new case) and job j4 (an AI draft row and a job row) demonstrate the record-level rule.
- **New cases** (`RecentCasesPolicy`): the last 7 calendar days, newest first, "Changed by automation" rows, "Since you last looked" where rows are newer than the last look.
- **AI jobs** (FRD-27, `ListWorkCentreAiJobs`): Draft ready, Taken, Queued, then Failed, newest first; Review estimate, Open query, Complete job, Open Case on a failed Case job.
- **Metrics**: five links to `/Cases?tab=`, 0 drawn as 0, a failed read drawing nothing.
- **Labels**: exact strings from `OperatorLabels.WorkCentre`, `OperatorLabels.AiJobs` and `OperatorLabels.Busy`; the Assign Engineer dialog is the live markup.
- **Banned-word exceptions already live**: "Work Centre is unavailable. Refresh to run the live queues again." and the job state "Queued" carry a word the design authority bans; the self-check exempts those two strings and nothing else.

## 3. Frame rules

- No working-set strip. The utility bar is 48px; the content cap is 1580px; the page pad is 18px.
- Body text 13.5px, controls 36px, the small row controls 32px, rows at least 40px, radius 3px (4px on panels).
- One primary (Create Case) in the page header; the utility bar's New case is omitted on this page.
- C's rail is 380px and B's 340px at 1201px and above, folding under the ledger below that; E's lanes go to two columns under 1201px and one under 980px; A, C and D fold Owner and Received into the task cell under 980px.
- No explanatory copy. Every visible sentence is a live string, a fixture value or a mockup-only control; the self-check's allow-list proves it.

## 4. Deliberate departures from live

- The Dismiss form posts nothing: the mockup removes the rows in place and moves focus. Live, every Dismiss is a POST and redirect (Stage 2 returns to `#wc-row-{next}`).
- Design D filters by one kind at a time (a dropdown), where the chips allow several.
- Design B's "New cases (n)" link and every other link that leaves the page open a "Mockup destination" dialog instead.
- Design E shows "Shown in the strip above" in the Out lane when the strip switch is on; that text is mockup-only.
- Fixture dates say Wednesday 7 October 2026 so that Today and This week differ; the shell clock reads 09:41 and Refresh moves it to 09:42.

## 5. Lettered sign-off items

- **A.** Choose a design: A · Even ledger, B · Morning brief, C · Split desk (recommended), D · One list or E · Flow lanes. *Confirm, or choose another, or name the pieces to combine.*
- **B.** Dismiss is an always-visible icon-only X at the end of every row of every list, named "Dismiss {reference}"; the open row no longer repeats it; focus moves to the next row. *Confirm, or a text "Dismiss" button.* (Strip switch "Dismiss control".)
- **C.** The seven figures: New cases today; Sent to Engineer today and this week; Reports sent today and this week; Completed this week; E-mails received today. Needs sorting is not restored (it is the Unidentified count). *Confirm, or amend.*
- **C2.** The new labels these designs need, all plain nouns: Activity, Today, This week, Sent to Engineer, Reports sent, Completed, E-mails received, "Activity is unavailable.", Kind, All kinds, New case, AI job, New since you last looked, AI jobs in progress, My AI jobs, Arrived, Waiting, Out, With AI, Taken until, and the table heads Case, Detail, Arrival, Job, Instruction, Record, Started, Note. *Confirm the ones the chosen design uses, or reword.*
- **D.** Windows: Today from midnight Europe/London, This week from Monday 00:00. *Confirm, or the last 7 days to match the New cases list.*
- **E.** Sent to Engineer counts a Case's first export to the Engineer (`EvaFirstHandoffProxies`), as the old tile did. *Confirm, or count every export.*
- **F.** Reports sent counts sent report e-mails (`StaffMailSendOperations`, CaseReport, Sent), the source Administration → Reports MI-01 uses, so the week agrees with that report. *Confirm, or the old tile's `CaseReportSentEvidence`.*
- **G.** New cases today leaves out Triage Cases, as the New cases list does. *Confirm, or count every Case.*
- **H.** Completed this week counts Cases entering Complete in the window, including one later reopened. *Confirm, or exclude reopened.*
- **I.** The figures are office-wide for every role, plain text except New cases, which opens the New cases section. The chosen design keeps its own placement (a panel table under the counts in A, a table in the rail in B and C, the counts' strip in D, the lane heads in E). *Confirm, or the panel table under the counts in every design, or let the figures follow Office / Mine, or link Reports sent to Administration Reports.* (Strip switch "Activity placement".)
- **J.** One "Updated HH:MM" in the header; the per-section clocks go. *Confirm, or keep them.* (Strip switch "Updated clock".)
- **K.** "Taken until HH:MM" replaces "Lease expires HH:MM". *Confirm, or keep it.* (Strip switch "Taken job note".)
- **L.** Controls that move from where you placed them on 25 September 2026, each by design: L1 the tabs removed (C, D, E); L2 Office / Mine moved to the header (B); L3 the chips replaced by a Kind dropdown (B, D); L4 the five counts moved into the lane heads (E); L5 AI job cards drawn as rows (A, B, C, D, E); L6 the first screen showing only New cases since you last looked, the 7-day list behind a link (B). *Confirm each for the chosen design, or keep the v30 placement.*
- **M.** The next action is a visible button in the row (A, D). *Confirm, or keep it in the open row only.*
- **N.** An Engineer opens on Mine and an Administrator on Office (B only). *Confirm, or Office for everyone.*
- **O.** On an all-empty page the Activity figures still render beside "No work to show." *Confirm, or hide them.*
- **P.** Documentation in Stage 2: FRD-15 replaces the stale "`New cases today` … separate from Sent to Engineer and Reports sent" paragraph with the restored definitions; `docs/capabilities.md` UI-04 and `docs/open-decisions.md` UI-04 close; CONTEXT.md's metric terms are re-confirmed. *Confirm.*

## 6. Self-check

- 5 October 2026: `python check-work-centre-designs.py` gave `RESULT {"fail":[],"okCount":1392}` with no console error and no external request.
- Coverage: 5 designs × 15 presets (one h1, one primary, no utility New case, no Operations link, one visible Dismiss as the last control of every row, none inside the open row, the five metric links and figures, the activity figures or their notice, one Updated clock, the banned-word scan, the copy allow-list, chips carrying text, the empty states, Mine = 7, the filters, Find, the dismissed preset, the unavailable presets, the Assign dialog), then the four strip switches, the Engineer role, and per design the Dismiss flow (two rows leave, focus moves, metrics unchanged, no notice; the job's draft row and job row leave together; the last job hands focus to the heading), Assign Engineer (focus, Tab containment, Escape returning focus, assignment turning the row into Review Case), Assign to me on a Triage, Complete job, Refresh keeping filters and the open row, the Mine switch, Find as you type, A's tab arrow keys, D's group collapse and Kind dropdown, E's lane membership, and the 760px geometry (no page overflow, controls at least 32px, rows at least 40px). The live baseline is checked for the three Dismiss treatments.
- Screenshots: 128 screenshots (eight states × five designs × three widths, the live baseline at three widths, five Assign Engineer dialogs) and 6 full-page captures in `v32-work-centre-shots/`, listed in `verification.json`.

## 7. Known limits

- **Round 2 (5 October 2026).** The operator's three remarks on A (stacked actions, the cramped state chip, the loose activity strip) are applied to every file; see the discussion log. The remark about the chip was read as "give it its own place": it has its own State column.

- **Actions are simulated.** Dismiss, Assign, Assign to me, Complete job and Refresh change only the page and land a fixed result. There is no concurrency conflict, no failed send and no server round trip.
- **Figures are fixed.** The activity numbers are fixture values; they do not recount when a row is dismissed, which matches the rule that a dismissal changes no figure.
- **Paging** is drawn as "Page 1 of 1"; no design has more than one page of fixture rows.
- **Fonts** are inlined from the checkout; a missing glyph falls back to the system font.
- **Links that leave the page** show a "Mockup destination" dialog; that dialog is demo control, not product UI.

## 8. Approval — 5 October 2026

The operator chose **A · Even ledger** ("Select option A") and confirmed every item B to P as written ("confirmed on all of the items"). For A that settles: the icon-only X at the end of every row with focus moving to the next row (B); the seven figures and the new labels A uses (C, C2); London day and Monday week (D); first export (E); the MI-01 source for Reports sent (F); no Triage Cases in New cases today (G); reopened Cases still counted as Completed (H); office-wide plain figures in the Activity panel under the counts, New cases linking to its tab (I); one Updated clock (J); "Taken until" (K); the tabs and chips stay, AI job cards become a table (L5), the next action is a row button (M); N does not apply to A; the figures still show on an all-empty page (O); the documentation changes (P). Stage 2 starts from this commit.
