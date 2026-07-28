# 8.14 Invoice and Payment Workflow Automation

## Purpose

Automate routine invoice generation, payment-status tracking and invoice document storage where the case outcome and fee rules are clear.

## Why this matters

Existing invoice templates show repeatable invoice structures for Audatex assessments and other work. Connecting invoicing to case state reduces missed billing and manual document creation.

## Step-by-step plan

### Step 1 — Catalogue invoice types

1. Audatex assessment invoice.
2. Standard engineering report invoice.
3. Private/direct client invoice.
4. Provider-specific fee arrangement.
5. Credit/refund/adjustment scenario.
6. Paid/stamped invoice scenario.

### Step 2 — Define billing triggers

1. Report issued.
2. Audatex assessment completed.
3. Case marked complete.
4. Provider-specific milestone met.
5. Manual billing approval received.
6. Invoice withheld due to exception.

### Step 3 — Build fee-rule configuration

1. Provider.
2. Work type.
3. Fee amount.
4. VAT treatment.
5. Payment terms.
6. Invoice recipient.
7. Contact email.
8. Required reference/VRM formatting.

### Step 4 — Generate draft invoices

1. Pull claimant/client, provider, VRM and reference from the work item.
2. Apply fee rule.
3. Generate a draft PDF/document using approved template.
4. Store the draft against the case.
5. Require staff approval before sending during pilot.

### Step 5 — Track payment status

1. Add invoice status to the work item or finance table.
2. Record sent date, paid date and payment method where available.
3. Flag overdue invoices.
4. Generate concise chaser drafts where appropriate.
5. Link invoice files to the case folder.

### Step 6 — Reconcile and report

1. Export invoice records for accounting.
2. Match payment references where possible.
3. Report outstanding invoices by provider/client.
4. Track invoice generation errors.
5. Keep templates versioned.

## Deliverables

- Invoice type catalogue.
- Fee-rule configuration.
- Draft invoice generator.
- Invoice approval/sending workflow.
- Payment-status dashboard.

## Acceptance criteria

- A reviewed/completed case can generate the correct draft invoice from configuration.
- Staff can approve, edit or reject the draft.
- Invoice files are stored and linked to the case.
- Overdue invoices are visible.

## Risks and controls

| Risk | Control |
|---|---|
| Wrong fee applied | Provider/work-type fee rules and approval step. |
| VAT treatment error | Finance review and template validation. |
| Duplicated invoices | One invoice record per billing trigger with duplicate checks. |

## Suggested priority

P3. Useful after case-state workflow is reliable; not a prerequisite for intake automation.
