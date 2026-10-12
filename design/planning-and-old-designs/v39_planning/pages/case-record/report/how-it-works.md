# Report delivery: how it works

Read from the live source on 6 October 2026 (branch `task/report-dispatch-integration`, PR 1015 as built).

## What the form does not show

- The form exists only in the edit session. `canSendReport` needs the section's lease (`leaseForReport` is `SectionIsEditable("report")`), a Confirmed generation and a confirmed report. Reading the Case shows no form, no recipients and no Send report.
- The subject is never shown. The plan works it out (`ReportDispatchPolicy.Subject`: "Re: {instruction subject}" for a reply, "{REG} Report" for a new message) and the send uses it.
- A copy that an answer adds is not drawn. The form is planned with no answers; at send, `ReportDispatchPolicy.Reconcile` adds the copy an answered rule makes.
- An address staff type that the rules exclude is taken out at send without a word. `Reconcile` returns what it removed, and `SendCaseReport` discards it (`var (review, _) = …`). The Cc hint lists only what the plan itself removed.
- A possible Stop has no sentence. When an unanswered question could make a Stop apply (`StopPossible`), the Override reason box appears, not required, with nothing saying why.
- A possible hold's tick is not required on the page. If the answer makes that rule hold, Core then requires the tick and refuses the send without it.
- The fee note is ticked by default whenever it is confirmed, for every Principal (`checked="@(always || attachKind == CaseReportArtifactKind.FeeNote)"`).
- When no mailbox address resolves, the From line prints its words with an empty address (`plan.MailboxAddress ?? string.Empty`).
- Filed estimates are named but cannot be unticked. The rules decide them.

## Governing documentation

| Document | What it settles for this form |
| --- | --- |
| [FRD-11 Companion documents and what a delivery attaches](../../../../../../docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md#companion-documents-and-what-a-delivery-attaches) | One step (6 October 2026), the form offered again after a send, the attached report's name, the covering message, recipients in SOP R12 order, removals winning |
| [FRD-11 Report sending rules](../../../../../../docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-sending-rules) | Mode, conditions, questions, holds, Stop, required companions, filed estimates, fee note warning, After sending, name pattern, refusal when the rules or instruction changed |
| [FRD-16 Report](../../../../../../docs/frd/frd-16-case-record-workspace.md#report) | The Report section; Generate and Send report in both views |
| [FRD-21 Outbound correspondence](../../../../../../docs/frd/frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence) | "From which mailbox": a reply from the mailbox holding the instruction, else the Principal's send-from, else the default; fail closed naming the address |
| [FRD-17 E-mail templates](../../../../../../docs/frd/frd-17-administration-workspace.md#e-mail-templates) | The Case report delivery template, its placeholders and built-in body |
| [Design authority](../../../../../../docs/design/README.md) | The approved sentences: the missing companion notice, the two warnings |
| [CONTEXT.md](../../../../../../CONTEXT.md) | Report sending rules; their Hold and Stop are not the Held Case state |

## Source by layer

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pages/Cases/Shared/_CaseReport.cshtml` | The form (`data-send-report`): every block listed in [panels](panels/README.md) |
| Web | `Pages/Cases/Details.cshtml.cs` | Reads the facts and send history, completes the facts with the generation, makes the one plan on load (`DeliveryDispatchPlan`); `PostedReportDecisions`; `OnPostSendReportAsync` and the outcome notices |
| Web | `Pages/Cases/Details.Report.cs` | `ReportDeliveryFileName`; `ReportDeliveryMessage`, the rendered template |
| Web | `Presentation/CaseWorkspaceLabels.cs` (`ReportDelivery`) | Every word on the form and every outcome sentence |
| Core | `Reports/ReportDispatchPolicy.cs` | `Plan`, `Evaluate`, `Reconcile`, `Subject`, `Fingerprint`, `Greeting`, the questions' words, the rule labels, the two warnings |
| Core | `Reports/CaseReportDelivery.cs` | `SendCaseReport`; `CaseReportDeliveryPolicy` (`RequireDispatchDecided`, `AttachChoice`, `ReviewedAddress`, `AddressBook`, `SuggestedReview`, `CoveringMessage`, `CompanionWords`); `CaseReportDeliveryNaming` |
| Core | `Reports/PrincipalReportSendingRules.cs` | The rules the plan reads |
| Infrastructure | `Persistence/EfReportRecipientSuggestionQueries.cs` | The facts: the Principal's rules, the instruction e-mail and its mailbox, the sender, the instruction text, the viewed work's Claim Source, Repairer, registration and outcome, the contact names, the default mailbox, the filed estimates |
| Infrastructure | `Persistence/EfCaseReportGenerationStore.cs` (`GetForDeliveryAsync`) | The generation re-read under the lease at send |

## The behaviours

### When the form is drawn

The form is drawn under the preview card when `canSendReport` holds: the section is editable, the generation is Confirmed and its report artifact is Confirmed. A send needs a confirmed report, not every artifact confirmed. The form is offered again after a send, "so the same report can be sent again (operator, 6 October 2026)" (`_CaseReport.cshtml`). When a report of this work was already sent, the first line is `ReportDelivery.AlreadySent`: "Already sent on {d MMM yyyy HH:mm} to {recipients}." from `ReportSendHistory`.

### One plan, made on load

`DetailsModel` reads `IReportRecipientSuggestionQueries.GetAsync` for the viewed work, adds the send history and the generation's confirmed kinds, and calls `ReportDispatchPolicy.Plan(facts, [], now)` once. Every part of the form reads that `DeliveryDispatchPlan`. Send report plans again with the posted answers. Origin: FRD-11 "Report sending rules"; Report Sending SOP v5 rule R12.

### From

`plan.Mode` is `ReplyInThread` when the Case holds its instruction in an approved mailbox (`ReportInstructionMessage`), else `NewMessage`. The address is the instruction's mailbox, else the Principal's `SendFromMailbox`, else the default staff-send mailbox. The line reads `FromReply` ("From {address} — reply in the instruction's thread") or `FromNewMessage` ("New message from {address} (no instruction e-mail on this Case)"). At send, `SendingMailboxAsync` refuses a mailbox that is not Approved, identity bound, bound for StaffSend and SentEvidence, with a valid Generation, naming the address.

### Recipients

`CaseReportDeliveryPolicy.SuggestedReview(plan)` fills To and Cc, one input per address, and an empty input when there is none. To is the Principal's Send to, else the original sender. Cc is the instruction's Cc when Reply all is on, then the Principal's Cc, then each applied rule's Cc add, none at all with Send to only. Never cc and each applied rule's Cc remove are taken out, and an address in To is not copied. Every field offers the Case's address book (`AddressBook`: the original sender and the instruction's Cc as "This case", the Principal's To and Cc as "Principal"). The hint `ReportDelivery.CcHint` reads "Cc set by: {sources}; removed: {address} ({reason})". Each source is "Instruction Cc", "Principal Cc" or "Rule N: …" (`RuleLabel`). At send, `Reconcile` applies the answered plan to what staff submitted and `ReviewedAddress` refuses an invalid address or an empty To.

### Questions

Each condition of an undecided rule that the Case cannot answer becomes one row under "Answer before sending" (`ReportDelivery.Questions`): the question text and a required Yes/No radio pair named `decision-{rule}-{condition}`. The words come from `ReportDispatchPolicy.Question`, for example "Is the Claim Source Car 2 Go?" or "Do the bodyshop details mention Easdons?". `PostedReportDecisions` reads them back. An answer never overrides a fact the Case holds (`Condition` returns the fact first). `RequireDispatchDecided` refuses the send while any question remains: "Answer every question about this Principal's rules before sending the report."

### Stop

When an applied rule has a Stop, the form shows a warning notice "**Stopped.** {stop text}" and a required "Override reason" box. When a Stop is only possible (`StopPossible`), the box appears, not required, because "the same click answers and sends" (`_CaseReport.cshtml`). Core refuses a Stop with no reason: "This delivery is stopped: {stop} Give a reason to override the stop." The send records the Stop and the reason (`ReportDispatchRecord`).

### Holds

The Principal's Hold and each applied rule's Hold are rows with a required "Done" tick (`holdsDone`). The holds of rules that are still undecided (`PossibleHolds`) have a tick that is not required. `RequireDispatchDecided` refuses unless every hold of the answered plan is ticked: "Tick Done against every hold before sending this report."

### Attach and required companions

The Attach ticks list each kind the generation holds Confirmed: Report, Fee note, Repair Spec, Images. The report is always ticked and locked. A companion the Principal requires (Vehicle images document, Figure breakdown) is ticked, locked and labelled "(required for this Principal)", and posts through a hidden input. A required companion that is not generated shows `ReportDelivery.MissingCompanion` ("The {document} this Principal requires has not been generated. Generate it, then send the report.") and Send report is not drawn. Core refuses the same case in `AttachChoice`.

### Filed estimates and warnings

When the Principal attaches the Audatex or the estimate, the form lists "Filed estimate" with the file names (`plan.FiledEstimates`). Audatex-format files go when Audatex is ticked; other recognised estimates go when Estimate is ticked. They are attached after the generation's documents and rechecked at send. With none filed, the muted line is `NoFiledEstimateWarning`, "No recognised estimate is filed on this Case.". A Principal that wants the fee note separately and has none confirmed gets `NoSeparateFeeNoteWarning`, "This Principal expects the fee note as a separate document and none is confirmed.". Neither stops the send.

### File name

`ReportDeliveryFileName` reads `CaseReportDeliveryNaming.ReportName`: the Principal's `AttachmentName` First on a first send and Resend after, with `{ref}`, `{reg}`, `{outcome}`. Without a pattern it is "{ref} {reg} {outcome} report" plus one dot per earlier send.

### Message

The textarea holds the Case report delivery template rendered for this Case (`ReportDeliveryMessage`), with `{greeting}` from `ReportDispatchPolicy.Greeting`: "morning" before 12:00 London, else "afternoon". It is required, at most `EmailTemplates.MaximumBodyLength` (5000), and what staff submit is what is sent (`CoveringMessage`). A blank or long message is refused with `MessageRefused`.

### After sending

The Principal's Reminders, each applied rule's Remind, and `GarageFiguresTask` ("Send the figures to the garage.") on a repairable outcome when Garage figures is on are listed under "After sending" as text. The send records them (`ReportDispatchRecord.AfterSendTasks`). They become Case tasks when Report sent is recorded (see [Tasks](../tasks/how-it-works.md)).

### When the rules or the Case changed under the form

The form posts `dispatchFingerprint`, the `ReportDispatchFacts.Fingerprint` it was drawn from. It hashes the rules (`Canonical`), the instruction message, the sender, the Claim Source, the Repairer, the outcome and the instruction text. A mismatch refuses with `CaseReportDeliveryPolicy.DispatchChanged`: "The report was not sent. This Principal's report sending rules or the Case's instruction changed; check the form and send again."

### Send and its outcome

`OnPostSendReportAsync` checks the message, then calls `SendCaseReport`. That reloads the generation under the lease, plans with the answers, runs `RequireDispatchDecided`, reconciles the recipients, works out the attachments and the name, picks the mailbox and hands one staff send command to the transport under the form's operation key. A refusal returns to Report with Core's sentence (`MutationRefusalMessage`). Otherwise the handler forgets the browser's copy of the lease (`ClearLeaseState`) and states the transport's outcome: "The report send was observed as sent.", "The report send was accepted.", "The report send is in progress.", "The send was cancelled.", "The report send failed." or "The send result is not yet known.". Only an observed send says sent. Origin: "Sends the current generation's report in one step (operator, 6 October 2026)" (`OnPostSendReportAsync`).

### Views

The facts are read for the viewed work (`CaseWorkScope.SelectedIds`), so the Inspection view plans from the Inspection's Claim Source, Repairer and outcome and names the Inspection report.

## Things the FRD does not settle

- Whether the form shows the subject it will send.
- Whether a copy an answer adds, and an address the rules remove from what staff typed, are shown before or after the send. FRD-11 says "the form says what was removed and why"; the source says it only for the plan's own removals.
- What the page says when a Stop is only possible.
- Whether the fee note is ticked by default for a Principal that does not expect it separately.
- What the From line says when no mailbox is known.
- Where the Already sent line sits, and whether it is the only record of earlier sends on the form.
- The order of the blocks: the holds come after the message and the After sending list, at the foot of the form, away from the questions that can create them.
- Which refusals, beyond the rules and the instruction, the "changed" sentence should name: the fingerprint also covers the Claim Source, Repairer, outcome and sender.
