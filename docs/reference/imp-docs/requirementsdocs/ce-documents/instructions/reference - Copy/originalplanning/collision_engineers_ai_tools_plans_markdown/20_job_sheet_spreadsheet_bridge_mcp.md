# Job Sheet / SharePoint Spreadsheet Bridge MCP

Generated: 2026-05-22

**Type:** MCP server + transitional adapter
**Priority:** Medium

## Objective

Expose the existing job sheet as a transitional control surface while gradually moving operational state into a canonical work-item database.

## Why it matters for Collision Engineers

The handover states the job sheet is SharePoint/OneDrive-hosted, must be opened in Excel Desktop, and contains VBA functions for row creation, folder creation, and row movement. It still reflects real operational columns such as Date, VRM, Principal, Client, Vehicle, Missing, Due, folder links, and formula-generated chaser text.

## Proposed shape

A bridge reads/writes approved rows through Microsoft Graph/Excel APIs or a controlled local sync process. It avoids altering workbook structure and treats the sheet as a mirror/control view, not the only source of truth.

## Candidate tools / MCP methods / skill actions

- `read_job_sheet_rows(filter?)`
- `create_job_sheet_row(work_item_id)`
- `update_missing_status(work_item_id, missing)`
- `sync_principals_table()`
- `sync_garage_contacts()`
- `export_control_view()`
- `check_conditional_format_rules()`

## Inputs

- Work item data
- spreadsheet URL/driveItem
- principal and garage sheets
- review status

## Outputs

- Job sheet row
- sync status
- principal/garage reference data
- format/rule health check

## Guardrails

- Do not edit workbook online in a way that breaks VBA.
- Do not add/remove columns without controlled migration.
- Avoid concurrent unsafely writes.
- Retain Excel Desktop requirement during transition.

## MVP implementation path

1. Start read-only: import principals/garages and current open rows.
2. Add export-only control CSV/table.
3. Then add controlled row update if safe.
4. Eventually replace with review dashboard.

## Test / acceptance criteria

- Reads principal list correctly.
- Conditional formatting rule check matches backup.
- No macro corruption.
- Round-trip work item to row export.

## Risks and open questions

- Excel/VBA fragility.
- SharePoint sync/version conflicts.
- API write limits.
- Column dependencies.

## Project source basis

- handover.docx
- Backup of CE Job Sheet 260429.xlsm
- Backup of Conditional Formatting 260202.txt
- Mapped Principals.xlsx

## External reference basis

- Model Context Protocol documentation: https://modelcontextprotocol.io/docs/getting-started/intro
- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
- Microsoft Graph OneDrive/SharePoint files: https://learn.microsoft.com/en-us/graph/api/resources/onedrive
