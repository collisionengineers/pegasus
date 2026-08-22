# Files

Committed in `3d7f87d6`.

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | `IntakeSourceChannel.Mailbox => "E-mail"` and `"mailbox" => "E-mail"` — the typed and string overloads together |

Two lines, because one label has two overloads. Changing one and not the other is the same
one-list-per-concept split that produced the Odometer/Mileage confusion in [[CASE-015]].

## Checked while there

The other `IntakeSourceChannel` labels — `ManualUpload` and `Automation` — were reviewed
for the same problem. Neither describes configuration rather than what the operator sees,
so both are left alone.
