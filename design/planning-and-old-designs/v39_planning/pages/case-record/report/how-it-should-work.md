# Report delivery: how it should work

Decided with the operator on 6 October 2026. The rules below are what the source now does under those rulings; the round awaits design sign-off.

- **D1.** A report is sent in one step: the Report section's delivery form holds the From line, the recipients, the documents to attach, the report's name and the covering message, and its one **Send report** sends them with no separate preparation and no second confirmation.
- **D2.** The form is offered in the edit session once the generation and its report are confirmed, and is offered again after a send whatever its outcome, headed by "Already sent on {date time} to {recipients}." once a report of this work was sent.
- **D3.** The page makes one plan from the Principal's report sending rules and the viewed work's facts when it loads, every part of the form follows it, and Send report plans again with staff's answers and follows that.
- **D4.** The From line says whether the report replies in the instruction's thread from the mailbox that holds it, or is a new message from the Principal's send-from mailbox, else the default staff-send mailbox.
- **D5.** To and Cc start as the plan's recipients, staff may edit them, and the hint under Cc names who set each copy and what the rules removed and why; removals win at send.
- **D6.** Each condition the Case cannot answer is a required Yes or No question under "Answer before sending", and an answer applies to that send only.
- **D7.** A rule's Stop shows "Stopped." with its text and a required Override reason, and the send records the Stop and the reason.
- **D8.** Each hold has a required **Done** tick; a hold an unanswered question could bring in has a tick that is not required, and Core refuses the send if the answer brings it in unticked.
- **D9.** The report is always attached; a companion the Principal requires is ticked, locked and labelled "(required for this Principal)", and while it is not generated the form shows the approved notice and no Send report.
- **D10.** The filed estimates the Principal's rules attach are named under "Filed estimate"; none filed, or no separate fee note where the Principal expects one, is a warning line and never stops the send.
- **D11.** File name shows the name the send will give the report, from the Principal's pattern or the standard name.
- **D12.** Message is the Case report delivery template rendered for this Case, editable, required and at most 5000 characters, and what is submitted is what is sent.
- **D13.** After sending lists the tasks the send records, which become Case tasks when Report sent is recorded.
- **D14.** A send whose form was drawn from rules or Case facts that have since changed is refused with "The report was not sent. This Principal's report sending rules or the Case's instruction changed; check the form and send again."
- **D15.** The outcome notice states only what the transport reports, and only an observed send says sent.

Open: the notice shown when a Stop is only possible: today the Override reason box appears with no sentence.

Open: the wording when the rules changed under an open form (D14's sentence, which names only the rules and the instruction).

Open: the hold ticks' alignment: the checkbox and "Done" sit far from the hold text.

Open: the "Yes" radio label is not visible beside the questions.

Open: two approved sentences were reworded from "prepare" to "send" when delivery became one step ([design authority](../../../../../../docs/design/README.md), "Voice, labels and necessary copy"). That section does not name the two; of the nine report sending sentences, the missing companion notice on this form and two of the Contact panel's hints ("Send stays off until staff tick this as done.", "Send is refused; staff may override with a reason.") use the word.

Open: whether the Already sent line belongs above or below the form.

Open: a Principal whose fee note is a separate PDF and whose separate fee note is not generated: a warning only today.
