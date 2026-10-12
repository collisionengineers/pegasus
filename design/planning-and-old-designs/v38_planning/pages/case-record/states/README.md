# Case record states (Repair Spec)

Temporary review artifact; see the [page README](../README.md).

The mockup draws one Case (With Engineer, the v36 frames) in read mode and in the edit session, with two repair specs. Every state is a query string of `current/pegasus_repair_spec_v38.html`.

| State | Query | What it shows |
| --- | --- | --- |
| Reading the Current spec | `mode=read&spec=2` | the imported supplementary estimate, 17 lines, the sent-version spec on the other tab |
| Editing the Current spec | `mode=edit&spec=2` | the same, with the editor's controls; P3's filled Supplementary on the proposal |
| Reading or editing the earlier spec | `spec=1` | the spec with versions v1 (imported) and v2 (sent on report); the working draft has one amended line |
| Target % preview | `mode=edit&scale=60` | the live preview: lowered cells amber, the readout, Preview, Apply |
| Agreed sum recorded | `mode=edit&sum=2750` | today: the read-only sum and the mismatch chip; proposal: the sum box previews the scaling, and after a later edit the mismatch chip rescales on press |
| Compare open | `dialog=compare-estimates-dialog` | today: spec to spec; proposal: the last sent version against Working, side by side |
| Versions open | `dialog=versions-dialog&spec=1` | the three rows; proposal adds Compare with current |
| Split | `split=1` | Today and the proposal side by side |
| Diff | `diff=1` | every changed region outlined and labelled P1–P5 |

Each proposal is a variable: `opt=cols:dash`, `cmp:side`, `supp:auto`, `agreed:drives`, `rate:both|bar`; `design=proposal` turns them all on (`rate:both`), `design=today` leaves them off.
