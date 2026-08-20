## Independent review — PR #441 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green CI (fix commit 6db44c83 updated the tool-count assertion 15→18 that the first run caught; the only other inventory, ExpectedTools, was already updated).

- New `automation.mail` scope added to the canonical scope list; MailMcpTools registered beside the four existing tool classes — the established convention exactly.
- Tools reuse the SAME Core use cases as the staff mail pages (ListRetainedMail / GetRetainedMail / GetRetainedMailFreshness / CorrectRetainedMailClassification) with AutomationActorResolver scope enforcement and AutomationMcpAuditor logging parity. The single mutation is the staff-equivalent classification correction; the class doc states the boundary plainly: no Outlook/mailbox mutation exists here — transport mutation stays a separately approved capability (the constraint the lane brief demanded).
- Folder parsing reuses IndexModel.TryParseFolder rather than duplicating the vocabulary (one list per concept).
- Structured result records expose Pegasus-side retained data only. Integration tests added in the AutomationMailIngressTests class following the ingress-test conventions.
- No FRD enumerates scopes/tool names, so no documentation gap.
