# Invoice / Fee Note Generation Tool

Generated: 2026-05-22

**Type:** Document/template tool
**Priority:** Medium-Low

## Objective

Generate and track standard Audatex/website invoice or fee-note documents from approved case data and provider rules.

## Why it matters for Collision Engineers

The uploaded invoice templates show standard Audatex assessment invoices and website invoice variations with client contact/VRM. Some principals require fee notes separately according to the job sheet/principal data.

## Proposed shape

A deterministic template renderer fills approved invoice fields, stores the output in Box, and links it to the case/job sheet. A skill can draft the short delivery email using CE tone.

## Candidate tools / MCP methods / skill actions

- `generate_invoice(work_item_id, template_type)`
- `preview_fee_note(work_item_id)`
- `store_invoice_in_box(work_item_id, invoice_file)`
- `draft_invoice_email(work_item_id)`
- `mark_invoice_sent(work_item_id)`

## Inputs

- Approved case data
- template type
- principal fee-note rule
- client contact info
- amounts/VAT

## Outputs

- Invoice DOCX/PDF
- Box link
- draft email
- sent status

## Guardrails

- Do not infer billing amounts without rule/approval.
- Preserve VAT/company details.
- Human approval before sending.
- Store a versioned copy.

## MVP implementation path

1. Map templates to data fields.
2. Render DOCX/PDF.
3. Use principal table for separate fee-note rules.
4. Add draft delivery email.

## Test / acceptance criteria

- Generated invoice matches template.
- VRM/client/date populate correctly.
- VAT/total math correct.
- Separate-fee-note rules respected.

## Risks and open questions

- Billing rules vary.
- Payment/accounting integration not defined.
- Template formatting.

## Project source basis

- Standard Audatex Invoice.docx
- Website Invoice Template.docx
- Backup of CE Job Sheet 260429.xlsm

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
