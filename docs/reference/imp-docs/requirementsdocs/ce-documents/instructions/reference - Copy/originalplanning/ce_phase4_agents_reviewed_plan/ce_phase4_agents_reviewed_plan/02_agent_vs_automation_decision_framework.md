# Agent vs Automation Decision Framework

## Rule of thumb

Use the simplest reliable mechanism that satisfies the business need.

- Use **deterministic automation** when the trigger, input, output and rule are clear.
- Use **AI assistance** when the system needs classification, summarisation, extraction from messy text, drafting or semantic matching.
- Use a **controlled agent** only when the AI must choose among approved tools, inspect multiple data sources, prepare a sequence of actions, and stop at permission boundaries.

## Capability classification

| Capability | Preferred implementation | Why |
|---|---|---|
| Poll/subscribe to new Outlook mail | Automation | Trigger and data capture are deterministic. |
| Save attachments to storage | Automation | File routing, hashing and metadata are deterministic. |
| Identify obvious image/PDF/DOCX attachments | Automation with MIME/extension rules | AI not needed for common cases. |
| Classify ambiguous documents | AI-assisted classifier | Useful for messy provider formats. |
| Match exact VRM + claim ref to case | Automation | Exact matches are deterministic. |
| Resolve ambiguous evidence match | Suggested-action agent | Needs context and human review. |
| Create dashboard status | Automation | State transitions and metrics are deterministic. |
| Detect missing items | Automation/checklist | Report-type checklist and case state should drive this. |
| Draft chaser email | AI-assisted draft or template | User review required; tone matters. |
| Send chaser email | Approved automation | Only after review or narrow allow-list. |
| Query DVLA/DVSA/valuation APIs | Automation/tool call | Deterministic external data lookup. |
| Summarise valuation evidence | AI-assisted draft | Helpful, but final value remains human approved. |
| Engineer pack creation | Automation + summarisation | File assembly deterministic; narrative summary can use AI. |
| Final engineering conclusion | Prohibited for automation/agent | Engineer-only professional judgement. |
| Weekly bottleneck report | BI + AI summary | Metrics are deterministic; AI can summarise. |

## Permission levels

### Level 0 — Read-only

Can read approved case data, documents and notes. Can answer questions and summarise.

### Level 1 — Draft-only

Can create draft emails, draft notes, draft report sections and draft pack summaries. Cannot update case state or send messages.

### Level 2 — Suggested action

Can recommend actions such as “merge these evidence items”, “ask for rear images” or “assign to engineer”. A user must approve.

### Level 3 — Internal action

Can perform reversible internal actions: create tasks, apply labels, update non-final statuses, attach a document to a case, generate internal pack.

### Level 4 — External action

Can send approved messages, submit approved payloads or call external systems only under defined rules and with clear audit logs.

### Level 5 — Prohibited / engineer-only

Cannot autonomously do final valuation, causation, roadworthiness, fraud allegation, liability conclusion, report sign-off, deletion of original evidence, or disputed external communications.

## Required controls for any agent

- Tool allow-list.
- Role-based permissions.
- Case-level access checks.
- Audit logging of inputs, retrieved sources, tool calls, output, approvals and final action.
- Confidence thresholds and escalation rules.
- Human approval for external actions at least in early production.
- Clear “why this action was suggested” reasoning shown to staff.
