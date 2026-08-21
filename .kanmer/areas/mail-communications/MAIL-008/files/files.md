# Files — MAIL-008

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | `MailClassification(MailCategory)` — one label map (family word + " · " + subtype word); Other renders the operator's own `OtherName` verbatim |
| `src/Pegasus.Web/Presentation/MailClassificationSelection.cs` | Picker option labels resolve through the same map |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | `DecisionLabel(MailCategory)` delegates to the map (Decision card, corrections, Index outcome cell, quick preview all inherit) |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Kebab-slug assertions replaced with the operator wording; coverage fact over `Enum.GetValues` |

The folder-move **reason** row is omitted from the move confirmation (rows
render only when populated) until the operator settles its wording — the
stored machine reason keeps being recorded unchanged.

`MailClassificationContracts.CategoryName` untouched — settled registry key;
`ParseReceivedFamily`/`ParseSentFamily` round-trips unaffected.
