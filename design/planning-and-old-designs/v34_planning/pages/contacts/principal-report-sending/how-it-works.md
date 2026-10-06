# A Principal's Report sending panel: how it works

Read from the live source on 6 October 2026 (branch `task/report-dispatch-integration`, PR 1015 as built).

## What the panel does not show

- There is no read view. The panel is a full edit form whenever the Contact page opens on an active Principal, like the other Principal panels.
- A refused save is not reported in the panel. The error is a page-level model error, so it appears in the validation summaries higher up the page: the Contact form's and Principal settings'.
- Without script an empty list cannot be added to. Send to, Cc, Never cc and Reminders draw one input per saved value and no blank one, and **Add address** / **Add reminder** are script.
- Without script every value field of an unchosen condition line shows. Only script hides the fields its kind does not read.
- Send from offers every Approved mailbox. The send itself also needs the mailbox bound for staff send and Sent evidence (`SendCaseReport.SendingMailboxAsync`), and the panel does not say which ones are.
- A saved Claim Source or Repairer that is no longer a choice is listed by its raw id (`ContactOptions`).
- Nothing says a save affects delivery forms already open. The rules are part of every form's fingerprint, so a form drawn before the save is refused when it is sent.

## Governing documentation

| Document | What it settles for this panel |
| --- | --- |
| [FRD-04 Contacts administration](../../../../../../docs/frd/frd-04-parties-accounts-and-access.md#contacts-administration) | What the rules are and mean, the default rules, no length limit, a refused save naming the rule and keeping what was typed, the one-time SOP import with the TL thresholds in Notes on every Case |
| [FRD-17 Contacts](../../../../../../docs/frd/frd-17-administration-workspace.md#contacts) | The panel's place after Report generation, a Principal only, its controls, three condition lines, one spare blank row, Add rule, one Save report sending |
| [FRD-17 E-mail templates](../../../../../../docs/frd/frd-17-administration-workspace.md#e-mail-templates) | The delivery message the rules do not set |
| [FRD-11 Report sending rules](../../../../../../docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md#report-sending-rules) | What each setting does on a delivery |
| [Design authority](../../../../../../docs/design/README.md) | The five hints are approved copy (6 October 2026) |
| [CONTEXT.md](../../../../../../CONTEXT.md) | "Report sending rules"; Hold and Stop are not the Held Case state |

## Source by layer

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pages/Administration/Contacts/Edit.cshtml` | The panel (`data-report-sending-editor`): every control in [panels](panels/README.md); the spare rule row; the three condition lines |
| Web | `Pages/Administration/Contacts/Edit.cshtml.cs` | `LoadReportSendingChoicesAsync` (mailboxes, Claim Sources, Repairers); `OnPostUpdateReportSendingAsync`; `PostedReportSending`; `ReportSendingRulesErrorMessage` |
| Web | `wwwroot/js/contacts-edit.js` | Add address / Add reminder, Add rule (clones the spare row under the next number), Remove rule, showing the value field a kind reads |
| Web | `Presentation/OperatorLabels.cs` (`PrincipalAdministration`) | Every label, the five hints, `ConditionKind` |
| Core | `Reports/PrincipalReportSendingRules.cs` | The shape, `Default`, `Normalize` and its refusals, `Canonical` |
| Core | `Cases/OrganizationAdministration.cs` | `UpdatePrincipalReportSending`; Administrator only; `PlanPrincipalReportSendingUpdate` (inactive refused; version moves only on a change); a successor inherits the rules |
| Infrastructure | `Persistence/Migrations/20261007090000_PrincipalReportSendingRules.cs` | The required column, the SOP seed, the default for every other Principal, the retired recipient columns dropped |

## The behaviours

### Where it sits

The panel is drawn only for an active Principal (`Model.Principal.IsActive`), after Report generation and before the Salvage matrix. Every Principal has rules: `Principal.ReportSending` falls back to `PrincipalReportSendingRules.Default`.

### Addressing

**Send from** lists the Approved mailboxes by address, sorted, with "Not set". A saved address not in that list is added so it stays selected. **Send to**, **Cc** and **Never cc** are repeated e-mail inputs. **Send to only**, **Reply all** and **Garage figures** are ticks. Send to carries the hint "Empty: the original instruction sender."

### Attach

The six ticks are Fee note is a separate PDF, Estimate, Audatex, Report carries vehicle images, Vehicle images document required and Figure breakdown required (`ReportSendingAttachments`).

### Hold, Reminders, Attachment name

**Hold** is one text box with "Send stays off until staff tick this as done." **Reminders** are repeated text inputs. **Attachment name** has First send and Re-send, with "Use {ref}, {reg} and {outcome}. Empty: the standard name." Both empty means no pattern. One filled and one empty is refused as "Attachment name: the text is required.".

### Rules

Each saved rule is a fieldset with Match ("All conditions" or "Any condition") and at least three condition lines (`Math.Max(3, rule.If.Count)`). Each line is a kind select starting "No condition", then the kinds in `ReportSendingConditionKind` order: Claim Source, Repairer, Images from, Instruction mentions, Sender is not, Outcome, Bodyshop mentions. The value field depends on the kind: a multi-select of Claim Source contacts, of Repairer contacts, or of the outcomes (humanised codes), or one text box with "Separate several with commas." for Images from, Instruction mentions, Bodyshop mentions and Sender is not. The actions are Cc add, Cc remove, Remind, Hold and Stop, and Stop carries "Send is refused; staff may override with a reason." Each rule has **Remove rule**. One blank rule row follows the saved rules: "A rule row left blank is ignored, so the list keeps one spare row to type into" (`Edit.cshtml`). **Add rule** clones that blank row as it was when the page loaded, under the next number.

### What a save reads

`PostedReportSending` reads the posted fields in the order of the `RuleIndex` inputs. A condition line with no kind is skipped. A rule with no condition and no action is the spare row and is skipped. Text values split on commas, semicolons and line breaks; address values also split on spaces. Match defaults to All. "Nothing is checked here; Normalize refuses what is wrong."

### What Core refuses

`PrincipalReportSendingRules.Normalize` trims and de-duplicates, and refuses:

- an invalid address ("{field}: enter valid e-mail addresses.");
- an address in both Cc and Never cc;
- Send to only with no Send to;
- empty required text;
- an unknown contact or outcome ("choose a contact from the list.", "choose an outcome from the list.");
- a rule with no condition ("add at least one condition.");
- a condition with no value ("a condition needs a value.");
- a rule with no action ("add at least one action.");
- an unknown name token ("Attachment name: use only {ref}, {reg} and {outcome}.").

A rule's refusal names "Rule N". There is no length limit on any text. Only an Administrator may save (`OrganizationAdministrationPolicy.Normalize` → `RequireAdministrator`). An inactive Principal is refused. Saving the same rules leaves the version as it was. A success returns to the Contact with "The principal's report sending rules were updated."; a refusal redraws the page with what was typed.

### What the migration seeded

`20261007090000_PrincipalReportSendingRules` adds `Principals.ReportSendingRulesJson`. It seeds five Claim Sources the rules name (Car 2 Go, SMC, CarClaims, Expert Claims, Rapid Rental Solutions) and the 17 SOP Principals Pegasus lacked (AMS, ACSP, ALISON, CS, HTU, KERR, KMR, MIDAS, MOTORX, SIX, SS, SWADE, SWAN, TEN, TP, WALKER, WLS). ACSP and CS are also made Claim Sources. It writes the SOP v5 rules for 32 Principal codes and appends the TL thresholds and QCL's contract repair line to Notes on every Case, once each. It gives every other Principal the default rules, makes the column required with the default as its value, and drops `IncludeOriginalInstructionSender` and `ReportRecipientAddressesJson` ("The retired report recipient settings (operator, 6 October 2026)"). It is forward-only.

## Things the FRD does not settle

- Whether the panel reads as values until edited, as the Case record does, or stays a form.
- Whether the spare blank rule row is kept.
- The order of the condition kinds in the select.
- Whether the five hints stay.
- Whether Send from lists only the mailboxes a report can actually be sent from.
- Where a refused save is reported.
- Whether the panel can be used without script.
