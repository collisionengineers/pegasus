# v32 current: five Work Centre proposals

**What this is.** Offline HTML mockups of the Work Centre, made at the operator's request on 5 October 2026 for issue 1017 (Dismiss only visible on New cases) and the activity figures that left the page in August. It is a temporary design review artifact. It is not application code, not design authority and not implementation evidence. The final Stage 2 PR removes or keeps this folder as the operator instructs.

**How to open it.** Open [pegasus_work_centre_designs_v32.html](pegasus_work_centre_designs_v32.html) in a browser and pick a design. Each design file opens on its own; no server or network is needed. [pegasus_work_centre_live_v32.html](pegasus_work_centre_live_v32.html) is today's page drawn from the same data, for side-by-side comparison.

**Mockup controls.** The dark panel at the bottom left is a demo control, not product UI. It holds:
- the A to E switch and Today
- the 15 page states
- the role (Administrator, Engineer, User)
- the four undecided choices as switches: Dismiss control (item B), Activity placement (item I), Updated clock (item J), Taken job note (item K)
- an opener for the Assign Engineer, Account and Notifications dialogs

Every state is also reachable by query string: `state=`, `role=`, `opt=dismiss:text,stats:strip,clock:section,taken:lease`, `scope=`, `selected=`, `tab=` (A and Today), `assign=1` and `embed=1`.

| File | Holds |
| --- | --- |
| [pegasus_work_centre_designs_v32.html](pegasus_work_centre_designs_v32.html) | Comparison page: the five designs, what they share, and the recommendation |
| [pegasus_work_centre_a_v32.html](pegasus_work_centre_a_v32.html) | A · Even ledger |
| [pegasus_work_centre_b_v32.html](pegasus_work_centre_b_v32.html) | B · Morning brief |
| [pegasus_work_centre_c_v32.html](pegasus_work_centre_c_v32.html) | C · Split desk (recommended) |
| [pegasus_work_centre_d_v32.html](pegasus_work_centre_d_v32.html) | D · One list |
| [pegasus_work_centre_e_v32.html](pegasus_work_centre_e_v32.html) | E · Flow lanes |
| [pegasus_work_centre_live_v32.html](pegasus_work_centre_live_v32.html) | Today: the live v30 B page drawn from the same fixtures |
| [build-work-centre-designs.mjs](build-work-centre-designs.mjs) | `node build-work-centre-designs.mjs` rebuilds all seven files from `lib/` and the live shell, CSS and sprite in this checkout |
| [lib/](lib/shared.mjs) | Shared shell (from v31, with the Operations link removed and New case omitted on this route), fixtures, the five renderers, CSS and the runtime |
| [check-work-centre-designs.py](check-work-centre-designs.py) | Self-check and screenshot capture (Playwright) |
| [v32-work-centre-shots/](v32-work-centre-shots/verification.json) | Screenshots and the dated self-check record |
| [v32-notes.md](v32-notes.md) | Changes, live rules mirrored, departures, lettered sign-off items, known limits |
| [discussion-log.md](discussion-log.md) | The operator's brief and each round |

## Status

On 5 October 2026 the operator chose design A and confirmed items B to P ([v32-notes.md](v32-notes.md), section 8). Stage 2, the Razor implementation of A, was delivered in the same pull request; FRD-15 owns the behaviour from then on. This folder is historical reference.
