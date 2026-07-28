# EV-2026-07-23 — Wave 8 MSG subset

Scope: custom BCL-only MSG/MAPI extraction subset over the managed CFB reader. Independent review rejected the initial implementation; a correction pass fixed variable multi-value layout, contextual one-pass decoding, cancellation, cumulative bounds, outcome/evidence preservation and storage/class projection defects. This remains a row-specific synthetic subset, not complete MS-OXMSG/MS-OXPROPS parity or Wave 8 acceptance.

Implemented behaviour includes bounded root/child property bags, fixed/variable/multi-valued values with raw unknown preservation, selected named-property and codepage handling, recipients, plain/HTML body policy, bounded MELA/LZFu with shallow passive RTF text, by-value/reference/OLE/embedded attachment policy, embedded-message depth, cancellation, protected-class recognition and selected mail/report/meeting/calendar/contact/list/task/note/generic projections.

No OLE object, external path, protected content or embedded program is activated. Unsupported properties/classes remain raw with issues.

```powershell
dotnet test --project tests\unit\CollisionDocNet.Outlook.Tests\CollisionDocNet.Outlook.Tests.csproj --configuration Release --no-restore
```

Corrected result: Outlook Release exit `0`; 42 succeeded, 0 failed, 0 skipped. The correction agent also ran the then-current full solution: exit `0`, 393 succeeded, 0 failed, 0 skipped. Formatting passed and the requested static performance scan found no critical pattern. Inputs were owned synthetic CFB/property fixtures only.

Remaining gates include the complete property catalogue and source spans, wider code pages, EML transport-header delegation, full RTF/encapsulated HTML, stable shared asset IDs, cumulative shared budgets, recurrence/time-zone and other item-class semantics, CMS/TNEF parsing, genuine item-class corpora, conformance, differential, fuzz and performance acceptance.
