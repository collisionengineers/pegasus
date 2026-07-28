# Outlook / Microsoft Graph Intake MCP

Generated: 2026-05-22

**Type:** MCP server + event intake service
**Priority:** High

## Objective

Capture relevant shared-mailbox emails and attachments, create work items, deduplicate messages, and hand source files to the storage/extraction pipeline.

## Why it matters for Collision Engineers

The handover notes that the digital account can access multiple shared inboxes, and current work arrives by email with instructions, images, and other attachments. Microsoft Graph supports mailbox data and attachment retrieval, and change notifications can reduce polling.

## Proposed shape

An intake service subscribes to mailbox/folder changes or polls on a schedule. The MCP exposes read/search operations for agents, while the production intake worker creates work items automatically.

## Candidate tools / MCP methods / skill actions

- `search_relevant_messages(query, mailbox, folder?)`
- `get_message_metadata(message_id)`
- `download_attachments(message_id)`
- `create_work_item_from_message(message_id)`
- `classify_message(message_id)`
- `mark_message_processed(message_id, status)`
- `find_possible_related_emails(work_item_id)`

## Inputs

- Mailbox/folder ID
- message ID
- sender
- subject
- received timestamp
- internet message ID
- attachments

## Outputs

- Work item
- source email record
- attachment records
- dedupe decisions
- missing/related email suggestions

## Guardrails

- Do not delete source emails.
- Use least-privilege Graph permissions.
- Treat shared mailbox access carefully because delegated access in Outlook Classic has caused issues.
- Store internetMessageId and checksums for dedupe.
- Do not expose unrestricted mailbox search to general staff agents.

## MVP implementation path

1. Start with a controlled folder in a shared mailbox.
2. Implement scheduled polling first if webhooks are operationally awkward.
3. Download PDF/image/DOCX/DOC/MSG attachments.
4. Compute checksums and create work item records.
5. Then add webhook/change notification support.

## Test / acceptance criteria

- Known sample email with PDF and images creates one work item.
- Same email reprocessed does not duplicate.
- Separate image email is routed to unmatched queue or linked when reference matches.
- Unsupported attachments remain visible.

## Risks and open questions

- Graph permissions/admin consent.
- Mailbox noise.
- Forwarded emails with nested attachments.
- Webhook endpoint availability if using change notifications.

## Project source basis

- handover.docx
- context_pack_2/05_OUTLOOK_INTAKE.md
- Microsoft Graph mail and attachment docs

## External reference basis

- Model Context Protocol documentation: https://modelcontextprotocol.io/docs/getting-started/intro
- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
- Microsoft Graph mail overview: https://learn.microsoft.com/en-us/graph/api/resources/mail-api-overview
- Microsoft Graph attachment API: https://learn.microsoft.com/en-us/graph/api/attachment-get
- Microsoft Graph change notifications: https://learn.microsoft.com/en-us/graph/change-notifications-overview
