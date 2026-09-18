# Create Case

- **Mockup route:** `pegasus_cases_index_v28.html#create` in [`../../../current/`](../../../current/README.md) (revealed inside the Cases index build, per the shell's own "New case" link)
- **Live source:** `src/Pegasus.Web/Pages/Cases/Create.cshtml` (model `Create.cshtml.cs`) — manual-creation branch only (`IsManual`)

- [**How it works**](how-it-works.md)

## Screenshots

- [s17-cases-create-1580.png](../../../current/v28-shots/s17-cases-create-1580.png) · [1440](../../../current/v28-shots/s17-cases-create-1440.png) · [760](../../../current/v28-shots/s17-cases-create-760.png)

## Notes

- Only the **manual** Create Case form (`IsManual` true, reached with no
  `receiptId` — the shell's "New case" link and Ctrl+N) is captured. The
  **received-file-seeded** variant of this same page (reached only from an
  Upload confirmation or a received item's own "Create Case" action, carrying
  a `receiptId`) is a materially different screen: it shows read values with
  provenance icons (Extracted/Entered), an inspection-address resolution
  step with a found-vs-entered choice, an Image Based Assessment notice, and
  a refusal state for material that cannot become a Case. It is not linked
  from Cases index or from the shell's own "New case" action, so it belongs
  to whichever lane captures Upload/received-item screens, not this one; it
  is dropped here rather than guessed at.
- The manual form's fields, order and both actions (Create case / Cancel)
  are transcribed exactly from the live `IsManual` branch. The Claim Source
  select's options (Wingrove Insurance, Fenwick Mutual) are representative;
  the live list is read from the contact directory's Claim Source role at
  request time.
- The "Audit" Case type option is never offered on the manual form in the
  live page (`CreateModel.OnPostCreateManualAsync` rejects `CaseType.Audit`
  outright, and the manual `<select>` only ever lists Inspection and
  Inspection and Audit) — this capture keeps the same two options and no more.
