# Open Questions and Decision Log

## Highest-priority decisions

1. **Central case database:** what database/application owns cases once the spreadsheet is reduced or replaced?
2. **Storage strategy:** continue Box, move to SharePoint/OneDrive/Azure/other, or hybrid?
3. **Mailbox access model:** shared mailboxes via Graph application permissions, delegated permissions, Power Automate, or another M365 pattern?
4. **WhatsApp:** manual/copy-paste only, or official WhatsApp Business API?
5. **EVA/Sentry access:** do you have production/sandbox credentials and endpoint permission for instruction, note, claim update, submit report and retrieve report?
6. **Valuation services:** which sources are licensed for API/data use — Glass’s/Autovista, Cazana, cap hpi, Percayso, Autotrader, other?
7. **Report templates:** which sections are factual vs engineer-only judgement?
8. **Provider rules:** which current principal/job-sheet notes should become formal workflow rules?
9. **AI vendor:** what model/service can process case data under acceptable retention, region and confidentiality terms?
10. **Human approval policy:** which actions can later become approved-send or auto-send?

## Implementation assumptions to verify

- The “Sent Mino” column refers to Minotaur/EVA-related report sending or a specific chase/report workflow; confirm exact semantics before automation.
- Provider-specific “Images location” and “Image based or address” notes can be formalised into workflow rules.
- The existing CE Document Mapper output schema can serve as an early extraction contract but is not a full case schema.
- The Sentry API documentation reflects the access Collision Engineers will actually have, not only a generic integration guide.
- A.N.D.I.E and existing Copilot/Claude tools are prototypes/assistants, not production case-management systems.
- The job sheet backup contains live-style PII; test data should be redacted before wider vendor sharing.

## Decisions already made in this revised plan

- Preserve original evidence; create derivatives only.
- Start with draft-only external communications.
- Avoid WhatsApp Desktop automation.
- Avoid EVA/Audatex/Glass’s screen scraping.
- Use valuation APIs only as evidence, never final judgement.
- Treat management “continuous learning” as analytics until governance exists.
- Build deterministic automations before agents.
- Keep engineer sign-off mandatory for expert conclusions.

## Questions for workshop

1. Which inboxes/folders are in scope for first intake automation?
2. Which providers produce the highest volume and/or most missing-info cases?
3. What are the top five fields that are most often wrong or missing?
4. What exact messages are currently sent for first/second/third chasers?
5. Which cases require physical inspection vs desktop/image-based assessment?
6. Which image angles are mandatory by report type?
7. How often do images arrive before the instruction?
8. How often are the same VRM/reference seen in multiple cases?
9. Who is allowed to approve valuation evidence?
10. Who is allowed to approve outgoing technical report/rebuttal wording?
11. Should the first build mirror the existing Excel holding pen or replace it?
12. What would count as a successful pilot after two weeks?
