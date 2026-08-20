Operator confirmed

## Release-14 live confirmation — 2026-08-20

Released in **release 14** (`d91fd7d7…`, PR #461), production smoke passed; promoted to `main` (`39bb118a`). Live (signed-in browser pass): the Not-ready Image-initiated table shows the **Chase** column with "Not yet due" chips on all eight records and the separate **Received** date per record; `ImageIntakeChaseSchedule.IsChaseDue` reuses `CaseChaseSchedule.FirstChaseAt` (no duplicated cadence). Pairing visibility is the derived Associated-with-Case state per the capability's own text, with `image_initiated_case_merged` history written in the merge transaction.
