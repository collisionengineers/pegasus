# Cases — how it works (Unidentified tab and Blocked rows so far)

Read from the live source on 13 September 2026 (`src/Pegasus.Web/Pages/Cases/Index.cshtml(.cs)`). Only the part touched by the received-file decisions is written up.

- The Unidentified tab lists open Unidentified items, then every receipt whose decision is Blocked intake, in one merged page (`MergedPageSize`).
- Blocked rows carry the chip "Blocked intake", the facts File, E-mail, Received, Reason (`OperatorLabels.IntakeFailure`), and the action "Open received item", which opens `/Intake/Details`.
- Blocked rows are listed but never counted: the tab count and the rail Cases sum count open Unidentified items only (D14 in the page's own notes), so "Unidentified" and "Blocked" keep separate meanings.
- The Work Centre's Blocked metric links here.

Governing documentation: [FRD-12 § Cases: queues and filters](../../../../../docs/frd/frd-12-operator-experience.md#cases-queues-and-filters) and § Work Centre.
